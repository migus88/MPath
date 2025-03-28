# MPath: High-Performance Pathfinding Library

MPath is a high-performance, memory-efficient A* pathfinding implementation designed for grid-based environments. The library is optimized for speed and minimal garbage collection overhead, making it suitable for real-time applications such as games.

## Overview

MPath provides a robust implementation of the A* pathfinding algorithm with several optimizations:

- **Memory Efficiency**: Uses unsafe code and array pooling to minimize GC overhead
- **Flexible Grid Representation**: Supports various ways to represent your grid data
- **Agent-Based Pathfinding**: Accounts for agent size and movement constraints
- **Customizable Behavior**: Configurable settings for diagonal movement, corner cutting, and more
- **Path Smoothing**: Optional string pulling algorithm for more natural, direct paths
- **Optimized Performance**: Designed for high-performance in real-time applications

## Documentation Sections

- [Getting Started](guides/getting-started.md) - Basic setup and usage
- [Core Concepts](guides/core-concepts.md) - Understanding the key components
- [Grid Setup Guide](guides/grid-setup.md) - Methods for creating and managing grids
- [Agent Configuration](guides/agent-configuration.md) - Working with pathfinding agents
- [Pathfinder Settings](guides/pathfinder-settings.md) - Customizing pathfinding behavior
- [Advanced Usage](guides/advanced-usage.md) - More complex pathfinding techniques
- [Performance Considerations](guides/performance.md) - Optimizing for different scenarios
- [API Reference](api/README.md) - Detailed API documentation
- [Usage Examples](examples/README.md) - Code examples for common scenarios

## Quick Links

### Basic Usage
- [Creating a Simple Grid](guides/getting-started.md#creating-a-simple-grid)
- [Defining an Agent](guides/getting-started.md#defining-an-agent)
- [Creating a Pathfinder](guides/getting-started.md#creating-a-pathfinder)
- [Finding a Path](guides/getting-started.md#finding-a-path)

### Grid Options
- [Cell Array](guides/grid-setup.md#option-1-cell-array)
- [Cell Matrix](guides/grid-setup.md#option-2-cell-matrix)
- [Cell Holder Array](guides/grid-setup.md#option-3-cell-holder-array)
- [Cell Holder Matrix](guides/grid-setup.md#option-4-cell-holder-matrix)
- [Dynamic Grid Updates](guides/grid-setup.md#dynamic-grid-updates)

### Agent Options
- [Basic Agents](guides/agent-configuration.md#basic-agent-implementation)
- [Multi-Cell Agents](guides/agent-configuration.md#agent-size-and-multi-cell-agents)
- [Dynamic Agents](guides/agent-configuration.md#dynamic-agent-properties)
- [Unity MonoBehaviour Agents](guides/agent-configuration.md#unity-monobehaviour-agent-example)

### Settings Options
- [Movement Settings](guides/pathfinder-settings.md#movement-pattern-settings)
- [Cell Handling Settings](guides/pathfinder-settings.md#cell-handling-settings)
- [Cost Calculation Settings](guides/pathfinder-settings.md#cost-calculation-settings)
- [Path Smoothing](guides/pathfinder-settings.md#pathsmoothingmethod)
- [Recommended Presets](guides/pathfinder-settings.md#recommended-settings-for-common-scenarios)

### Performance
- [Memory Optimization](guides/performance.md#memory-optimization)
- [Computational Optimization](guides/performance.md#computational-optimization)
- [Path Caching](guides/performance.md#path-caching)
- [Distributed Pathfinding](guides/performance.md#distributed-pathfinding)