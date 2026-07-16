# Crystalfly architecture

## Scope

Crystalfly manages Hollow Knight game instances, mutually exclusive loaders,
mods, instance save data, named snapshots, Steam depot downloads, and verified
speedrun environments on Windows 10/11 x64.

## Storage layout

```text
<version-root>/
  <instance>/
    .crystalfly-instance.json
  .crystalfly/
    catalog/
    downloads/
    instances/<instance-id>/
      local-low/
      snapshots/<snapshot-id>/
        snapshot.json
        data/
    local-low/
      takeover.json
      shared-backup/
      transactions/<session-id>/journal.json
    packages/
    transactions/
```

Only direct children of the selected version root are scanned as instances.
The `.crystalfly` directory is always excluded. Portable and installed-mode
application settings do not change this per-root layout.

## Build identity

Build records are immutable and are keyed by a verified build ID. The `latest`
and `public` names are channels that point to a build. A new Steam public
manifest therefore creates a new build record instead of inheriting loader
compatibility from the previous public build.

Known builds are identified from SHA-256 values of unmodified game files.
`Assembly-CSharp.dll` is inspected only as loader evidence because Modding API
replaces it. Unknown manifests remain launchable as vanilla instances, but
automatic loader installation stays locked until the catalog verifies them.

## Transaction boundary

Every loader, mod, and LocalLow switch/write-back operation follows the same
journal states:

```text
Prepared -> Applying -> Committed
                  \-> RollingBack -> RolledBack
                                   \-> NeedsAttention
```

Target files are staged and hashed before replacement. Existing files are
copied to transaction-private backups. A successful transaction removes its
temporary recovery point. An incomplete rollback leaves the journal and blocks
launch until the user resolves it.

Named snapshot creation is append-only. Restore uses a verified directory swap:
the previous instance directory remains beside the target until the restored
directory hash is verified. Snapshots are never used as temporary transaction
backups and are never removed automatically.

## Loader and package trust

An instance may have exactly one effective loader state: `Vanilla`,
`ModdingApi`, `BepInEx`, `Conflict`, or `Drifted`. Installation is driven by a
catalog file manifest, never by archive layout guesses. Downloads are accepted
only after URL policy, size, SHA-256, and ZIP path validation pass.

Catalog precedence is:

1. official Crystalfly catalog;
2. official Hollow Knight XML sources;
3. namespaced user HTTPS sources.

User sources cannot replace official IDs or grant verified speedrun status.

## LocalLow isolation

Before first takeover, Crystalfly copies the complete shared Hollow Knight
LocalLow directory, including logs, to `local-low/shared-backup`. The backup is
hashed and retained permanently. The selected instance's first baseline is
copied from that backup without Unity, Modding API, or `Logs` directory files.

`InstanceRuntimeSession` acquires the global named mutex
`Global\Crystalfly.HollowKnight.Runtime` and checks every process named
`hollow_knight` before changing LocalLow. The mutex handle remains alive for the
whole runtime session. Completion refuses to write back while such a process is
still running.

Switch-in stages the instance data beside the shared LocalLow directory, moves
the current shared directory to a session-specific preserved path, then moves
the staged instance into place. After process exit, non-log data is copied to a
staging directory beside the instance, the previous instance directory is
preserved, and the verified capture replaces it. Only then is active LocalLow
removed and the preserved shared directory restored.

The LocalLow journal records aggregate directory hashes and these phases:

```text
Prepared -> ActivationStaged -> SharedPreserved -> InstanceActive
         -> CaptureStaged -> InstanceCaptured -> SharedRestored -> Completed
         \-> RolledBack
         \-> NeedsAttention
```

Recovery accepts only path layouts and directory hashes that prove which move
completed. It either rolls back a switch that never became active or finishes
write-back for an active session. Missing, changed, or ambiguous data becomes
`NeedsAttention`; recovery retains every relevant path and blocks another
launch instead of guessing.

## Named snapshots

Named snapshots are manual, per-instance copies under
`instances/<instance-id>/snapshots`. Each snapshot stores immutable metadata and
a deterministic directory SHA-256. Creation and restore use the same runtime
mutex and process check as launch. Restore verifies the stored snapshot before
touching the instance, replaces the instance directory exactly, and retains the
named snapshot permanently after successful restore.

## Speedrun trust

Verified templates always use dedicated full-copy instances. Verification
checks the game build, approved tools, rule revision, and all managed file
hashes before every launch. Modding API, BepInEx, DebugMod, and any unlisted
file invalidate official status. Custom templates never receive the official
verification mark.
