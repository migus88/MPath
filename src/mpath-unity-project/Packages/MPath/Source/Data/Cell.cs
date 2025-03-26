using System.Runtime.CompilerServices;

namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Represents a single cell in the pathfinding grid.
    /// Contains both public properties used by client code and internal fields used by the pathfinding algorithm.
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
        /// Gets or sets the movement cost multiplier for this cell.
        /// Higher values make the cell more costly to traverse.
        /// This is used when <see cref="IPathfinderSettings.IsCellWeightEnabled"/> is enabled.
        /// </summary>
        public float Weight { get; set; }
        
        // Internal fields used by the pathfinding algorithm
        internal bool IsClosed { get; set; }
        internal float ScoreF { get; set; }
        internal float ScoreH { get; set; }
        internal float ScoreG { get; set; }
        internal int Depth { get; set; }
        internal Coordinate ParentCoordinate { get; set; }
        internal int QueueIndex { get; set; }
    
        /// <summary>
        /// Resets the internal pathfinding state of this cell.
        /// This method is called at the beginning of each pathfinding operation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            IsClosed = false;
            ScoreF = 0;
            ScoreH = 0;
            ScoreG = 0;
            Depth = 0;
            ParentCoordinate.Reset();
        }
    }
}