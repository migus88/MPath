# Pathfinder.StepwiseSearch Class

**Namespace:** `Migs.MPath.Core` (nested in [`Pathfinder`](Pathfinder.md))

A resumable, tick-by-tick driver over the A* search, intended for **visualizing or teaching** how the pathfinder explores the grid. Obtain one from [`Pathfinder.BeginStepwiseSearch`](Pathfinder.md). Each tick performs a single expansion using the owning `Pathfinder`'s own machinery — so it produces the same path as `GetPath` — and returns a [`SearchStep`](SearchStep.md) snapshot.

Implements `IDisposable`.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `State` | [`SearchState`](SearchState.md) | The overall state of the search (`InProgress`, `Success` or `Failure`). |
| `IsComplete` | `bool` | `true` once the search has finished (succeeded or failed). |
| `Iteration` | `int` | The number of expansions performed so far. |

## Methods

| Method | Description |
|--------|-------------|
| `SearchStep Tick()` | Expands a single cell and returns a snapshot of the search afterwards. Once complete, it is a no-op that returns the final snapshot again. |
| `SearchStep RunToCompletion()` | Advances the search until it completes and returns the final snapshot. |
| `void Dispose()` | Releases the pinned cell buffer and frees the owning `Pathfinder` for other queries. |

## Remarks

- **Disposal is required.** The session pins the pathfinder's cell buffer for the lifetime of the search (the open set holds raw pointers into it that must stay valid across ticks). Always wrap it in a `using` statement.
- **One at a time.** While a session is active, the owning `Pathfinder` must not be used for any other query — including `GetPath`, `GetReachable`, or a second `BeginStepwiseSearch`. Starting a second session before disposing the first throws `InvalidOperationException`.
- **Not a hot path.** Unlike `GetPath`, the stepwise API favours clarity over the allocation-free design — each `SearchStep` owns plain arrays. It is meant for visualization and teaching, not real-time movement.
- `Tick()` after the search completes returns the same final `SearchStep` instance; `Tick()` after `Dispose()` throws `ObjectDisposedException`.

## Example

```csharp
using var pathfinder = new Pathfinder(cells, 10, 10);
var agent = new SimpleAgent { Size = 1 };

using var search = pathfinder.BeginStepwiseSearch(agent, new Coordinate(0, 0), new Coordinate(9, 9));

while (!search.IsComplete)
{
    var step = search.Tick();
    Console.WriteLine($"tick {step.Iteration}: {step.OpenCount} open, {step.ClosedCount} closed");
}
```

## See also

- [SearchStep](SearchStep.md) — the per-tick snapshot
- [SearchNode](SearchNode.md) — a single searched cell with its A* scores
- [SearchState](SearchState.md) — the overall search state
- [Visualizing the search](../guides/visualizing-search.md) — usage guide
