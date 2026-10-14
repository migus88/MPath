# OccupancyRule Enum

**Namespace:** `Migs.MPath.Core.Data`

Decides, for a single query, whether occupied cells (`Cell.IsOccupied`) block the agent. Set through [`QueryOptions.Occupancy`](QueryOptions.md).

## Values

| Value | Description |
|-------|-------------|
| `UseSettings` | **(default, `0`)** Follow `IPathfinderSettings.AreOccupiedCellsBlocking`, like the queries without options. |
| `Block` | Occupied cells block entry and clearance and count as occupied for the corner rule, even when the settings ignore them. |
| `Ignore` | Occupied cells are treated as free, even when the settings block them (e.g. a threat range that sees through units). |

## Remarks

- The rule only reclassifies occupied cells; walls are unaffected.
- `QueryOptions.AlwaysEnterable` exempts individual cells under `UseSettings` and `Block`; under `Ignore` every occupied cell is already free.
