using System;
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
        /// <remarks>
        /// A view over <see cref="CornerBlocking"/>: reads <c>true</c> only when it is <see cref="Data.CornerBlocking.None"/>.
        /// Setting <c>true</c> sets <see cref="Data.CornerBlocking.None"/>; setting <c>false</c> sets
        /// <see cref="Data.CornerBlocking.BetweenBlockedCells"/> unless a blocking rule is already configured.
        /// </remarks>
        public bool IsMovementBetweenCornersEnabled
        {
            get => CornerBlocking == CornerBlocking.None;
            set
            {
                if (value == IsMovementBetweenCornersEnabled)
                {
                    return;
                }

                CornerBlocking = value ? CornerBlocking.None : CornerBlocking.BetweenBlockedCells;
            }
        }

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
        [Obsolete("Ignored: the open set is sized to the grid when the pathfinder is created and never grows. This member will be removed in the next major version.")]
        public int? InitialBufferSize { get; set; } = null;

        /// <inheritdoc/>
        public TieBreaking TieBreaking { get; set; } = TieBreaking.None;
    }
}