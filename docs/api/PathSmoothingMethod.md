# PathSmoothingMethod Enum

**Namespace:** `Migs.MPath.Core.Data`

Defines the available methods for smoothing paths after they are calculated by the A* algorithm.

## Values

| Value | Description |
|-------|-------------|
| `None` | No smoothing is applied. Returns the raw A* path with all waypoints. |
| `Simple` | Removes redundant waypoints based on direction changes. Very fast with good results for most cases. |
| `StringPulling` | Creates optimal direct paths using line-of-sight checks. Most computationally expensive. |

## Usage Example

```csharp
var settings = new PathfinderSettings
{
    PathSmoothingMethod = PathSmoothingMethod.StringPulling
};
```

## Remarks

- `None` is the fastest option but may result in zigzag paths with many waypoints
- `Simple` provides a good balance of performance and path quality, removing redundant waypoints based on direction changes
- `StringPulling` produces the most optimal paths by using line-of-sight checks, but is more computationally expensive and might not fit all scenarios
- All smoothing methods maintain the same memory efficiency as they reuse the original path array
- Choose the smoothing method based on your specific needs:
  - Use `None` when you need to take into account every waypoint in the way
  - Use `Simple` for most game scenarios - good balance of performance and path quality
  - Use `StringPulling` when you need the most direct and natural-looking paths and can afford the slight performance cost 