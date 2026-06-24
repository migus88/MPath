namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// A snapshot of a single cell that has been touched by a stepwise A* search, together with the A*
    /// scores the algorithm currently associates with it. Returned in batches as part of a
    /// <see cref="SearchStep"/> so the progress of the search can be displayed.
    /// </summary>
    public readonly struct SearchNode
    {
        /// <summary>
        /// Gets the coordinate of the cell.
        /// </summary>
        public Coordinate Coordinate { get; }

        /// <summary>
        /// Gets whether the cell is currently on the <see cref="SearchNodeState.Open"/> frontier or has
        /// already been expanded into the <see cref="SearchNodeState.Closed"/> set.
        /// </summary>
        public SearchNodeState State { get; }

        /// <summary>
        /// Gets the cost of the cheapest path discovered so far from the origin to this cell (the A* <c>g</c> score).
        /// </summary>
        public float ScoreG { get; }

        /// <summary>
        /// Gets the heuristic estimate from this cell to the destination (the A* <c>h</c> score, Manhattan distance).
        /// </summary>
        public float ScoreH { get; }

        /// <summary>
        /// Gets the priority by which the cell is ordered in the open set: <c>g + h</c> (the A* <c>f</c> score).
        /// </summary>
        public float ScoreF { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchNode"/> struct.
        /// </summary>
        /// <param name="coordinate">The coordinate of the cell.</param>
        /// <param name="state">Whether the cell is open (frontier) or closed (expanded).</param>
        /// <param name="scoreG">The cheapest cost from the origin discovered so far.</param>
        /// <param name="scoreH">The heuristic estimate to the destination.</param>
        /// <param name="scoreF">The combined priority (<c>g + h</c>).</param>
        public SearchNode(Coordinate coordinate, SearchNodeState state, float scoreG, float scoreH, float scoreF)
        {
            Coordinate = coordinate;
            State = state;
            ScoreG = scoreG;
            ScoreH = scoreH;
            ScoreF = scoreF;
        }

        public override string ToString()
        {
            return $"{Coordinate} [{State}] g={ScoreG} h={ScoreH} f={ScoreF}";
        }
    }
}
