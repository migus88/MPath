# TieBreaking Enum

**Namespace:** `Migs.MPath.Core.Data`

Decides which route the pathfinder returns when several routes cost the same. Set it with `IPathfinderSettings.TieBreaking` (default `None`). Honoured by `GetPath`, `GetReachable` and `BeginStepwiseSearch`.

## Values

| Value | Description |
|-------|-------------|
| `None` (0) | Default and the fastest search. Among equal-cost routes the search keeps whichever it expands first. Deterministic for a given grid and query, but the choice can change with unrelated grid edits, and on open ground it can zig-zag. |
| `FewerDiagonals` (1) | Among the cheapest routes, the one with the fewest diagonal steps, then the fewest steps. A straight line is the only route returned whenever it is one of the cheapest. |
| `FewerTurns` (2) | Among the cheapest routes, the one with the fewest direction changes (exact, not a heuristic). `GetReachable` has no route to straighten and ranks equal-cost routes like `FewerDiagonals` for its step counts. |

## How ties are decided

- **Exact costs.** Costs are integers in every mode, so routes of equal cost tie whatever order their steps are added in, and a budget equal to a route's cost admits it.
- **A fixed final rule.** Among routes equal on cost and on the tie-break key, the route is fixed by the grid alone, never by search order: walking back from the destination, it keeps its direction where it can, otherwise it takes the first of West, East, North, South, South-West, North-West, South-East, North-East (North is `-Y`).
- **Deterministic.** The same query on the same grid returns the same route on every run and platform, whatever cell order or grid representation the pathfinder was built from, and an edit elsewhere on the grid that leaves the competing routes alone does not change it.
- Step costs are at least 1 (the pathfinder validates them).

## Example

```csharp
var settings = new PathfinderSettings
{
    StraightStepCost = 1,
    DiagonalStepCost = 1,                      // a diagonal costs one move, like a king
    IsCellWeightEnabled = false,
    TieBreaking = TieBreaking.FewerDiagonals   // ...but straight lines win ties
};
using var pathfinder = new Pathfinder(cells, settings);

using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(0, 5));
// (0,1) (0,2) (0,3) (0,4) (0,5): the straight column, path.Steps == 5, path.DiagonalSteps == 0

using var range = pathfinder.GetReachable(new Coordinate(0, 0), budget: 3);
foreach (var cell in range.Cells)
{
    // cell.Steps and cell.DiagonalSteps are integers: no need to decode them from Cost
}
```

## Performance

`None` runs the plain search. A tie-break also orders equal-cost cells by the tie-break key and then by distance travelled, so every route's cells are settled before the cells after them. On mazes this costs little; on open ground with many equal-cost routes it explores more cells than `None`. See the benchmarks in [docs/benchmarks](../benchmarks/README.md).
