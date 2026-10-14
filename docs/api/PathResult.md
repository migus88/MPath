# PathResult Class

**Namespace:** `Migs.MPath.Core.Data`

Represents the result of a pathfinding operation. Implements `IDisposable`.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsSuccess` | `bool` | Whether the pathfinding operation was successful. |
| `Length` | `int` | The number of coordinates in the path. |
| `Steps` | `int` | The number of grid steps of the route the search found (origin excluded). Equals `Length` unless path smoothing removed waypoints; `0` for a failure. |
| `DiagonalSteps` | `int` | How many of `Steps` are diagonal (before smoothing). |
| `Path` | `PathResult.PathEnumerable` | The coordinates forming the path from start to destination, as a `readonly struct` implementing `IEnumerable<Coordinate>`. `foreach (var c in result.Path)` uses the struct `Enumerator` and allocates nothing, like `foreach (var c in result)`; used as an `IEnumerable<Coordinate>` (LINQ, a typed variable) it is boxed. Enumerating a disposed result throws `ObjectDisposedException`. |
| `this[int index]` | `Coordinate` | The coordinate at the index. Throws `ArgumentOutOfRangeException` when out of range (any index of a failed result), `ObjectDisposedException` after dispose. |

## Methods

| Method | Description |
|--------|-------------|
| `static PathResult Success(Coordinate[] path, int length, int steps, int diagonalSteps)` | Creates a successful path result with the step counts of the route it came from (useful for custom caches and test doubles). Throws `ArgumentNullException` for a null path, `ArgumentOutOfRangeException` for a `length` outside the array, negative counts or `diagonalSteps > steps`. |
| `static PathResult Failure()` | Creates a failed path result. |
| `Coordinate Get(int index)` | Gets the coordinate at the specified index. |
| `ReadOnlySpan<Coordinate> AsSpan()` | The path as a span over the pooled array (no copy); empty for a failure. Valid until the result is disposed. |
| `Enumerator GetEnumerator()` | A struct enumerator (implements `IEnumerator<Coordinate>`), so `foreach (var c in result)` allocates nothing. |
| `void Dispose()` | Releases resources used by the PathResult. |

## Remarks

- **IMPORTANT:** Always dispose of `PathResult` objects after use to release memory resources.
- Use the `using` statement for automatic disposal.
- Every result returned by `Pathfinder.GetPath` is owned by the caller, including cache hits (they are fresh copies), so disposing it never affects the path cache.
- The object uses array pooling internally to minimize garbage collection overhead.
- If you need to store path results for later use, copy the coordinates before disposing (for example `result.AsSpan().ToArray()`).
- Spans from `AsSpan()` and `foreach` enumerators read the pooled array: don't use them after disposing the result.
- `Steps` and `DiagonalSteps` are exact integers: read movement points from them instead of dividing a cost. With a [`TieBreaking`](TieBreaking.md) the route is the cheapest one with the fewest diagonals (or turns), chosen by a fixed rule.

## Example

```csharp
// Get and use a path
using (var pathResult = pathfinder.GetPath(startPos, endPos))
{
    if (pathResult.IsSuccess)
    {
        Console.WriteLine($"Path found with {pathResult.Length} steps!");
        
        foreach (var coordinate in pathResult)
        {
            Console.WriteLine($"Step: {coordinate}");
        }
    }
}
``` 