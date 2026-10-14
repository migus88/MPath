namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Decides, for a single query, whether occupied cells (<see cref="PathCell.IsOccupied"/>) block the agent.
    /// Passed through <see cref="QueryOptions"/>.
    /// </summary>
    public enum OccupancyRule
    {
        /// <summary>
        /// Follow <see cref="Interfaces.IPathfinderSettings.AreOccupiedCellsBlocking"/>, as the queries without
        /// options do.
        /// </summary>
        UseSettings = 0,

        /// <summary>
        /// Occupied cells block entry, agent clearance and count as occupied for the corner rule, whatever the
        /// settings say.
        /// </summary>
        Block = 1,

        /// <summary>
        /// Occupied cells are treated as free for this query, whatever the settings say.
        /// </summary>
        Ignore = 2
    }
}
