# Core Concepts

This document explains the fundamental concepts behind MPath and how they work together to provide efficient pathfinding functionality.

## Grid-Based Pathfinding

MPath is designed specifically for grid-based environments where:

- The world is divided into a grid of cells (typically squares)
- Each cell has properties like walkability and movement cost
- Agents navigate from one cell to another following optimal paths

The A* algorithm used in MPath calculates the shortest path while considering both the distance traveled so far and an estimated distance to the destination.

## Key Components

### 1. The Grid

The grid represents your navigable space. In MPath, a grid consists of `Cell` objects arranged in a 2D structure. Each cell contains:

- **Coordinates**: Position within the grid
- **Walkability**: Whether agents can traverse this cell
- **Occupation**: Whether the cell is currently occupied by an agent
- **Weight**: Movement cost multiplier for traversing this cell

MPath is flexible in how you represent your grid:
- As a flat array of `Cell` objects
- As a 2D matrix of `Cell` objects
- Via `ICellHolder` objects that provide cell data

### 2. Agents

An agent is any entity that needs to find a path through the grid. In MPath, agents implement the `IAgent` interface, which defines:

- **Size**: The number of cells the agent occupies (e.g., 1 for a single-cell agent, 2 for a 2x2 agent)

The agent's size affects which paths are considered valid, as the pathfinder ensures all cells along the path can accommodate the agent.

### 3. Pathfinder

The `Pathfinder` class is the core component that performs the actual pathfinding calculations. It:

- Takes grid data and agent information as input
- Runs the A* algorithm to find the optimal path
- Returns a `PathResult` containing the calculated path or failure information

### 4. Settings

`PathfinderSettings` control various aspects of the pathfinding behavior:

- **Diagonal Movement**: Whether agents can move diagonally
- **Occupied Cell Handling**: Whether to treat occupied cells as blocked
- **Corner Movement**: Whether agents can move between corners
- **Cell Weight**: Whether to consider additional cell weight in path calculations
- **Movement Cost Multipliers**: Cost factors for straight and diagonal movements

### 5. Path Result

A `PathResult` represents the outcome of a pathfinding operation and contains:

- **Success Status**: Whether a path was found
- **Path Coordinates**: Sequence of coordinates forming the path
- **Resource Management**: Automatic return of arrays to the pool when disposed

## Workflow

The typical usage flow of MPath is:

1. Set up your grid with appropriate cell data
2. Configure an agent with the required properties
3. Create a pathfinder instance with your grid and settings
4. Request a path for your agent between two points
5. Use the resulting path in your application
6. Dispose of resources properly when done

Understanding these core concepts provides the foundation for effectively using MPath in your applications. 