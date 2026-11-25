# concurrent-order-processor

.NET 8 console application from a personal development plan on advanced .NET (async, concurrency,
performance). It reads orders from a CSV file, gets product prices from a local test API, calculates
order totals and writes a summary report.

The project grows in four stages. Current state: **stage 1 (sequential baseline) and the async
version are done.** Cancellation, concurrency and the performance investigation come next.

| Stage | Topic | Status |
|---|---|---|
| 1 | Baseline: sequential processing, report, measurements | done |
| 2 | Async file and HTTP (done), cancellation and error handling (todo) | in progress |
| 3 | Concurrency with a configurable limit and safe aggregation | todo |
| 4 | Profiling, bottleneck, before/after measurements | todo |

## Requirements

- .NET SDK 8 (`global.json` pins 8.0.204 and allows newer feature bands)
- PowerShell, only for `scripts/run-benchmark.ps1`

## Layout

| Path | Purpose |
|---|---|
| `src/OrderProcessor.Api` | Test API with simulated delay and errors |
| `src/OrderProcessor.App` | Console app: `generate` and `process` commands |
| `tests/OrderProcessor.Tests` | xUnit unit and integration tests |
| `scripts/run-benchmark.ps1` | Repeatable benchmark runs |
| `benchmarks/` | Saved measurements (`results.csv`, `README.md` table) |
| `data/` | Generated input and reports (not committed) |

## Quick start

Run all commands from the repository root. You need two terminals.

**Terminal 1: start the API** (leave it running)

```
dotnet run --project src/OrderProcessor.Api -- --urls http://localhost:5080
```

**Terminal 2: generate orders and process them**

```
dotnet run --project src/OrderProcessor.App -- generate
dotnet run --project src/OrderProcessor.App -- process --mode sync
dotnet run --project src/OrderProcessor.App -- process --mode async
```

With the default settings (no delay, no errors) each run takes a few seconds. Expected result for
the default data (5,000 rows, seed 42):

```
Processed orders: 4705
Failed orders: 295
  InvalidRow: 145
  UnknownProduct: 150
Grand total: 2082242.16
```

The same report is saved to `data/report.txt`. To check the code, run `dotnet test`.
To measure sync against async, see "Measure and save results" below.

## Run in detail

### 1. Generate orders

```
dotnet run --project src/OrderProcessor.App -- generate [--rows 5000] [--seed 42] [--output data/orders.csv]
```

The same seed gives the same file. About 3% of rows are invalid (bad or non-positive quantity, empty
customer) and about 3% use an unknown product.

### 2. Start the test API

```
dotnet run --project src/OrderProcessor.Api -- --urls http://localhost:5080 --Simulation:DelayMs=5 --Simulation:ErrorRate=0.1 --Simulation:Seed=42
```

| Setting | Default | Meaning |
|---|---|---|
| `Simulation:DelayMs` | 0 | Fixed delay for every response |
| `Simulation:ErrorRate` | 0 | Share of requests that fail (0.0 to 1.0). Failures are HTTP 500, HTTP 429, or a timeout (held for `TimeoutMs`, then 504) |
| `Simulation:Seed` | 42 | Same seed gives the same failures |
| `Simulation:TimeoutMs` | 10000 | How long a simulated timeout holds the request |

Endpoints: `GET /products/{id}` (200, 404 for unknown product, or a simulated failure) and
`GET /simulation` (current settings).

Failures depend on the seed, the product and how often that product was already requested. **Restart
the API between runs** to repeat the same failure pattern.

### 3. Process orders

```
dotnet run --project src/OrderProcessor.App -- process [--mode sync|async] [--input data/orders.csv] [--api http://localhost:5080] [--timeout-ms 5000] [--report data/report.txt]
```

Prints the report and writes it to `data/report.txt`. It shows processed and failed counts, failures
by category (`InvalidRow`, `UnknownProduct`, `ApiError`), the grand total, and one line for each
failed order with its reason. A failed order never stops the run or changes other totals.

### 4. Measure and save results

Add `--measure true` to `process` to record time, allocations and GC counts and append them to
`benchmarks/`. To run it properly (fresh API and fresh process for every run, Release build):

```
./scripts/run-benchmark.ps1 [-Modes sync,async] [-Runs 3] [-Rows 5000] [-DelayMs 5] [-ErrorRate 0] [-Seed 42]
```

### 5. Tests

```
dotnet test
```

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Every valid order is an `ApiError` ("request failed" or "timeout") | The API is not running or the port differs. Start it, or pass `--api http://localhost:<port>` to `process` |
| `address already in use` when starting the API | Port 5080 is taken. Stop the other process or use another port in `--urls` and `--api` |
| `Could not find file ... orders.csv` | Run `generate` first, or pass `--input` |
| Failures differ between two runs with the same settings | Restart the API between runs. The failure pattern depends on how often each product was requested before |
| Benchmark script cannot run | Run it from PowerShell. If scripts are blocked, use `powershell -ExecutionPolicy Bypass -File scripts/run-benchmark.ps1` |

## Measurements

`benchmarks/results.csv` has one row per run. `benchmarks/README.md` shows the same data as a table
and explains how to read it. Compare runs only when orders, API settings, concurrency and machine are
the same. Each run is a fresh process, so first-call JIT cost is included. `peak_working_set_mb` covers
the whole process, the other numbers cover the processing only.

Baseline (5,000 orders, 5 ms API delay, no errors, 3 runs each):

| Mode | Elapsed | Allocated | Peak working set | GC gen 0/1/2 |
|---|---|---|---|---|
| sync | about 76 s | 12.0 MB | about 52 MB | 1/0/0 |
| async | about 76 s | 17.3 MB | about 54 MB | 1/0/0 |

Both modes are sequential, so they take the same time. Async allocates more because of state
machines and tasks. Elapsed time is about 15.7 ms per request even though the API delay is 5 ms. The
likely cause is the default Windows timer resolution of about 15 ms.
