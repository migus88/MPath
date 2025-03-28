# PathResult Class

**Namespace:** `Migs.MPath.Core.Data`

Represents the result of a pathfinding operation. Implements `IDisposable`.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsSuccess` | `bool` | Whether the pathfinding operation was successful. |
| `Length` | `int` | The number of coordinates in the path. |
| `Path` | `IEnumerable<Coordinate>` | The coordinates forming the path from start to destination. |

## Methods

| Method | Description |
|--------|-------------|
| `static PathResult Success(Coordinate[] path, int length)` | Creates a successful path result. |
| `static PathResult Failure()` | Creates a failed path result. |
| `Coordinate Get(int index)` | Gets the coordinate at the specified index. |
| `void Dispose()` | Releases resources used by the PathResult. |

## Remarks

- **IMPORTANT:** Always dispose of `PathResult` objects after use to release memory resources.
- Use the `using` statement for automatic disposal.
- The object uses array pooling internally to minimize garbage collection overhead.
- If you need to store path results for later use, copy the coordinates before disposing.

## Example

```csharp
// Get and use a path
using (var pathResult = pathfinder.GetPath(agent, startPos, endPos))
{
    if (pathResult.IsSuccess)
    {
        Console.WriteLine($"Path found with {pathResult.Length} steps!");
        
        foreach (var coordinate in pathResult.Path)
        {
            Console.WriteLine($"Step: {coordinate}");
        }
    }
}
``` 