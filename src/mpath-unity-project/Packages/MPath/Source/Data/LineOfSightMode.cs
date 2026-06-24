namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Controls how <c>Pathfinder.HasLineOfSight</c> treats cells that are not walkable.
    /// </summary>
    /// <remarks>
    /// This only governs the walkability of cells between the endpoints. Occupancy is handled separately:
    /// when <c>IsCalculatingOccupiedCells</c> is enabled, an occupied cell blocks sight under either mode
    /// (so "ignore terrain, but units still block" is expressible).
    /// </remarks>
    [System.Serializable]
    public enum LineOfSightMode
    {
        /// <summary>
        /// Cells that are not walkable block line of sight. This is the default and matches how walls
        /// interrupt vision in most grid games.
        /// </summary>
        BlockedByUnwalkableCells = 0,

        /// <summary>
        /// Cells that are not walkable are treated as transparent and do not block line of sight.
        /// Use this when an obstacle blocks movement but not vision (water, a pit, a chasm, a glass wall).
        /// </summary>
        IgnoreUnwalkableCells = 1
    }
}
