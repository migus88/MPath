namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Decides which route the pathfinder returns when several routes cost the same
    /// (see <see cref="Interfaces.IPathfinderSettings.TieBreaking"/>).
    /// </summary>
    /// <remarks>
    /// With a tie-break (any value but <see cref="None"/>) costs are compared exactly, as integers in units of
    /// 0.0001: every movement multiplier, cell weight and budget is rounded to four decimal places (values written
    /// with up to four decimals, such as 1.41, are exact below 1000; a step costs at most 2^24), so routes of equal
    /// cost tie no matter the order their steps are added in, and a budget of 3 x 1.41 admits three 1.41 steps. Among routes equal on cost and on the tie-break key, the result is fixed
    /// by the grid alone, never by the search order: walking back from the destination, the route keeps its
    /// direction where it can, otherwise it takes the first of West, East, North, South, South-West, North-West,
    /// South-East, North-East (North is -Y). The same query on the same grid returns the same route on every run
    /// and platform, and an edit elsewhere on the grid that leaves the competing routes alone does not change it.
    /// Step costs must be positive.
    /// </remarks>
    [System.Serializable]
    public enum TieBreaking
    {
        /// <summary>
        /// No tie-break (the default, and the behaviour of earlier versions): costs are float sums and equal-cost
        /// routes are chosen by the order the search happens to expand them in. Deterministic for a given grid and
        /// query, but the choice can change with unrelated grid edits.
        /// </summary>
        None = 0,

        /// <summary>
        /// Among the cheapest routes, the one with the fewest diagonal steps, then the fewest steps. On open ground
        /// a straight line is the only route chosen whenever it is one of the cheapest.
        /// </summary>
        FewerDiagonals = 1,

        /// <summary>
        /// Among the cheapest routes, the one with the fewest direction changes (exact, not a heuristic).
        /// <see cref="Pathfinder.GetReachable(Interfaces.IAgent, Coordinate, float)"/> has no route to straighten
        /// and ranks equal-cost routes like <see cref="FewerDiagonals"/> for its step counts.
        /// </summary>
        FewerTurns = 2
    }
}
