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
      snapshots/
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

Every loader, mod, instance save-data, and snapshot operation follows the same
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

Before first takeover, Crystalfly backs up the shared Hollow Knight LocalLow
directory. Launch copies the selected instance's non-log data into LocalLow.
Process exit copies it back, then restores the original shared data. Process
checks, a named mutex, and the transaction journal prevent concurrent mutation.

## Speedrun trust

Verified templates always use dedicated full-copy instances. Verification
checks the game build, approved tools, rule revision, and all managed file
hashes before every launch. Modding API, BepInEx, DebugMod, and any unlisted
file invalidate official status. Custom templates never receive the official
verification mark.

