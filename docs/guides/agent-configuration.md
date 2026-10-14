# Agent Configuration

This guide explains how to path agents of different sizes with MPath.

## What is an Agent?

In MPath, an agent is any entity that needs to find a path through the grid. The only property that affects pathfinding is its **size**: the side of the square of cells it occupies, anchored at its top-left cell. You pass it to every query as `int agentSize`. MPath has no agent type to implement: keep your own unit types and pass their size.

## Passing the Size

`agentSize` is always the **third** argument. It defaults to `1`, so single-cell units pass nothing:

```csharp
using var path   = pathfinder.GetPath(start, end);                // 1x1 agent
using var truck  = pathfinder.GetPath(start, end, 2);             // 2x2 agent
using var range  = pathfinder.GetReachable(start, 500, 2);
using var search = pathfinder.BeginStepwiseSearch(start, end, 2);

// With per-query options, the options follow the size
using var attack = pathfinder.GetPath(start, target, 2,
    new QueryOptions(isDestinationAlwaysEnterable: true));
```

- `agentSize` must be at least `1` (`ArgumentOutOfRangeException` otherwise). A bare `default` in the third position is a size of 0 and throws; there is no overload with options in that position.
- The path cache is keyed by `(agentSize, from, to)`, so a size-1 and a size-2 query of the same pair never share an entry.

When calculating paths for multi-cell agents, MPath ensures that:

1. All cells occupied by the agent are walkable
2. The agent can fit through corridors and openings
3. The path considers the agent's full size when determining valid moves

## Agent Size Illustration

Here's how different agent sizes appear on a grid:

```
Size 1 (1x1):    Size 2 (2x2):    Size 3 (3x3):
┌───┐            ┌───┬───┐        ┌───┬───┬───┐
│ A │            │ A │ A │        │ A │ A │ A │
└───┘            ├───┼───┤        ├───┼───┼───┤
                 │ A │ A │        │ A │ A │ A │
                 └───┴───┘        ├───┼───┼───┤
                                  │ A │ A │ A │
                                  └───┴───┴───┘
```

## Sizes That Change

The size is read per query, so an agent whose size changes during play just passes its current size on the next query:

```csharp
using var path = pathfinder.GetPath(start, end, unit.Size);

unit.Grow();
using var newPath = pathfinder.GetPath(start, end, unit.Size); // recalculated for the new size
```

## Unity Example

```csharp
using UnityEngine;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

public class Character : MonoBehaviour
{
    [SerializeField, Min(1)] private int _size = 1;

    public PathResult FindPath(Pathfinder pathfinder, Coordinate from, Coordinate destination)
    {
        return pathfinder.GetPath(from, destination, _size); // dispose the result when done
    }
}
```

## Example: Vehicles of Different Sizes

```csharp
public enum VehicleType { Car, Truck, Tank }

public static int GetSize(VehicleType type) => type switch
{
    VehicleType.Truck => 2,
    VehicleType.Tank => 3,
    _ => 1
};

// Each vehicle gets a path appropriate for its size
using var carPath   = pathfinder.GetPath(start, end, GetSize(VehicleType.Car));
using var truckPath = pathfinder.GetPath(start, end, GetSize(VehicleType.Truck));
using var tankPath  = pathfinder.GetPath(start, end, GetSize(VehicleType.Tank));
```

## Important Considerations

### Path Availability

Larger agents may not be able to find paths through narrow corridors:

```
Map with walls (#):    Size 1 can pass:    Size 2 cannot pass:
┌───┬───┬───┬───┐      ┌───┬───┬───┬───┐   ┌───┬───┬───┬───┐
│   │   │   │   │      │   │   │   │   │   │   │   │   │   │
├───┼───┼───┼───┤      ├───┼───┼───┼───┤   ├───┼───┼───┼───┤
│   │ # │   │   │  →   │   │ # │ A │   │   │   │ # │A A│   │
├───┼───┼───┼───┤      ├───┼───┼───┼───┤   ├───┼───┼───┼───┤
│   │ # │   │   │      │   │ # │   │   │   │   │ # │A A│   │
└───┴───┴───┴───┘      └───┴───┴───┴───┘   └───┴───┴───┴───┘
                       (can find path)      (path impossible)
```

Always check for `PathResult.IsSuccess` before using a path.

### Performance Impact

Larger agents generally require more computation for pathfinding because:

1. More neighbor cells need to be checked for walkability
2. Fewer paths are generally available
3. More complex validation is needed for each move