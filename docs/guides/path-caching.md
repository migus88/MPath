# Path Caching

## Overview

MPath provides an optional path caching feature that can significantly improve performance in scenarios where the same paths are requested multiple times. Path caching stores the results of previously calculated paths, avoiding redundant calculations for identical requests.

By default, path caching is disabled. When enabled, paths are cached based on:
- Starting coordinate
- Destination coordinate
- Agent size

## Basic Usage

### Enabling Path Caching

```csharp
// Using the default path caching implementation
var pathfinder = new Pathfinder(cells, width, height);
pathfinder.EnablePathCaching();

// Get a path (will be calculated)
using var firstPath = pathfinder.GetPath(agent, from, to);

// Get the same path again (will be retrieved from cache)
using var secondPath = pathfinder.GetPath(agent, from, to);
```

### Result Ownership

Every `PathResult` that `GetPath` returns is owned by the caller, cache hit or not, so dispose it as usual. The cache keeps its own private copy of each stored path and never hands that instance out: a cache hit returns a fresh result holding a copy of the cached coordinates. Disposing a result, holding it across `InvalidateCache()`, or getting the same cached path twice never affects the cache or another caller's result.

### Disabling Path Caching

```csharp
// Disable path caching when no longer needed
pathfinder.DisablePathCaching();
```

### Invalidating the Cache

The cell-update methods (`SetWalkable`, `SetOccupied`, `SetWeight`, `SetCell`) clear the cache themselves whenever the change can affect a cached path. Any other grid change (writing to your `Cell[]`/`Cell[,]` directly, or changing what your cell holders report) is invisible to the cache: invalidate it without disabling caching entirely, or later queries may return paths computed for the old grid:

```csharp
// Clear the path cache but keep caching enabled
pathfinder.InvalidateCache();
```

## Custom Path Caching Implementation

You can provide your own path caching implementation by creating a class that implements the `IPathCaching` interface.

Ownership contract: the `PathResult` passed to `CachePath` belongs to your cache (dispose it when you evict or clear the entry). The instance you return from `TryGetCachedPath` stays yours too: the pathfinder copies it before returning it to its caller.

```csharp
public class CustomPathCaching : IPathCaching
{
    // Implement the interface methods...
    
    public bool TryGetCachedPath(IAgent agent, Coordinate from, Coordinate to, out PathResult pathResult)
    {
        // Your custom logic to retrieve cached paths
    }
    
    public void CachePath(IAgent agent, Coordinate from, Coordinate to, PathResult pathResult)
    {
        // Your custom logic to store paths
    }
    
    public void ClearCache()
    {
        // Your custom logic to clear the cache
    }
    
    public void Dispose()
    {
        // Cleanup resources
    }
}

// Use your custom implementation
pathfinder.EnablePathCaching(new CustomPathCaching());
```

## When to Use Path Caching

Path caching is most beneficial in scenarios where:

- The same paths are frequently requested, such as in games where multiple units follow the same routes
- Path calculations are expensive (large maps or complex environments)
- Memory usage is less critical than CPU performance

Consider disabling path caching in scenarios where:

- Paths are rarely reused
- Memory usage is more critical than CPU performance
- The environment changes frequently, invalidating cached paths

## Performance Considerations

### Benefits

- Significantly reduces CPU time for repeated path requests
- Can improve responsiveness in real-time applications

### Costs

- Increased memory usage
- Storing a path and serving a cache hit each copy the path's coordinates into a pooled array (far cheaper than a search)

## Implementation Details

The default path caching implementation (`DefaultPathCaching`):

- Uses a dictionary to store paths, with keys based on agent size, start, and destination coordinates
- Owns the paths it stores (copies made by the pathfinder) and disposes them when they are replaced or cleared
- Doesn't cache failed path results

## Best Practices

1. Enable path caching only when there's a reasonable chance of path reuse
2. Invalidate the cache when the environment changes (e.g., obstacles are added or removed)
3. Consider implementing a custom caching strategy for specific needs:
   - Time-based expiration of cached paths
   - Limited cache size (LRU or LFU replacement policy)
   - Selective caching based on path characteristics 