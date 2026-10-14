# Movement Range (Reachability)

## Overview

In addition to finding a path between two points, MPath can answer "where can this agent move?" — every cell reachable from an origin without exceeding a movement budget. This is the core mechanic behind tactics and strategy games that highlight a unit's reachable tiles.

`Pathfinder.GetReachable` performs a uniform-cost (Dijkstra) flood fill bounded by the budget. It honours exactly the same movement rules as `GetPath`:

- Diagonal movement (`IsDiagonalMovementEnabled`) and corner-cutting (`CornerBlocking`)
- Occupied cells (`AreOccupiedCellsBlocking`)
- Agent clearance for agents with `Size > 1`

## Cost model

Each step's cost is:

- `StraightStepCost` for a cardinal (horizontal/vertical) move (100 by default)
- `DiagonalStepCost` for a diagonal move (141 by default)

When `IsCellWeightEnabled` is set, the destination cell's `Weight` is **added** to the step cost (so a cell with `Weight = 200` costs an extra 200 to enter, on top of the travel cost). Weight is added rather than multiplied — consistent with the main pathfinder, and so that the default `Weight` of 0 leaves the step cost equal to the travel cost instead of zeroing it out. The origin cell is always included with a cost of `0`.

A cell is reachable when the cheapest accumulated cost to it is **less than or equal to** the budget. Costs and budgets are `int`s in the unit of the step costs, so the comparison is exact: a budget equal to a route's cost admits it, and one less excludes it. Routes costing more than 1,073,741,823 (`int.MaxValue / 2`) are never reached, so `int.MaxValue` is an unbounded budget. A negative weight written into the grid counts as 0 (`SetWeight`/`SetCell` reject one).

## Basic usage

```csharp
using var pathfinder = new Pathfinder(cells, width, height);
// Find every cell reachable from (4, 4) for a budget of 5 straight steps (default step costs 100/141).
using var range = pathfinder.GetReachable(new Coordinate(4, 4), 500);

Console.WriteLine($"{range.Length} cells reachable");

foreach (var cell in range)
{
    Highlight(cell.Coordinate, cell.Cost);
}
```

## Querying the result

[`RangeResult`](../api/RangeResult.md) exposes the reachable cells as [`ReachableCell`](../api/ReachableCell.md) values (a `Coordinate`, its `Cost`, and the integer `Steps` / `DiagonalSteps` of its cheapest route):

```csharp
// Index and span access (no allocation; foreach (var cell in range) neither)
var first = range[0];
ReadOnlySpan<ReachableCell> all = range.AsSpan();

// Lookups by coordinate, O(1)
if (range.TryGetCost(new Coordinate(6, 4), out var cost))
{
    Console.WriteLine($"Reachable for {cost}");
}

// A dense per-cell cost field in the pathfinder's layout (costs[pathfinder.GetIndex(c)]), -1 where unreached
var costs = new int[width * height];
range.CopyCostsTo(costs);
```

`Contains`, `TryGetCost` and `TryGetCell` are O(1): the first lookup builds an index over the reached cells (one pass, rented from the array pool and sized to their bounding box), so "is this cell in range?" over a whole board costs no more than walking the range once. The index belongs to the result: it stays valid after later queries and returns to the pool when the result is disposed. Spans and enumerators read the result's pooled array, so don't keep them past `Dispose`. `Cells` still works but allocates an iterator per enumeration.

## Counting moves instead of costs

Board games often count moves: every step costs one movement point, diagonals included, and straight lines should win ties. Use equal step costs of 1, a [`TieBreaking`](../api/TieBreaking.md), and read the step counts directly:

```csharp
var settings = new PathfinderSettings
{
    StraightStepCost = 1,
    DiagonalStepCost = 1,
    IsCellWeightEnabled = false,
    TieBreaking = TieBreaking.FewerDiagonals
};
using var pathfinder = new Pathfinder(cells, settings);
using var range = pathfinder.GetReachable(origin, budget: 4);

if (range.TryGetCell(target, out var cell))
{
    Console.WriteLine($"{cell.Steps} moves, {cell.DiagonalSteps} of them diagonal");
}
```

Costs are integers, so the counts and the budget never drift. With a tie-break the counts describe the route with the fewest diagonals, then the fewest steps; without one, `Steps` / `DiagonalSteps` still describe the cheapest route the search kept.

## Per-query occupancy

The same pathfinder can answer "where can this unit move?" (units block) and "what can this unit threaten?" (units ignored) on one grid. Pass [`QueryOptions`](../api/QueryOptions.md):

```csharp
using var move   = pathfinder.GetReachable(origin, 500);
using var threat = pathfinder.GetReachable(origin, 500, 1, new QueryOptions(OccupancyRule.Ignore));

// Units block, except the ones listed (e.g. allies this unit may pass through):
ReadOnlySpan<Coordinate> passable = stackalloc Coordinate[] { ally1, ally2 };
using var pass = pathfinder.GetReachable(origin, 500, 1, new QueryOptions(alwaysEnterable: passable));
```

The options apply to that call only and never modify the grid. `default(QueryOptions)` behaves exactly like the overload without options.

## Disposal

Like `PathResult`, `RangeResult` rents its backing array from a pool and **must be disposed** — always wrap it in a `using` statement (or call `Dispose()`), and copy out any cells you need to keep before disposing.

## Edge cases

- A negative budget returns an empty result (`IsSuccess == false`, `Length == 0`).
- A budget of `0` returns just the origin.
- An origin outside the grid throws `ArgumentException`; an `agentSize` below 1 throws `ArgumentOutOfRangeException`.
