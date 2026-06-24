# SearchStep Class

**Namespace:** `Migs.MPath.Core.Data`

An immutable snapshot of a stepwise A* search after a single [`StepwiseSearch.Tick`](StepwiseSearch.md). It carries the accumulated searched area (every cell discovered so far, open and closed) and — once the search succeeds — the resulting path, letting callers render the algorithm's progress frame by frame.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `State` | [`SearchState`](SearchState.md) | The overall state of the search after this tick. |
| `IsComplete` | `bool` | `true` once the search has finished (succeeded or failed). |
| `Iteration` | `int` | The 1-based index of the tick that produced this snapshot (cells expanded so far). |
| `Current` | `Coordinate` | The cell expanded on this tick — the "head" of the search. On the final successful tick this is the destination. |
| `Searched` | `IReadOnlyList<`[`SearchNode`](SearchNode.md)`>` | Every cell discovered so far, each tagged `Open` (frontier) or `Closed` (expanded), with its A* scores. |
| `OpenCount` | `int` | The number of cells currently on the open frontier. |
| `ClosedCount` | `int` | The number of cells currently in the closed (expanded) set. |
| `Path` | `IReadOnlyList<Coordinate>` | The path once `State` is `Success`; otherwise empty. |

## Remarks

- Unlike [`PathResult`](PathResult.md) and [`RangeResult`](RangeResult.md), a `SearchStep` owns plain arrays rather than pooled buffers, so it does **not** need to be disposed and stays valid after the search advances. This trades a little allocation per tick for simplicity — the stepwise API is meant for visualization, not the allocation-free hot path.
- `Path` follows the same convention as `PathResult`: the **origin cell is not included**, and the list ends at the destination. It is the raw A* path (parent-pointer chain); path smoothing is a separate post-process and is **not** applied here.
- `OpenCount + ClosedCount == Searched.Count` always holds.

## See also

- [StepwiseSearch](StepwiseSearch.md) — the driver that produces these snapshots
- [SearchNode](SearchNode.md) — an entry in `Searched`
- [Visualizing the search](../guides/visualizing-search.md) — usage guide
