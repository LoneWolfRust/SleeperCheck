# SleeperCheck (Oxide/uMod Rust Plugin)

SleeperCheck is an administrative Rust server plugin that scans sleeping players and reports pairs that appear to be sleeping unusually close together **inside the same building** and **outside safe zones**.

It is useful for moderation workflows, base auditing, and identifying potential alt-account clusters or coordinated sleeper placement.

---

## Features

- Scans all currently sleeping players (`BasePlayer.sleepingPlayerList`).
- Filters out sleepers in safe zones.
- Detects nearby building context via nearby `BuildingBlock` entities.
- Optionally enforces same `buildingID` for pair matching.
- Reports sleeper pairs within configurable proximity distance.
- Supports both:
  - Chat command: `/sleepercheck`
  - Console command: `sleepercheck`
- Permission-gated for non-admin users.
- Optional grid coordinate display via `GridAPI` plugin if present.
- Configurable max printed results to avoid console/chat spam.

---

## Requirements

- Rust dedicated server running Oxide/uMod-compatible plugins.
- This plugin file at:
  - `plugins/SleeperCheck.cs`

Optional:
- `GridAPI` plugin, if you want grid references in output.

---

## Installation

1. Copy `plugins/SleeperCheck.cs` into your server's `oxide/plugins/` directory.
2. (Optional) Copy the example config from `configs/SleeperCheck.json` into `oxide/config/SleeperCheck.json`.
3. Reload plugin:

```bash
oxide.reload SleeperCheck
```

Or restart the server.

---

## Permissions

The plugin registers:

- `sleepercheck.use`

By default, server admins can always run the command. Non-admins must be granted this permission.

### Example permission grant

```bash
oxide.grant user <steamid64> sleepercheck.use
oxide.grant group admin sleepercheck.use
```

---

## Commands

### Chat

```text
/sleepercheck
```

### Server/client console

```text
sleepercheck
```

If run by a player without permission, the plugin returns an access error.

---

## How Detection Works

For each sleeping player:

1. Ignore invalid/null entities.
2. Ignore duplicate user IDs.
3. Ignore sleepers currently in safe zones.
4. Find nearest building ID from nearby construction-layer entities.
5. Keep only sleepers associated with a valid building ID.

Then pairwise comparison is performed:

- If `RequireSameBuildingId = true`, only sleepers in same building are compared.
- If distance between two sleepers is less than or equal to `SleeperProximityMeters`, a match is recorded.
- Results are sorted by:
  1. Distance ascending
  2. Sleeper A name
  3. Sleeper B name

---

## Configuration

Default settings:

```json
{
  "SleeperProximityMeters": 6.0,
  "BuildingSearchRadiusMeters": 5.0,
  "MaxResultsToPrint": 100,
  "RequireSameBuildingId": true,
  "IncludeGridIfGridApiPresent": true
}
```

### Option reference

- `SleeperProximityMeters` (float)
  - Maximum distance between two sleepers to count as a match.
- `BuildingSearchRadiusMeters` (float)
  - Radius used to locate nearby building blocks and infer building ID.
- `MaxResultsToPrint` (int)
  - Safety cap for printed output lines.
- `RequireSameBuildingId` (bool)
  - If true, only report pairs in exactly the same building ID.
- `IncludeGridIfGridApiPresent` (bool)
  - If true and `GridAPI` is installed, include map grid tag per sleeper.

---

## Output Example

```text
SleeperCheck: scanned 42 qualifying sleepers. Found 3 matching pair(s).
[1] PlayerOne (76561198000000001) [H19] <-> PlayerTwo (76561198000000002) [H19] | Distance: 1.24m | BuildingID: 137521
[2] Alpha (76561198000000003) [J14] <-> Bravo (76561198000000004) [J14] | Distance: 2.03m | BuildingID: 224901
```

If result count exceeds `MaxResultsToPrint`, plugin emits truncation notice.

---

## Troubleshooting

### Compilation error: `The name 'Pool' does not exist in the current context`

This plugin uses Facepunch pooling utilities (`Pool.GetList` / `Pool.FreeList`). Ensure the plugin includes:

```csharp
using Facepunch;
```

### No results found

- Sleepers may be in safe zones and filtered out.
- `BuildingSearchRadiusMeters` may be too small for your build layouts.
- `RequireSameBuildingId` may be too strict for your use case.

### Grid shows `n/a`

- `GridAPI` is not installed, unloaded, or not returning expected format.
- Set `IncludeGridIfGridApiPresent` to `false` to skip lookup.

---

## Performance Notes

- Pair finding is O(n²) on qualifying sleepers.
- For very high-pop servers, keep proximity and search constraints tuned to reduce candidate sets.
- `MaxResultsToPrint` limits output size only (not scan cost).

---

## Security / Access Control

- Use `sleepercheck.use` conservatively.
- Restrict usage to trusted staff roles.
- Consider logging command usage in your moderation pipeline.

---

## Versioning

Current plugin metadata:

- Name: `SleeperCheck`
- Author: `ScarDev`
- Version: `1.0.0`

---

## License

No explicit license is included in this repository. Add a license file if you want to define reuse terms.
