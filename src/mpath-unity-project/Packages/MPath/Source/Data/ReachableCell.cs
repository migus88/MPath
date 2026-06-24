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
        public float Cost { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReachableCell"/> struct.
        /// </summary>
        /// <param name="coordinate">The coordinate of the reachable cell.</param>
        /// <param name="cost">The cost of the cheapest path to the cell.</param>
        public ReachableCell(Coordinate coordinate, float cost)
        {
            Coordinate = coordinate;
            Cost = cost;
        }

        public override string ToString()
        {
            return $"{Coordinate} ({Cost})";
        }
    }
}
