# CellLayout Enum

**Namespace:** `Migs.MPath.Core.Data`

Which index of a `Cell[]` or [`ICellHolder`](ICellHolder.md)`[]` array holds the cell at (X, Y). Set it through [`IPathfinderSettings.CellLayout`](IPathfinderSettings.md). A cell's slot is its position: only the first `fieldWidth * fieldHeight` elements of the array are the grid, and nothing is sorted, checked or placed by anything but the index. Queries never reorder or otherwise write your array, and the pathfinder keeps a reference to it. No effect on the `Cell[,]` and `ICellHolder[,]` constructors, where the cell at (X, Y) is `[X, Y]`.

## Values

| Value | Description |
|-------|-------------|
| `ColumnMajor` (1) | **Default.** Cell (X, Y) is at `X * fieldHeight + Y`, the order of a `Cell[,]` in memory. |
| `RowMajor` (2) | Cell (X, Y) is at `Y * fieldWidth + X`, the usual `y * width + x`. |

Any other value throws `ArgumentOutOfRangeException` when the pathfinder is created. A `Cell[]` is searched in place in either layout: a query costs only the cells it searches, and direct writes to the array are seen by the next query. An `ICellHolder[]` is copied into a pooled buffer in the same layout (see [`GridSnapshotMode`](GridSnapshotMode.md)).

## `GetIndex`, `CopyCostsTo` and the cell-update methods

[`Pathfinder.GetIndex(c)`](Pathfinder.md) returns the cell's index in your array: `X * fieldHeight + Y` or `Y * fieldWidth + X` (for a matrix, the column-major index). [`RangeResult.CopyCostsTo`](RangeResult.md) writes each cost at that same index, so `costs[pathfinder.GetIndex(c)]` is the cost of `c`. `SetWalkable`/`SetOccupied`/`SetWeight`/`SetCell` write your array at that index, in O(1).

## Example

```csharp
var cells = new Cell[width * height];
for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
        cells[y * width + x] = new Cell { IsWalkable = true };

var settings = new PathfinderSettings { CellLayout = CellLayout.RowMajor };
using var pathfinder = new Pathfinder(cells, width, height, settings);

cells[2 * width + 5].IsWalkable = false;   // your index stays valid; the next query sees it
using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(9, 2));
```

## See also

- [GridSnapshotMode](GridSnapshotMode.md) — when a matrix or holder grid is copied
- [Grid setup guide](../guides/grid-setup.md)
