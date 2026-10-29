#!/usr/bin/env python3
"""Offline benchmark for the v0.2 weight controller (docs/specs/v0.2-weight-controller.md, section Benchmark).

Replays eight days of automation demand against one weekly subscription pool and one daily API pool,
under four policies, and reports API spend, held work, owner-blocked time and weight flapping.
Standard library only, deterministic per seed, no network. Not product code: it is the reference
model whose numbers the spec quotes, and the oracle the .NET allocator is later compared with.

  scripts/bench-controller.py                       # synthetic demand, 40 seeds, capacity sweep
  scripts/bench-controller.py --trace demand.csv    # day,consumer,usd rows (API-equivalent dollars)
  scripts/bench-controller.py --check               # exits 1 when a budget in the spec is missed
"""
import argparse, csv, math, random, statistics, sys

TICK_MIN = 5
TICKS_PER_DAY = 24 * 60 // TICK_MIN
HOURS_WEEK = 168.0
CHEAP_RATIO = 0.10          # cheap model price relative to the asked model (list-price ratio)
HOLD_RETRY_TICKS = 12       # a held job is offered again after an hour
HOLD_EXPIRE_TICKS = 12 * 12 # and dropped after twelve hours

# name: (share of the week, API-equivalent $ a day, mean job $, hourly shape, q_max, is_owner)
def shape_work(h):   return 1.0 if 6 <= h < 20 else 0.15
def shape_flat(h):   return 1.0
def shape_burst(h):  return 0.5 + (6.0 if 3 <= h < 5 else 0.0)
def shape_owner(h):  return 1.0 if 7 <= h < 22 else 0.05

SYNTHETIC = {
    "orchestrator": dict(share=0.25, daily=12.0, job=0.45, shape=shape_work,  q_max=0.0, owner=False),
    "memory":       dict(share=0.15, daily=14.0, job=0.70, shape=shape_burst, q_max=0.8, owner=False),
    "assistant":    dict(share=0.05, daily=2.0,  job=0.10, shape=shape_flat,  q_max=0.5, owner=False),
    "owner":        dict(share=0.55, daily=15.0, job=0.30, shape=shape_owner, q_max=0.0, owner=True),
}
SHAPES = {"orchestrator": shape_work, "memory": shape_burst, "assistant": shape_flat, "owner": shape_owner}

def poisson(rng, lam):
    if lam <= 0: return 0
    if lam > 30: return max(0, int(rng.gauss(lam, math.sqrt(lam)) + 0.5))
    L, k, p = math.exp(-lam), 0, 1.0
    while True:
        p *= rng.random()
        if p <= L: return k
        k += 1

def load_trace(path):
    days = {}
    with open(path) as f:
        for row in csv.DictReader(f):
            days.setdefault(row["consumer"], {})[int(row["day"])] = float(row["usd"])
    return days

