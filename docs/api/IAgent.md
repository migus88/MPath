# IAgent Interface

**Namespace:** `Migs.MPath.Core.Interfaces`

Represents an entity that can move through the pathfinding grid.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Size` | `int` | The square size of the agent, measured in occupied cells. |

## Remarks

- Optional: every `Pathfinder` query also has an overload taking `int agentSize = 1` instead (for example `GetPath(from, to, agentSize: 2)`), so single-cell units need no `IAgent` type. Both forms return the same results for the same size. See [agent size](Pathfinder.md#agent-size).
- For a value of `1`, the agent occupies a single cell (1x1 square).
- For a value of `2`, the agent occupies a 2x2 square of cells.
- The agent is positioned at the top-left cell of its square area.
- All cells within the agent's size must be walkable for a valid path.

## Example

```csharp
// Single-cell agent: no IAgent needed
using var path = pathfinder.GetPath(from, to);

// A type that already knows its size can implement IAgent
public class Truck : IAgent
{
    public int Size => 2;
}

// In Unity with MonoBehaviour
public class UnitAgent : MonoBehaviour, IAgent
{
    [SerializeField] private int _agentSize = 1;
    public int Size => _agentSize;
}
``` 