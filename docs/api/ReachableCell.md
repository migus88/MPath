# ReachableCell Struct

**Namespace:** `Migs.MPath.Core.Data`

A readonly value type representing a single cell reachable within a movement budget, together with the cost of the cheapest path to it from the query origin. Returned as part of a [RangeResult](RangeResult.md).

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Coordinate` | `Coordinate` | The coordinate of the reachable cell. |
| `Cost` | `float` | The cost of the cheapest path from the origin to this cell. Always `<=` the query budget. |

## Constructors

| Constructor | Description |
|-------------|-------------|
| `ReachableCell(Coordinate coordinate, float cost)` | Initializes a new reachable cell with the given coordinate and cost. |

## Remarks

- The cost is accumulated from per-step movement costs: `StraightMovementMultiplier` for cardinal moves and `DiagonalMovementMultiplier` for diagonal moves, multiplied by the destination cell's `Weight` when `IsCellWeightEnabled` is set.
- This is a `readonly struct`; enumerate it via [RangeResult.Cells](RangeResult.md) or index it with `RangeResult.Get(int)`.
