using System;
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
        /// Determines is the agent can move between two corners.<br/>
        /// For example, if set to 'false', this movement is illegal:<br/>
        /// ◻◎◼◻<br/>
        /// ◻◼◎◻<br/>
        /// * Circle is the movement path, blank squares - walkable cells and black squares - walls.
        /// </summary>
        /// <remarks>
        /// This is the all-or-nothing shorthand for <see cref="CornerBlocking"/>: <c>true</c> means
        /// <see cref="Data.CornerBlocking.None"/>, <c>false</c> means <see cref="Data.CornerBlocking.BetweenBlockedCells"/>.
        /// The pathfinder reads only <see cref="CornerBlocking"/>.
        /// </remarks>
        bool IsMovementBetweenCornersEnabled { get; }

        /// <summary>
        /// Decides which cells beside a diagonal step block it (corner-cutting), separately for walls and
        /// occupied cells. See <see cref="Data.CornerBlocking"/> for the flags and presets.<br/>
        /// Implementations that don't override it derive it from <see cref="IsMovementBetweenCornersEnabled"/>,
        /// which reproduces the behaviour of versions before this setting existed.
        /// </summary>
        CornerBlocking CornerBlocking => IsMovementBetweenCornersEnabled
            ? CornerBlocking.None
            : CornerBlocking.BetweenBlockedCells;

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
        /// When a pathfinder built over a <see cref="Cell"/>[,] matrix or over cell holders copies the caller's grid
        /// into its internal snapshot: before every query (<see cref="Data.GridSnapshotMode.PerQuery"/>), or once and
        /// then only after <see cref="Pathfinder.Refresh"/> (<see cref="Data.GridSnapshotMode.Manual"/>). Has no effect
        /// on a pathfinder built over a <see cref="Cell"/>[] array.<br/>
        /// Implementations that don't override it use <see cref="Data.GridSnapshotMode.PerQuery"/>, the behaviour of
        /// versions before this setting existed.
        /// </summary>
        GridSnapshotMode GridSnapshotMode => GridSnapshotMode.PerQuery;

        /// <summary>
        /// Formerly the initial size of the open set buffer. Ignored: the open set is sized to the grid once, when the
        /// pathfinder is created, so it never grows and searches never allocate for it.
        /// </summary>
        [Obsolete("Ignored: the open set is sized to the grid when the pathfinder is created and never grows. This member will be removed in the next major version.")]
        int? InitialBufferSize { get; }
    }
}