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
      speedrun-reports/
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

Named snapshot creation is append-only. Restore first stages and verifies the
snapshot, then uses a recoverable per-file transaction. The transaction backs
up every overwritten or removed file, records type-conflicting directories,
removes declared blockers before writing, and verifies each applied file before
commit. A pre-commit failure or crash rolls the original file and directory tree
back from the journal. Commit removes the temporary restore point after all
per-file checks pass; the snapshot service then repeats the aggregate directory
hash check. Named snapshots are never transaction backups and are never removed
automatically.

## Loader and package trust

An instance may have exactly one effective loader state: `Vanilla`,
`ModdingApi`, `BepInEx`, `Conflict`, or `Drifted`. Downloads are accepted only
after URL policy, declared size or trusted HTTP content length, SHA-256, and ZIP
path validation pass. Packages are cached by SHA-256; a corrupt cache entry is
never installed.

Advanced local loader imports require a Crystalfly JSON manifest beside the
ZIP. The manifest declares loader family, supported builds, size, SHA-256, and
managed files. Local loaders remain unverified in their persisted receipt.

Catalog precedence is:

1. official Crystalfly catalog;
2. official Hollow Knight XML sources;
3. namespaced user HTTPS sources.

User sources cannot replace official IDs or grant verified speedrun status.

The embedded catalog is always the trust floor. A valid remote catalog replaces
the previous remote snapshot, so entries removed upstream do not reappear from
stale cache data. When the remote fetch or validation fails, the unsigned local
cache may only add channel aliases that point to builds already trusted by the
embedded catalog. It cannot contribute build fingerprints, executable packages,
or speedrun verification data. Unknown schema versions, invalid hashes, non-HTTPS
package URLs, and broken references are rejected before the cache is replaced.

Official `ModLinks.xml` and `ApiLinks.xml` entries are merged below Crystalfly's
official IDs. Custom sources are forced into their own namespace and are never
allowed to contribute builds, loaders, channels, or verified speedrun data.

## LocalLow isolation

Before first takeover, Crystalfly copies the complete shared Hollow Knight
LocalLow directory, including logs, to `local-low/shared-backup`. The backup is
hashed and retained permanently. The selected instance's first baseline is
copied from that backup without Unity, Modding API, or `Logs` directory files.
At discovery time this baseline is created for every known instance, not only
for the first instance launched.

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
touching the instance, replaces its files through the shared recoverable file
transaction, supports file/directory type changes, and retains the named snapshot
permanently after successful restore.

## Speedrun trust

Verified templates always use dedicated full-copy instances. The pre-launch
integrity check covers the game build, approved tools, rule revision, and all
managed file hashes. Its report is a point-in-time snapshot, not an attestation
that files remain unchanged after the report is written. Modding API, BepInEx,
DebugMod, and any unlisted file invalidate official status. Custom templates
never receive the official verification mark.

Provisioning resolves `RequiredAssetIds` through the catalog. ScreenShakeModifier
is installed as a verified raw assembly; LoadNormaliser is selected from its
verified archive by build and the chosen 1/2/3/5-second variant. All tool targets
are replaced in one file transaction. Every speedrun launch writes a JSON report
under the instance state directory; a failed trusted-template report blocks launch.
