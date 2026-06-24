# SearchState Enum

**Namespace:** `Migs.MPath.Core.Data`

Describes the overall state of a stepwise (tick-by-tick) A* search driven by [`Pathfinder.StepwiseSearch`](StepwiseSearch.md). Exposed on every [`SearchStep`](SearchStep.md).

## Values

| Value | Description |
|-------|-------------|
| `InProgress` | The search has not finished: the frontier still contains cells to expand and the destination has not been reached. Call `Tick()` again to advance it. |
| `Success` | The destination has been reached. The final path is available on the returned `SearchStep`. |
| `Failure` | The frontier was exhausted without reaching the destination: no path exists for this agent. |

## See also

- [StepwiseSearch](StepwiseSearch.md) — the driver
- [SearchStep](SearchStep.md) — the per-tick snapshot that carries this state
