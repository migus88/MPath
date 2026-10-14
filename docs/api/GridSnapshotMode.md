# GridSnapshotMode Enum

**Namespace:** `Migs.MPath.Core.Data`

Decides when a pathfinder built over a `Cell[,]` matrix or over [`ICellHolder`](ICellHolder.md)s copies your grid into its internal snapshot. Set it through [`IPathfinderSettings.GridSnapshotMode`](IPathfinderSettings.md). A `Cell[]` array (see [`CellLayout`](CellLayout.md)) is searched in place and has no snapshot, so the setting does not affect it. A snapshot writes every grid cell (a `null` holder becomes a wall), and a `Cell[,]` is copied in one block.

## Values

| Value | Description |
|-------|-------------|
| `PerQuery` (0) | **Default.** Copy the whole grid before every query, so direct edits to the matrix or the holders are always seen. Every query costs at least O(grid size). The behaviour of versions before this setting existed. |
| `Manual` (1) | Copy the grid once, before the first query, and again only after [`Pathfinder.Refresh()`](Pathfinder.md). A query then costs only the cells it searches. |

## What a query sees under `Manual`

| Edit | Seen by the next query? |
|------|-------------------------|
| Before the first query (any way) | Yes |
| `SetWalkable` / `SetOccupied` / `SetWeight` / `SetCell` on a `Cell[,]` pathfinder | Yes, at once (the setters write your grid and the snapshot) |
| Direct write to the `Cell[,]` matrix | Only after `Refresh()` |
| Change to an `ICellHolder.CellData` | Only after `Refresh()` |

`Refresh()` is cheap: it marks the snapshot stale, and the next query copies once. It also clears the path cache, since cached paths were computed for the grid before the edit.

## Example

```csharp
var settings = new PathfinderSettings { GridSnapshotMode = GridSnapshotMode.Manual };
using var pathfinder = new Pathfinder(cellMatrix, settings);

// Many short queries per turn: none of them copies the 256x256 grid.
using (var range = pathfinder.GetReachable(unitPosition, 600)) { /* ... */ }

// Through the pathfinder: seen at once.
pathfinder.SetOccupied(new Coordinate(4, 7), true);

// Behind its back: call Refresh before the next query.
cellMatrix[9, 2].IsWalkable = false;
pathfinder.Refresh();
```

## See also

- [Pathfinder](Pathfinder.md) — `Refresh()` and the cell-update methods
- [Performance guide](../guides/performance.md)