class Sim:
    def __init__(self, policy, seed, cap_week, api_cap, cfg, trace, days, week_phase_h, u0, params):
        self.policy, self.rng, self.cap_week, self.api_cap = policy, random.Random(seed), cap_week, api_cap
        self.cfg, self.trace, self.days, self.p = cfg, trace, days, params
        self.week_phase_h = week_phase_h       # hours since the meter's last reset at t=0
        self.sub_used = u0 * cap_week          # API-equivalent dollars drawn this week
        self.limited = False
        self.api_today, self.day_idx = 0.0, 0
        # controller state per consumer
        names = [n for n, c in cfg.items() if not c["owner"]]
        self.names = names
        self.a = {n: 0.0 for n in names}; self.pub = {n: 0.0 for n in names}; self.q = {n: 0.0 for n in names}
        self.acc_a = {n: 0.0 for n in names}; self.acc_q = {n: 0.0 for n in names}
        self.demand_ewma = {n: 0.0 for n in names}; self.last_dir = {n: 0 for n in names}; self.last_flip = {n: -999 for n in names}
        self.used_est = {n: 0.0 for n in names}
        rb = random.Random(seed * 7919 + 1)
        self.bias = {n: math.exp(rb.gauss(0, params["est_sigma"])) for n in names}   # attribution error, fixed per run
        self.meter, self.meter_age = self.sub_used / cap_week, 0
        self.backlog = []   # (ready_tick, born_tick, consumer, usd)
        self.m = dict(api=0.0, served=0.0, demand=0.0, held_jobs=0, dropped=0.0, wait_ticks=0, waited=0,
                      owner_blocked=0, writes=0, flips=0, reversals=0, max_step=0.0, peak_u=0.0, api_cap_ticks=0,
                      cheap_jobs=0, jobs=0)
        self.writes_per_c = {n: 0 for n in names}

    # --- demand ---
    def demand(self, tick):
        day, h = tick // TICKS_PER_DAY, (tick % TICKS_PER_DAY) * TICK_MIN / 60.0
        out = []
        for n, c in self.cfg.items():
            daily = self.trace[n].get(day, 0.0) if self.trace else c["daily"]
            shape = c["shape"]
            norm = sum(shape(x / 12.0) for x in range(24 * 12)) / 12.0
            lam_usd = daily * shape(h) / norm * TICK_MIN / 60.0
            for _ in range(poisson(self.rng, lam_usd / c["job"])):
                out.append((n, c["job"] * math.exp(self.rng.gauss(0, 0.5) - 0.125)))
        return out

    # --- pools ---
    def week_hours(self, tick): return (self.week_phase_h + tick * TICK_MIN / 60.0) % HOURS_WEEK
    def elapsed_frac(self, tick): return max(self.week_hours(tick), 0.01) / HOURS_WEEK

    def try_sub(self, usd):
        if self.limited: return False
        self.sub_used += usd
        return True

    def try_api(self, usd, cheap):
        cost = usd * (CHEAP_RATIO if cheap else 1.0)
        if self.api_today + cost > self.api_cap: return False
        self.api_today += cost; self.m["api"] += cost
        if cheap: self.m["cheap_jobs"] += 1
        return True

    # --- policies ---
    def route(self, n, usd, tick):
        """Returns 'sub', 'api', 'cheap' or 'hold' for one job."""
        if self.cfg[n]["owner"]: return "sub"
        pol = self.policy
        if pol == "reactive":                       # subscription until it refuses, then the API at full price
            return "api" if self.limited else "sub"
        a = self.pub[n] if pol != "static" else self.p["static_api"]
        self.acc_a[n] += a
        if self.acc_a[n] >= 1.0:
            self.acc_a[n] -= 1.0
            self.acc_q[n] += self.q[n] if pol != "static" else 0.0
            if self.acc_q[n] >= 1.0:
                self.acc_q[n] -= 1.0
                return "cheap"
            return "api"
        return "sub"

    def settle(self, n, usd, tick, born):
        r = self.route(n, usd, tick)
        ok = False
        if r == "sub": ok = self.try_sub(usd)
        elif r == "api": ok = self.try_api(usd, False)
        elif r == "cheap": ok = self.try_api(usd, True)
        if ok:
            self.m["served"] += usd; self.m["jobs"] += 1
            if tick > born: self.m["wait_ticks"] += tick - born; self.m["waited"] += 1
        else:
            self.m["held_jobs"] += 1
            self.backlog.append((tick + HOLD_RETRY_TICKS, born, n, usd))
        return ok

    # --- controller ---
    def control(self, tick):
        p = self.p
        u = self.meter
        pace = self.elapsed_frac(tick)
        remaining_h = max(HOURS_WEEK - self.week_hours(tick), 0.5)
        day_frac = ((tick % TICKS_PER_DAY) + 1) / TICKS_PER_DAY
        k = self.cap_week     # dollars per 100% of the week
        for n in self.names:
            c = self.cfg[n]
            allowance = c["share"] * pace * k
            used = self.used_est[n] * self.bias[n]
            headroom = max(allowance - used, 0.0)
            if self.policy == "borrow":
                # work-conserving: automation may draw on a fraction of the account's slack below the pace line
                slack = max(0.0, pace - u - p["margin"]) * k * p["borrow_frac"]
                tot = sum(self.demand_ewma[x] for x in self.names) or 1.0
                headroom += slack * self.demand_ewma[n] / tot
            dem_per_h = self.demand_ewma[n]   # $/h of demand, all routes, measured from reports
            horizon = p["horizon_h"]
            p_sub = 1.0 if dem_per_h < 1e-9 else min(1.0, headroom / (dem_per_h * horizon))
            a_raw = 1.0 - p_sub
            if u >= p["backstop_u"]: a_raw = 1.0           # protect the owner: account near its limit
            elif u > pace + p["margin"]: a_raw = max(a_raw, 0.5)
            # cheap lever from projected API spend
            api_rate = sum(self.demand_ewma[x] * self.a[x] for x in self.names)
            proj = self.api_today + api_rate * (1 - day_frac) * 24.0
            over = max(0.0, proj / (p["target_util"] * self.api_cap) - 1.0)
            q_raw = min(c["q_max"], over)
            if self.policy == "raw":
                self.a[n], self.q[n] = a_raw, q_raw
                new = a_raw
            else:
                self.a[n] += max(-p["slew"], min(p["slew"], p["alpha"] * (a_raw - self.a[n])))
                self.q[n] += max(-p["slew"], min(p["slew"], p["alpha"] * (q_raw - self.q[n])))
                new = round(self.a[n] / p["quant"]) * p["quant"]
            new = min(1.0, max(0.0, new))
            old = self.pub[n]
            publish = abs(new - old) >= (1e-9 if self.policy == "raw" else p["deadband"]) or (new in (0.0, 1.0) and new != old)
            if publish and self.policy != "raw":
                d = 1 if new > old else -1
                if d != self.last_dir[n] and self.last_dir[n] != 0 and tick - self.last_flip[n] < p["dwell"] // TICK_MIN:
                    publish = False
            if publish:
                d = 1 if new > old else -1
                if self.last_dir[n] != 0 and d != self.last_dir[n]:
                    self.m["reversals"] += 1; self.last_flip[n] = tick
                if self.last_dir[n] == 0: self.last_flip[n] = tick
                self.last_dir[n] = d
                self.m["writes"] += 1; self.writes_per_c[n] += 1
                self.m["max_step"] = max(self.m["max_step"], abs(new - old))
                self.pub[n] = new

    # --- main loop ---
    def run(self):
        ticks = self.days * TICKS_PER_DAY
        sample_every = self.p["signal_ticks"]
        for tick in range(ticks):
            if tick % TICKS_PER_DAY == 0: self.api_today = 0.0
            if abs(self.week_hours(tick)) < TICK_MIN / 60.0 / 2 and tick > 0:
                self.sub_used, self.limited = 0.0, False
                self.used_est = {n: 0.0 for n in self.names}
            # meter reading: quantised to one point, refreshed by the sampler every signal_ticks
            if tick % sample_every == 0:
                self.meter = math.floor(self.sub_used / self.cap_week * 100.0) / 100.0
            before = {n: 0.0 for n in self.names}
            arrivals = self.demand(tick)
            due = [b for b in self.backlog if b[0] <= tick]; self.backlog = [b for b in self.backlog if b[0] > tick]
            work = [(n, usd, tick) for n, usd in arrivals]
            for (_, born, n, usd) in due:
                if tick - born > HOLD_EXPIRE_TICKS: self.m["dropped"] += usd
                else: work.append((n, usd, born))
            for n, usd, born in work:
                self.m["demand"] += usd if born == tick else 0.0
                cfgc = self.cfg[n]
                if cfgc["owner"]:
                    if self.limited: self.m["owner_blocked"] += 1; continue
                    self.sub_used += usd; self.m["served"] += usd; self.m["jobs"] += 1
                    continue
                self.settle(n, usd, tick, born)
                before[n] += usd
            if self.sub_used >= self.cap_week and not self.limited:
                self.limited = True
            self.m["peak_u"] = max(self.m["peak_u"], min(self.sub_used / self.cap_week, 1.0))
            if self.api_today >= self.api_cap - 1e-9: self.m["api_cap_ticks"] += 1
            # demand and subscription use as reported to the ledger
            for n in self.names:
                self.demand_ewma[n] = 0.97 * self.demand_ewma[n] + 0.03 * (before[n] / (TICK_MIN / 60.0))
            if self.policy not in ("reactive", "static"):
                # subscription use per consumer is estimated as its demand times the published subscription share
                for n in self.names:
                    self.used_est[n] += before[n] * (1.0 - self.pub[n])
                self.control(tick)
            elif self.policy == "static":
                for n in self.names: self.used_est[n] += before[n] * (1.0 - self.p["static_api"])
        self.m["held_end"] = sum(b[3] for b in self.backlog)
        self.m["dropped"] += self.m["held_end"]
        return self.m

