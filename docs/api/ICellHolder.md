# ICellHolder Interface

**Namespace:** `Migs.MPath.Core.Interfaces`

Represents an object that contains cell data used in the pathfinding system.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `CellData` | `Cell` | Gets the cell data associated with this holder. |

## Remarks

- Abstracts the ownership of cell data from grid representation
- Allows game objects to provide cell information without directly managing the data
- Used with `Pathfinder` constructors that accept `ICellHolder` arrays or matrices
- A holder's slot is its cell's position: `[x, y]` in an `ICellHolder[,]`, the index under [`CellLayout`](CellLayout.md) in an `ICellHolder[]` (column-major by default). The cell itself carries no coordinate.
- A `null` holder is a non-walkable cell.
- An `ICellHolder[]` must hold at least `fieldWidth * fieldHeight` holders; a shorter one throws `ArgumentException`.

## Example

```csharp
// Simple implementation
public class GridTile : ICellHolder
{
    private Cell _cell;
    public Cell CellData => _cell;
    
    public GridTile(bool isWalkable, int weight = 0)
    {
        _cell = new Cell
        {
            IsWalkable = isWalkable,
            Weight = weight
        };
    }
}

// Unity MonoBehaviour implementation: put it at holders[x, y] (or at its column-major index in an array)
public class GridCell : MonoBehaviour, ICellHolder
{
    [SerializeField] private bool _isWalkable = true;
    [SerializeField] private int _weight = 0;
    
    private Cell _cellData;
    public Cell CellData => _cellData;
    
    private void Awake()
    {
        _cellData = new Cell
        {
            IsWalkable = _isWalkable,
            Weight = _weight
        };
    }
}
``` 