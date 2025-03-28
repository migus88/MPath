# Cell Struct

**Namespace:** `Migs.MPath.Core.Data`

Represents a single cell in the pathfinding grid. This structure contains both public properties used by client code and internal fields used by the pathfinding algorithm. `Cell` is the fundamental unit of the grid used for pathfinding.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Coordinate` | `Coordinate` | The position of this cell in the grid. |
| `IsWalkable` | `bool` | Indicates whether an agent can traverse this cell. |
| `IsOccupied` | `bool` | Indicates whether this cell is currently occupied by an agent. |
| `Weight` | `float` | The movement cost multiplier for this cell. |

## Methods

| Method | Description |
|--------|-------------|
| `void Reset()` | Resets the internal pathfinding state of this cell. |

## Internal Fields

The `Cell` struct contains several internal fields used by the pathfinding algorithm:

- `IsClosed`: Indicates whether the cell has been fully processed
- `ScoreF`: The combined score (F = G + H) used in the A* algorithm
- `ScoreG`: The cost from the starting point to this cell
- `ScoreH`: The estimated cost from this cell to the destination
- `Depth`: The number of steps from the starting point
- `ParentCoordinate`: The coordinate of the previous cell in the path
- `QueueIndex`: Used for efficient queue operations during pathfinding

## Example

```csharp
// Create a walkable cell
var cell = new Cell
{
    Coordinate = new Coordinate(5, 10),
    IsWalkable = true,
    Weight = 1.0f
};
```

## Remarks

- The `Cell`