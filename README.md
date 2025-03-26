# MPath

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/Version-1.0.0-blue.svg)](src/mpath-unity-project/Packages/MPath/package.json)

A high-performance A* implementation for 2D grid navigation, optimized for speed and minimal memory allocations.

## Features

- Fast A* pathfinding with near-zero garbage collection overhead
- Allocates memory only when necessary to maximize performance
- Designed for 2D grid-based navigation
- Works in Unity projects and any other type of .NET applications
- Extensively tested with comprehensive unit tests
- Includes performance benchmarks

> ⚠️ **Note:** MPath is not yet thread-safe and should not be used across multiple threads.

## Project Structure

- **docs** - Usage documentation and guides
- **src/mpath-unity-project** - Unity project
  - **Assets/Examples** - Unity usage examples
  - **Packages/MPath** - The Unity package
- **src/mpath-source** - Non-Unity .NET solution
  - **Migs.MPath.Benchmarks** - Performance benchmarks
  - **Migs.MPath.Core** - Core functionality wrapped for standalone .NET usage
  - **Migs.MPath.Tests** - Unit tests
  - **Migs.MPath.Tools** - Utilities for tests and benchmarks

## Installation

<details>
<summary>Unity (via OpenUPM) - Recommended</summary>

[OpenUPM](https://openupm.com/) package name coming soon.
</details>

<details>
<summary>Unity (via Git URL)</summary>

Add MPath to your project via the Unity Package Manager:

1. Open the Package Manager window in Unity (Window > Package Manager)
2. Click the "+" button and select "Add package from git URL..."
3. Enter the following URL:
   ```
   https://github.com/migus88/MPath.git?path=/src/mpath-unity-project/Packages/MPath
   ```

To use a specific version, append a tag with version (e.g `1.0.0`) to the URL:
   ```
   https://github.com/migus88/MPath.git?path=/src/mpath-unity-project/Packages/MPath#1.0.0
   ```
</details>

<details>
<summary>Unity (via .unitypackage)</summary>

1. Download the latest `.unitypackage` from the [Releases](https://github.com/migus88/MPath/releases) page
2. Import it into your Unity project (Assets > Import Package > Custom Package)
</details>

<details>
<summary>.NET Projects (via NuGet)</summary>

NuGet package name coming soon.
</details>

## Quick Start

### Unity Example

See more examples inside Unity Project

```csharp
// Define a cell holder for Unity objects
public class GridCell : MonoBehaviour, ICellHolder
{
    public Cell CellData { get; private set; }
    
    [SerializeField] private bool _isWalkable = true;
    [SerializeField] private Vector2Int _position;
    [SerializeField] private float _weight = 1.0f;
    
    private void Awake()
    {
        CellData = new Cell
        {
            Coordinate = new Coordinate(_position.x, _position.y),
            IsWalkable = _isWalkable,
            Weight = _weight
        };
    }
}

// Define a Unity agent
public class UnitAgent : MonoBehaviour, IAgent
{
    public Coordinate Coordinate { get; set; }
    public int Size => 1; // Single cell agent
    
    // Movement logic using the path
    public IEnumerator FollowPath(PathResult result, float speed)
    {
        foreach (var coordinate in result.Path)
        {
            var targetPosition = new Vector3(coordinate.X, 0, coordinate.Y);
            
            while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, 
                    targetPosition,
                    speed * Time.deltaTime
                );
                yield return null;
            }
            
            Coordinate = coordinate;
        }
    }
}

// In your game controller
[SerializeField] private ScriptablePathfinderSettings _settings;
[SerializeField] private GridCell[] _gridCells;
[SerializeField] private Vector2Int _gridSize;

private Pathfinder _pathfinder;

private void Start()
{
    // Initialize the pathfinder with the grid cells
    _pathfinder = new Pathfinder(_gridCells, _gridSize.x, _gridSize.y, _settings);
}

// Find a path for an agent
public PathResult FindPath(UnitAgent agent, Coordinate destination)
{
    return _pathfinder.GetPath(agent, agent.Coordinate, destination);
}

private void OnDestroy()
{
    // Dispose the pathfinder when no longer needed
    _pathfinder?.Dispose();
}
```

### .NET Projects

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
            Weight = 1.0f       // Default movement cost
        };
    }
}

// Add some obstacles
cells[12].IsWalkable = false;
cells[13].IsWalkable = false;

// Create a simple agent
public class SimpleAgent : IAgent
{
    // Agent occupies a single cell
    public int Size => 1;
}
var agent = new SimpleAgent();

// Configure pathfinder settings
IPathfinderSettings settings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = true,      // Allow diagonal movement
    IsMovementBetweenCornersEnabled = false // Don't allow cutting corners
};

// Create the pathfinder
using var pathfinder = new Pathfinder(cells, width, height, settings);

// Find a path
var start = new Coordinate(1, 1);
var end = new Coordinate(8, 8);

using var result = pathfinder.GetPath(agent, start, end);

// Check if a path was found and use it
if (result.IsSuccess)
{
    Console.WriteLine($"Path found with {result.Length} steps!");
    
    foreach (Coordinate coordinate in result.Path)
    {
        Console.WriteLine($"Step: {coordinate}");
    }
}
```

### Important Notes

- Always dispose `PathResult` objects after use (use `using` statements)
- Reuse the pathfinder instance for best performance
- For larger grids, consider using dynamic grid updates to improve performance

## Documentation

For detailed usage instructions and API reference, see the [documentation](docs/README.md).

## Benchmarks

```
Benchmark results will be added here.
```

## License

MPath is licensed under the MIT License. See [LICENSE](LICENSE) for details. 