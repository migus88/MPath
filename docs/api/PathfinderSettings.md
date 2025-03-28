# PathfinderSettings Class

**Namespace:** `Migs.MPath.Core.Data`

Default implementation of the `IPathfinderSettings` interface.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | `true` | Whether agents can move diagonally. |
| `IsCalculatingOccupiedCells` | `bool` | `true` | Whether occupied cells are considered as blocked. |
| `IsMovementBetweenCornersEnabled` | `bool` | `false` | Whether agents can move between two diagonal corners. |
| `IsCellWeightEnabled` | `bool` | `true` | Whether cell weight calculation is enabled. |
| `StraightMovementMultiplier` | `float` | `1.0f` | The cost multiplier for horizontal/vertical movement. |
| `DiagonalMovementMultiplier` | `float` | `1.41f` | The cost multiplier for diagonal movement. |
| `InitialBufferSize` | `int?` | `null` | The initial size of the Open Set buffer. |

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
    InitialBufferSize = 128
};

// Use with pathfinder
var pathfinder = new Pathfinder(cells, width, height, customSettings);
```

## Remarks

- This class provides a simple implementation of `IPathfinderSettings` with reasonable defaults.
- All properties have sensible default values but can be customized to fit specific requirements.
- For Unity projects, consider using `ScriptablePathfinderSettings` instead, which provides the same functionality with Inspector integration.
- The default diagonal movement multiplier (1.41f) approximates the actual Euclidean distance multiplier (√2 ≈ 1.414).
- When `InitialBufferSize` is null, the pathfinder will use an internal default size based on performance benchmarks. 