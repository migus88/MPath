# Pathfinder Settings

The behavior of MPath's pathfinding algorithm can be customized through various settings. This guide explains all available options and how to configure them for your specific needs.

## Overview

MPath provides a `PathfinderSettings` class that implements the `IPathfinderSettings` interface. These settings control:

- Movement patterns
- Cell handling
- Performance optimization
- Cost calculations

## Basic Usage

```csharp
// Create settings with default values
var settings = new PathfinderSettings();

// Or create and customize settings
var customSettings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,
    IsCalculatingOccupiedCells = true,
    IsMovementBetweenCornersEnabled = false,
    IsCellWeightEnabled = true,
    StraightMovementMultiplier = 1.0f,
    DiagonalMovementMultiplier = 1.41f,
    InitialBufferSize = 128
};

// Create pathfinder with settings
var pathfinder = new Pathfinder(cells, width, height, customSettings);
```

## Available Settings

### Movement Pattern Settings

#### IsDiagonalMovementEnabled

Controls whether agents can move diagonally or only horizontally and vertically.

```csharp
settings.IsDiagonalMovementEnabled = true; // Allow diagonal movement (default)
```

- **True**: Agents can move in 8 directions (N, NE, E, SE, S, SW, W, NW)
- **False**: Agents can only move in 4 directions (N, E, S, W)

**Effect on pathfinding**: When enabled, paths can be shorter but may look less natural for certain types of games. When disabled, paths will follow a grid-like pattern.

#### IsMovementBetweenCornersEnabled

Controls whether agents can move diagonally between two diagonal obstacles.

```csharp
settings.IsMovementBetweenCornersEnabled = false; // Don't allow corner cutting (default)
```

- **True**: Agents can squeeze between diagonal obstacles
- **False**: Agents cannot move between diagonal obstacles

**Visualization**:
```
When false, this movement is illegal:
◻◎◼◻
◻◼◎◻
(◎ is the movement path, ◻ is walkable, ◼ is a wall)
```

**Effect on pathfinding**: When disabled, paths are more realistic as agents can't "squeeze" through impossible corners. When enabled, more paths become available but may look unnatural.

#### PathSmoothingMethod

Controls what type of path smoothing is applied to the calculated path.

```csharp
settings.PathSmoothingMethod = PathSmoothingMethod.None; // No path smoothing (default)
```

Available options:

- **PathSmoothingMethod.None**: No smoothing is applied, original A* path is returned
- **PathSmoothingMethod.Simple**: Simple angle-based smoothing that removes redundant waypoints
- **PathSmoothingMethod.StringPulling**: More advanced string pulling algorithm that creates optimal direct paths

**Effect on pathfinding**:
- With **None**, paths follow the exact grid cells calculated by A*, which may include unnecessary zigzags and turns.
- With **Simple**, redundant waypoints are removed based on angle changes, creating smoother paths while being computationally efficient.
- With **StringPulling**, a more thorough line-of-sight calculation is performed to create the most direct paths possible, though with slightly higher computational cost.


**Limitations**:
- **StringPullingSmoothing** might not behave as expected in all scenarios:
  - In complex mazes with narrow passages, it can occasionally create paths that appear to cut corners
  - When used with agents that have a size greater than 1, some paths might seem to brush too close to obstacles
  - In highly dynamic environments where obstacles change frequently, the more optimized path might become invalid more quickly

**Example visualization**:
```
No Smoothing:         Simple Smoothing:      String Pulling:
S---+                 S----+                 S
    |                      |                  \
    |                      |                   \
    |                      +-----E              \
    +---+                                        \
        |                                         \
        +---E                                      E

S = Start, E = End, + = Waypoint
```

**When to use**:
- **None**: When you need exact cell-by-cell paths or when performance is critical and path aesthetics don't matter
- **Simple**: For most game scenarios - good balance of performance and path quality. Almost no overhead.
- **StringPulling**: When you need the most direct and natural-looking paths and can afford the slight performance cost; best for open areas rather than complex mazes

### Cell Handling Settings

#### IsCalculatingOccupiedCells

Determines whether occupied cells are treated as blocked or passable.

```csharp
settings.IsCalculatingOccupiedCells = true; // Consider occupied cells as blocked (default)
```

- **True**: Cells marked as `IsOccupied = true` are considered unwalkable
- **False**: Occupation status is ignored; multiple agents can occupy the same cell

**Effect on pathfinding**: When enabled, paths avoid cells occupied by other agents. When disabled, multiple agents can share the same cells, useful for games where stacking is allowed.

### Cost Calculation Settings

#### IsCellWeightEnabled

Controls whether to consider the weight/cost of individual cells.

```csharp
settings.IsCellWeightEnabled = true; // Consider cell weights (default)
```

- **True**: The `Weight` property of cells affects path calculation
- **False**: All walkable cells are treated with equal cost

**Effect on pathfinding**: When enabled, paths prefer cells with lower weights. For example, you can make roads have a lower weight than rough terrain to encourage path generation along roads.

#### StraightMovementMultiplier

The cost multiplier for horizontal and vertical movements.

```csharp
settings.StraightMovementMultiplier = 1.0f; // Default value
```

