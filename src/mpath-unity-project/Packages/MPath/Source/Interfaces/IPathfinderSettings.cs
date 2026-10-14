using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Interfaces
{
    public interface IPathfinderSettings
    {
        /// <summary>
        /// Determines is the agent can move diagonally.
        /// </summary>
        bool IsDiagonalMovementEnabled { get; }

        /// <summary>
        /// Whether occupied cells (<see cref="Data.PathCell.IsOccupied"/>) block: they cannot be entered, count against
        /// an agent's clearance and the corner rule, and block line of sight. Set it to 'false' when several agents
        /// can share a cell. A query can override it with <see cref="Data.QueryOptions.Occupancy"/>.
        /// </summary>
        bool AreOccupiedCellsBlocking { get; }

        /// <summary>
        /// Decides which cells beside a diagonal step block it (corner-cutting), separately for walls and
        /// occupied cells. See <see cref="Data.CornerBlocking"/> for the flags and presets;
        /// <see cref="Data.CornerBlocking.BetweenBlockedCells"/> is the usual default.
        /// </summary>
        CornerBlocking CornerBlocking { get; }

        /// <summary>
        /// Is additional cell weight calculation enabled.
        /// </summary>
        bool IsCellWeightEnabled { get; }

        /// <summary>
        /// The cost of one horizontal or vertical step, at least 1. Costs are integers in a unit the caller picks:
        /// cell weights, budgets and every reported cost use the same one (with the defaults 100 and 141, a step
        /// costs 100 and costs read as hundredths of a step).
        /// </summary>
        /// <remarks>
        /// The pathfinder constructor throws <see cref="ArgumentOutOfRangeException"/> when a step cost is below 1,
        /// or when <c>(width + height)</c> times the larger step cost in use exceeds 1,073,741,823
        /// (<c>int.MaxValue / 2</c>), the headroom that keeps route costs from overflowing.
        /// </remarks>
        int StraightStepCost { get; }

        /// <summary>
        /// The cost of one diagonal step, at least 1, in the same unit as <see cref="StraightStepCost"/>. Only used
        /// when <see cref="IsDiagonalMovementEnabled"/> is set (it is still validated otherwise).
        /// </summary>
        /// <remarks><inheritdoc cref="StraightStepCost" path="/remarks"/></remarks>
        int DiagonalStepCost { get; }

        /// <summary>
        /// Specifies the path smoothing method to apply to the calculated path.<br/>
        /// Different methods offer trade-offs between path quality and performance.
        /// </summary>
        PathSmoothingMethod PathSmoothingMethod { get; }

        /// <summary>
        /// When a pathfinder built over a <see cref="PathCell"/>[,] matrix or over cell holders copies the caller's grid
        /// into its internal snapshot: before every query (<see cref="Data.GridSnapshotMode.PerQuery"/>), or once and
        /// then only after <see cref="Pathfinder.Refresh"/> (<see cref="Data.GridSnapshotMode.Manual"/>). Has no
        /// effect on a <see cref="PathCell"/>[], which is searched in place.
        /// <see cref="Data.GridSnapshotMode.PerQuery"/> is the usual default.
        /// </summary>
        GridSnapshotMode GridSnapshotMode { get; }

        /// <summary>
        /// Which index of a <see cref="PathCell"/>[] or <see cref="ICellHolder"/>[] array holds the cell at (X, Y):
        /// column-major (<c>X * fieldHeight + Y</c>) or row-major (<c>Y * fieldWidth + X</c>). Queries never reorder
        /// the array. Has no effect on the <see cref="PathCell"/>[,] and <see cref="ICellHolder"/>[,] constructors.
        /// <see cref="Data.CellLayout.ColumnMajor"/> is the usual default; any value but the two is rejected with an
        /// <see cref="System.ArgumentOutOfRangeException"/> when the pathfinder is built.
        /// </summary>
        CellLayout CellLayout { get; }

        /// <summary>
        /// Which route to return when several cost the same. See <see cref="Data.TieBreaking"/>;
        /// <see cref="Data.TieBreaking.None"/> is the usual default.
        /// </summary>
        TieBreaking TieBreaking { get; }
    }
}