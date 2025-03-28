using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core.Internal
{
    // TODO: Implement using source generation
    internal struct FastPathfinderSettings
    {
        public readonly bool IsDiagonalMovementEnabled;
        public readonly bool IsCalculatingOccupiedCells;
        public readonly bool IsMovementBetweenCornersEnabled;
        public readonly bool IsCellWeightEnabled;
        public readonly PathSmoothingMethod PathSmoothingMethod;
        public readonly float StraightMovementMultiplier;
        public readonly float DiagonalMovementMultiplier;

        private FastPathfinderSettings(bool isDiagonalMovementEnabled, bool isCalculatingOccupiedCells,
            bool isMovementBetweenCornersEnabled, bool isCellWeightEnabled, PathSmoothingMethod pathSmoothingMethod,
            float straightMovementMultiplier, float diagonalMovementMultiplier)
        {
            IsDiagonalMovementEnabled = isDiagonalMovementEnabled;
            IsCalculatingOccupiedCells = isCalculatingOccupiedCells;
            IsMovementBetweenCornersEnabled = isMovementBetweenCornersEnabled;
            IsCellWeightEnabled = isCellWeightEnabled;
            PathSmoothingMethod = pathSmoothingMethod;
            StraightMovementMultiplier = straightMovementMultiplier;
            DiagonalMovementMultiplier = diagonalMovementMultiplier;
        }

        public static FastPathfinderSettings FromSettings(IPathfinderSettings settings)
        {
            return new FastPathfinderSettings(settings.IsDiagonalMovementEnabled, settings.IsCalculatingOccupiedCells,
                settings.IsMovementBetweenCornersEnabled, settings.IsCellWeightEnabled, settings.PathSmoothingMethod,
                settings.StraightMovementMultiplier, settings.DiagonalMovementMultiplier);
        }
    }
}