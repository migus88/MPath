# PathfinderSettings Class

**Namespace:** `Migs.MPath.Core.Data`

Default implementation of the `IPathfinderSettings` interface.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | `true` | Whether agents can move diagonally. |
| `AreOccupiedCellsBlocking` | `bool` | `true` | Whether occupied cells block: entry, agent clearance, the corner rule and line of sight. |
| `CornerBlocking` | [`CornerBlocking`](CornerBlocking.md) | `BetweenBlockedCells` | Which flanking cells (walls, occupied cells) block a diagonal step. |
| `IsCellWeightEnabled` | `bool` | `true` | Whether cell weight calculation is enabled. |
| `StraightStepCost` | `int` | `100` | The cost of a horizontal/vertical step, at least 1. Costs are integers in a unit you pick (here hundredths of a step). |
| `DiagonalStepCost` | `int` | `141` | The cost of a diagonal step, at least 1 (1.41 straight steps). |
| `PathSmoothingMethod` | `PathSmoothingMethod` | `None` | The method used to smooth the calculated path. |
| `TieBreaking` | [`TieBreaking`](TieBreaking.md) | `None` | Which route to return when several cost the same (`FewerDiagonals`, `FewerTurns`). |
| `GridSnapshotMode` | [`GridSnapshotMode`](GridSnapshotMode.md) | `PerQuery` | When a `PathCell[,]` or cell-holder pathfinder copies the grid: every query, or once and after `Refresh()` (`Manual`). No effect on a `PathCell[]`. |
| `CellLayout` | [`CellLayout`](CellLayout.md) | `ColumnMajor` | Which index of a `PathCell[]` or `ICellHolder[]` holds cell (X, Y): `ColumnMajor` (`X * height + Y`) or `RowMajor` (`Y * width + X`). |

## Example

```csharp
// Create settings with default values
var defaultSettings = new PathfinderSettings();

// Create customized settings
var customSettings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,
    AreOccupiedCellsBlocking = false,
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