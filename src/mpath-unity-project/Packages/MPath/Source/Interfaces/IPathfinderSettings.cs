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
        /// Determines is the occupied cells (by other agents) considered as blocked or ignored.<br/>
        /// When multiple agents can occupy one cell, this should be set to 'false'.
        /// </summary>
        bool IsCalculatingOccupiedCells { get; }

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
        /// The cost of the movement in a horizontal or vertical line
        /// </summary>
        float StraightMovementMultiplier { get; }

        /// <summary>
        /// The cost of the movement in a diagonal line
        /// </summary>
        float DiagonalMovementMultiplier { get; }

        /// <summary>
        /// Specifies the path smoothing method to apply to the calculated path.<br/>
        /// Different methods offer trade-offs between path quality and performance.
        /// </summary>
        PathSmoothingMethod PathSmoothingMethod { get; }

        /// <summary>
        /// When a pathfinder built over a <see cref="Cell"/>[,] matrix, over cell holders or over an unordered
        /// <see cref="Cell"/>[] array copies the caller's grid into its internal snapshot: before every query
        /// (<see cref="Data.GridSnapshotMode.PerQuery"/>), or once and then only after <see cref="Pathfinder.Refresh"/>
        /// (<see cref="Data.GridSnapshotMode.Manual"/>). Has no effect on a row- or column-major <see cref="Cell"/>[].
        /// <see cref="Data.GridSnapshotMode.PerQuery"/> is the usual default.
        /// </summary>
        GridSnapshotMode GridSnapshotMode { get; }

        /// <summary>
        /// Where a pathfinder built over a <see cref="Cell"/>[] array finds the cell at (X, Y): column-major
        /// (<c>X * fieldHeight + Y</c>), row-major (<c>Y * fieldWidth + X</c>), unordered (each cell's coordinate
        /// says where it is), or detected at the first query (<see cref="Data.CellLayout.Auto"/>). Queries never
        /// reorder the array. Has no effect on the <see cref="Cell"/>[,] and cell holder constructors.
        /// <see cref="Data.CellLayout.Auto"/> is the usual default.
        /// </summary>
        CellLayout CellLayout { get; }

        /// <summary>
        /// Which route to return when several cost the same. See <see cref="Data.TieBreaking"/>;
        /// <see cref="Data.TieBreaking.None"/> is the usual default.
        /// </summary>
        TieBreaking TieBreaking { get; }
    }
}