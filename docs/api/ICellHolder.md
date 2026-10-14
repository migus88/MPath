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

## Example

```csharp
// Simple implementation
public class GridTile : ICellHolder
{
    private Cell _cell;
    public Cell CellData => _cell;
    
    public GridTile(int x, int y, bool isWalkable, int weight = 0)
    {
        _cell = new Cell
        {
            Coordinate = new Coordinate(x, y),
            IsWalkable = isWalkable,
            Weight = weight
        };
    }
}

// Unity MonoBehaviour implementation
public class GridCell : MonoBehaviour, ICellHolder
{
    [SerializeField] private bool _isWalkable = true;
    [SerializeField] private Vector2Int _position;
    [SerializeField] private int _weight = 0;
    
    private Cell _cellData;
    public Cell CellData => _cellData;
    
    private void Awake()
    {
        _cellData = new Cell
        {
            Coordinate = new Coordinate(_position.x, _position.y),
            IsWalkable = _isWalkable,
            Weight = _weight
        };
    }
}
``` 