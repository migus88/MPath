# Grid Setup Guide

The grid is the foundation of your pathfinding system in MPath. This guide explains the various ways to set up and configure your grid.

## Grid Representation Options

MPath provides several options for representing your grid, each with different benefits:

1. **Cell Array**: Simple, flat array of cells
2. **Cell Matrix**: 2D array/matrix of cells
3. **Cell Holder Array**: Array of objects implementing ICellHolder
4. **Cell Holder Matrix**: 2D array of objects implementing ICellHolder

## When Your Game Already Has a `Cell` Type

MPath's grid square is `Migs.MPath.Core.Data.Cell`, and many games already have a `Cell` of their own. Importing both namespaces makes `Cell` ambiguous (`CS0104`). Alias MPath's type in the files that build the grid:

```csharp
using Migs.MPath.Core.Data;
using GridCell = Migs.MPath.Core.Data.Cell;

var grid = new GridCell[width, height];
grid[x, y] = new GridCell { Coordinate = new Coordinate(x, y), IsWalkable = true };
```

Or alias your own type (`using GameCell = MyGame.Board.Cell;`) and keep `Cell` for MPath. A distinct name for MPath's type is planned for the next major version; until then the alias is the supported way.

## Option 1: Cell Array

The most straightforward approach is to use a flat array of `Cell` objects. Note that this option is most suitable for static or rarely changing environments, as it's the most performant but least flexible for dynamic grid updates.

```csharp
// Create a 20x20 grid
var width = 20;
var height = 20;
var cells = new Cell[width * height];

// Initialize cells
for (var y = 0; y < height; y++)
{
    for (var x = 0; x < width; x++)
    {
        var index = y * width + x;
        cells[index] = new Cell
        {
            Coordinate = new Coordinate(x, y),
            IsWalkable = true,
            Weight = 100
        };
    }
}

// Create the pathfinder. The array is row-major (y * width + x): say so, and it is searched in place.
var settings = new PathfinderSettings { CellLayout = CellLayout.RowMajor };
var pathfinder = new Pathfinder(cells, width, height, settings);
```

The pathfinder keeps a reference to your array and reads it on every query; it never reorders or writes it. Set
[`CellLayout`](../api/CellLayout.md) to the order you filled it in:

- `RowMajor`: `cells[y * width + x]` (as above). `ColumnMajor`: `cells[x * height + y]`. Both are searched in place,
  so a query costs only the cells it searches and your own writes are seen by the next query. A declared layout is
  checked once, at the first query, and a mismatch throws `InvalidOperationException`.
- `Unordered`: any order; each cell's `Coordinate` says where it is. The array is copied into a pooled buffer before
  every query (or once and after `Refresh()` with `GridSnapshotMode.Manual`), like a `Cell[,]`.
- `Auto` (the default): detects which of the three at the first query, and again after `Refresh()`.

Only the first `width * height` elements are read; a shorter array throws `ArgumentException`.

**Benefits:**
- Simple implementation
- Fastest performance
- Direct control over cell properties

**Considerations:**
- Requires manual coordinate-to-index conversion (`pathfinder.GetIndex(coordinate)` does it for you)
- No direct integration with game objects
- Not ideal for dynamic environments as the entire grid must be recreated when cells change

## Option 2: Cell Matrix

For more natural 2D grid access, you can use a matrix/2D array. Like the Cell Array option, this is best suited for static environments where the grid structure doesn't change frequently.

```csharp
// Create a 20x20 grid matrix
var width = 20;
var height = 20;
var cellMatrix = new Cell[width, height];

// Initialize cells
for (var y = 0; y < height; y++)
{
    for (var x = 0; x < width; x++)
    {
        cellMatrix[x, y] = new Cell
        {
            Coordinate = new Coordinate(x, y),
            IsWalkable = true,
            Weight = 100
        };
    }
}

// Create the pathfinder
var pathfinder = new Pathfinder(cellMatrix);
```

