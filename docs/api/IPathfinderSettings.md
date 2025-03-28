# IPathfinderSettings Interface

**Namespace:** `Migs.MPath.Core.Interfaces`

Defines the configuration settings for the MPath pathfinding system. This interface allows customization of the pathfinding behavior, including movement constraints, costs, and memory management.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | Whether agents can move diagonally. |
| `IsCalculatingOccupiedCells` | `bool` | Whether occupied cells are considered as blocked. |
| `IsMovementBetweenCornersEnabled` | `bool` | Whether agents can move between two diagonal corners. |
| `IsCellWeightEnabled` | `bool` | Whether cell weight calculation is enabled. |
| `StraightMovementMultiplier` | `float` | The cost multiplier for horizontal/vertical movement. |
| `DiagonalMovementMultiplier` | `float` | The cost multiplier for diagonal movement. |
| `InitialBufferSize` | `int?` | The initial size of the Open Set buffer. |

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
    IsMovementBetweenCornersEnabled = true,
    IsCellWeightEnabled = true,
    StraightMovementMultiplier = 1.0f,
    DiagonalMovementMultiplier = 1.4f,
    InitialBufferSize = 128
};

// Use these settings when creating a pathfinder
var pathfinder = new Pathfinder(cells, width, height, settings);
```

## Remarks

- These settings can significantly impact both the performance and the quality of pathfinding results.
- Enabling diagonal movement generally results in more natural-looking paths but can be more computationally expensive.
- Consider the trade-offs between realistic movement and performance when configuring these settings. 