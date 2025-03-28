# DefaultPathCaching Class

**Namespace:** `Migs.MPath.Core.Caching`

Default implementation of the [IPathCaching](IPathCaching.md) interface that stores calculated paths in memory.

## Constructors

| Constructor | Description |
|-------------|-------------|
| `DefaultPathCaching()` | Initializes a new instance of the DefaultPathCaching class. |

## Methods

| Method | Description |
|--------|-------------|
| `bool TryGetCachedPath(IAgent agent, Coordinate from, Coordinate to, out PathResult pathResult)` | Tries to get a cached path result for the specified parameters. |
| `void CachePath(IAgent agent, Coordinate from, Coordinate to, PathResult pathResult)` | Caches a path result for the specified parameters. |
| `void ClearCache()` | Clears all cached paths. |
| `void Dispose()` | Releases all resources used by the path caching implementation. |

## Remarks

- Implements the [IPathCaching](IPathCaching.md) interface.
- Uses a dictionary-based cache with a composite key based on agent size, start position, and destination.
- Failed path results (where `PathResult.IsSuccess` is `false`) are not cached.
- When caching a path with the same key as an existing entry, the old entry is properly disposed.
- This is the default implementation used when `Pathfinder.EnablePathCaching()` is called without a custom caching parameter.

## Examples

```csharp
// The DefaultPathCaching class is used automatically when no custom implementation is provided

var pathfinder = new Pathfinder(cells, width, height);

// This uses DefaultPathCaching internally
pathfinder.EnablePathCaching();

// Find a path
var result = pathfinder.GetPath(agent, from, to);

// Subsequent calls with the same parameters will use the cached result
var cachedResult = pathfinder.GetPath(agent, from, to);
``` 