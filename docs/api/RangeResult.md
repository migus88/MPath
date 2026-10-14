# RangeResult Class

**Namespace:** `Migs.MPath.Core.Data`

Represents the result of a movement-range (reachability) query produced by `Pathfinder.GetReachable`. Contains every cell whose cheapest path cost from the origin is within the supplied budget. Implements `IDisposable`.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsSuccess` | `bool` | Whether the query produced at least one reachable cell. |
| `Length` | `int` | The number of reachable cells. |
| `Cells` | `IEnumerable<ReachableCell>` | The reachable cells, each paired with its cheapest path cost. Allocates an iterator per enumeration: prefer `foreach (var c in range)` or `AsSpan()`. |
| `this[int index]` | `ReachableCell` | The reachable cell at the index. Throws `ArgumentOutOfRangeException` when out of range, `ObjectDisposedException` after dispose. |

## Methods

| Method | Description |
|--------|-------------|
| `static RangeResult Create(ReachableCell[] cells, int length)` | Creates a successful range result backed by the specified array. |
| `static RangeResult Empty()` | Returns a shared empty result (no reachable cells). |
| `ReachableCell Get(int index)` | Gets the reachable cell at the specified index. |
| `ReadOnlySpan<ReachableCell> AsSpan()` | The reachable cells as a span over the pooled array (no copy), in the order the search settled them, origin first. Valid until the result is disposed. |
| `Enumerator GetEnumerator()` | A struct enumerator, so `foreach (var c in range)` allocates nothing. |
| `bool Contains(Coordinate coordinate)` | Determines whether the coordinate is reachable (O(1)). |
| `bool TryGetCost(Coordinate coordinate, out int cost)` | Gets the cheapest path cost to the coordinate, if reachable (O(1)). |
| `bool TryGetCell(Coordinate coordinate, out ReachableCell cell)` | Gets the reachable cell (cost and step counts) at the coordinate, if reachable (O(1)). |
| `int CopyCostsTo(Span<int> costByCellIndex, int unreached = -1)` | Fills a dense per-cell cost field: each reachable cell's cost at its layout index (`Pathfinder.GetIndex`; column-major `X * fieldHeight + Y` for an unordered `Cell[]`), `unreached` in every other entry. Returns `Length`. Throws `ArgumentException` when the span is shorter than the grid, and `InvalidOperationException` for a non-empty result built with `Create` (it knows no grid). |
| `void Dispose()` | Releases resources used by the RangeResult. |

## Remarks

- **IMPORTANT:** Always dispose of `RangeResult` objects after use to return the pooled backing array.
- Use the `using` statement for automatic disposal.
- The origin cell is always included with a cost of `0` when the budget is non-negative.
- `Contains`, `TryGetCost` and `TryGetCell` are O(1). The first lookup builds an index over the reached cells in one pass (an `int` array rented from the pool, sized to their bounding box, at most the grid); later lookups allocate nothing. The index belongs to the result: it stays valid after later queries and after the pathfinder is disposed, and goes back to the pool on `Dispose`. A result built with `Create` whose cells are spread far apart is scanned linearly instead.
- Spans from `AsSpan()` and `foreach` enumerators read the pooled array: don't use them after disposing the result.
- If you need to store reachable cells for later use, copy them before disposing.

## Example

```csharp
// Find every cell an agent can reach within 5 movement points
using (var range = pathfinder.GetReachable(agent, new Coordinate(4, 4), 500))
{
    Console.WriteLine($"{range.Length} cells reachable");

    foreach (var cell in range)
    {
        Console.WriteLine($"{cell.Coordinate} costs {cell.Cost}");
    }

    if (range.TryGetCost(new Coordinate(6, 4), out var cost))
    {
        Console.WriteLine($"That tile is reachable for {cost}");
    }
}
```
