# PathfinderSettings Class

**Namespace:** `Migs.MPath.Core.Data`

Default implementation of the `IPathfinderSettings` interface.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | `true` | Whether agents can move diagonally. |
| `IsCalculatingOccupiedCells` | `bool` | `true` | Whether occupied cells are considered as blocked. |
| `CornerBlocking` | [`CornerBlocking`](CornerBlocking.md) | `BetweenBlockedCells` | Which flanking cells (walls, occupied cells) block a diagonal step. |
| `IsCellWeightEnabled` | `bool` | `true` | Whether cell weight calculation is enabled. |
| `StraightStepCost` | `int` | `100` | The cost of a horizontal/vertical step, at least 1. Costs are integers in a unit you pick (here hundredths of a step). |
| `DiagonalStepCost` | `int` | `141` | The cost of a diagonal step, at least 1 (1.41 straight steps). |
| `PathSmoothingMethod` | `PathSmoothingMethod` | `None` | The method used to smooth the calculated path. |
| `TieBreaking` | [`TieBreaking`](TieBreaking.md) | `None` | Which route to return when several cost the same (`FewerDiagonals`, `FewerTurns`). |
| `GridSnapshotMode` | [`GridSnapshotMode`](GridSnapshotMode.md) | `PerQuery` | When a `Cell[,]`, cell-holder or unordered `Cell[]` pathfinder copies the grid: every query, or once and after `Refresh()` (`Manual`). No effect on a row- or column-major `Cell[]`. |
| `CellLayout` | [`CellLayout`](CellLayout.md) | `Auto` | Where a `Cell[]` pathfinder finds cell (X, Y): `ColumnMajor`, `RowMajor`, `Unordered`, or `Auto` (detected at the first query). |

## Example

```csharp
// Create settings with default values
var defaultSettings = new PathfinderSettings();

// Create customized settings
var customSettings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,
    IsCalculatingOccupiedCells = false,
    IsCellWeightEnabled = true,
    DiagonalStepCost = 140,
    PathSmoothingMethod = PathSmoothingMethod.StringPulling
};

// Use with pathfinder
var pathfinder = new Pathfinder(cells, width, height, customSettings);
```

## Remarks

- This class provides a standard implementation of `IPathfinderSettings` for general use.
- All properties have sensible defaults that work well for most scenarios.
- The `PathSmoothingMethod` property controls how the path is optimized after calculation:
  - `None`: Returns the raw A* path without any smoothing
  - `Simple`: Removes redundant waypoints based on direction changes
  - `StringPulling`: Creates optimal direct paths using line-of-sight checks
- For Unity projects, consider using `ScriptablePathfinderSettings`