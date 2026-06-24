# LineOfSightMode Enum

**Namespace:** `Migs.MPath.Core.Data`

Controls how [`Pathfinder.HasLineOfSight`](Pathfinder.md) treats cells that are not walkable when tracing the line between two coordinates.

## Values

| Value | Description |
|-------|-------------|
| `BlockedByUnwalkableCells` | **(default)** A non-walkable cell between the endpoints blocks line of sight, matching how walls interrupt vision in most grid games. |
| `IgnoreUnwalkableCells` | Non-walkable cells are treated as transparent and do not block line of sight. Use this when an obstacle blocks movement but not vision (water, a pit, a chasm, a glass wall). |

## Usage Example

```csharp
// Walls block the shot (default).
bool blockedByWalls = pathfinder.HasLineOfSight(shooter, target);

// Terrain is see-through; only occupants can still block.
bool ignoringTerrain = pathfinder.HasLineOfSight(shooter, target, LineOfSightMode.IgnoreUnwalkableCells);
```

## Remarks

- The mode **only** governs walkability. Occupancy is handled separately: when
  `IsCalculatingOccupiedCells` is enabled, an occupied cell blocks sight under **either** mode. This makes
  "ignore terrain, but units still block" expressible by combining `IgnoreUnwalkableCells` with occupancy.
- The endpoints themselves are never tested, so a target standing on a blocked or occupied cell can still be
  seen regardless of mode.
- The default value (`BlockedByUnwalkableCells`) is `0`, so `default(LineOfSightMode)` preserves the
  blocking behaviour.

See the [Distance and Line of Sight guide](../guides/distance-and-line-of-sight.md) for the full picture.
