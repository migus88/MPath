# PathfinderSettings Class

**Namespace:** `Migs.MPath.Core.Data`

Default implementation of the `IPathfinderSettings` interface.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | `true` | Whether agents can move diagonally. |
| `IsCalculatingOccupiedCells` | `bool` | `true` | Whether occupied cells are considered as blocked. |
| `IsMovementBetweenCornersEnabled` | `bool` | `false` | All-or-nothing shorthand for `CornerBlocking`: `true` is `None`, `false` is `BetweenBlockedCells`. |
| `CornerBlocking` | [`CornerBlocking`](CornerBlocking.md) | `BetweenBlockedCells` | Which flanking cells (walls, occupied cells) block a diagonal step. |
| `IsCellWeightEnabled` | `bool` | `true` | Whether cell weight calculation is enabled. |
| `StraightMovementMultiplier` | `float` | `1.0f` | The cost multiplier for horizontal/vertical movement. |
| `DiagonalMovementMultiplier` | `float` | `1.41f` | The cost multiplier for diagonal movement. |
| `InitialBufferSize` | `int?` | `null` | The initial size of the Open Set buffer. |
| `PathSmoothingMethod` | `PathSmoothingMethod` | `None` | The method used to smooth the calculated path. |

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
    DiagonalMovementMultiplier = 1.4f,
    InitialBufferSize = 128,
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
- `IsMovementBetweenCornersEnabled` is a view over `CornerBlocking`: it reads `true` only for `None`; setting `true` sets `None`, setting `false` sets `BetweenBlockedCells` unless a blocking rule is already set.
- For Unity projects, consider using `ScriptablePathfinderSettings`