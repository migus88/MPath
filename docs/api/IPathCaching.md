# IPathCaching Interface

**Namespace:** `Migs.MPath.Core.Interfaces`

Interface for path caching functionality that allows storing and retrieving calculated paths based on start, destination, and agent properties.

## Methods

| Method | Description |
|--------|-------------|
| `bool TryGetCachedPath(IAgent agent, Coordinate from, Coordinate to, out PathResult pathResult)` | Tries to get a cached path result for the specified parameters. |
| `void CachePath(IAgent agent, Coordinate from, Coordinate to, PathResult pathResult)` | Caches a path result for the specified parameters. |
| `void ClearCache()` | Clears all cached paths. |
| `void Dispose()` | Releases all resources used by the path caching implementation. |

## Remarks

- Implements `IDisposable` and should be disposed when no longer needed.
- Implementations should efficiently store and retrieve paths based on agent properties and start/end coordinates.
- Custom implementations can be provided to the `Pathfinder.EnablePathCaching()` method.

## Examples

```csharp
// Create a custom path caching implementation
public class CustomPathCaching : IPathCaching
{
    private readonly Dictionary<string, PathResult> _cache = new();
    
    public bool TryGetCachedPath(IAgent agent, Coordinate from, Coordinate to, out PathResult pathResult)
    {
        var key = $"{agent.Size}_{from.X}_{from.Y}_{to.X}_{to.Y}";
        return _cache.TryGetValue(key, out pathResult);
    }
    
    public void CachePath(IAgent agent, Coordinate from, Coordinate to, PathResult pathResult)
    {
        if (!pathResult.IsSuccess) return;
        
        var key = $"{agent.Size}_{from.X}_{from.Y}_{to.X}_{to.Y}";
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