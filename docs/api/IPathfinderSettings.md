# IPathfinderSettings Interface

**Namespace:** `Migs.MPath.Core.Interfaces`

Defines the configuration settings for the MPath pathfinding system. This interface allows customization of the pathfinding behavior, including movement constraints, costs, and memory management.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | Whether agents can move diagonally. |
| `IsCalculatingOccupiedCells` | `bool` | Whether occupied cells are considered as blocked. |
| `CornerBlocking` | [`CornerBlocking`](CornerBlocking.md) | Which flanking cells block a diagonal step. Usual default: `BetweenBlockedCells`. |
| `IsCellWeightEnabled` | `bool` | Whether cell weight calculation is enabled. |
| `StraightMovementMultiplier` | `float` | The cost multiplier for horizontal/vertical movement. |
| `DiagonalMovementMultiplier` | `float` | The cost multiplier for diagonal movement. |
| `PathSmoothingMethod` | `PathSmoothingMethod` | The method used to smooth the calculated path. |
| `TieBreaking` | [`TieBreaking`](TieBreaking.md) | Which route to return when several cost the same. Usual default: `None`. |
| `GridSnapshotMode` | [`GridSnapshotMode`](GridSnapshotMode.md) | When a `Cell[,]`, cell-holder or unordered `Cell[]` pathfinder copies the grid: every query (`PerQuery`) or once and after `Refresh()` (`Manual`). Usual default: `PerQuery`. |
| `CellLayout` | [`CellLayout`](CellLayout.md) | Where a `Cell[]` pathfinder finds cell (X, Y): column-major, row-major, unordered, or detected (`Auto`). Usual default: `Auto`. |

Every member is required: a custom implementation returns a value for each, with no default bodies to fall back on. `PathfinderSettings`'s defaults (`BetweenBlockedCells`, `PerQuery`, `Auto`, `None`) are a safe starting point.

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
    IsCalculatingOccupiedCells = false,
    CornerBlocking = CornerBlocking.None,
    IsCellWeightEnabled = true,
    StraightMovementMultiplier = 1.0f,
    DiagonalMovementMultiplier = 1.4f,
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