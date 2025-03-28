# MPath: High-Performance Pathfinding Library

MPath is a high-performance, memory-efficient A* pathfinding implementation designed for grid-based environments. The library is optimized for speed and minimal garbage collection overhead, making it suitable for real-time applications such as games.

## Overview

MPath provides a robust implementation of the A* pathfinding algorithm with several optimizations:

- **Memory Efficiency**: Uses unsafe code and array pooling to minimize GC overhead
- **Flexible Grid Representation**: Supports various ways to represent your grid data
- **Agent-Based Pathfinding**: Accounts for agent size and movement constraints
- **Customizable Behavior**: Configurable settings for diagonal movement, corner cutting, and more
- **Optimized Performance**: Designed for high-performance in real-time applications

## Documentation Sections

- [Getting Started](guides/getting-started.md) - Basic setup and usage
- [Core Concepts](guides/core-concepts.md) - Understanding the key components
- [Grid Setup Guide](guides/grid-setup.md) - Methods for creating and managing grids
- [Agent Configuration](guides/agent-configuration.md) - Working with pathfinding agents
- [Pathfinder Settings](guides/pathfinder-settings.md) - Customizing pathfinding behavior
- [Performance Considerations](guides/performance.md) - Optimizing for different scenarios
- [API Reference](api/README.md) - Detailed API documentation
