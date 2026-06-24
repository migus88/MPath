namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Describes the overall state of a stepwise (tick-by-tick) A* search driven by
    /// <see cref="Pathfinder.StepwiseSearch"/>.
    /// </summary>
    public enum SearchState
    {
        /// <summary>
        /// The search has not finished yet: the frontier still contains cells to expand and the
        /// destination has not been reached. Call <see cref="Pathfinder.StepwiseSearch.Tick"/> again to advance it.
        /// </summary>
        InProgress,

        /// <summary>
        /// The destination has been reached. The final path is available on the returned <see cref="SearchStep"/>.
        /// </summary>
        Success,

        /// <summary>
        /// The frontier was exhausted without reaching the destination: no path exists for this agent.
        /// </summary>
        Failure
    }
}
