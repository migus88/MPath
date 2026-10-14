# Getting Started with MPath

This guide will help you quickly set up and start using MPath for pathfinding in your application.

## Basic Setup

To use MPath, you need:
1. A grid of cells representing your navigable space
2. A pathfinder that will calculate the path
3. Optionally, the agent's size (single-cell agents need nothing)

## Creating a Simple Grid

The first step is to create a grid of cells:

```csharp
// Create a 10x10 grid of cells
int width = 10;
int height = 10;
Cell[] cells = new Cell[width * height];

// Initialize each cell
for (int y = 0; y < height; y++)
{
    for (int x = 0; x < width; x++)
    {
        int index = y * width + x;
        cells[index] = new Cell
        {
            Coordinate = new Coordinate(x, y),
            IsWalkable = true,  // All cells are walkable by default
            IsOccupied = false, // No cells are occupied by default
            Weight = 100        // Extra cost to enter, in step-cost units (100 = one straight step by default)
        };
    }
}

// Make some cells unwalkable (e.g., walls or obstacles)
cells[12].IsWalkable = false;
cells[13].IsWalkable = false;
cells[22].IsWalkable = false;
```

## Agent Size

Queries assume a single-cell agent. For a larger one, pass its size as the third argument (`2` for a 2x2 unit); see the [Agent Configuration](agent-configuration.md) guide:

```csharp
using var truckPath = pathfinder.GetPath(start, end, 2); // a 2x2 truck
```

## Creating a Pathfinder

Now, create a pathfinder with your grid:

```csharp
// Create default pathfinder settings
IPathfinderSettings settings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,      // Allow diagonal movement
    AreOccupiedCellsBlocking = true,     // Consider occupied cells as blocked
    CornerBlocking = CornerBlocking.BetweenBlockedCells // Don't squeeze between two blocked corners
};

// Create the pathfinder with your grid and settings
Pathfinder pathfinder = new Pathfinder(cells, width, height, settings);

// Dispose of the pathfinder when no longer needed
pathfinder.Dispose();
```

## Finding a Path

With all components set up, you can now find a path:

```csharp
// Define start and end coordinates
var start = new Coordinate(1, 1);
var end = new Coordinate(8, 8);

// Calculate the path for a single-cell agent
using var result = pathfinder.GetPath(start, end);

// Or for a 2x2 agent: pathfinder.GetPath(start, end, 2)

// Check if a path was found
if (result.IsSuccess)
{
    Console.WriteLine($"Path found with {result.Length} steps!");
    
    // Iterate through the path coordinates
    foreach (Coordinate coordinate in result.Path)
    {
        Console.WriteLine($"Step: {coordinate}");
    }
}
else
{
    Console.WriteLine("No path found!");
}
```

## Important Notes

- Always dispose of `PathResult` objects after use to return arrays to the pool
- Consider using the `using` statement or `using` declaration to ensure proper disposal
- For large grids or frequent pathfinding, consider reusing the pathfinder instance

## Next Steps

Now that you have the basics working, you might want to explore:

- [Grid Setup Guide](grid-setup.md) for advanced grid configurations
- [Pathfinder Settings](pathfinder-settings.md) to customize pathfinding behavior
- [Agent Configuration](agent-configuration.md) for multi-cell agents
- [Performance Considerations](performance.md) for optimizing pathfinding operations 