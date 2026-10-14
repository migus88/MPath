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
    AreOccupiedCellsBlocking = true,
    CornerBlocking = CornerBlocking.BetweenBlockedCells,
    IsCellWeightEnabled = true,
    StraightStepCost = 100,
    DiagonalStepCost = 141
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

#### CornerBlocking

Decides, separately for walls and occupied cells, which cells beside a diagonal step block it ("corner-cutting").

**Visualization**:
```
With the default (BetweenBlockedCells), this movement is illegal; with None it is allowed:
◻◎◼◻
◻◼◎◻
(◎ is the movement path, ◻ is walkable, ◼ is a wall)
```
 A diagonal step passes between two flanking cells; each is **open**, a **wall** (not walkable or off-grid) or **occupied** (when `AreOccupiedCellsBlocking` is on). Each [`CornerBlocking`](../api/CornerBlocking.md) flag blocks one combination:

```csharp
// Default: blocked only between two blocked cells
settings.CornerBlocking = CornerBlocking.BetweenBlockedCells;

// Tactics games: walls block squeezing between them; units can't be entered but never block a diagonal
settings.CornerBlocking = CornerBlocking.BetweenWalls;

// No cutting past a single wall corner, units never block
settings.CornerBlocking = CornerBlocking.AnyWall;

// Squeeze between any two corners
settings.CornerBlocking = CornerBlocking.None;

// Any mix of flags
settings.CornerBlocking = CornerBlocking.WallAndWall | CornerBlocking.WallAndOpen | CornerBlocking.OccupiedAndOccupied;
```

Flags: `WallAndWall`, `WallAndOccupied`, `OccupiedAndOccupied`, `WallAndOpen`, `OccupiedAndOpen`. Presets: `None`, `BetweenBlockedCells` (default), `BetweenWalls`, `AnyWall`, `All`.

For agents with `agentSize > 1`, a flanking cell is the agent's whole footprint at that position: a wall if any of its cells is a wall, else occupied if any is occupied. The rule applies to `GetPath`, `GetReachable`, the stepwise search and `StringPulling` smoothing; it is read once at construction, so it costs nothing per query.

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


**Guarantees**:
- **Simple** returns the first step, every turn point and the destination, with no repeated waypoints.
- **StringPulling** only takes a shortcut the search itself could walk: the segment is traced cell by cell and every step must fit the agent's full `Size x Size` footprint (walls and, with `AreOccupiedCellsBlocking`, occupied cells block) and obey the corner rule. With the default `CornerBlocking` (or diagonal movement disabled) a shortcut never passes diagonally between two obstacles that touch at a corner.

**Limitations**:
- **StringPulling** is the most expensive method, and larger agents make it costlier (`Size x Size` checks per traced cell).
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

#### GridSnapshotMode

Decides when a pathfinder built over a `PathCell[,]` matrix or over `ICellHolder`s copies your grid into its internal snapshot. It has no effect on a `PathCell[]`, which is searched in place (see `CellLayout` below).

```csharp
settings.GridSnapshotMode = GridSnapshotMode.PerQuery; // Copy before every query (default)
settings.GridSnapshotMode = GridSnapshotMode.Manual;   // Copy once, then only after pathfinder.Refresh()
```

- **PerQuery**: every query sees direct edits to your matrix or holders, but costs at least O(grid size) for the copy, however short the search.
- **Manual**: a query costs only the cells it searches. `SetWalkable`/`SetOccupied`/`SetWeight`/`SetCell` are seen at once; after editing the matrix or a holder directly, call `pathfinder.Refresh()` before the next query (it also clears the path cache).

**When to use Manual**: many short queries on a large grid, such as movement ranges per unit during an AI turn or in simulations, with grid changes that go through the `Set*` methods or happen at known points (end of a turn) where you can call `Refresh()`. See [GridSnapshotMode](../api/GridSnapshotMode.md).

#### TieBreaking

Decides which route is returned when several routes cost the same (common on open ground, and always with equal straight and diagonal costs).

```csharp
settings.TieBreaking = TieBreaking.None;           // Default: whichever the search expands first
settings.TieBreaking = TieBreaking.FewerDiagonals; // Fewest diagonal steps, then fewest steps
settings.TieBreaking = TieBreaking.FewerTurns;     // Fewest direction changes
```

**Effect on pathfinding**:
- With **None**, equal-cost routes resolve by search order: open ground can produce interleaved diagonal/straight zig-zags, and an unrelated grid edit can change the route.
- With **FewerDiagonals** or **FewerTurns**, equal-cost routes tie exactly (costs are integers), the tie-break key decides, and a fixed rule settles the rest (keep the direction, else West, East, North, South, then the diagonals). The route depends on the grid alone.
- `PathResult.Steps` / `DiagonalSteps` and `ReachableCell.Steps` / `DiagonalSteps` report integer step counts in every mode, so you never have to recover them from costs.

**When to use**: whenever players see the route (units moving on a board, previews). Keep **None** when only the cost matters and you want the fastest search on open ground. See [TieBreaking](../api/TieBreaking.md).

#### CellLayout

