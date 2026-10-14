using System;

namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Movement rules that override the pathfinder's settings for a single query (<see cref="Pathfinder.GetPath(Interfaces.IAgent, Coordinate, Coordinate, QueryOptions)"/>,
    /// <see cref="Pathfinder.GetReachable(Interfaces.IAgent, Coordinate, int, QueryOptions)"/> and
    /// <see cref="Pathfinder.BeginStepwiseSearch(Interfaces.IAgent, Coordinate, Coordinate, QueryOptions)"/>).
    /// The grid and the settings are left untouched.
    /// </summary>
    /// <remarks>
    /// A <c>ref struct</c>, so it never allocates: pass a <c>stackalloc</c>, an array or any span as
    /// <see cref="AlwaysEnterable"/>. <c>default(QueryOptions)</c> reproduces the overloads without options.
    /// </remarks>
    public readonly ref struct QueryOptions
    {
        /// <summary>
        /// Creates per-query options.
        /// </summary>
        /// <param name="occupancy">Whether occupied cells block the agent in this query.</param>
        /// <param name="alwaysEnterable">Cells treated as unoccupied in this query, e.g. the mover's own cell and its target.</param>
        /// <param name="isDestinationAlwaysEnterable">Whether the destination is treated as unoccupied (queries with a destination only).</param>
        public QueryOptions(OccupancyRule occupancy = OccupancyRule.UseSettings,
            ReadOnlySpan<Coordinate> alwaysEnterable = default,
            bool isDestinationAlwaysEnterable = false)
        {
            Occupancy = occupancy;
            AlwaysEnterable = alwaysEnterable;
            IsDestinationAlwaysEnterable = isDestinationAlwaysEnterable;
        }

        /// <summary>
        /// Whether occupied cells block the agent in this query. <see cref="OccupancyRule.UseSettings"/> (the
        /// default) follows <see cref="Interfaces.IPathfinderSettings.AreOccupiedCellsBlocking"/>.
        /// </summary>
        public OccupancyRule Occupancy { get; }

        /// <summary>
        /// Cells whose <see cref="Cell.IsOccupied"/> flag is ignored in this query: they can be entered, count as
        /// clear for a multi-cell agent's footprint and as open for the corner rule. Walls (not walkable) stay
        /// walls. Typical entries are the mover's own cell and the unit it moves to attack. Scanned linearly, and
        /// only when an occupied cell is met, so keep it short.
        /// </summary>
        public ReadOnlySpan<Coordinate> AlwaysEnterable { get; }

        /// <summary>
        /// Whether the destination is treated like an <see cref="AlwaysEnterable"/> cell, so a path to an occupied
        /// destination (move to attack) can succeed. Applies to <c>GetPath</c> and <c>BeginStepwiseSearch</c>;
        /// <c>GetReachable</c> has no destination and ignores it.
        /// </summary>
        public bool IsDestinationAlwaysEnterable { get; }
    }
}
