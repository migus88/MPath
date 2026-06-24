# Movement Range (Reachability)

## Overview

In addition to finding a path between two points, MPath can answer "where can this agent move?" — every cell reachable from an origin without exceeding a movement budget. This is the core mechanic behind tactics and strategy games that highlight a unit's reachable tiles.

`Pathfinder.GetReachable` performs a uniform-cost (Dijkstra) flood fill bounded by the budget. It honours exactly the same movement rules as `GetPath`:

- Diagonal movement (`IsDiagonalMovementEnabled`) and corner-cutting (`IsMovementBetweenCornersEnabled`)
- Occupied cells (`IsCalculatingOccupiedCells`)
- Agent clearance for agents with `Size > 1`

## Cost model

Each step's cost is:

- `StraightMovementMultiplier` for a cardinal (horizontal/vertical) move
- `DiagonalMovementMultiplier` for a diagonal move

When `IsCellWeightEnabled` is set, the step cost is multiplied by the destination cell's `Weight` (so a cell with `Weight = 2` is twice as expensive to enter). The origin cell is always included with a cost of `0`.

A cell is reachable when the cheapest accumulated cost to it is **less than or equal to** the budget.

## Basic usage

```csharp
using var pathfinder = new Pathfinder(cells, width, height);
var agent = new SimpleAgent { Size = 1 };

// Find every cell reachable from (4, 4) for a budget of 5 movement points.
using var range = pathfinder.GetReachable(agent, new Coordinate(4, 4), 5f);

Console.WriteLine($"{range.Length} cells reachable");

foreach (var cell in range.Cells)
{
    Highlight(cell.Coordinate, cell.Cost);
}
```

## Querying the result

[`RangeResult`](../api/RangeResult.md) exposes the reachable cells as [`ReachableCell`](../api/ReachableCell.md) values (a `Coordinate` plus its `Cost`):

```csharp
// Index access
var first = range.Get(0);

// Convenience lookups (linear scans)
if (range.Contains(new Coordinate(6, 4)))
{
    range.TryGetCost(new Coordinate(6, 4), out var cost);
    Console.WriteLine($"Reachable for {cost}");
}
```

For repeated lookups, build a `Dictionary<Coordinate, float>` from `range.Cells` once rather than calling `TryGetCost` in a loop.

## Disposal

Like `PathResult`, `RangeResult` rents its backing array from a pool and **must be disposed** — always wrap it in a `using` statement (or call `Dispose()`), and copy out any cells you need to keep before disposing.

## Edge cases

- A negative budget returns an empty result (`IsSuccess == false`, `Length == 0`).
- A budget of `0` returns just the origin.
- An origin outside the grid throws `ArgumentException`; a `null` agent throws `ArgumentNullException`.
