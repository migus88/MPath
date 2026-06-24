# Visualizing the Search

## Overview

MPath normally computes a path in one shot with [`GetPath`](../api/Pathfinder.md). For teaching, debugging, or building an animated demo, it can also run the **same** A* search one expansion at a time, exposing the frontier and the explored area after every step. This is what [`Pathfinder.BeginStepwiseSearch`](../api/Pathfinder.md) provides.

It is an **educational / visualization** facility, not a real-time movement API:

- It runs the exact same expansion as `GetPath` (it shares the pathfinder's open set and cell buffer), so it finds the same path — it just pauses between expansions.
- It favours clarity over the allocation-free hot path: each snapshot owns plain arrays you can keep and render.

If you just want the path, keep using `GetPath`. Reach for the stepwise API when you want to *show how the path was found*.

## The loop

`BeginStepwiseSearch` returns a disposable [`StepwiseSearch`](../api/StepwiseSearch.md). Call `Tick()` to advance the search by one cell; each call returns a [`SearchStep`](../api/SearchStep.md) snapshot.

```csharp
using var pathfinder = new Pathfinder(cells, width, height);
var agent = new SimpleAgent { Size = 1 };

using var search = pathfinder.BeginStepwiseSearch(agent, new Coordinate(1, 1), new Coordinate(8, 8));

while (!search.IsComplete)
{
    var step = search.Tick();

    DrawFrontier(step.Searched);                 // open vs closed cells
    Highlight(step.Current);                      // the cell expanded this tick
    await NextFrame();
}

var final = search.Tick();                        // idempotent once complete
if (final.State == SearchState.Success)
{
    DrawPath(final.Path);
}
```

`RunToCompletion()` is a convenience that ticks until the search finishes and returns the final snapshot — handy when you only want the end state plus the full searched area:

```csharp
using var search = pathfinder.BeginStepwiseSearch(agent, from, to);
var final = search.RunToCompletion();
```

## What a step contains

Each [`SearchStep`](../api/SearchStep.md) is an immutable snapshot of the search so far:

| Member | Meaning |
|--------|---------|
| `State` | `InProgress`, `Success`, or `Failure`. |
| `Iteration` | How many cells have been expanded. |
| `Current` | The cell expanded on this tick — the "head" of the search. |
| `Searched` | Every cell discovered so far, each an [`Open`](../api/SearchNode.md) frontier cell or a `Closed` (expanded) cell, with its A* `g`/`h`/`f` scores. |
| `OpenCount` / `ClosedCount` | Sizes of the frontier and the closed set (`OpenCount + ClosedCount == Searched.Count`). |
| `Path` | The path once `State` is `Success`; otherwise empty. |

The `Searched` list is the accumulated searched area — the cells you typically colour to show progress. The standard A* picture is to draw the `Closed` set in one colour (already explored), the `Open` frontier in another (queued to explore), `Current` as the cursor, and `Path` once the search succeeds. Each [`SearchNode`](../api/SearchNode.md) also carries its `ScoreG` (cost from the origin) and `ScoreF` (priority), which you can label to show *why* A* picks the cells it does.

### The path

`Path` is the **raw** A* path — the parent-pointer chain from the origin to the destination. Following the same convention as [`PathResult`](../api/PathResult.md), it **excludes the origin cell** and ends at the destination. Path smoothing (configured via `PathfinderSettings.PathSmoothingMethod`) is a separate post-process and is **not** applied to the visualized path, so what you see is the search's own result.

## Lifetime and rules

- **Dispose the session.** It pins the pathfinder's cell buffer for the duration of the search (the open set holds raw pointers into it that must stay valid across ticks). Always wrap it in `using`.
- **One search at a time.** While a session is active, don't use the owning `Pathfinder` for anything else — not `GetPath`, not `GetReachable`, not a second `BeginStepwiseSearch`. Starting a second session before disposing the first throws `InvalidOperationException`. Once disposed, the pathfinder is free for normal use again.
- `Tick()` after completion returns the same final snapshot; `Tick()` after `Dispose()` throws `ObjectDisposedException`.

## Edge cases

- **Origin equals destination** succeeds on the first tick with an empty `Path` (matching `GetPath`).
- **Unreachable destination** ends in `State == Failure` with an empty `Path`; `Searched` then contains every cell the agent could reach (all `Closed`, `OpenCount == 0`).
- An origin or destination outside the grid throws `ArgumentException`; a `null` agent throws `ArgumentNullException`.
