using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core.Internal;

// TODO: Implement using source generation
internal struct FastPathfinderSettings
{
    public readonly bool IsDiagonalMovementEnabled;
    public readonly bool IsCalculatingOccupiedCells;
    public readonly bool IsMovementBetweenCornersEnabled;
    public readonly bool IsCellWeightEnabled;
    public readonly float StraightMovementMultiplier;
    public readonly float DiagonalMovementMultiplier;

    private FastPathfinderSettings(bool isDiagonalMovementEnabled, bool isCalculatingOccupiedCells,
        bool isMovementBetweenCornersEnabled, bool isCellWeightEnabled, float straightMovementMultiplier,
        float diagonalMovementMultiplier)
    {
        IsDiagonalMovementEnabled = isDiagonalMovementEnabled;
        IsCalculatingOccupiedCells = isCalculatingOccupiedCells;
        IsMovementBetweenCornersEnabled = isMovementBetweenCornersEnabled;
        IsCellWeightEnabled = isCellWeightEnabled;
        StraightMovementMultiplier = straightMovementMultiplier;
        DiagonalMovementMultiplier = diagonalMovementMultiplier;
    }

    public static FastPathfinderSettings FromSettings(IPathfinderSettings settings)
    {
        return new FastPathfinderSettings(settings.IsDiagonalMovementEnabled, settings.IsCalculatingOccupiedCells,
            settings.IsMovementBetweenCornersEnabled, settings.IsCellWeightEnabled, settings.StraightMovementMultiplier,
            settings.DiagonalMovementMultiplier);
    }
}