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
| `bool TryGetCachedPath(int agentSize, Coordinate from, Coordinate to, out PathResult pathResult)` | Tries to get a cached path result for the specified parameters. |
| `void CachePath(int agentSize, Coordinate from, Coordinate to, PathResult pathResult)` | Caches a path result for the specified parameters. |
| `void ClearCache()` | Clears all cached paths. |
| `void Dispose()` | Releases all resources used by the path caching implementation. |

## Remarks

- Implements the [IPathCaching](IPathCaching.md) interface.
- Uses a dictionary-based cache with a composite key of the agent size, start position, and destination, so queries of different sizes never share an entry.
- Failed path results (where `PathResult.IsSuccess` is `false`) are not cached.
- Owns every stored `PathResult`: when caching a path with the same key as an existing entry, the old entry is disposed, and `ClearCache`/`Dispose` dispose all entries.
- `Pathfinder` stores a private copy and returns a fresh copy on every cache hit, so results returned by `GetPath` are always the caller's to dispose.
- This is the default implementation used when `Pathfinder.EnablePathCaching()` is called without a custom caching parameter.

## Examples

```csharp
// The DefaultPathCaching class is used automatically when no custom implementation is provided

var pathfinder = new Pathfinder(cells, width, height);

// This uses DefaultPathCaching internally
pathfinder.EnablePathCaching();

// Find a path
using var result = pathfinder.GetPath(from, to);

// Subsequent calls with the same parameters return a fresh copy of the cached path
using var cachedResult = pathfinder.GetPath(from, to);
``` 