# PathfindingExtensions Class

**Namespace:** `Migs.MPath`

Extension methods for converting between MPath's `Coordinate` struct and Unity's `Vector2Int` struct.

## Methods

| Method | Description |
|--------|-------------|
| `Vector2Int ToVector2Int(this Coordinate coordinate)` | Converts a MPath `Coordinate` to a Unity `Vector2Int`. |
| `Coordinate ToCoordinate(this Vector2Int vector2Int)` | Converts a Unity `Vector2Int` to a MPath `Coordinate`. |

## Example

```csharp
// Convert from Coordinate to Vector2Int
Coordinate position = new Coordinate(5, 10);
Vector2Int unityPosition = position.ToVector2Int();

// Convert from Vector2Int to Coordinate
Vector2Int selectedPos = new Vector2Int(3, 4);
Coordinate pathCoordinate = selectedPos.ToCoordinate();
```

## Remarks

- These extension methods simplify the integration of MPath with Unity's coordinate system.
- No data is lost during the conversion as both types use integer coordinates.
- These extensions are particularly useful when working with Unity's Tilemap system or grid-based components.
- The extensions allow for seamless integration with Unity's built-in Vector mathematics and other Vector2Int-based APIs. 