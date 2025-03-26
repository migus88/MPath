# Agent Configuration

This guide explains how to configure and use agents with MPath for pathfinding.

## What is an Agent?

In MPath, an agent is any entity that needs to find a path through the grid. Agents are defined by the `IAgent` interface, which specifies the agent's size and any other properties that affect pathfinding.

## Basic Agent Implementation

The simplest agent implementation only needs to specify its size:

```csharp
public class SimpleAgent : IAgent
{
    public int Size => 1; // Agent occupies a single cell
}
```

This creates an agent that occupies a single cell on the grid.

## Agent Size and Multi-Cell Agents

The `Size` property determines how many cells the agent occupies in both width and height. For example:

```csharp
public class LargeAgent : IAgent
{
    public int Size => 2; // Agent occupies a 2x2 square of cells
}

public class MassiveAgent : IAgent
{
    public int Size => 3; // Agent occupies a 3x3 square of cells
}
```

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

## Dynamic Agent Properties

You can create agents with dynamic properties that change during gameplay:

```csharp
public class DynamicAgent : IAgent
{
    private int _size = 1;
    
    public int Size => _size;
    
    public void Grow()
    {
        _size = 2;
    }
    
    public void Shrink()
    {
        _size = 1;
    }
}
```

When the agent's properties change, you need to recalculate its path:

```csharp
var agent = new DynamicAgent();
using var path = pathfinder.GetPath(agent, start, end);

// Later in the game
agent.Grow();
// Recalculate path with the new agent size
using var newPath = pathfinder.GetPath(agent, start, end);
```

## Advanced: Creating Context-Aware Agents

You can create more sophisticated agents that adapt to the game context:

```csharp
public class ContextAwareAgent : IAgent
{
    private readonly GameState _gameState;
    
    public ContextAwareAgent(GameState gameState)
    {
        _gameState = gameState;
    }
    
    // Size changes based on the agent's game state
    public int Size => _gameState.IsVehicleMode ? 2 : 1;
}
```

## Unity MonoBehaviour Agent Example

For Unity games, you can implement the `IAgent` interface on your MonoBehaviours. Here's a simple example:

```csharp
using UnityEngine;
using Migs.MPath.Core.Interfaces;

public class SimpleCharacter : MonoBehaviour, IAgent
{
    [SerializeField] private int _agentSize = 1;
    
    // IAgent implementation
    public int Size => _agentSize;
    
    // You can modify the size at runtime if needed
    public void SetSize(int newSize)
    {
        if (newSize < 1) newSize = 1;
        _agentSize = newSize;
        
        // You might also want to update visuals or other components
        transform.localScale = Vector3.one * _agentSize;
    }
}
```

You can also use scriptable objects for certain types of agents:

```csharp
using UnityEngine;
using Migs.MPath.Core.Interfaces;

[CreateAssetMenu(fileName = "NewAgentType", menuName = "Game/Agent Type")]
public class AgentType : ScriptableObject, IAgent
{
    [SerializeField] private int _size = 1;
    [SerializeField] private string _displayName;
    [SerializeField] private Sprite _icon;
    
    public int Size => _size;
    public string DisplayName => _displayName;
    public Sprite Icon => _icon;
}

// Usage in a character class
public class Character : MonoBehaviour
{
    [SerializeField] private AgentType _agentType;
    
    public IAgent Agent => _agentType;
    
    public void FindPath(Coordinate destination)
    {
        using var path = _pathfinder.GetPath(_agentType, CurrentPosition, destination);
        // Process path...
    }
}
```

## Practical Examples

### Example 1: Simple Character in a Game

```csharp
public class PlayerCharacter : IAgent
{
    // Most characters only occupy one cell
    public int Size => 1;
}

// Using the agent
var player = new PlayerCharacter();
var path = pathfinder.GetPath(player, playerPosition, targetPosition);

// Process the path
if (path.IsSuccess)
{
    // Move the player along the path
    foreach (var coordinate in path.Path)
    {
        // Move logic
    }
}
```

### Example 2: Vehicle with Different Sizes

```csharp
public enum VehicleType { Car, Truck, Tank }

public class Vehicle : IAgent
{
    private VehicleType _type;
    
    public Vehicle(VehicleType type)
    {
        _type = type;
    }
    
    public int Size => _type switch
    {
        VehicleType.Car => 1,
        VehicleType.Truck => 2,
        VehicleType.Tank => 3,
        _ => 1
    };
}

// Using different vehicles
var car = new Vehicle(VehicleType.Car);
var truck = new Vehicle(VehicleType.Truck);
var tank = new Vehicle(VehicleType.Tank);

// Each vehicle will get a path appropriate for its size
var carPath = pathfinder.GetPath(car, start, end);
var truckPath = pathfinder.GetPath(truck, start, end);
var tankPath = pathfinder.GetPath(tank, start, end);
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