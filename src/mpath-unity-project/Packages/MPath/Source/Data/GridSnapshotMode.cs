namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Decides when a pathfinder built over a <see cref="Cell"/>[,] matrix, over cell holders or over an unordered
    /// <see cref="Cell"/>[] array copies the caller's grid into its internal snapshot. Set through
    /// <see cref="Interfaces.IPathfinderSettings.GridSnapshotMode"/>.
    /// </summary>
    /// <remarks>
    /// A row- or column-major <see cref="Cell"/>[] array (see <see cref="CellLayout"/>) is searched in place and has
    /// no snapshot, so this setting does not affect it.
    /// </remarks>
    public enum GridSnapshotMode
    {
        /// <summary>
        /// Copy the whole grid before every query, so direct edits to the caller's grid are always seen. Every query
        /// costs at least O(grid size). The default, and the behaviour of versions before this setting existed.
        /// </summary>
        PerQuery = 0,

        /// <summary>
        /// Copy the grid once, before the first query, and again only after <see cref="Pathfinder.Refresh"/>. Edits
        /// made through the pathfinder's cell-update methods (<see cref="Pathfinder.SetWalkable"/>,
        /// <see cref="Pathfinder.SetOccupied"/>, <see cref="Pathfinder.SetWeight"/>, <see cref="Pathfinder.SetCell"/>)
        /// are seen at once; direct edits to the caller's grid are not seen until <see cref="Pathfinder.Refresh"/>.
        /// A query then costs only the cells it searches.
        /// </summary>
        Manual = 1
    }
}
