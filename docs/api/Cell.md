# Cell Struct

**Namespace:** `Migs.MPath.Core.Data`

Represents a single cell in the pathfinding grid: its coordinate and the data a search reads. `Cell` is the fundamental unit of the grid used for pathfinding. Searches only read cells: their state (scores, parents, open/closed) lives in the pathfinder, so a query never writes into your grid.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Coordinate` | `Coordinate` | The position of this cell in the grid. |
| `IsWalkable` | `bool` | Indicates whether an agent can traverse this cell. |
| `IsOccupied` | `bool` | Indicates whether this cell is currently occupied by an agent. |
| `Weight` | `float` | Extra cost of entering this cell, added to the straight/diagonal movement multiplier of each step into it (when `IsCellWeightEnabled`). Keep it non-negative. |

## Methods

| Method | Description |
|--------|-------------|
| `void Reset()` | **Obsolete, does nothing.** The search state no longer lives in `Cell`. Will be removed in the next major version. |

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

- `Cell` holds only the four public properties. Before the search state moved into the pathfinder it also carried internal scratch fields (56 bytes per cell); it is now 20 bytes, which makes the per-query copy of a matrix or holder grid and the array-mode sort cheaper.
- A search never writes into the cells: a `Cell[]` grid is still reordered into layout order on each query (column-major, `index = X * height + Y`), but its values are left untouched.
