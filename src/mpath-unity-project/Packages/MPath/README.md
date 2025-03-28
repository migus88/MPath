# MPath for Unity

MPath is a high-performance A* pathfinding system for grid-based environments, designed for both 2D and 3D games.

## Features

- Fast A* pathfinding with minimal memory allocations
- Support for agent sizes (larger than 1 tile)
- Path smoothing for more natural movement
- Diagonal movement support
- Path caching for better performance with repeated path requests
- Cell weighting for varied movement costs
- Simple integration with Unity's grid-based systems

## Quick Start

1. Create a grid of cells:

```csharp
// Create cells for a 10x10 grid
var cells = new Cell[100];
for (int i = 0; i < 100; i++)
{
    int x = i % 10;
    int y = i / 10;
    cells[i] = new Cell(new Coordinate(x, y), true);
}
```

2. Create a pathfinder:

```csharp
using var pathfinder = new Pathfinder(cells, 10, 10);

// Optional: Enable path caching for better performance
pathfinder.EnablePathCaching();
```

3. Find a path:

```csharp
var agent = new SimpleAgent(1); // Agent with size 1
var from = new Coordinate(0, 0);
var to = new Coordinate(9, 9);

using var result = pathfinder.GetPath(agent, from, to);

if (result.IsSuccess)
{
    // Use the path
    foreach (var coordinate in result.Path)
    {
        Debug.Log($"Move to: {coordinate.X}, {coordinate.Y}");
    }
}
```

## Important Notes

- Always dispose PathResult objects using `using` statements or `Dispose()`
- Reuse the Pathfinder instance for best performance
- Dispose the Pathfinder when no longer needed

## Documentation

Comprehensive documentation is available at the [MPath GitHub repository](https://github.com/migus88/MPath). 