# Core Concepts

This document explains the fundamental concepts behind MPath and how they work together to provide efficient pathfinding functionality.

## Grid-Based Pathfinding

MPath is designed specifically for grid-based environments where:

- The world is divided into a grid of cells (typically squares)
- Each cell has properties like walkability and movement cost
- Agents navigate from one cell to another following optimal paths

The A* algorithm used in MPath calculates the cheapest path while considering both the cost traveled so far and an estimated cost to the destination. The estimate never exceeds the real cost, so the returned path is always a cheapest one; its cost is the sum of each step's movement multiplier plus the entered cell's weight, the same cost `GetReachable` reports.

## Key Components

### 1. The Grid

The grid represents your navigable space. In MPath, a grid consists of `Cell` objects arranged in a 2D structure. Each cell contains:

- **Coordinates**: Position within the grid
- **Walkability**: Whether agents can traverse this cell
- **Occupation**: Whether the cell is currently occupied by an agent
- **Weight**: Extra cost added to every step that enters this cell (an `int` in the same units as the step costs; 0 by default)

MPath is flexible in how you represent your grid:
- As a flat array of `Cell` objects
- As a 2D matrix of `Cell` objects
- Via `ICellHolder` objects that provide cell data

### 2. Agents

An agent is any entity that needs to find a path through the grid. MPath needs only its **size**, passed to every query as `int agentSize` (the third argument, 1 by default):

- **Size**: The side of the square of cells the agent occupies (e.g., 1 for a single-cell agent, 2 for a 2x2 agent)

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
- **Step Costs**: Integer costs of one straight and one diagonal step (`StraightStepCost` = 100, `DiagonalStepCost` = 141 by default); weights, budgets and every reported cost use the same unit

### 5. Path Result

A `PathResult` represents the outcome of a pathfinding operation and contains:

- **Success Status**: Whether a path was found
- **Path Coordinates**: Sequence of coordinates forming the path
- **Resource Management**: Automatic return of arrays to the pool when disposed

## Workflow

The typical usage flow of MPath is:

1. Set up your grid with appropriate cell data
2. Know your agent's size (1 for single-cell agents, the default)
3. Create a pathfinder instance with your grid and settings
4. Request a path for your agent between two points
5. Use the resulting path in your application
6. Dispose of resources properly when done

Understanding these core concepts provides the foundation for effectively using MPath in your applications. 