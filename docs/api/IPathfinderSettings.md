# IPathfinderSettings Interface

**Namespace:** `Migs.MPath.Core.Interfaces`

Defines the configuration settings for the MPath pathfinding system. This interface allows customization of the pathfinding behavior, including movement constraints, costs, and memory management.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | Whether agents can move diagonally. |
| `AreOccupiedCellsBlocking` | `bool` | Whether occupied cells block: entry, agent clearance, the corner rule and line of sight. |
| `CornerBlocking` | [`CornerBlocking`](CornerBlocking.md) | Which flanking cells block a diagonal step. Usual default: `BetweenBlockedCells`. |
| `IsCellWeightEnabled` | `bool` | Whether cell weight calculation is enabled. |
| `StraightStepCost` | `int` | The cost of a horizontal/vertical step, at least 1. Costs are integers in a unit you pick; weights, budgets and reported costs use the same one. |
| `DiagonalStepCost` | `int` | The cost of a diagonal step, at least 1 (only used with diagonal movement). |
| `PathSmoothingMethod` | `PathSmoothingMethod` | The method used to smooth the calculated path. |
| `TieBreaking` | [`TieBreaking`](TieBreaking.md) | Which route to return when several cost the same. Usual default: `None`. |
| `GridSnapshotMode` | [`GridSnapshotMode`](GridSnapshotMode.md) | When a `PathCell[,]` or cell-holder pathfinder copies the grid: every query (`PerQuery`) or once and after `Refresh()` (`Manual`). Usual default: `PerQuery`. |
| `CellLayout` | [`CellLayout`](CellLayout.md) | Which index of a `PathCell[]` or `ICellHolder[]` holds cell (X, Y): `ColumnMajor` or `RowMajor`; any other value throws at construction. Usual default: `ColumnMajor`. |

Every member is required: a custom implementation returns a value for each, with no default bodies to fall back on. `PathfinderSettings`'s defaults (`BetweenBlockedCells`, `PerQuery`, `ColumnMajor`, `None`) are a safe starting point.

## Implementations

MPath provides two implementations of this interface:

- `PathfinderSettings`: Standard implementation for general use
- `ScriptablePathfinderSettings`: Unity ScriptableObject implementation for configuration through the Inspector

## Usage Example

```csharp
// Create custom settings
var settings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,
    AreOccupiedCellsBlocking = false,
    CornerBlocking = CornerBlocking.None,
    IsCellWeightEnabled = true,
    StraightStepCost = 10,
    DiagonalStepCost = 14,
    PathSmoothingMethod = PathSmoothingMethod.StringPulling
};

// Use these settings when creating a pathfinder
var pathfinder = new Pathfinder(cells, width, height, settings);
```

## Remarks

- These settings can significantly impact both the performance and the quality of pathfinding results.
- Enabling diagonal movement generally results in more natural-looking paths but can be more computationally expensive.
- Consider the trade-offs between realistic movement and performance when configuring these settings.
- Path smoothing can significantly reduce the number of waypoints in a path, but different smoothing methods have different performance characteristics:
  - `None`: Fastest but may result in zigzag paths
  - `Simple`: Very fast with good path quality for most cases
  - `StringPulling`: Most computationally expensive but produces the most optimal paths 