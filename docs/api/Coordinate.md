# Coordinate Struct

**Namespace:** `Migs.MPath.Core.Data`

Represents a position in the 2D grid used for pathfinding. This struct provides a simple, immutable representation of an X,Y coordinate pair with optimized equality comparison operations.

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `X` | `int` | The X-coordinate (horizontal position) in the grid. |
| `Y` | `int` | The Y-coordinate (vertical position) in the grid. |
| `IsInitialized` | `bool` | Indicates whether this coordinate has been properly initialized. |

## Constructors

| Constructor | Description |
|-------------|-------------|
| `Coordinate(int x, int y)` | Creates a new coordinate with the specified X and Y values. |

## Methods

| Method | Description |
|--------|-------------|
| `void Reset()` | Resets the coordinate to (0,0) and marks it as uninitialized. |
| `bool Equals(Coordinate other)` | Determines whether the specified coordinate is equal to the current coordinate. |
| `bool Equals(object obj)` | Determines whether the specified object is equal to the current coordinate. |
| `int GetHashCode()` | Returns a hash code for this coordinate. |
| `string ToString()` | Returns a string representation in the format "X:Y". |

## Operators

| Operator | Description |
|----------|-------------|
| `==`, `!=` | Equality and inequality comparison operators. |
| `implicit operator (int x, int y)` | Converts a Coordinate to a tuple. |
| `explicit operator Coordinate` | Converts a tuple to a Coordinate. |

## Example

```csharp
// Create and use a coordinate
var position = new Coordinate(5, 10);
int x = position.X;
int y = position.Y;

// Compare coordinates
var otherPosition = new Coordinate(5, 10);
bool areEqual = position == otherPosition; // true

// Convert to/from tuples
(int posX, int posY) = position;
Coordinate fromTuple = (Coordinate)(3, 4);
```

## Remarks

- The `Coordinate` struct is optimized for performance with aggressive inlining of methods.
- The struct implements `IEquatable<Coordinate>` for efficient equality comparison.
- Coordinates are used throughout the MPath system to identify positions in the grid.
- The tuple conversion operators provide convenient interoperability with C# tuple syntax. 