def summarize(runs, days):
    def col(k): return [r[k] for r in runs]
    med = statistics.median
    p95 = lambda xs: sorted(xs)[min(len(xs) - 1, int(0.95 * len(xs)))]
    return dict(
        api_per_day=med(col("api")) / days, api_per_day_p95=p95(col("api")) / days,
        served_pct=100 * med([r["served"] / max(r["demand"], 1e-9) for r in runs]),
        dropped_usd=med(col("dropped")), owner_blocked_h=med(col("owner_blocked")) * TICK_MIN / 60.0,
        peak_u=p95(col("peak_u")), writes_per_day=med(col("writes")) / days / len(runs[0]["names"]),
        reversals_per_day=med(col("reversals")) / days / len(runs[0]["names"]), max_step=p95(col("max_step")),
        cheap_pct=100 * med([r["cheap_jobs"] / max(r["jobs"], 1) for r in runs]),
        api_cap_h=med(col("api_cap_ticks")) * TICK_MIN / 60.0,
        wait_min=med([r["wait_ticks"] / max(r["waited"], 1) * TICK_MIN for r in runs]))

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", type=int, default=40)
    ap.add_argument("--days", type=int, default=8)
    ap.add_argument("--capacity", type=float, nargs="*", default=[150.0, 250.0, 400.0], help="weekly subscription capacity, API-equivalent $")
    ap.add_argument("--api-cap", type=float, default=6.0)
    ap.add_argument("--trace")
    ap.add_argument("--week-phase-h", type=float, default=64.0, help="hours since the meter's last reset at t=0")
    ap.add_argument("--u0", type=float, default=0.30)
    ap.add_argument("--static-api", type=float, default=0.25)
    for k, v in dict(alpha=0.05, slew=0.10, quant=0.10, deadband=0.10, dwell=60, horizon_h=6.0, margin=0.05, target_util=0.8, borrow_frac=0.5).items():
        ap.add_argument("--" + k.replace("_", "-"), type=type(v), default=v, help="controller parameter")
    ap.add_argument("--share", action="append", default=[], metavar="NAME=FRACTION", help="override a consumer's share of the week")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args()
    params = dict(horizon_h=a.horizon_h, alpha=a.alpha, slew=a.slew, quant=a.quant, deadband=a.deadband, dwell=a.dwell,
                  margin=a.margin, borrow_frac=a.borrow_frac, backstop_u=0.95, target_util=a.target_util, est_sigma=0.2, signal_ticks=6,
                  static_api=a.static_api)
    cfg, trace = dict(SYNTHETIC), None
    if a.trace:
        trace = load_trace(a.trace)
        cfg = {n: dict(SYNTHETIC.get(n, SYNTHETIC["assistant"]), shape=SHAPES.get(n, shape_flat),
                       daily=0.0, job=SYNTHETIC.get(n, SYNTHETIC["assistant"])["job"]) for n in trace}
        for n in cfg: trace.setdefault(n, {})
    for kv in a.share:
        n, v = kv.split("="); cfg[n] = dict(cfg[n], share=float(v))
    results, failed = {}, []
    for cap in a.capacity:
        for pol in ("reactive", "static", "raw", "controller", "borrow"):
            runs = []
            for seed in range(a.seeds):
                s = Sim(pol, seed, cap, a.api_cap, cfg, trace, a.days, a.week_phase_h, a.u0, params)
                m = s.run(); m["names"] = s.names; runs.append(m)
            results[(cap, pol)] = summarize(runs, a.days)
    if a.json:
        import json; print(json.dumps({f"{c}:{p}": v for (c, p), v in results.items()}, indent=1))
    else:
        hdr = "capacity policy      api$/d  p95   served%  dropped$ ownerBlk_h  peakU  writes/d/c  revers/d  maxStep  cheap%  capHit_h  wait_min"
        print(hdr)
        for (cap, pol), r in results.items():
            print(f"{cap:7.0f} {pol:11s} {r['api_per_day']:6.2f} {r['api_per_day_p95']:5.2f} {r['served_pct']:7.1f} {r['dropped_usd']:9.2f} {r['owner_blocked_h']:10.1f} "
                  f"{r['peak_u']:6.2f} {r['writes_per_day']:11.1f} {r['reversals_per_day']:9.1f} {r['max_step']:8.2f} {r['cheap_pct']:7.1f} {r['api_cap_h']:9.1f} {r['wait_min']:9.1f}")
    if a.check:
        for cap in a.capacity:
            c, raw, st, re_ = results[(cap, "controller")], results[(cap, "raw")], results[(cap, "static")], results[(cap, "reactive")]
            checks = [
                (f"B1 cap={cap:.0f} controller api$/day p95 <= api cap", c["api_per_day_p95"] <= a.api_cap + 1e-9),
                (f"B2 cap={cap:.0f} controller owner blocked <= reactive", c["owner_blocked_h"] <= re_["owner_blocked_h"] + 1e-9),
                (f"B3 cap={cap:.0f} controller reversals/day/consumer <= 2", c["reversals_per_day"] <= 2.0),
                (f"B4 cap={cap:.0f} controller writes/day/consumer <= 12", c["writes_per_day"] <= 12.0),
                (f"B5 cap={cap:.0f} controller reversals < raw reversals", c["reversals_per_day"] < raw["reversals_per_day"] or raw["reversals_per_day"] == 0),
                (f"B6 cap={cap:.0f} controller api$/day below static", c["api_per_day"] < st["api_per_day"]),
                (f"B7 cap={cap:.0f} controller served% within 5 points of static (capacity >= 250)", cap < 250 or c["served_pct"] >= st["served_pct"] - 5.0),
            ]
            bo = results[(cap, "borrow")]
            checks += [
                (f"B8 cap={cap:.0f} borrow owner blocked <= reactive", bo["owner_blocked_h"] <= re_["owner_blocked_h"] + 1e-9),
                (f"B9 cap={cap:.0f} borrow api$/day <= controller api$/day (capacity >= 250)", cap < 250 or bo["api_per_day"] <= c["api_per_day"] + 1e-9),
                (f"B10 cap={cap:.0f} borrow reversals/day/consumer <= 2", bo["reversals_per_day"] <= 2.0),
            ]
            for name, ok in checks:
                print(("pass " if ok else "FAIL ") + name)
                if not ok: failed.append(name)
        sys.exit(1 if failed else 0)

if __name__ == "__main__":
    main()
