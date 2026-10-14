# Cell Struct

**Namespace:** `Migs.MPath.Core.Data`

Represents a single cell in the pathfinding grid: its coordinate and the data a search reads. `Cell` is the fundamental unit of the grid used for pathfinding. Searches only read cells: their state (scores, parents, open/closed) lives in the pathfinder, so a query never writes into your grid.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Coordinate` | `Coordinate` | The position of this cell in the grid. |
| `IsWalkable` | `bool` | Indicates whether an agent can traverse this cell. |
| `IsOccupied` | `bool` | Indicates whether this cell is currently occupied by an agent. |
| `Weight` | `int` | Extra cost of entering this cell, in the step costs' unit, added to the straight/diagonal step cost of each step into it (when `IsCellWeightEnabled`). `SetWeight`/`SetCell` reject a negative weight; one written straight into the grid counts as 0. Weights above 1,073,741,823 (`int.MaxValue / 2`) count as that, and a route whose cost would pass it is never taken. |

## Example

```csharp
// Create a walkable cell
var cell = new Cell
{
    Coordinate = new Coordinate(5, 10),
    IsWalkable = true,
    Weight = 100
};
```

## Remarks

- `Cell` holds only the four public properties. Before the search state moved into the pathfinder it also carried internal scratch fields (56 bytes per cell); it is now 16 bytes (an 8-byte `Coordinate`, two `bool`s and the weight), which makes the per-query copy of a matrix, holder or unordered-array grid cheaper.
- A search never writes into the cells, and a `Cell[]` grid is never reordered: see [`CellLayout`](CellLayout.md).
