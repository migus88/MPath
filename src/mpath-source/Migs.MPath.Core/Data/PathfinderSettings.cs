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
        public bool IsMovementBetweenCornersEnabled { get; set; } = false;

        /// <inheritdoc/>
        public bool IsCellWeightEnabled { get; set; } = true;

        /// <inheritdoc/>
        public float StraightMovementMultiplier { get; set; } = 1f;

        /// <inheritdoc/>
        public float DiagonalMovementMultiplier { get; set; } = 1.41f;

        /// <inheritdoc/>
        public int? InitialBufferSize { get; set; } = null;
    }
}