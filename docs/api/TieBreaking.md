# TieBreaking Enum

**Namespace:** `Migs.MPath.Core.Data`

Decides which route the pathfinder returns when several routes cost the same. Set it with `IPathfinderSettings.TieBreaking` (default `None`). Honoured by `GetPath`, `GetReachable` and `BeginStepwiseSearch`.

## Values

| Value | Description |
|-------|-------------|
| `None` (0) | Default and the behaviour of earlier versions. Costs are float sums; among equal-cost routes the search keeps whichever it expands first. Deterministic for a given grid and query, but the choice can change with unrelated grid edits, and on open ground it can zig-zag. |
| `FewerDiagonals` (1) | Among the cheapest routes, the one with the fewest diagonal steps, then the fewest steps. A straight line is the only route returned whenever it is one of the cheapest. |
| `FewerTurns` (2) | Among the cheapest routes, the one with the fewest direction changes (exact, not a heuristic). `GetReachable` has no route to straighten and ranks equal-cost routes like `FewerDiagonals` for its step counts. |

## How ties are decided

- **Exact costs.** With a tie-break, costs are compared as integers in units of 0.0001: every movement multiplier, cell weight and budget is rounded to four decimal places (exact for values written with up to four decimals below 1000, such as `1.41`; a single step costs at most 2^24). Routes of equal cost tie whatever order their steps are added in, and a budget of `3 * 1.41f` admits three 1.41 steps.
- **A fixed final rule.** Among routes equal on cost and on the tie-break key, the route is fixed by the grid alone, never by search order: walking back from the destination, it keeps its direction where it can, otherwise it takes the first of West, East, North, South, South-West, North-West, South-East, North-East (North is `-Y`).
- **Deterministic.** The same query on the same grid returns the same route on every run and platform, whatever cell order or grid representation the pathfinder was built from, and an edit elsewhere on the grid that leaves the competing routes alone does not change it.
- Step costs must be positive (the default multipliers are).

## Example

```csharp
var settings = new PathfinderSettings
{
    StraightMovementMultiplier = 1f,
    DiagonalMovementMultiplier = 1f,           // a diagonal costs one move, like a king
    IsCellWeightEnabled = false,
    TieBreaking = TieBreaking.FewerDiagonals   // ...but straight lines win ties
};
using var pathfinder = new Pathfinder(cells, settings);

using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(0, 5));
// (0,1) (0,2) (0,3) (0,4) (0,5): the straight column, path.Steps == 5, path.DiagonalSteps == 0

using var range = pathfinder.GetReachable(new Coordinate(0, 0), budget: 3f);
foreach (var cell in range.Cells)
{
    // cell.Steps and cell.DiagonalSteps are integers: no need to decode them from Cost
}
```

## Performance

`None` runs the original float search unchanged. A tie-break uses integer keys and also orders equal-cost cells by the tie-break key and then by distance travelled, so every route's cells are settled before the cells after them. On mazes this costs little; on open ground with many equal-cost routes it explores more cells than `None`. See the benchmarks in [docs/benchmarks](../benchmarks/README.md).
