# Unity Integration Guide

This guide demonstrates how to integrate MPath into your Unity project for efficient pathfinding.

## Basic Implementation

Below is a comprehensive example showing how to implement MPath in a Unity project:

```csharp
// Define a cell holder for Unity objects
public class GridCell : MonoBehaviour, ICellHolder
{
    public Cell CellData { get; private set; }
    
    [SerializeField] private bool _isWalkable = true;
    [SerializeField] private Vector2Int _position;
    [SerializeField] private float _weight = 1.0f;
    
    private void Awake()
    {
        CellData = new Cell
        {
            Coordinate = new Coordinate(_position.x, _position.y),
            IsWalkable = _isWalkable,
            Weight = _weight
        };
    }
}

// Define a Unity agent
public class UnitAgent : MonoBehaviour, IAgent
{
    public Coordinate Coordinate { get; set; }
    public int Size => 1; // Single cell agent
    
    // Movement logic using the path
    public IEnumerator FollowPath(PathResult result, float speed)
    {
        foreach (var coordinate in result.Path)
        {
            var targetPosition = new Vector3(coordinate.X, 0, coordinate.Y);
            
            while (Vector3.Distance(transform.position, targetPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, 
                    targetPosition,
                    speed * Time.deltaTime
                );
                yield return null;
            }
            
            Coordinate = coordinate;
        }
    }
}

// In your game controller
[SerializeField] private ScriptablePathfinderSettings _settings;
[SerializeField] private GridCell[] _gridCells;
[SerializeField] private Vector2Int _gridSize;

private Pathfinder _pathfinder;

private void Start()
{
    // Initialize the pathfinder with the grid cells
    _pathfinder = new Pathfinder(_gridCells, _gridSize.x, _gridSize.y, _settings);
}

// Find a path for an agent
public PathResult FindPath(UnitAgent agent, Coordinate destination)
{
    return _pathfinder.GetPath(agent, agent.Coordinate, destination);
}

private void OnDestroy()
{
    // Dispose the pathfinder when no longer needed
    _pathfinder?.Dispose();
}
```

## Implementation Details

### Grid Cell Component

The `GridCell` component implements the `ICellHolder` interface and manages the cell data for each position in your grid. You can attach this component to GameObjects that represent cells in your grid.

### Agent Implementation

The `UnitAgent` component implements the `IAgent` interface and handles movement logic. The `FollowPath` coroutine demonstrates how to move the agent along the calculated path.

### Pathfinding Controller

In your game controller:

1. Reference your grid cells, grid size, and pathfinder settings
2. Initialize the pathfinder in `Start()`
3. Provide a method to find paths for agents
4. Properly dispose of the pathfinder when it's no longer needed

## Best Practices

1. Create pathfinder settings as a ScriptableObject for easy configuration
2. Reuse the pathfinder instance for all path calculations
3. Always dispose of PathResult objects after use
4. Consider using object pooling for dynamic agents and cells

For more advanced Unity integration techniques, see the examples in the Unity project. 