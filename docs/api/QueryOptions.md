# QueryOptions Struct

**Namespace:** `Migs.MPath.Core.Data`

Movement rules that override the pathfinder's settings for **one query**: whether occupied cells block, and which occupied cells may be entered anyway. Pass it to the `GetPath`, `GetReachable` and `BeginStepwiseSearch` overloads that take options. The grid and the settings stay untouched, so one `Pathfinder` serves "units block" and "units ignored" queries on the same board.

A `readonly ref struct`: it never allocates, and `AlwaysEnterable` takes any span (`stackalloc`, an array, a slice). It can't be stored in a field or captured by a lambda; build it at the call site.

## Constructor

| Constructor | Description |
|-------------|-------------|
| `QueryOptions(OccupancyRule occupancy = OccupancyRule.UseSettings, ReadOnlySpan<Coordinate> alwaysEnterable = default, bool isDestinationAlwaysEnterable = false)` | Creates options; every argument is optional. |

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Occupancy` | [`OccupancyRule`](OccupancyRule.md) | Whether occupied cells block in this query. `UseSettings` (default) follows `IsCalculatingOccupiedCells`; `Block` and `Ignore` override it. |
| `AlwaysEnterable` | `ReadOnlySpan<Coordinate>` | Cells whose `IsOccupied` flag is ignored in this query, e.g. the mover's own cells and its target. |
| `IsDestinationAlwaysEnterable` | `bool` | Treats the destination as an `AlwaysEnterable` cell, so a path to an occupied cell (move to attack) can succeed. `GetPath` and `BeginStepwiseSearch` only; `GetReachable` has no destination and ignores it. |

## Remarks

- `default(QueryOptions)` (or `new QueryOptions()`) reproduces the overloads without options exactly.
- An always-enterable cell is treated as **unoccupied** for the query: it can be entered, it counts as clear in a multi-cell agent's footprint, and it is an **open** flank for the [`CornerBlocking`](CornerBlocking.md) rule. Walls (`IsWalkable == false`) stay walls.
- `Occupancy` reclassifies occupied cells the same way everywhere: entry, clearance, the corner rule, the destination check and `StringPulling` smoothing. `HasLineOfSight` takes no options and keeps following the settings.
- The span is scanned linearly, and only when an occupied cell is met, so free cells pay nothing and a handful of entries (typically 1-4) costs almost nothing. It is read only during the call (and, for a stepwise search, copied when the search begins).
- With [path caching](../guides/path-caching.md) enabled, a query whose options change the rules neither reads nor fills the cache (cached paths were found under the settings' rules). Options that resolve to the settings' rules use the cache as usual.

## Example

```csharp
// Movement range with units blocking, threat range with units ignored: same pathfinder, same grid.
using var moveRange   = pathfinder.GetReachable(agent, unitCell, 5f);
using var threatRange = pathfinder.GetReachable(agent, unitCell, 5f, new QueryOptions(OccupancyRule.Ignore));

// Move to attack: the mover stands on its own (occupied) cells and the target cell is occupied.
ReadOnlySpan<Coordinate> ownCells = stackalloc Coordinate[] { unitCell };
using var approach = pathfinder.GetPath(agent, unitCell, targetCell,
    new QueryOptions(alwaysEnterable: ownCells, isDestinationAlwaysEnterable: true));
```
