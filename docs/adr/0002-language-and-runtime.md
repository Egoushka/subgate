# 0002. Language and runtime

- Status: proposed (the three-way benchmark is running; this ADR is final when its table is filled)
- Date: 2026-10-28

## Context

subgate is a streaming reverse proxy: it forwards server-sent events unbuffered, parses usage from them on the side, and ships as one binary or container next to an LLM gateway. What matters: added latency to the first byte, memory at rest, throughput, a static build, and how much proxy code the standard library already gives.

## Evidence

**Spike (2026-10-28), Go `httputil.ReverseProxy` with a streaming usage tap**, against a local mock, 2000 requests, 20 concurrent, warm:

| | direct to mock | through the Go proxy |
|---|---|---|
| time to first byte p50 | 3.6 ms | 5.1 ms |
| time to first byte p95 | 4.8 ms | 7.8 ms |
| total p95 | 5.2 ms | 8.6 ms |
| RSS | — | 33 MB |

A model's first token takes 1–3 s, so +3 ms p95 is under 0.3% of it.

**Three-way benchmark (pending)**: the same proxy and mock in Go, Rust (hyper) and .NET 10 Native AOT (Kestrel), measuring added time to first byte p50/p95, total p95, requests per second at 20 and 200 concurrent, steady RSS, binary size and cold start.

| | Go | Rust | .NET 10 Native AOT |
|---|---|---|---|
| added TTFB p50 / p95 | | | |
| total p95 | | | |
| req/s at 20 / 200 | | | |
| RSS | | | |
| binary | | | |
| cold start | | | |

## Options

1. **Go.** Streaming reverse proxy in the standard library; static binary; the gateway it sits in front of (Bifrost) is Go, so a later move into a plugin is a port, not a rewrite.
2. **Rust.** Likely the lowest RSS and tail latency. No toolchain on the author's machine today; more code for the same proxy.
3. **.NET 10 Native AOT.** The author's main stack; YARP is a strong proxy. AOT binaries are larger, and the shared .NET libraries (switchknife) have nothing for this job yet.

## Decision

Go, unless the benchmark shows another option at least 2× better on p95 added latency or RSS at equal throughput. Either margin would still be a small share of first-token time, so the bar for switching is deliberately high.

## Consequences

One static binary, `CGO_ENABLED=0`; SQLite through a pure-Go driver (ADR 0009).

## Reopen if

A deployment runs at hundreds of concurrent streams where RSS or tail latency starts to matter, or the engine needs to run inside a .NET program.
