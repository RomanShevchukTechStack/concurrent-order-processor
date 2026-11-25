# Benchmarks

Generated from `results.csv` by `process --measure true`. Do not edit by hand.
Written findings go in `notes.md`.

How to read:
- `elapsed_ms`, `allocated_mb` and `gc*` cover the processing only (file, HTTP, report).
- `peak_working_set_mb` is the peak of the whole process and includes runtime startup.
- Compare runs only when orders, API settings, concurrency and machine are the same.
- Each run is a fresh process and a fresh API, so the first-call JIT cost is included.
- A `+dirty` commit means uncommitted changes outside `benchmarks/` existed during the run.

| date_utc | commit | mode | orders | api_delay_ms | api_error_rate | api_seed | concurrency | elapsed_ms | peak_working_set_mb | allocated_mb | gc0 | gc1 | gc2 | processed | failed | grand_total |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 2026-11-25 11:20:13 | 257bb4b | sync | 5000 | 5 | 0 | 42 | 1 | 76161 | 52.0 | 12.0 | 1 | 0 | 0 | 4705 | 295 | 2082242.16 |
| 2026-11-25 11:21:32 | 257bb4b | sync | 5000 | 5 | 0 | 42 | 1 | 76184 | 53.2 | 12.0 | 1 | 0 | 0 | 4705 | 295 | 2082242.16 |
| 2026-11-25 11:22:51 | 257bb4b | sync | 5000 | 5 | 0 | 42 | 1 | 76154 | 53.4 | 12.0 | 1 | 0 | 0 | 4705 | 295 | 2082242.16 |
| 2026-11-25 11:24:09 | 257bb4b | async | 5000 | 5 | 0 | 42 | 1 | 76194 | 54.0 | 17.3 | 1 | 0 | 0 | 4705 | 295 | 2082242.16 |
| 2026-11-25 11:25:28 | 257bb4b | async | 5000 | 5 | 0 | 42 | 1 | 76219 | 54.1 | 17.3 | 1 | 0 | 0 | 4705 | 295 | 2082242.16 |
| 2026-11-25 11:26:46 | 257bb4b | async | 5000 | 5 | 0 | 42 | 1 | 76111 | 53.5 | 17.2 | 1 | 0 | 0 | 4705 | 295 | 2082242.16 |
