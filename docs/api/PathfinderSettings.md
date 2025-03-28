| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | `true` | Whether agents can move diagonally. |
| `IsCalculatingOccupiedCells` | `bool` | `true` | Whether occupied cells are considered as blocked. |
| `IsMovementBetweenCornersEnabled` | `bool` | `false` | Whether agents can move between two diagonal corners. |
| `IsCellWeightEnabled` | `bool` | `true` | Whether cell weight calculation is enabled. |
| `PathSmoothingMethod` | `PathSmoothingMethod` | `None` | The method used for smoothing paths (None, Simple, or StringPulling). |
| `StraightMovementMultiplier` | `float` | `1.0f` | The cost multiplier for horizontal/vertical movement. |
| `DiagonalMovementMultiplier` | `float` | `1.41f` | The cost multiplier for diagonal movement. |
| `InitialBufferSize` | `int?` | `null` | The initial size of the Open Set buffer. |

// Create customized settings
var customSettings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,
    IsCalculatingOccupiedCells = false,
    IsCellWeightEnabled = true,
    PathSmoothingMethod = PathSmoothingMethod.StringPulling,
    DiagonalMovementMultiplier = 1.4f,
    InitialBufferSize = 128
}; 