
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
        /// Gets or sets the extra cost of entering this cell, in the same units as
        /// <see cref="IPathfinderSettings.StraightStepCost"/> and <see cref="IPathfinderSettings.DiagonalStepCost"/>:
        /// it is added to the step cost of every step into the cell. Higher values make the cell more costly to
        /// traverse; 0 (the default) adds nothing.
        /// This is used when <see cref="IPathfinderSettings.IsCellWeightEnabled"/> is enabled.
        /// </summary>
        /// <remarks>
        /// <see cref="Pathfinder.SetWeight"/> and <see cref="Pathfinder.SetCell"/> reject a negative weight; a negative
        /// weight written straight into the grid counts as 0. A weight above 1,073,741,823 (<c>int.MaxValue / 2</c>)
        /// counts as that, and a route whose cost would pass it is never taken, so a huge weight makes the cells
        /// behind it unreachable rather than wrapping around.
        /// </remarks>
        public int Weight { get; set; }
    }
}