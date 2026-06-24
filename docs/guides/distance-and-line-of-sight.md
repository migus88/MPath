# Distance and Line of Sight

## Overview

Not every spatial question needs a full path. MPath exposes three lightweight queries on `Pathfinder` for the common "how far?" and "can I see it?" cases that show up in fog-of-war, ranged combat, AI target selection and movement-range UIs:

- `Pathfinder.GetManhattanDistance(from, to)` — taxicab distance
- `Pathfinder.GetChebyshevDistance(from, to)` — chessboard distance
- `pathfinder.HasLineOfSight(from, to)` — unobstructed straight line test

All three are allocation-free. The two distance methods are also `static` and pure, so you can call them without a `Pathfinder` instance.

## Distance metrics

The distance helpers operate purely on coordinates — they ignore walls, cell weights and movement settings, so they always return the *geometric* grid distance, not a traversal cost. Use `GetReachable` or `GetPath` when you need the real cost of moving across the grid.

| Metric | Formula | Use it when |
|--------|------------------------|-------------|
| Manhattan | `\|dx\| + \|dy\|` | Movement is cardinal-only (no diagonals). Also the A* heuristic MPath uses internally. |
| Chebyshev | `max(\|dx\|, \|dy\|)` | Diagonal moves cost the same as cardinal ones (8-directional grids). |

```csharp
var a = new Coordinate(1, 1);
var b = new Coordinate(4, 5);

int manhattan = Pathfinder.GetManhattanDistance(a, b); // 3 + 4 = 7
int chebyshev = Pathfinder.GetChebyshevDistance(a, b); // max(3, 4) = 4
```

Both metrics are symmetric (`distance(a, b) == distance(b, a)`), return `0` for identical coordinates, and return an `int` because grid coordinates are integers. `Chebyshev <= Manhattan` always holds.

## Line of sight

`HasLineOfSight` traces a [Bresenham line](https://en.wikipedia.org/wiki/Bresenham%27s_line_algorithm) between the two cells and returns `false` as soon as it crosses a blocked cell — the same line-tracing the string-pulling path smoother uses internally. It is an O(distance) walk that allocates nothing.

```csharp
using var pathfinder = new Pathfinder(cells, width, height);

if (pathfinder.HasLineOfSight(shooter, target))
{
    // Fire away — nothing is in the way.
}
else
{
    // Need to path around cover.
    using var path = pathfinder.GetPath(agent, shooter, target);
}
```

### What blocks sight

A cell between the endpoints blocks line of sight when it is **not walkable**, or — when `IsCalculatingOccupiedCells` is enabled (the default) — when it is **occupied**. This mirrors how `GetPath` and `GetReachable` treat the grid, so "can I see it?" and "can I walk there?" stay consistent.

### Behavior and edge cases

- **Endpoints are never tested.** Only the cells *between* `from` and `to` are checked, so a target standing on a blocked or occupied cell (for example, an enemy unit) can still be seen.
- **Single-cell ray.** Agent `Size` is not considered — the line is one cell wide. A large agent that *fits through* a gap visually may still be unable to *path* through it.
- **Self.** A coordinate always has line of sight to itself.
- **Bounds.** `from` and `to` must be inside the grid; an out-of-range coordinate throws `ArgumentException`.
- **Symmetry.** `HasLineOfSight(a, b)` equals `HasLineOfSight(b, a)`.

## When to use which

- Need a quick range check or to sort candidates by closeness? Use a **distance metric**.
- Need to know whether something is *visible* (no walls in between)? Use **`HasLineOfSight`**.
- Need the actual route, accounting for walls and weights? Use [`GetPath`](../api/Pathfinder.md).
- Need every tile within a movement budget? Use [`GetReachable`](movement-range.md).
