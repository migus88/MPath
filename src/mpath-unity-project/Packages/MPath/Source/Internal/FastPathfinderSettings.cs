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

        /// <summary>
        /// The overflow headroom of a route's cost: a step whose route would cost more is never taken, so the
        /// cheapest cost <c>g</c> and the heuristic <c>h</c> each stay at or below it and <c>g + h</c> fits an
        /// <see cref="int"/>. Larger cell weights count as this value.
        /// </summary>
        public const int MaxCost = int.MaxValue / 2;

        public readonly bool IsDiagonalMovementEnabled;
        public readonly bool IsCalculatingOccupiedCells;
        public readonly CornerBlocking CornerBlocking;
        public readonly bool IsCellWeightEnabled;
        public readonly PathSmoothingMethod PathSmoothingMethod;
        public readonly int StraightStepCost;
        public readonly int DiagonalStepCost;
        public readonly GridSnapshotMode GridSnapshotMode;
        public readonly TieBreaking TieBreaking;

        /// <summary>
        /// Whether the search keeps a secondary tie-break key (any <see cref="TieBreaking"/> but
        /// <see cref="Data.TieBreaking.None"/>).
        /// </summary>
        public readonly bool IsTieBreaking;
        public readonly CellLayout CellLayout;

        /// <summary>
        /// Heuristic cost per unit of the larger axis distance to the destination
        /// (see <see cref="Pathfinder"/>'s <c>GetH</c>).
        /// </summary>
        public readonly int HeuristicMajorAxisCost;

        /// <summary>
        /// Heuristic cost per unit of the smaller axis distance to the destination
        /// (see <see cref="Pathfinder"/>'s <c>GetH</c>).
        /// </summary>
        public readonly int HeuristicMinorAxisCost;

        /// <summary>
        /// <see cref="CornerBlocking"/> unrolled into a symmetric 3x3 bit table: bit
        /// <c>first * 3 + second</c> (indexed by <see cref="CellBlocker"/>) is set when a diagonal step
        /// between those two flanking cells is blocked. Read with one shift in the neighbour expansion.
        /// </summary>
        private readonly int _cornerBlockingTable;

        private FastPathfinderSettings(bool isDiagonalMovementEnabled, bool isCalculatingOccupiedCells,
            CornerBlocking cornerBlocking, bool isCellWeightEnabled, PathSmoothingMethod pathSmoothingMethod,
            int straightStepCost, int diagonalStepCost, GridSnapshotMode gridSnapshotMode,
            TieBreaking tieBreaking, CellLayout cellLayout)
        {
            IsDiagonalMovementEnabled = isDiagonalMovementEnabled;
            IsCalculatingOccupiedCells = isCalculatingOccupiedCells;
            CornerBlocking = cornerBlocking;
            IsCellWeightEnabled = isCellWeightEnabled;
            PathSmoothingMethod = pathSmoothingMethod;
            StraightStepCost = straightStepCost;
            DiagonalStepCost = diagonalStepCost;
            GridSnapshotMode = gridSnapshotMode;
            CellLayout = cellLayout;
            (HeuristicMajorAxisCost, HeuristicMinorAxisCost) = GetHeuristicAxisCosts(isDiagonalMovementEnabled,
                straightStepCost, diagonalStepCost);
            _cornerBlockingTable = BuildCornerBlockingTable(cornerBlocking);
            TieBreaking = tieBreaking;
            IsTieBreaking = tieBreaking != TieBreaking.None;
        }

        public static FastPathfinderSettings FromSettings(IPathfinderSettings settings)
        {
            return new FastPathfinderSettings(settings.IsDiagonalMovementEnabled, settings.IsCalculatingOccupiedCells,
                settings.CornerBlocking, settings.IsCellWeightEnabled, settings.PathSmoothingMethod,
                settings.StraightStepCost, settings.DiagonalStepCost, settings.GridSnapshotMode,
                settings.TieBreaking, settings.CellLayout);
        }

        /// <summary>
        /// Checks the step costs for a <paramref name="width"/> x <paramref name="height"/> grid: each must be at
        /// least 1 (the searches need every step to cost something), and crossing the grid at the dearer one in use
        /// (the diagonal cost counts only with diagonal movement) must stay within <see cref="MaxCost"/>, which
        /// bounds the heuristic so <c>g + h</c> cannot overflow.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a step cost is out of range.</exception>
        public void Validate(int width, int height)
        {
            if (StraightStepCost < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(IPathfinderSettings.StraightStepCost), StraightStepCost,
                    "The straight step cost must be at least 1.");
            }

            if (DiagonalStepCost < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(IPathfinderSettings.DiagonalStepCost), DiagonalStepCost,
                    "The diagonal step cost must be at least 1.");
            }

            var largest = IsDiagonalMovementEnabled ? Math.Max(StraightStepCost, DiagonalStepCost) : StraightStepCost;

            if (((long)width + height) * largest > MaxCost)
            {
                throw new ArgumentOutOfRangeException(
                    largest == StraightStepCost
                        ? nameof(IPathfinderSettings.StraightStepCost)
                        : nameof(IPathfinderSettings.DiagonalStepCost), largest,
                    $"(width + height) * the larger step cost must not exceed {MaxCost} on a {width}x{height} grid.");
            }
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
        /// optimal while closing each cell once. Integer costs make it exact.
        /// </summary>
        /// <remarks>
        /// Without diagonals it is the Manhattan distance times the straight cost. With diagonals it is the octile
        /// distance: <c>min(dx, dy)</c> diagonal steps plus the rest straight. A diagonal dearer than two straight
        /// steps is never worth taking, so its cost is capped at two straight steps; one cheaper than a straight
        /// step makes zigzagging diagonals the cheapest way to cover any distance, so every unit of the larger
        /// axis costs one diagonal. Both coefficients are at most the larger step cost, so the heuristic stays within
        /// <see cref="MaxCost"/> on a grid that passed <see cref="Validate"/>.
        /// </remarks>
        private static (int major, int minor) GetHeuristicAxisCosts(bool isDiagonalMovementEnabled,
            int straightStepCost, int diagonalStepCost)
        {
            var straight = Math.Max(straightStepCost, 0);

            if (!isDiagonalMovementEnabled)
            {
                return (straight, straight);
            }

            var diagonal = Math.Max(diagonalStepCost, 0);

            if (diagonal < straight)
            {
                return (diagonal, 0);
            }

            return (straight, (int)Math.Min(diagonal, 2L * straight) - straight);
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
