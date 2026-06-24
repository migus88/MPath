# RangeResult Class

**Namespace:** `Migs.MPath.Core.Data`

Represents the result of a movement-range (reachability) query produced by `Pathfinder.GetReachable`. Contains every cell whose cheapest path cost from the origin is within the supplied budget. Implements `IDisposable`.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsSuccess` | `bool` | Whether the query produced at least one reachable cell. |
| `Length` | `int` | The number of reachable cells. |
| `Cells` | `IEnumerable<ReachableCell>` | The reachable cells, each paired with its cheapest path cost. |

## Methods

| Method | Description |
|--------|-------------|
| `static RangeResult Create(ReachableCell[] cells, int length)` | Creates a successful range result backed by the specified array. |
| `static RangeResult Empty()` | Returns a shared empty result (no reachable cells). |
| `ReachableCell Get(int index)` | Gets the reachable cell at the specified index. |
| `bool Contains(Coordinate coordinate)` | Determines whether the coordinate is reachable (linear scan). |
| `bool TryGetCost(Coordinate coordinate, out float cost)` | Gets the cheapest path cost to the coordinate, if reachable (linear scan). |
| `void Dispose()` | Releases resources used by the RangeResult. |

## Remarks

- **IMPORTANT:** Always dispose of `RangeResult` objects after use to return the pooled backing array.
- Use the `using` statement for automatic disposal.
- The origin cell is always included with a cost of `0` when the budget is non-negative.
- `Contains` and `TryGetCost` perform a linear scan over the result. For repeated lookups, build your own `HashSet`/`Dictionary` from `Cells` once.
- If you need to store reachable cells for later use, copy them before disposing.

## Example

```csharp
// Find every cell an agent can reach within 5 movement points
using (var range = pathfinder.GetReachable(agent, new Coordinate(4, 4), 5f))
{
    Console.WriteLine($"{range.Length} cells reachable");

    foreach (var cell in range.Cells)
    {
        Console.WriteLine($"{cell.Coordinate} costs {cell.Cost}");
    }

    if (range.TryGetCost(new Coordinate(6, 4), out var cost))
    {
        Console.WriteLine($"That tile is reachable for {cost}");
    }
}
```
