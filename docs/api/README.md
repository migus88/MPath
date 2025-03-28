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
| [Cell](Cell.md) | Represents a single cell in the pathfinding grid |
| [Coordinate](Coordinate.md) | Represents a position in the grid |
| [IAgent](IAgent.md) | Interface for entities that need pathfinding |
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