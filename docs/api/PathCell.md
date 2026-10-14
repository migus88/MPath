# PathCell Struct

**Namespace:** `Migs.MPath.Core.Data`

Represents a single cell in the pathfinding grid: the data a search reads. `PathCell` is the fundamental unit of the grid used for pathfinding. A cell does not carry its position: its slot in the grid is its position (`[x, y]` in a `PathCell[,]`, the index under [`CellLayout`](CellLayout.md) in a `PathCell[]`). Searches only read cells: their state (scores, parents, open/closed) lives in the pathfinder, so a query never writes into your grid.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsWalkable` | `bool` | Indicates whether an agent can traverse this cell. `default` is `false`, a wall. |
| `IsOccupied` | `bool` | Indicates whether this cell is currently occupied by an agent. |
| `Weight` | `int` | Extra cost of entering this cell, in the step costs' unit, added to the straight/diagonal step cost of each step into it (when `IsCellWeightEnabled`). `SetWeight`/`SetCell` reject a negative weight; one written straight into the grid counts as 0. Weights above 1,073,741,823 (`int.MaxValue / 2`) count as that, and a route whose cost would pass it is never taken. |

## Example

```csharp
// The cell at (5, 10): its slot is its position
cells[5, 10] = new PathCell
{
    IsWalkable = true,
    Weight = 100
};
```

## Remarks

- `PathCell` is 8 bytes (two `bool`s and the weight), which keeps the searched buffer and the per-query copy of a matrix or holder grid small. The pathfinder derives every coordinate it reports from the cell's index.
- A search never writes into the cells, and a `PathCell[]` grid is never reordered: see [`CellLayout`](CellLayout.md).
