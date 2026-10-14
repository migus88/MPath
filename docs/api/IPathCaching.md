# IPathCaching Interface

**Namespace:** `Migs.MPath.Core.Interfaces`

Interface for path caching functionality that allows storing and retrieving calculated paths keyed by the agent size, the start and the destination.

## Methods

| Method | Description |
|--------|-------------|
| `bool TryGetCachedPath(int agentSize, Coordinate from, Coordinate to, out PathResult pathResult)` | Tries to get a cached path result for the specified parameters. |
| `void CachePath(int agentSize, Coordinate from, Coordinate to, PathResult pathResult)` | Caches a path result for the specified parameters. |
| `void ClearCache()` | Clears all cached paths. |
| `void Dispose()` | Releases all resources used by the path caching implementation. |

## Remarks

- **Ownership:** the `PathResult` passed to `CachePath` is owned by the cache, which disposes it when it evicts or clears the entry. The instance returned by `TryGetCachedPath` also stays owned by the cache: `Pathfinder` copies it before returning a result to its caller, so callers may dispose every result they receive.

- **Key:** every call receives the query's `agentSize` (the third argument of `GetPath`, 1 by default) with `from` and `to`. Key on all three, as `DefaultPathCaching` does: a size-1 and a size-2 query of the same pair must not share an entry. Queries whose [`QueryOptions`](QueryOptions.md) change the rules never reach the cache.
- Implements `IDisposable` and should be disposed when no longer needed.
- Implementations should efficiently store and retrieve paths by agent size and start/end coordinates.
- Custom implementations can be provided to the `Pathfinder.EnablePathCaching()` method.

## Examples

```csharp
// Create a custom path caching implementation
public class CustomPathCaching : IPathCaching
{
    private readonly Dictionary<string, PathResult> _cache = new();
    
    public bool TryGetCachedPath(int agentSize, Coordinate from, Coordinate to, out PathResult pathResult)
    {
        var key = $"{agentSize}_{from.X}_{from.Y}_{to.X}_{to.Y}";
        return _cache.TryGetValue(key, out pathResult);
    }
    
    public void CachePath(int agentSize, Coordinate from, Coordinate to, PathResult pathResult)
    {
        if (!pathResult.IsSuccess) return;
        
        var key = $"{agentSize}_{from.X}_{from.Y}_{to.X}_{to.Y}";
        _cache[key] = pathResult;
    }
    
    public void ClearCache()
    {
        foreach (var path in _cache.Values)
        {
            path.Dispose();
        }
        _cache.Clear();
    }
    
    public void Dispose()
    {
        ClearCache();
    }
}

// Use the custom implementation with a pathfinder
var pathfinder = new Pathfinder(cells, width, height);
pathfinder.EnablePathCaching(new CustomPathCaching());
``` 