namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// The role a <see cref="SearchNode"/> currently plays in a stepwise A* search. This is the distinction
    /// most useful when visualizing the algorithm: the <see cref="Open"/> frontier versus the already
    /// expanded <see cref="Closed"/> set.
    /// </summary>
    public enum SearchNodeState
    {
        /// <summary>
        /// The cell has been discovered and sits on the frontier (the open set), waiting to be expanded.
        /// </summary>
        Open,

        /// <summary>
        /// The cell has already been expanded (the closed set); its cheapest cost from the origin is final.
        /// </summary>
        Closed
    }
}
