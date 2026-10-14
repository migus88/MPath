using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core.Data
{
    public class PathfinderSettings : IPathfinderSettings
    {
        /// <inheritdoc/>
        public bool IsDiagonalMovementEnabled { get; set; } = true;

        /// <inheritdoc/>
        public bool AreOccupiedCellsBlocking { get; set; } = true;

        /// <inheritdoc/>
        public CornerBlocking CornerBlocking { get; set; } = CornerBlocking.BetweenBlockedCells;

        /// <inheritdoc/>
        public bool IsCellWeightEnabled { get; set; } = true;

        /// <inheritdoc/>
        /// <remarks>Defaults to 100.</remarks>
        public int StraightStepCost { get; set; } = 100;

        /// <inheritdoc/>
        /// <remarks>Defaults to 141, so a diagonal costs 1.41 straight steps.</remarks>
        public int DiagonalStepCost { get; set; } = 141;

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