Which index of a `PathCell[]` or `ICellHolder[]` array holds the cell at (X, Y). A cell's slot is its position; queries never reorder or write your array.

```csharp
settings.CellLayout = CellLayout.ColumnMajor; // cells[x * height + y] (default)
settings.CellLayout = CellLayout.RowMajor;    // cells[y * width + x]
```

- A `PathCell[]` is searched in place in either layout: no copy, no sort; a query costs only the cells it searches. An `ICellHolder[]` is copied in the same layout, as `GridSnapshotMode` says.
- `pathfinder.GetIndex(c)` and `RangeResult.CopyCostsTo` use the same layout.
- Any other value throws `ArgumentOutOfRangeException` when the pathfinder is created.

No effect on the `PathCell[,]` and `ICellHolder[,]` constructors (the cell at (X, Y) is `[X, Y]`). See [CellLayout](../api/CellLayout.md).

### Cell Handling Settings

#### AreOccupiedCellsBlocking

Determines whether occupied cells are treated as blocked or passable.

```csharp
settings.AreOccupiedCellsBlocking = true; // Consider occupied cells as blocked (default)
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

**Effect on pathfinding**: When enabled, each step into a cell costs its step cost **plus** the cell's `Weight` (an `int` in the same unit as the step costs), and paths prefer cells with lower weights. For example, you can make roads have a lower weight than rough terrain to encourage path generation along roads.

#### StraightStepCost

The integer cost of one horizontal or vertical step. Costs have no hidden scale: you pick the resolution (10/14, 100/141, 1000/1414, ...), and cell weights, budgets and every reported cost use the same unit.

```csharp
settings.StraightStepCost = 100; // Default value
```

**Effect on pathfinding**: Higher values make straight movements more costly, potentially favoring diagonal movements.

#### DiagonalStepCost

The integer cost of one diagonal step, in the same unit as `StraightStepCost`.

```csharp
settings.DiagonalStepCost = 141; // Default value (1.41 straight steps, approximating √2)
```

**Effect on pathfinding**: Higher values make diagonal movements more costly, potentially favoring straight movements.

**Validation and limits**:
- Each step cost must be at least 1; the pathfinder constructor throws `ArgumentOutOfRangeException` otherwise.
- `(width + height) * the larger step cost in use` must not exceed 1,073,741,823 (`int.MaxValue / 2`, the overflow headroom), or the constructor throws `ArgumentOutOfRangeException`. The diagonal cost counts only with diagonal movement enabled.
- A route whose cost would pass that headroom is never taken: cells behind it are unreachable, and nothing wraps around. Weights above it count as that value.
- `PathCell.Weight` must not be negative: `SetWeight` and `SetCell` throw `ArgumentOutOfRangeException`, and a negative weight written straight into your grid counts as 0.

## Recommended Settings for Common Scenarios

### Top-down Grid-based Strategy Game

```csharp
var settings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,
    AreOccupiedCellsBlocking = true,
    CornerBlocking = CornerBlocking.BetweenBlockedCells,
    IsCellWeightEnabled = true,
    StraightStepCost = 100,
    DiagonalStepCost = 141
};
```

### Tile-based Puzzle Game (4-way movement only)

```csharp
var settings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = false,
    AreOccupiedCellsBlocking = true,
    IsCellWeightEnabled = false
};
```

### RTS with Terrain Costs

```csharp
var settings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,
    AreOccupiedCellsBlocking = false, // Allow units to stack
    IsCellWeightEnabled = true // Consider terrain costs
};
```

## Advanced: Creating Custom Settings

You can create your own implementation of `IPathfinderSettings` if you need more dynamic behavior. Every member is required (there are no default bodies); return `PathfinderSettings`'s defaults for the ones you don't care about. Here's an example of a MonoBehaviour implementation for Unity:

```csharp
using UnityEngine;
using Migs.MPath.Core.Data;
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
    
    [field: SerializeField, Tooltip("Cost of one horizontal or vertical step")]
    public int StraightCost { get; set; } = 100;
    
    [field: SerializeField, Tooltip("Cost of one diagonal step")]
    public int DiagonalCost { get; set; } = 141;
    
    // Game state reference
    [SerializeField] private GameStateManager _gameState;
    
    // IPathfinderSettings implementation
    public bool IsDiagonalMovementEnabled => AllowDiagonalMovement && !_gameState.IsInPuzzleMode;
    public bool AreOccupiedCellsBlocking => RespectOccupiedCells;
    public CornerBlocking CornerBlocking => AllowCornerMovement ? CornerBlocking.None : CornerBlocking.BetweenBlockedCells;
    public bool IsCellWeightEnabled => UseCellWeights;
    public int StraightStepCost => StraightCost;
    public int DiagonalStepCost => DiagonalCost;
    public PathSmoothingMethod PathSmoothingMethod => PathSmoothingMethod.None;
    public GridSnapshotMode GridSnapshotMode => GridSnapshotMode.PerQuery;
    public CellLayout CellLayout => CellLayout.ColumnMajor;
    public TieBreaking TieBreaking => TieBreaking.None;
    
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
                StraightCost = 100;
                DiagonalCost = 141;
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