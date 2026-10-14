# ReachableCell Struct

**Namespace:** `Migs.MPath.Core.Data`

A readonly value type representing a single cell reachable within a movement budget, together with the cost of the cheapest path to it from the query origin. Returned as part of a [RangeResult](RangeResult.md).

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Coordinate` | `Coordinate` | The coordinate of the reachable cell. |
| `Cost` | `int` | The cost of the cheapest path from the origin to this cell. Always `<=` the query budget. |
| `Steps` | `int` | The number of steps of that cheapest path (`0` for the origin). With a [`TieBreaking`](TieBreaking.md): the cheapest path with the fewest diagonal steps, then the fewest steps; without one, whichever cheapest path the search kept. |
| `DiagonalSteps` | `int` | How many of `Steps` are diagonal, on the same path. |

## Constructors

| Constructor | Description |
|-------------|-------------|
| `ReachableCell(Coordinate coordinate, int cost, int steps, int diagonalSteps)` | Initializes a new reachable cell with step counts. |

## Remarks

- The cost is accumulated from per-step movement costs: `StraightStepCost` for cardinal moves and `DiagonalStepCost` for diagonal moves, plus the destination cell's `Weight` (added) when `IsCellWeightEnabled` is set.
- Costs are integers, so they are exact: with the default step costs a budget of `3 * 141` admits three diagonal steps (`Cost` reads `423`) and `3 * 141 - 1` does not.
- This is a `readonly struct`; enumerate it via [RangeResult.Cells](RangeResult.md) or index it with `RangeResult.Get(int)`.
