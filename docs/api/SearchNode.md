# SearchNode Struct

**Namespace:** `Migs.MPath.Core.Data`

A readonly value type capturing a single cell that has been touched by a stepwise A* search, together with the A* scores the algorithm currently associates with it. Returned in batches as part of a [`SearchStep`](SearchStep.md) so the progress of the search can be displayed.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Coordinate` | `Coordinate` | The coordinate of the cell. |
| `State` | `SearchNodeState` | Whether the cell is on the `Open` frontier or has been expanded into the `Closed` set. |
| `ScoreG` | `float` | The cost of the cheapest path discovered so far from the origin (A* `g`). |
| `ScoreH` | `float` | The heuristic estimate to the destination (A* `h`, Manhattan distance). |
| `ScoreF` | `float` | The priority by which the open set is ordered: `g + h` (A* `f`). |

## Constructors

| Constructor | Description |
|-------------|-------------|
| `SearchNode(Coordinate coordinate, SearchNodeState state, float scoreG, float scoreH, float scoreF)` | Initializes a new searched-cell snapshot. |

## SearchNodeState enum

The role a node currently plays in the search — the distinction most useful when visualizing the algorithm:

| Value | Description |
|-------|-------------|
| `Open` | The cell has been discovered and sits on the frontier (the open set), waiting to be expanded. |
| `Closed` | The cell has already been expanded; its cheapest cost from the origin is final. |

## Remarks

- This is a `readonly struct`; enumerate it via [`SearchStep.Searched`](SearchStep.md).
- Visualizers typically colour `Open` and `Closed` cells differently and may label each with its `ScoreG`/`ScoreF` to show how A* prioritizes the frontier.

## See also

- [SearchStep](SearchStep.md) — the snapshot that carries these nodes
- [StepwiseSearch](StepwiseSearch.md) — the driver