**Benefits:**
- Natural 2D indexing (cellMatrix[x, y])
- More intuitive for grid-based games
- No need for manual coordinate-to-index calculation

**Considerations:**
- Slightly slower than flat array
- Still requires separate mapping to game objects
- Not ideal for dynamic environments as the entire grid must be recreated when cells change

## Option 3: Cell Holder Array

When your cells are represented by game objects or other entities, you can use the `ICellHolder` interface. This approach is ideal for dynamic environments where cell properties change frequently.

```csharp
// Define a MonoBehaviour class that implements ICellHolder (Unity example)
using UnityEngine;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

public class TerrainTile : MonoBehaviour, ICellHolder
{
    public bool IsObstacle { get; set; }
    public int MovementCost = 100; // Extra cost to enter, in step-cost units
    
    private Cell _cellData;
    
    // Unity-specific properties
    public SpriteRenderer SpriteRenderer;
    public Color WalkableColor = Color.green;
    public Color ObstacleColor = Color.red;
    
    public Cell CellData => _cellData;
    
    public void Initialize(int x, int y)
    {
        _cellData = new Cell
        {
            Coordinate = new Coordinate(x, y),
            IsWalkable = !IsObstacle,
            Weight = MovementCost
        };
        
        UpdateVisuals();
    }
    
    public void SetObstacle(bool isObstacle)
    {
        IsObstacle = isObstacle;
        // Cell data is automatically updated since we return the _cellData reference
        _cellData.IsWalkable = !isObstacle;
        UpdateVisuals();
    }
    
    public void SetMovementCost(int cost)
    {
        MovementCost = cost;
        // Cell data is automatically updated 
        _cellData.Weight = cost;
    }
    
    private void UpdateVisuals()
    {
        if (SpriteRenderer != null)
        {
            SpriteRenderer.color = IsObstacle ? ObstacleColor : WalkableColor;
        }
    }
}

// Create a grid of terrain tiles (in Unity, this would typically be done in a manager class)
public class GridManager : MonoBehaviour
{
    public GameObject TerrainTilePrefab;
    public int Width = 20;
    public int Height = 20;
    
    private TerrainTile[] _terrain;
    private Pathfinder _pathfinder;
    
    void Start()
    {
        _terrain = new TerrainTile[Width * Height];
        
        // Create the grid
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var index = y * Width + x;
                
                // Instantiate a terrain tile GameObject
                var tileObject = Instantiate(TerrainTilePrefab, new Vector3(x, y, 0), Quaternion.identity);
                tileObject.transform.parent = transform;
                tileObject.name = $"Tile_{x}_{y}";
                
                // Get the TerrainTile component and initialize it
                var tile = tileObject.GetComponent<TerrainTile>();
                tile.Initialize(x, y);
                
                _terrain[index] = tile;
            }
        }
        
        // Create the pathfinder with cell holders
        _pathfinder = new Pathfinder(_terrain, Width, Height);
    }
}
```

**Benefits:**
- Direct integration with game entities
- Allows dynamic updating of pathfinding data based on game state
- Better separation of concerns
- Ideal for dynamic environments as cell data updates automatically

**Considerations:**
- Slightly more overhead than direct cell arrays
- Requires implementing the ICellHolder interface

## Option 4: Cell Holder Matrix

Similar to the cell holder array, but with 2D array access:

```csharp
// Using the same TerrainTile MonoBehaviour from the previous example

// Create a 2D grid of terrain tiles
public class GridManager : MonoBehaviour
{
    public GameObject TerrainTilePrefab;
    public int Width = 20;
    public int Height = 20;
    
    private TerrainTile[,] _terrainMatrix;
    private Pathfinder _pathfinder;
    
    void Start()
    {
        _terrainMatrix = new TerrainTile[Width, Height];
        
        // Create the grid
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                // Instantiate a terrain tile GameObject
                var tileObject = Instantiate(TerrainTilePrefab, new Vector3(x, y, 0), Quaternion.identity);
                tileObject.transform.parent = transform;
                tileObject.name = $"Tile_{x}_{y}";
                
                // Get the TerrainTile component and initialize it
                var tile = tileObject.GetComponent<TerrainTile>();
                tile.Initialize(x, y);
                
                _terrainMatrix[x, y] = tile;
            }
        }
        
        // Create the pathfinder with cell holder matrix
        _pathfinder = new Pathfinder(_terrainMatrix);
    }
}
```

**Benefits:**
- Combines natural 2D indexing with game entity integration
- Most intuitive for grid-based games with object representation
- No need for manual coordinate-to-index calculation
- Ideal for dynamic environments as cell data updates automatically

**Considerations:**
- Slightly more overhead than other options

## Dynamic Grid Updates

MPath does not automatically update your grid when game state changes. However, the way you manage these updates depends on your grid representation choice:

### Direct Cell Representation (Options 1 & 2)

The pathfinder keeps a reference to your array or matrix and reads it on every query, so keep the same instance and update cells through it:

```csharp
// O(1), writes into your own array/matrix, and clears the path cache if needed
pathfinder.SetWalkable(new Coordinate(4, 2), false);
pathfinder.SetOccupied(unit.Position, true);
pathfinder.SetWeight(new Coordinate(7, 3), 250); // ints in step-cost units; a negative weight throws

// The next query sees the change
using var path = pathfinder.GetPath(start, end);
```

The setters find the cell by its coordinate, so they work whatever order your `Cell[]` is in. Writing into the array yourself also works: queries never reorder it, so your own indexing (or `pathfinder.GetIndex(coordinate)`) stays valid. After direct writes, call `pathfinder.Refresh()` if path caching is enabled (it clears the cache) or if the array is unordered under `GridSnapshotMode.Manual`; moving cells to other indexes or changing their coordinates also needs `Refresh()`.

### Cell Holder Representation (Options 3 & 4)

The ICellHolder interface is specifically designed to minimize the overhead of grid updates. Since the pathfinder re-reads every holder on every query, updates are automatically reflected. The holder is the source of truth, so the `Set*` cell-update methods throw `NotSupportedException` in these modes (call `InvalidateCache()` yourself after a change when path caching is enabled):

```csharp
// Example: Dynamically updating terrain in a game
public void OnPlayerInteractWithTile(TerrainTile tile)
{
    // Toggle the obstacle state
    tile.SetObstacle(!tile.IsObstacle);
    
    // That's it! No need to reinitialize the pathfinder.
    // Next path request will use the updated data automatically.
    
    // Example: Calculate a new path with the updated grid
    using var newPath = _pathfinder.GetPath(player, player.Position, destination);
}

// Example: Changing terrain cost based on game events
public void OnRainStart()
{
    // Make all dirt tiles more costly to traverse
    foreach (var tile in _terrain)
    {
        if (tile.TerrainType == TerrainType.Dirt)
        {
            tile.SetMovementCost(250); // Muddy terrain is slower to traverse
        }
    }
    
    // All subsequent path calculations will account for the new costs
}
```

This approach is particularly powerful because:

1. The update happens directly on the game object or entity
2. The cell data is automatically updated through the reference
3. No manual reinitialization of the pathfinder is needed
4. Game logic remains clean and intuitive

## Performance Considerations

- **Update Frequency**: If your grid changes infrequently, use direct Cell arrays
- **Dynamic Environments**: For frequently changing environments, use cell holders
- **Cell Reuse**: The pathfinder pools and reuses cell arrays where possible to minimize GC overhead

## Next Steps

- See the [Agent Configuration](agent-configuration.md) guide to understand how agents navigate your grid
- Explore [Pathfinder Settings](pathfinder-settings.md) to optimize pathfinding for your specific needs 