**Effect on pathfinding**: Higher values make straight movements more costly, potentially favoring diagonal movements.

#### DiagonalMovementMultiplier

The cost multiplier for diagonal movements.

```csharp
settings.DiagonalMovementMultiplier = 1.41f; // Default value (approximation of √2)
```

**Effect on pathfinding**: Higher values make diagonal movements more costly, potentially favoring straight movements.

### Performance Settings

#### InitialBufferSize

The initial size of the internal priority queue buffer used during pathfinding.

```csharp
settings.InitialBufferSize = 128; // Set initial buffer size to 128 elements
```

- Default is `null`, which uses a system default (typically 64)
- Set to a higher value for large grids or complex paths
- Set to a lower value for simple grids to save memory

**Effect on performance**: A properly sized buffer reduces memory allocations during pathfinding. For most cases, the default size is sufficient, but for large maps or complex scenarios, increasing this value can improve performance. Change this value if you're know what you're doing.

## Recommended Settings for Common Scenarios

### Top-down Grid-based Strategy Game

```csharp
var settings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,
    IsCalculatingOccupiedCells = true,
    IsMovementBetweenCornersEnabled = false,
    IsCellWeightEnabled = true,
    StraightMovementMultiplier = 1.0f,
    DiagonalMovementMultiplier = 1.41f
};
```

### Tile-based Puzzle Game (4-way movement only)

```csharp
var settings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = false,
    IsCalculatingOccupiedCells = true,
    IsCellWeightEnabled = false
};
```

### RTS with Terrain Costs

```csharp
var settings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,
    IsCalculatingOccupiedCells = false, // Allow units to stack
    IsMovementBetweenCornersEnabled = false,
    IsCellWeightEnabled = true, // Consider terrain costs
    InitialBufferSize = 256 // Larger buffer for complex maps
};
```

## Advanced: Creating Custom Settings

You can create your own implementation of `IPathfinderSettings` if you need more dynamic behavior. Here's an example of a MonoBehaviour implementation for Unity:

```csharp
using UnityEngine;
using Migs.MPath.Core.Interfaces;

// This MonoBehaviour can be attached to a game manager or pathfinding manager object
public class GamePathfinderSettings : MonoBehaviour, IPathfinderSettings
{
    [Header("Movement Settings")]
    [field: SerializeField, Tooltip("If true, agents can move diagonally")]
    public bool AllowDiagonalMovement { get; set; } = true;
    
    [field: SerializeField, Tooltip("If true, agents cannot pass through occupied cells")]
    public bool RespectOccupiedCells { get; set; } = true;
    
    [field: SerializeField, Tooltip("If true, agents can squeeze between diagonal obstacles")]
    public bool AllowCornerMovement { get; set; } = false;
    
    [Header("Cost Settings")]
    [field: SerializeField, Tooltip("If true, different terrain types can have different movement costs")]
    public bool UseCellWeights { get; set; } = true;
    
    [field: SerializeField, Tooltip("Cost multiplier for moving horizontally or vertically")]
    public float StraightCost { get; set; } = 1.0f;
    
    [field: SerializeField, Tooltip("Cost multiplier for moving diagonally")]
    public float DiagonalCost { get; set; } = 1.41f;
    
    [Header("Performance")]
    [field: SerializeField, Tooltip("Size of the internal buffer. Leave at 0 for default value")]
    public int BufferSize { get; set; } = 0;
    
    // Game state reference
    [SerializeField] private GameStateManager _gameState;
    
    // IPathfinderSettings implementation
    public bool IsDiagonalMovementEnabled => AllowDiagonalMovement && !_gameState.IsInPuzzleMode;
    public bool IsCalculatingOccupiedCells => RespectOccupiedCells;
    public bool IsMovementBetweenCornersEnabled => AllowCornerMovement;
    public bool IsCellWeightEnabled => UseCellWeights;
    public float StraightMovementMultiplier => StraightCost;
    public float DiagonalMovementMultiplier => DiagonalCost;
    public int? InitialBufferSize => BufferSize > 0 ? BufferSize : null;
    
    // This allows you to easily swap between different pathfinding presets
    public void ApplyPreset(PathfindingPreset preset)
    {
        switch (preset)
        {
            case PathfindingPreset.Strategy:
                AllowDiagonalMovement = true;
                RespectOccupiedCells = true;
                AllowCornerMovement = false;
                UseCellWeights = true;
                StraightCost = 1.0f;
                DiagonalCost = 1.41f;
                break;
                
            case PathfindingPreset.Puzzle:
                AllowDiagonalMovement = false;
                RespectOccupiedCells = true;
                AllowCornerMovement = false;
                UseCellWeights = false;
                break;
                
            case PathfindingPreset.RTS:
                AllowDiagonalMovement = true;
                RespectOccupiedCells = false;
                AllowCornerMovement = false;
                UseCellWeights = true;
                break;
        }
    }
    
    public enum PathfindingPreset
    {
        Strategy,
        Puzzle,
        RTS
    }
}
```

With this approach, you can:
1. Configure pathfinding settings directly in the Unity Inspector
2. Change settings dynamically at runtime
3. Integrate with your game's systems seamlessly
4. Create different presets for different gameplay situations