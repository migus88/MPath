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

### Buffer Sizing

The `InitialBufferSize` setting controls the initial size of the internal priority queue used during pathfinding:

```csharp
var settings = new PathfinderSettings
{
    InitialBufferSize = null // Use default size, which is usually sufficient
};
```

In most cases, the default buffer size is sufficient, even for large grids. The implementation has been tested with maze-like environments of approximately 300x500 cells without issues using the default buffer size.

You should only adjust this value if:
1. You notice excessive garbage collection during profiling
2. You're working with extremely large grids or very complex path calculations
3. You've measured and confirmed that changing this value actually improves performance

Let the library handle the sizing automatically unless you have a specific reason to override it.

## Computational Optimization

> **Recommendation**: For most applications, you should prioritize code clarity and maintainability over micro-optimizations. MPath is already designed with performance in mind, and the differences between various configuration options are often negligible in real-world scenarios.

### Grid Representation

The grid representation affects performance:

1. **Flat Cell Array**: Fastest option, best for performance-critical scenarios
2. **Cell Matrix**: Slightly slower due to 2D indexing
3. **Cell Holder Array**: Additional overhead from interface calls
4. **Cell Holder Matrix**: Most overhead but most convenient

```csharp
// For best performance, use flat arrays when possible
Cell[] cells = new Cell[width * height];
// Initialize cells...
var pathfinder = new Pathfinder(cells, width, height, settings);
```

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
    IsCalculatingOccupiedCells = false // Skips occupation checks
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

For static environments where paths don't change frequently:

```csharp
private Dictionary<(Coordinate, Coordinate, int), PathResult> _pathCache = new();

public PathResult GetCachedPath(IAgent agent, Coordinate start, Coordinate end)
{
    // Include agent size in the cache key
    var key = (start, end, agent.Size);
    
    if (_pathCache.TryGetValue(key, out var cachedPath))
    {
        return cachedPath;
    }
    
    var newPath = _pathfinder.GetPath(agent, start, end);
    _pathCache[key] = newPath; 
    return newPath;
}

private PathResult ClonePath(PathResult original)
{
    // Implementation to create a persistent copy
    // ...
}
```

By including the agent's size in the cache key, you ensure that agents of different sizes will get appropriate paths, even between the same start and end points.

> Consider not disposing the `PathResult`s in this scenario because disposal will return a lot of arrays to the pool that potentially not needed anymore and it can create a memory leak.

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