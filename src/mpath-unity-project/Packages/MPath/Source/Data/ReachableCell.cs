namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Represents a single cell that is reachable within a movement budget,
    /// together with the cost of the cheapest path to it from the query origin.
    /// Returned as part of a <see cref="RangeResult"/>.
    /// </summary>
    public readonly struct ReachableCell
    {
        /// <summary>
        /// Gets the coordinate of the reachable cell.
        /// </summary>
        public Coordinate Coordinate { get; }

        /// <summary>
        /// Gets the cost of the cheapest path from the query origin to this cell.
        /// This value is always less than or equal to the budget supplied to the query.
        /// </summary>
        public int Cost { get; }

        /// <summary>
        /// Gets the number of steps of the cheapest route to this cell (0 for the origin). With a
        /// <see cref="TieBreaking"/> it is the route with the fewest diagonal steps, then the fewest steps, among
        /// the cheapest ones; without one, whichever cheapest route the search kept.
        /// </summary>
        public int Steps { get; }

        /// <summary>
        /// Gets how many of <see cref="Steps"/> are diagonal, on the same route.
        /// </summary>
        public int DiagonalSteps { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReachableCell"/> struct.
        /// </summary>
        /// <param name="coordinate">The coordinate of the reachable cell.</param>
        /// <param name="cost">The cost of the cheapest path to the cell.</param>
        /// <param name="steps">The number of steps of that path.</param>
        /// <param name="diagonalSteps">How many of <paramref name="steps"/> are diagonal.</param>
        public ReachableCell(Coordinate coordinate, int cost, int steps, int diagonalSteps)
        {
            Coordinate = coordinate;
            Cost = cost;
            Steps = steps;
            DiagonalSteps = diagonalSteps;
        }

        public override string ToString()
        {
            return $"{Coordinate} ({Cost}, {Steps} steps, {DiagonalSteps} diagonal)";
        }
    }
}
