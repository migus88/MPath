# ScriptablePathfinderSettings Class

**Namespace:** `Migs.MPath.Settings`

A Unity ScriptableObject implementation of the `IPathfinderSettings` interface. This class allows pathfinding settings to be configured in the Unity Inspector and saved as assets in your project.

## Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `IsDiagonalMovementEnabled` | `bool` | `true` | Whether agents can move diagonally. |
| `AreOccupiedCellsBlocking` | `bool` | `true` | Whether occupied cells block: entry, agent clearance, the corner rule and line of sight. |
| `CornerBlocking` | [`CornerBlocking`](CornerBlocking.md) | `BetweenBlockedCells` | Which flanking cells (walls, occupied cells) block a diagonal step. |
| `IsCellWeightEnabled` | `bool` | `true` | Whether cell weight calculation is enabled. |
| `StraightStepCost` | `int` | `100` | The cost of a horizontal/vertical step (at least 1). |
| `DiagonalStepCost` | `int` | `141` | The cost of a diagonal step (at least 1). |
| `TieBreaking` | [`TieBreaking`](TieBreaking.md) | `None` | Which route wins among equal-cost routes. |
| `GridSnapshotMode` | [`GridSnapshotMode`](GridSnapshotMode.md) | `PerQuery` | When a `PathCell[,]` or cell-holder pathfinder copies the grid: every query, or once and after `Refresh()` (`Manual`). |
| `CellLayout` | [`CellLayout`](CellLayout.md) | `ColumnMajor` | Which index of a `PathCell[]` or `ICellHolder[]` holds cell (X, Y): `ColumnMajor` (`X * height + Y`) or `RowMajor` (`Y * width + X`). |

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
- You can create multiple settings assets for different scenarios (e.g., terrain types, game modes, etc.).
- These settings can be referenced from multiple components without duplication. 