# Compatibility and performance measurement

Compares the working tree with the published `ReedSolomon` packages 1.0.0 through 3.0.0. The same
source is compiled once per version, so every version runs identical checks.

```bash
bash validation/compatibility/run.sh        # five process rounds
bash validation/compatibility/run.sh 1      # a quick single round
```

The script needs Bash and the .NET 10 SDK. On Windows it runs in Git Bash. It downloads the published
packages from nuget.org and replaces `results/`, which is not versioned. `results/COMPATIBILITY.md`
is the readable comparison. Each `report-*.json` holds the raw observations of one version, and
`performance.jsonl` holds one line per measured process. `api-differences-to-2.0.0.xml` and
`api-differences-to-3.0.0.xml` are the SDK's package validation output, listing each public API
difference. A file is missing when there is no difference.

## What is measured

- **Stored format:** SHA-256 of the parity for 43 shard counts, and recovery of all or, for large
  shard counts, representative loss patterns through each version's buffer API.
- **Shard layout:** SHA-256 of all shards from the allocating encoder, from version 1.1.0 on.
- **Recovery and behavior:** the allocating decoder and scenarios where a drop-in upgrade could differ,
  for 2.0.0, 3.0.0 and the working tree. An exception is recorded as an observation.
- **Public API:** the SDK's package validation against 2.0.0, the last release of the old API, and
  against 3.0.0, the first release of the current API.
- **Performance:** 10 data and 2 parity shards with 4 KiB, 16 MiB and 420 MiB payloads, for 2.0.0, 3.0.0
  and the working tree with default settings. `managed-*` returns new results and `reuse-*` keeps the caller's
  buffers. Decoding restores the first and last data shard.

Each performance value comes from a fresh process pinned to one logical processor, with tiered
compilation and concurrent garbage collection disabled. The process warms up for at least one second
and one call, then times five batches of at least 200 ms and one call. Results are verified after
timing. The order of versions reverses between rounds. The table shows the median of the process
medians and the fastest and slowest process.

## Limits

The machine is not isolated, so close values do not establish a ranking. Timings describe the measuring
machine, not an absolute limit. The loss patterns are exhaustive only up to 12 total shards.
The checks cover what the published packages expose: 1.x recovery uses the buffer API because their
allocating decoders differ in signature, and only their encoded layout is compared.
