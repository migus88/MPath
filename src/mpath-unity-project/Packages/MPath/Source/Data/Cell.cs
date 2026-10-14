
namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Represents a single cell in the pathfinding grid: its coordinate and the data a search reads (walkability,
    /// occupancy and weight). Searches only read cells; their state lives in the pathfinder.
    /// </summary>
    public struct Cell
    {
        /// <summary>
        /// Gets or sets the coordinate position of this cell in the grid.
        /// </summary>
        public Coordinate Coordinate { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether an agent can traverse this cell.
        /// </summary>
        public bool IsWalkable { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether this cell is currently occupied by an agent.
        /// This is used when <see cref="IPathfinderSettings.IsCalculatingOccupiedCells"/> is enabled.
        /// </summary>
        public bool IsOccupied { get; set; }
        
        /// <summary>
        /// Gets or sets the extra cost of entering this cell: it is added to the straight or diagonal movement
        /// multiplier of every step into the cell. Higher values make the cell more costly to traverse; keep it
        /// non-negative (a negative weight voids the cheapest-path guarantee).
        /// This is used when <see cref="IPathfinderSettings.IsCellWeightEnabled"/> is enabled.
        /// </summary>
        public float Weight { get; set; }
    }
}