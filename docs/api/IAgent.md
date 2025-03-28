# IAgent Interface

**Namespace:** `Migs.MPath.Core.Interfaces`

Represents an entity that can move through the pathfinding grid.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Size` | `int` | The square size of the agent, measured in occupied cells. |

## Remarks

- For a value of `1`, the agent occupies a single cell (1x1 square).
- For a value of `2`, the agent occupies a 2x2 square of cells.
- The agent is positioned at the top-left cell of its square area.
- All cells within the agent's size must be walkable for a valid path.

## Example

```csharp
// Single-cell agent
public class SingleCellAgent : IAgent
{
    public int Size => 1;
}

// In Unity with MonoBehaviour
public class UnitAgent : MonoBehaviour, IAgent
{
    [SerializeField] private int _agentSize = 1;
    public int Size => _agentSize;
}
``` 