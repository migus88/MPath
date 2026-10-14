using System;
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
        public readonly GridSnapshotMode GridSnapshotMode;

        /// <summary>
        /// Heuristic cost per unit of the larger axis distance to the destination
        /// (see <see cref="Pathfinder"/>'s <c>GetH</c>).
        /// </summary>
        public readonly float HeuristicMajorAxisCost;

        /// <summary>
        /// Heuristic cost per unit of the smaller axis distance to the destination
        /// (see <see cref="Pathfinder"/>'s <c>GetH</c>).
        /// </summary>
        public readonly float HeuristicMinorAxisCost;

        /// <summary>
        /// <see cref="CornerBlocking"/> unrolled into a symmetric 3x3 bit table: bit
        /// <c>first * 3 + second</c> (indexed by <see cref="CellBlocker"/>) is set when a diagonal step
        /// between those two flanking cells is blocked. Read with one shift in the neighbour expansion.
        /// </summary>
        private readonly int _cornerBlockingTable;

        private FastPathfinderSettings(bool isDiagonalMovementEnabled, bool isCalculatingOccupiedCells,
            CornerBlocking cornerBlocking, bool isCellWeightEnabled, PathSmoothingMethod pathSmoothingMethod,
            float straightMovementMultiplier, float diagonalMovementMultiplier, GridSnapshotMode gridSnapshotMode)
        {
            IsDiagonalMovementEnabled = isDiagonalMovementEnabled;
            IsCalculatingOccupiedCells = isCalculatingOccupiedCells;
            CornerBlocking = cornerBlocking;
            IsCellWeightEnabled = isCellWeightEnabled;
            PathSmoothingMethod = pathSmoothingMethod;
            StraightMovementMultiplier = straightMovementMultiplier;
            DiagonalMovementMultiplier = diagonalMovementMultiplier;
            GridSnapshotMode = gridSnapshotMode;
            (HeuristicMajorAxisCost, HeuristicMinorAxisCost) = GetHeuristicAxisCosts(isDiagonalMovementEnabled,
                straightMovementMultiplier, diagonalMovementMultiplier);
            _cornerBlockingTable = BuildCornerBlockingTable(cornerBlocking);
        }

        public static FastPathfinderSettings FromSettings(IPathfinderSettings settings)
        {
            return new FastPathfinderSettings(settings.IsDiagonalMovementEnabled, settings.IsCalculatingOccupiedCells,
                settings.CornerBlocking, settings.IsCellWeightEnabled, settings.PathSmoothingMethod,
                settings.StraightMovementMultiplier, settings.DiagonalMovementMultiplier, settings.GridSnapshotMode);
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

        /// <summary>
        /// Picks the two coefficients of the heuristic <c>major * max(dx, dy) + minor * min(dx, dy)</c> so it is
        /// the cheapest cost of crossing an empty grid with the configured step costs. That never exceeds the
        /// real cost (cell weights only add to a step) and drops by at most one step's cost per step, so A* stays
        /// optimal while closing each cell once.
        /// </summary>
        /// <remarks>
        /// Without diagonals it is the Manhattan distance times the straight cost. With diagonals it is the octile
        /// distance: <c>min(dx, dy)</c> diagonal steps plus the rest straight. A diagonal dearer than two straight
        /// steps is never worth taking, so its cost is capped at two straight steps; one cheaper than a straight
        /// step makes zigzagging diagonals the cheapest way to cover any distance, so every unit of the larger
        /// axis costs one diagonal. Negative multipliers count as 0 (the heuristic must not go negative).
        /// </remarks>
        private static (float major, float minor) GetHeuristicAxisCosts(bool isDiagonalMovementEnabled,
            float straightMovementMultiplier, float diagonalMovementMultiplier)
        {
            var straight = Math.Max(straightMovementMultiplier, 0f);

            if (!isDiagonalMovementEnabled)
            {
                return (straight, straight);
            }

            var diagonal = Math.Max(diagonalMovementMultiplier, 0f);

            if (diagonal < straight)
            {
                return (diagonal, 0f);
            }

            return (straight, Math.Min(diagonal, 2f * straight) - straight);
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
