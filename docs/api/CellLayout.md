# CellLayout Enum

**Namespace:** `Migs.MPath.Core.Data`

Where a pathfinder built over a `Cell[]` array finds the cell at (X, Y). Set it through [`IPathfinderSettings.CellLayout`](IPathfinderSettings.md). Only the first `fieldWidth * fieldHeight` elements of the array are the grid. Queries never reorder or otherwise write your array, and the pathfinder keeps a reference to it. No effect on the `Cell[,]` and [`ICellHolder`](ICellHolder.md) constructors.

## Values

| Value | Description |
|-------|-------------|
| `Auto` (0) | **Default.** Detect the layout at the first query, and again at the first query after [`Pathfinder.Refresh()`](Pathfinder.md): `ColumnMajor` or `RowMajor` when every cell sits at its index under that layout, otherwise `Unordered`. One O(grid size) pass per detection. Arrays in any order keep working as in earlier versions, without being sorted. |
| `ColumnMajor` (1) | Cell (X, Y) is at `X * fieldHeight + Y`. Searched in place: a query costs only the cells it searches, and direct writes to the array are seen by the next query. Checked once at the first query (and after `Refresh()`); a cell at the wrong index throws `InvalidOperationException`. |
| `RowMajor` (2) | Cell (X, Y) is at `Y * fieldWidth + X`, the usual `y * width + x`. Searched in place like `ColumnMajor`. |
| `Unordered` (3) | Any order; each cell's `Coordinate` says where it is. Copied into a pooled buffer like a `Cell[,]`: before every query with [`GridSnapshotMode.PerQuery`](GridSnapshotMode.md), once and after `Refresh()` with `Manual`. A grid cell the array does not hold is a wall; a coordinate outside the grid throws `InvalidOperationException` at the query. |

## `GetIndex` and the cell-update methods

[`Pathfinder.GetIndex(c)`](Pathfinder.md) returns the cell's index in your array: `X * fieldHeight + Y`, `Y * fieldWidth + X`, or, for an unordered array, wherever the cell with that coordinate is. With `Auto` it reports column-major until the first query has detected the layout, so declare the layout when you index through `GetIndex` before querying. `SetWalkable`/`SetOccupied`/`SetWeight`/`SetCell` write your array (and an unordered array's snapshot) in every layout. `RangeResult.CopyCostsTo` fills its field in the searched layout: your array's order for row- and column-major arrays, column-major for an unordered one.

## Example

```csharp
var cells = new Cell[width * height];
for (var y = 0; y < height; y++)
    for (var x = 0; x < width; x++)
        cells[y * width + x] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };

var settings = new PathfinderSettings { CellLayout = CellLayout.RowMajor };
using var pathfinder = new Pathfinder(cells, width, height, settings);

cells[2 * width + 5].IsWalkable = false;   // your index stays valid; the next query sees it
using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(9, 2));
```

## See also

- [GridSnapshotMode](GridSnapshotMode.md) — when an unordered array is copied
- [Grid setup guide](../guides/grid-setup.md)
