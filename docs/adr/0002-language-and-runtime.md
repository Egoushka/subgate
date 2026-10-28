# 0002. Language and runtime

- Status: accepted (owner, 2026-10-29). Reopened the same day: the ledger-first decision (ADR 0003) removed the reasons Go was picked.
- Date: 2026-10-29

## Context

Go was picked for a streaming proxy: the standard library has one, and the gateway it sat in front of (Bifrost) is Go. Out of the request path, subgate is a low-rate HTTP service (a few calls per job), a client CLI that wraps each job, a sampler and a status-line command. What matters now: memory at rest, binary size, cold start of the CLI (it runs per job and per status-line refresh), cross-platform builds (macOS and Linux), and reuse with the author's other code.

## Evidence

Run 1 of a three-way benchmark (2026-10-29), preliminary and superseded: the benchmark measured a proxy that will not be built (ADR 0010), and its re-run was cancelled. It is kept for the memory, binary-size and cold-start columns. The same streaming proxy with a usage tap in each language, against an SSE mock, on Linux arm64 (4 cores, in a VM on the author's laptop), loopback. Preliminary: the Go tap missed 0.8% of output tokens and logged connection errors, and Rust's total latency (~42 ms against ~1.3 ms to first byte) points to a socket setting. Both are harness bugs that hurt latency and throughput, so those columns are shown but don't decide anything.

| | Go | .NET 10 Native AOT | Rust (hyper) |
|---|---|---|---|
| RSS idle / after load | 6 / 32 MB | 21 / 43 MB | 3 / 21 MB |
| binary | 6.1 MB | 14 MB | 1.4 MB |
| cold start (median of 5) | 11 ms | 83 ms | 5.5 ms |
| added time to first byte p95, 20 concurrent | ~3 ms (harness bug) | ~0.3 ms | ~2.5 ms (harness bug) |
| requests per second, 20 / 200 concurrent | 9.3k / 15.3k (harness bug) | 17.7k / 23.2k | 0.6k / 10.6k (harness bug) |

At a ledger's rate (well under 10 requests per second) every column but memory, size and cold start is irrelevant.

## Options

1. **Go.** Small, fast cold start, trivial cross-compiles. Doesn't match the author's other services any more (they are .NET), and the shared library set (switchknife) can't take a Go engine.
2. **.NET 10 Native AOT.** The author's main stack; the engine can be a switchknife package that the orchestrator (also .NET) can use in-process for its own admission logic or tests. Costs 15 MB more memory, 8 MB more binary, about 70 ms more cold start per CLI call, and AOT builds per OS (a CI matrix, no cross-compiles).
3. **Rust.** Smallest on every measure; no toolchain on the author's machine and no other Rust code to share with.

## Decision

**.NET 10 Native AOT**, with the engine as a switchknife package (`Switchknife.Pace`, ADR 0004). The costs are small for this job: 70 ms on a CLI that wraps runs of seconds to minutes, and 21 MB at rest on a server. What it buys is one stack for the author's services and a reusable engine in the shared library set. Go was the alternative, for the smaller binary and faster cold start.

## Consequences

The service and the CLI are one AOT binary per OS. CI builds on Linux and macOS runners (ADR 0011). SQLite through `Microsoft.Data.Sqlite` with the bundled native library.

## Reopen if

The status-line command's cold start becomes visible to the owner (it runs on every assistant message), or a proxy transport comes back (ADR 0010).
