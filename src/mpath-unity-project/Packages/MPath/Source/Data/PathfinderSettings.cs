using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core.Data
{
    public class PathfinderSettings : IPathfinderSettings
    {
        /// <inheritdoc/>
        public bool IsDiagonalMovementEnabled { get; set; } = true;

        /// <inheritdoc/>
        public bool IsCalculatingOccupiedCells { get; set; } = true;

        /// <inheritdoc/>
        public CornerBlocking CornerBlocking { get; set; } = CornerBlocking.BetweenBlockedCells;

        /// <inheritdoc/>
        public bool IsCellWeightEnabled { get; set; } = true;

        /// <inheritdoc/>
        public float StraightMovementMultiplier { get; set; } = 1f;

        /// <inheritdoc/>
        public float DiagonalMovementMultiplier { get; set; } = 1.41f;

        /// <inheritdoc/>
        public PathSmoothingMethod PathSmoothingMethod { get; set; } = PathSmoothingMethod.None;

        /// <inheritdoc/>
        public GridSnapshotMode GridSnapshotMode { get; set; } = GridSnapshotMode.PerQuery;

        /// <inheritdoc/>
        public CellLayout CellLayout { get; set; } = CellLayout.Auto;

        /// <inheritdoc/>
        public TieBreaking TieBreaking { get; set; } = TieBreaking.None;
    }
}