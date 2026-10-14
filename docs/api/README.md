# MPath API Reference

## Project Structure

MPath uses a single codebase for both Unity and .NET:
- Core source code in `src/mpath-unity-project/Packages/MPath/Source`
- .NET project links to this code via `<Compile Include=""/>` in the `.csproj` file
- Unity-specific code in `src/mpath-unity-project/Packages/MPath/Runtime`

## Classes and Interfaces

| Type | Description |
|------|-------------|
| [Pathfinder](Pathfinder.md) | Main class for performing pathfinding operations |
| [PathResult](PathResult.md) | Contains the results of pathfinding operations |
| [RangeResult](RangeResult.md) | Contains the results of a movement-range (reachability) query |
| [ReachableCell](ReachableCell.md) | A single reachable cell paired with its cheapest path cost |
| [StepwiseSearch](StepwiseSearch.md) | A tick-by-tick driver over the A* search for visualizing its progress |
| [SearchStep](SearchStep.md) | A per-tick snapshot of a stepwise search (searched area + path) |
| [SearchNode](SearchNode.md) | A single searched cell with its open/closed state and A* scores |
| [SearchState](SearchState.md) | The overall state of a stepwise search (in progress / success / failure) |
| [Cell](Cell.md) | Represents a single cell in the pathfinding grid |
| [Coordinate](Coordinate.md) | Represents a position in the grid |
| [CornerBlocking](CornerBlocking.md) | Which walls / occupied cells block a diagonal step (corner-cutting) |
| [TieBreaking](TieBreaking.md) | Which route wins among equal-cost routes (fewer diagonals / fewer turns) |
| [QueryOptions](QueryOptions.md) | Per-query overrides: occupancy rule and always-enterable cells |
| [OccupancyRule](OccupancyRule.md) | Whether occupied cells block in one query (`UseSettings` / `Block` / `Ignore`) |
| [GridSnapshotMode](GridSnapshotMode.md) | When a `Cell[,]`/holder pathfinder copies the grid (`PerQuery` / `Manual`) |
| [CellLayout](CellLayout.md) | Which index of a `Cell[]`/`ICellHolder[]` holds cell (X, Y) (`ColumnMajor` / `RowMajor`) |
| [LineOfSightMode](LineOfSightMode.md) | Controls whether unwalkable cells block `HasLineOfSight` |
| [ICellHolder](ICellHolder.md) | Interface for objects that hold cell data |
| [IPathfinderSettings](IPathfinderSettings.md) | Interface for configuring pathfinding behavior |
| [PathfinderSettings](PathfinderSettings.md) | Default implementation of IPathfinderSettings |
| [IPathCaching](IPathCaching.md) | Interface for path caching functionality |
| [DefaultPathCaching](DefaultPathCaching.md) | Default implementation of path caching |

## Unity-Specific Components

| Type | Description |
|------|-------------|
| [ScriptablePathfinderSettings](ScriptablePathfinderSettings.md) | Unity ScriptableObject implementation of pathfinding settings |
| [PathfindingExtensions](PathfindingExtensions.md) | Extension methods for Unity integration | 