using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core.Internal
{
    // TODO: Implement using source generation
    internal readonly struct FastPathfinderSettings
    {
        private const int BlockerKinds = 3;

        public readonly bool IsDiagonalMovementEnabled;
        public readonly bool IsCalculatingOccupiedCells;
        public readonly CornerBlocking CornerBlocking;
        public readonly bool IsCellWeightEnabled;
        public readonly PathSmoothingMethod PathSmoothingMethod;
        public readonly float StraightMovementMultiplier;
        public readonly float DiagonalMovementMultiplier;

        /// <summary>
        /// <see cref="CornerBlocking"/> unrolled into a symmetric 3x3 bit table: bit
        /// <c>first * 3 + second</c> (indexed by <see cref="CellBlocker"/>) is set when a diagonal step
        /// between those two flanking cells is blocked. Read with one shift in the neighbour expansion.
        /// </summary>
        private readonly int _cornerBlockingTable;

        private FastPathfinderSettings(bool isDiagonalMovementEnabled, bool isCalculatingOccupiedCells,
            CornerBlocking cornerBlocking, bool isCellWeightEnabled, PathSmoothingMethod pathSmoothingMethod,
            float straightMovementMultiplier, float diagonalMovementMultiplier)
        {
            IsDiagonalMovementEnabled = isDiagonalMovementEnabled;
            IsCalculatingOccupiedCells = isCalculatingOccupiedCells;
            CornerBlocking = cornerBlocking;
            IsCellWeightEnabled = isCellWeightEnabled;
            PathSmoothingMethod = pathSmoothingMethod;
            StraightMovementMultiplier = straightMovementMultiplier;
            DiagonalMovementMultiplier = diagonalMovementMultiplier;
            _cornerBlockingTable = BuildCornerBlockingTable(cornerBlocking);
        }

        public static FastPathfinderSettings FromSettings(IPathfinderSettings settings)
        {
            return new FastPathfinderSettings(settings.IsDiagonalMovementEnabled, settings.IsCalculatingOccupiedCells,
                settings.CornerBlocking, settings.IsCellWeightEnabled, settings.PathSmoothingMethod,
                settings.StraightMovementMultiplier, settings.DiagonalMovementMultiplier);
        }

        /// <summary>
        /// Whether a diagonal step may pass between two flanking positions classified as
        /// <paramref name="first"/> and <paramref name="second"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCornerPassable(CellBlocker first, CellBlocker second)
        {
            return (_cornerBlockingTable & (1 << ((int)first * BlockerKinds + (int)second))) == 0;
        }

        private static int BuildCornerBlockingTable(CornerBlocking cornerBlocking)
        {
            var table = 0;
            table |= Bit(cornerBlocking, CornerBlocking.WallAndWall, CellBlocker.Wall, CellBlocker.Wall);
            table |= Bit(cornerBlocking, CornerBlocking.WallAndOccupied, CellBlocker.Wall, CellBlocker.Occupied);
            table |= Bit(cornerBlocking, CornerBlocking.OccupiedAndOccupied, CellBlocker.Occupied, CellBlocker.Occupied);
            table |= Bit(cornerBlocking, CornerBlocking.WallAndOpen, CellBlocker.Wall, CellBlocker.Open);
            table |= Bit(cornerBlocking, CornerBlocking.OccupiedAndOpen, CellBlocker.Occupied, CellBlocker.Open);
            return table;
        }

        private static int Bit(CornerBlocking cornerBlocking, CornerBlocking flag, CellBlocker first, CellBlocker second)
        {
            if ((cornerBlocking & flag) == 0)
            {
                return 0;
            }

            return (1 << ((int)first * BlockerKinds + (int)second)) | (1 << ((int)second * BlockerKinds + (int)first));
        }
    }
}
