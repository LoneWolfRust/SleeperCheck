# SleeperCheck (Oxide/uMod Rust Plugin)

SleeperCheck is an administrative Rust plugin that finds pairs of sleeping players who are:

- close to each other (within a configurable distance),
- associated with nearby building blocks (and optionally the same building ID),
- and outside safe zones.

It is intended for moderation and auditing workflows where staff need quick visibility into suspicious sleeper clustering.

---

## Current Plugin Metadata

From `plugins/SleeperCheck.cs`:

- **Name:** `SleeperCheck`
- **Author:** `scar.dev`
- **Version:** `1.0.1`

---

## Features

- Scans sleepers from `BasePlayer.sleepingPlayerList`.
- Deduplicates by `userID`.
- Excludes sleepers in safe zones (`InSafeZone()`).
- Infers building context from nearby construction-layer `BuildingBlock` entities.
- Optionally requires exact same `buildingID` when matching pairs.
- Matches sleepers within configurable proximity distance.
- Sorts matches by:
  1. distance ascending,
  2. sleeper A name (case-insensitive),
  3. sleeper B name (case-insensitive).
- Supports both:
  - chat command: `/sleepercheck`
  - console command: `sleepercheck`
- Permission-gated for non-admin players (`sleepercheck.use`).
- Includes grid labels using `MapHelper.PositionToString`.
- Truncates printed results using configurable max-output cap.

---

## Requirements

- Rust dedicated server with Oxide/uMod plugin support.
- `plugins/SleeperCheck.cs` in your Oxide plugins folder.

No external plugin dependency is required for grid output.

---

## Installation

1. Copy `plugins/SleeperCheck.cs` to `oxide/plugins/SleeperCheck.cs`.
2. (Optional) Copy `configs/SleeperCheck.json` to `oxide/config/SleeperCheck.json`.
3. Reload plugin:

```bash
oxide.reload SleeperCheck
```

Or restart the server.

---

## Permissions

Registered permission:

- `sleepercheck.use`

Access behavior:

- Server admins are always allowed.
- Non-admin players must have `sleepercheck.use`.

Examples:

```bash
oxide.grant user <steamid64> sleepercheck.use
oxide.grant group admin sleepercheck.use
```

---

## Commands

### Chat command

```text
/sleepercheck
```

### Console command

```text
sleepercheck
```

- Player execution checks permission/admin access.
- RCON/server console execution is allowed.

---

## Detection Flow

For each sleeping player, the plugin:

1. skips null/invalid entities,
2. skips duplicate user IDs,
3. skips sleepers in safe zones,
4. finds the nearest valid nearby `BuildingBlock` and its `buildingID`,
5. keeps only sleepers with non-zero building IDs.

Then it compares sleeper pairs:

- uses `SleeperProximityMeters` as the maximum pair distance,
- optionally requires identical building IDs (`RequireSameBuildingId`),
- records and sorts all matching pairs.

---

## Configuration

Default configuration:

```json
{
  "SleeperProximityMeters": 6.0,
  "BuildingSearchRadiusMeters": 5.0,
  "MaxResultsToPrint": 100,
  "RequireSameBuildingId": true,
  "IncludeGridIfGridApiPresent": true
}
```

> Note: `IncludeGridIfGridApiPresent` controls whether grid is included, but current implementation uses `MapHelper.PositionToString` directly and does not call an external Grid API plugin.

### Options

- `SleeperProximityMeters` (`float`)
  - Maximum distance between two sleepers for a match.
- `BuildingSearchRadiusMeters` (`float`)
  - Radius used to scan construction entities to infer nearest building ID.
- `MaxResultsToPrint` (`int`)
  - Maximum number of pair lines printed after summary.
- `RequireSameBuildingId` (`bool`)
  - When true, only pairs sharing the exact same building ID are reported.
- `IncludeGridIfGridApiPresent` (`bool`)
  - When true, includes grid text for each sleeper in output.

---

## Output Example

```text
SleeperCheck: scanned 42 qualifying sleepers. Found 3 matching pair(s).
[1] PlayerOne (76561198000000001) [H19] <-> PlayerTwo (76561198000000002) [H19] | Distance: 1.24m | BuildingID: 137521
[2] Alpha (76561198000000003) [J14] <-> Bravo (76561198000000004) [J14] | Distance: 2.03m | BuildingID: 224901
```

If output exceeds `MaxResultsToPrint`, plugin prints a truncation line.

---

## Troubleshooting

### Command says permission denied

- Ensure player is admin or granted `sleepercheck.use`.

### No matches found

- There may be fewer than 2 qualifying sleepers.
- Sleepers in safe zones are intentionally excluded.
- `BuildingSearchRadiusMeters` may be too small for your structures.
- `RequireSameBuildingId` may be filtering expected pairs.
- `SleeperProximityMeters` may be set too low.

### Grid displays `n/a`

- Grid inclusion may be disabled (`IncludeGridIfGridApiPresent = false`).
- Grid conversion can return empty/unexpected values in edge cases.

---

## Performance Notes

- Pair comparison is **O(n²)** over qualifying sleepers.
- `MaxResultsToPrint` limits only output volume, not scan cost.
- On high-pop servers, tune building search radius and proximity values to reduce match volume.

---

## License

No explicit license file is currently included in this repository.
