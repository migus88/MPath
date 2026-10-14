# Performance Considerations

MPath is designed for high performance, but there are several factors that can affect pathfinding efficiency. This guide provides tips and techniques for optimizing your pathfinding operations.

> **Important Disclaimer**: MPath has been designed with performance as a primary goal. The optimizations suggested in this document will typically have minimal impact on overall performance and should only be considered if you encounter actual performance issues in your specific use case. It's good to understand what's happening under the hood, but avoid premature optimization. Focus first on making your code work correctly and cleanly, then optimize if necessary.

## Memory Optimization

MPath uses several techniques to minimize memory allocations:

### Array Pooling

MPath uses `ArrayPool<T>` internally to reuse arrays:

```csharp
// Path results must be disposed to return arrays to the pool
using var result = pathfinder.GetPath(agent, start, end);
```

Always dispose of `PathResult` objects when you're done with them. Consider using `using` statements or `using` declarations to ensure proper disposal.

### Pathfinder Reuse

Creating a new `Pathfinder` instance allocates memory for internal data structures. When possible, reuse pathfinder instances:

```csharp
// Create once and reuse
var pathfinder = new Pathfinder(cells, width, height, settings);

// Use for multiple path calculations
var path1 = pathfinder.GetPath(agent, start1, end1);
var path2 = pathfinder.GetPath(agent, start2, end2);

// Dispose when completely done
pathfinder.Dispose();
```

### Search Buffers

A pathfinder keeps two fixed buffers sized to the grid, rented from the pool once when it is created and returned by
`Dispose`: the search state (24 bytes per cell: scores, parent, open-set slot, a generation stamp) and the open set
(the priority queue of cells waiting to be expanded, 8 bytes per cell). Neither grows, so a search allocates nothing
for them however wide its frontier gets.

The search state is reset lazily: each query starts a new generation, and a cell's state counts only once the
running query has touched it. Starting a query never walks the grid, so a 2-step search costs a 2-step search, not
the grid size. Searches never write into your cells.

## Computational Optimization

> **Recommendation**: For most applications, you should prioritize code clarity and maintainability over micro-optimizations. MPath is already designed with performance in mind, and the differences between various configuration options are often negligible in real-world scenarios.

### Grid Representation

The grid representation affects performance:

1. **Flat Cell Array**: Fastest option when the array is row- or column-major: it is searched in place, so a query
   never copies the grid (declare the order with [`CellLayout`](../api/CellLayout.md); `Auto` detects it once). An
   unordered array is copied per query like a matrix.
2. **Cell Matrix**: Copied into a snapshot per query (see `GridSnapshotMode` below)
3. **Cell Holder Array**: Additional overhead from interface calls
4. **Cell Holder Matrix**: Most overhead but most convenient

```csharp
// For best performance, use flat arrays when possible
Cell[] cells = new Cell[width * height];        // cells[y * width + x]
// Initialize cells...
settings.CellLayout = CellLayout.RowMajor;
var pathfinder = new Pathfinder(cells, width, height, settings);
```

The `Cell[,]` and holder constructors search a snapshot of your grid. By default
([`GridSnapshotMode.PerQuery`](../api/GridSnapshotMode.md)) every query copies the whole grid into it first, so each
query costs at least O(grid size) however short it is. If you run many short queries on a large grid (movement
ranges per unit, AI turns, simulations), switch to `Manual`: the grid is copied once and then only after
`Refresh()`, and edits through `SetWalkable`/`SetOccupied`/`SetWeight`/`SetCell` are seen at once:

```csharp
var settings = new PathfinderSettings { GridSnapshotMode = GridSnapshotMode.Manual };
using var pathfinder = new Pathfinder(cellMatrix, settings);

pathfinder.SetOccupied(unitCell, true); // seen by the next query
cellMatrix[3, 4].IsWalkable = false;    // a direct edit...
pathfinder.Refresh();                   // ...needs Refresh before the next query
```

On a 1024x1024 open grid a 6-step `GetReachable` drops from milliseconds to microseconds this way (see
[benchmarks](../benchmarks/README.md)).

However, the performance differences between these options are usually minimal in most real-world scenarios. Choose the representation that makes the most sense for your game architecture and is easiest to work with. If your game frequently updates the grid, the benefits of using `ICellHolder` implementations will likely outweigh any small performance penalty from the interface calls.

### Settings Impact

Different settings can slightly impact performance:

- **Diagonal Movement**: Enabling increases computation by considering more neighbors
- **Cell Weights**: Enabling adds cost calculations
- **Corner Movement**: Minimal performance impact

```csharp
// Performance-optimized settings for simple scenarios
var fastSettings = new PathfinderSettings
{
    IsDiagonalMovementEnabled = false, // Reduces neighbor checks
    IsCellWeightEnabled = false,       // Skips weight calculations
    AreOccupiedCellsBlocking = false // Skips occupation checks
};
```

Again, these optimizations should only be considered if you've identified a specific performance bottleneck. In most cases, the gameplay benefits of features like diagonal movement and cell weights outweigh their small performance costs.

### Agent Size

Larger agents require more computation:

- Size 1: Fast, minimal neighbor checks
- Size 2+: Slower, checks all cells the agent would occupy

For large agents, consider caching paths when possible.

## Real-World Optimization Strategies

### Path Caching

For static environments where the same paths are requested repeatedly, use the built-in cache instead of keeping `PathResult`s yourself:

```csharp
_pathfinder.EnablePathCaching();

using var path = _pathfinder.GetPath(agent, start, end); // a cache hit is a fresh copy, still yours to dispose
```

The cache is keyed by start, destination and agent size, keeps its own copy of every stored path, and never hands that copy out, so disposing results as usual is safe. Call `InvalidateCache()` after changing the grid. See [Path Caching](path-caching.md).

### Distributed Pathfinding

For games with many agents, distribute pathfinding calculations:

```csharp
// Process a few agents each frame
private Queue<Agent> _pathfindingQueue = new Queue<Agent>();

public void Update()
{
    const int MaxPathsPerFrame = 5;
    
    for (int i = 0; i < MaxPathsPerFrame && _pathfindingQueue.Count > 0; i++)
    {
        var agent = _pathfindingQueue.Dequeue();
        using var path = _pathfinder.GetPath(agent, agent.Position, agent.Destination);
        agent.SetPath(path);
    }
}
```
## Advanced Optimization Techniques

### Multi-threading

The current implementation of MPath is not thread-safe. Multi-threaded pathfinding is planned for future versions, but at present, you should not attempt to use the same Pathfinder instance from multiple threads simultaneously.

If you need to perform pathfinding from multiple threads, create a separate Pathfinder instance for each thread and ensure they don't share any state.

## Memory Usage Patterns

MPath has been optimized to minimize garbage collection:

- Reuses arrays via `ArrayPool<T>`
- Uses value types where possible
- Uses unsafe code for direct memory access

The main sources of allocations are:

1. Creating new Pathfinder instances
2. Creating PathResult objects (minimized through pooling)