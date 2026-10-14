using System;

namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Decides which cells beside a diagonal step stop the agent from taking it ("corner-cutting").
    /// </summary>
    /// <remarks>
    /// A diagonal step from (x, y) to (x + dx, y + dy) passes between two flanking cells: (x + dx, y) and
    /// (x, y + dy). Each flanking cell is classified as one of:
    /// <list type="bullet">
    /// <item><description><b>open</b> - the agent could enter it;</description></item>
    /// <item><description><b>wall</b> - not walkable, or outside the grid;</description></item>
    /// <item><description><b>occupied</b> - walkable but occupied, while
    /// <see cref="Interfaces.IPathfinderSettings.IsCalculatingOccupiedCells"/> is enabled (otherwise an
    /// occupied cell counts as open).</description></item>
    /// </list>
    /// Each flag names one combination of the two flanking cells; when the actual combination's flag is
    /// set, the diagonal step is not allowed. The order of the two cells does not matter.<br/>
    /// For an agent with <see cref="Interfaces.IAgent.Size"/> greater than 1, a flanking "cell" is the agent's
    /// whole footprint at the flanking position: it is a wall when any cell of the footprint is a wall or
    /// outside the grid, otherwise occupied when any cell of the footprint is occupied, otherwise open.<br/>
    /// The rule only gates diagonal steps. Entering a cell is decided separately (walls never, occupied
    /// cells not while <see cref="Interfaces.IPathfinderSettings.IsCalculatingOccupiedCells"/> is enabled).
    /// </remarks>
    [Flags]
    [Serializable]
    public enum CornerBlocking
    {
        /// <summary>
        /// No flanking cells block a diagonal step: the agent may squeeze between any two corners.
        /// Equivalent to <see cref="Interfaces.IPathfinderSettings.IsMovementBetweenCornersEnabled"/> = <c>true</c>.
        /// </summary>
        None = 0,

        /// <summary>Two walls on both sides block the diagonal step.</summary>
        WallAndWall = 1 << 0,

        /// <summary>A wall on one side and an occupied cell on the other block the diagonal step.</summary>
        WallAndOccupied = 1 << 1,

        /// <summary>Occupied cells on both sides block the diagonal step.</summary>
        OccupiedAndOccupied = 1 << 2,

        /// <summary>A wall on one side and an open cell on the other block the diagonal step (no cutting past a wall's corner).</summary>
        WallAndOpen = 1 << 3,

        /// <summary>An occupied cell on one side and an open cell on the other block the diagonal step (no cutting past a unit).</summary>
        OccupiedAndOpen = 1 << 4,

        /// <summary>
        /// The default: a diagonal step is blocked only when both flanking cells are blocked, whatever blocks
        /// them. Equivalent to <see cref="Interfaces.IPathfinderSettings.IsMovementBetweenCornersEnabled"/> = <c>false</c>.
        /// </summary>
        BetweenBlockedCells = WallAndWall | WallAndOccupied | OccupiedAndOccupied,

        /// <summary>
        /// Only walls matter: a diagonal step is blocked between two walls, while occupied cells never block it
        /// (neither two units nor a unit next to a wall). Typical for tactics games where units stand in cells
        /// but do not fill them.
        /// </summary>
        BetweenWalls = WallAndWall,

        /// <summary>
        /// Walls block corner-cutting entirely (a single wall corner is enough), occupied cells never do.
        /// </summary>
        AnyWall = WallAndWall | WallAndOccupied | WallAndOpen,

        /// <summary>
        /// No corner-cutting at all: a diagonal step needs both flanking cells to be open.
        /// </summary>
        All = WallAndWall | WallAndOccupied | OccupiedAndOccupied | WallAndOpen | OccupiedAndOpen
    }
}
