# CornerBlocking Enum

**Namespace:** `Migs.MPath.Core.Data`

A `[Flags]` enum that decides which cells beside a diagonal step stop an agent from taking it (corner-cutting). Set it through [`IPathfinderSettings.CornerBlocking`](IPathfinderSettings.md).

A diagonal step from `(x, y)` to `(x + dx, y + dy)` passes between two **flanking cells**, `(x + dx, y)` and `(x, y + dy)`. Each flanking cell is one of:

- **open**: the agent could enter it;
- **wall**: not walkable, or outside the grid;
- **occupied**: walkable but `IsOccupied`, while `AreOccupiedCellsBlocking` is enabled (otherwise an occupied cell counts as open).

Each flag names one combination of the two flanking cells. If the flag for the actual combination is set, the diagonal step is not allowed. The order of the two cells does not matter.

## Flags

| Value | Blocks a diagonal step when the two flanking cells are |
|-------|---------------------------------------------------------|
| `None` (0) | never blocked. |
| `WallAndWall` | wall + wall |
| `WallAndOccupied` | wall + occupied |
| `OccupiedAndOccupied` | occupied + occupied |
| `WallAndOpen` | wall + open (no cutting past a single wall corner) |
| `OccupiedAndOpen` | occupied + open (no cutting past a single unit) |

## Presets

| Preset | Flags | Use it when |
|--------|-------|-------------|
| `BetweenBlockedCells` | `WallAndWall \| WallAndOccupied \| OccupiedAndOccupied` | **Default.** Diagonals are blocked only when both flanking cells are blocked, by anything. |
| `BetweenWalls` | `WallAndWall` | Only walls matter. Units stand in cells but don't fill them, so neither two units nor a unit next to a wall block the diagonal. Typical for tactics games. |
| `AnyWall` | `WallAndWall \| WallAndOccupied \| WallAndOpen` | A single wall corner blocks the diagonal; units never do. |
| `All` | every flag | No corner-cutting at all: a diagonal step needs both flanking cells open. |

## Usage example

```csharp
// Walls block squeezing between them; units can't be entered but never block a diagonal.
var settings = new PathfinderSettings
{
    AreOccupiedCellsBlocking = true,
    CornerBlocking = CornerBlocking.BetweenWalls
};

// Any custom mix: walls block on their own, two units side by side block too.
settings.CornerBlocking = CornerBlocking.AnyWall | CornerBlocking.OccupiedAndOccupied;
```

## Remarks

- The rule only gates **diagonal steps**. Whether a cell can be entered is decided separately: walls never, occupied cells not while `AreOccupiedCellsBlocking` is enabled.
- It applies the same way to `GetPath`, `GetReachable`, the stepwise search and `StringPulling` path smoothing (a pulled shortcut never takes a diagonal step the rule forbids).
- **Larger agents** (`agentSize > 1`): a flanking "cell" is the agent's whole `Size×Size` footprint at the flanking position. It counts as a wall when any of its cells is a wall or outside the grid, otherwise as occupied when any of its cells is occupied, otherwise as open.
- The rule is read once when the `Pathfinder` is constructed, into a 9-bit lookup table, so it adds no per-query cost.
- `HasLineOfSight` is a visibility query and ignores the corner rule.
