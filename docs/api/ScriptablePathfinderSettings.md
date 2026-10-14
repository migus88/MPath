# ScriptablePathfinderSettings Class

**Namespace:** `Migs.MPath.Settings`

A Unity ScriptableObject implementation of the `IPathfinderSettings` interface. This class allows pathfinding settings to be configured in the Unity Inspector and saved as assets in your project.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | `true` | Whether agents can move diagonally. |
| `IsCalculatingOccupiedCells` | `bool` | `true` | Whether occupied cells are considered as blocked. |
| `IsMovementBetweenCornersEnabled` | `bool` | `false` | Legacy shorthand: `true` is `CornerBlocking.None`, `false` is `BetweenBlockedCells`. Ignored while `IsCornerBlockingOverridden` is on. |
| `IsCornerBlockingOverridden` | `bool` | `false` | Use `CornerBlockingOverride` instead of `IsMovementBetweenCornersEnabled`. |
| `CornerBlockingOverride` | [`CornerBlocking`](CornerBlocking.md) | `BetweenBlockedCells` | The corner rule while the override is on. |
| `CornerBlocking` | [`CornerBlocking`](CornerBlocking.md) | (computed) | The effective rule. Setting it turns the override on. |
| `IsCellWeightEnabled` | `bool` | `true` | Whether cell weight calculation is enabled. |
| `StraightMovementMultiplier` | `float` | `1.0f` | The cost multiplier for horizontal/vertical movement. |
| `DiagonalMovementMultiplier` | `float` | `1.41f` | The cost multiplier for diagonal movement. |
| `InitialBufferSizeSerialized` | `int` | `-1` | The serialized value for the initial buffer size. |
| `InitialBufferSize` | `int?` | `null` | The nullable initial buffer size (computed property). |

## Creation

To create a new ScriptablePathfinderSettings asset in Unity:
1. Right-click in the Project window
2. Select Create → MPath → Pathfinder Settings

## Example

```csharp
// Reference in a MonoBehaviour
public class PathfindingController : MonoBehaviour
{
    [SerializeField] private ScriptablePathfinderSettings _settings;
    private Pathfinder _pathfinder;
    
    private void Start()
    {
        _pathfinder = new Pathfinder(cells, width, height, _settings);
    }
}
```

## Remarks

- ScriptablePathfinderSettings inherits from Unity's ScriptableObject, making it easy to create and manage pathfinding configurations.
- The `CreateAssetMenu` attribute allows creating assets directly in the Unity Editor.
- The settings are serialized, allowing them to be edited in the Inspector and saved with the project.
- The nullable `InitialBufferSize` property is handled through a serialized integer field with -1 representing `null`.
- You can create multiple settings assets for different scenarios (e.g., terrain types, game modes, etc.).
- These settings can be referenced from multiple components without duplication. 