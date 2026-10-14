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
        /// Fixed-point cost keys count cost in units of 1 / <see cref="CostKeyScale"/>: four decimal places, so the
        /// values people write (1.41, 0.25, 2.5) are exact and a budget of three 1.41 steps is exactly 4.23.
        /// A float holds about 7 significant digits, so that is exact for values below 1000.
        /// </summary>
        public const double CostKeyScale = 10000.0;

        /// <summary>
        /// The largest cost of one step: larger multipliers and weights are clamped to it, so a route of up to 2^25
        /// steps can never overflow a <see cref="long"/>.
        /// </summary>
        public const float MaxStepCost = 16777216f;

        /// <summary>
        /// The relative precision of a float (2^-23): a budget admits the routes within it, since the budget itself
        /// was rounded to a float.
        /// </summary>
        private const double FloatPrecision = 1.0 / (1 << 23);

        private const long MaxStepCostKey = (long)(MaxStepCost * CostKeyScale);

        /// <summary>
        /// The largest budget key: any larger budget is effectively unbounded.
        /// </summary>
        private const long MaxBudgetKey = 1L << 62;

        public readonly bool IsDiagonalMovementEnabled;
        public readonly bool IsCalculatingOccupiedCells;
        public readonly CornerBlocking CornerBlocking;
        public readonly bool IsCellWeightEnabled;
        public readonly PathSmoothingMethod PathSmoothingMethod;
        public readonly float StraightMovementMultiplier;
        public readonly float DiagonalMovementMultiplier;
        public readonly GridSnapshotMode GridSnapshotMode;
        public readonly TieBreaking TieBreaking;

        /// <summary>
        /// Whether costs are compared as exact integer keys with a secondary tie-break key
        /// (any <see cref="TieBreaking"/> but <see cref="Data.TieBreaking.None"/>).
        /// </summary>
        public readonly bool IsTieBreaking;

        /// <summary>
        /// <see cref="StraightMovementMultiplier"/> as a cost key (see <see cref="ToCostKey"/>).
        /// </summary>
        public readonly long StraightCostKey;

        /// <summary>
        /// <see cref="DiagonalMovementMultiplier"/> as a cost key (see <see cref="ToCostKey"/>).
        /// </summary>
        public readonly long DiagonalCostKey;

        /// <summary>
        /// <see cref="HeuristicMajorAxisCost"/> for cost keys, derived from the rounded step keys so it stays exact.
        /// </summary>
        public readonly long HeuristicMajorAxisKey;

        /// <summary>
        /// <see cref="HeuristicMinorAxisCost"/> for cost keys, derived from the rounded step keys so it stays exact.
        /// </summary>
        public readonly long HeuristicMinorAxisKey;
        public readonly CellLayout CellLayout;

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
            float straightMovementMultiplier, float diagonalMovementMultiplier, GridSnapshotMode gridSnapshotMode,
            TieBreaking tieBreaking, CellLayout cellLayout)
        {
            IsDiagonalMovementEnabled = isDiagonalMovementEnabled;
            IsCalculatingOccupiedCells = isCalculatingOccupiedCells;
            CornerBlocking = cornerBlocking;
            IsCellWeightEnabled = isCellWeightEnabled;
            PathSmoothingMethod = pathSmoothingMethod;
            StraightMovementMultiplier = straightMovementMultiplier;
            DiagonalMovementMultiplier = diagonalMovementMultiplier;
            GridSnapshotMode = gridSnapshotMode;
            CellLayout = cellLayout;
            (HeuristicMajorAxisCost, HeuristicMinorAxisCost) = GetHeuristicAxisCosts(isDiagonalMovementEnabled,
                straightMovementMultiplier, diagonalMovementMultiplier);
            _cornerBlockingTable = BuildCornerBlockingTable(cornerBlocking);
            TieBreaking = tieBreaking;
            IsTieBreaking = tieBreaking != TieBreaking.None;
            StraightCostKey = ToCostKey(straightMovementMultiplier);
            DiagonalCostKey = ToCostKey(diagonalMovementMultiplier);
            (HeuristicMajorAxisKey, HeuristicMinorAxisKey) = GetHeuristicAxisKeys(isDiagonalMovementEnabled,
                StraightCostKey, DiagonalCostKey);
        }

        public static FastPathfinderSettings FromSettings(IPathfinderSettings settings)
        {
            return new FastPathfinderSettings(settings.IsDiagonalMovementEnabled, settings.IsCalculatingOccupiedCells,
                settings.CornerBlocking, settings.IsCellWeightEnabled, settings.PathSmoothingMethod,
                settings.StraightMovementMultiplier, settings.DiagonalMovementMultiplier, settings.GridSnapshotMode,
                settings.TieBreaking, settings.CellLayout);
        }

        /// <summary>
        /// Converts a step cost (a multiplier or a cell weight) to an exact fixed-point key: the nearest multiple of
        /// 1 / <see cref="CostKeyScale"/>, clamped to +-<see cref="MaxStepCost"/> (NaN counts as the maximum). Keys
        /// add without rounding, so equal-cost routes tie whatever order their steps are summed in.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToCostKey(float cost)
        {
            var scaled = Math.Round(cost * CostKeyScale);

            if (!(scaled < MaxStepCostKey))
            {
                return MaxStepCostKey;
            }

            return scaled > -MaxStepCostKey ? (long)scaled : -MaxStepCostKey;
        }

        /// <summary>
        /// Converts a movement budget to a key, rounded like <see cref="ToCostKey"/> and widened by the float's own
        /// precision, so a budget equal to a route's cost admits that route even when the budget's float is a hair
        /// below the decimal it was written as.
        /// </summary>
        public static long ToBudgetKey(float budget)
        {
            var scaled = budget * CostKeyScale;
            scaled = Math.Round(scaled + Math.Abs(scaled) * FloatPrecision);

            if (!(scaled < MaxBudgetKey))
            {
                return MaxBudgetKey;
            }

            return scaled > 0 ? (long)scaled : 0;
        }

        /// <summary>
        /// Converts a cost key back to a cost.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToCost(long costKey)
        {
            return (float)(costKey / CostKeyScale);
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

        /// <summary>
        /// <see cref="GetHeuristicAxisCosts"/> over integer step keys, so the heuristic is exact and admissible for
        /// the keys the search adds up.
        /// </summary>
        private static (long major, long minor) GetHeuristicAxisKeys(bool isDiagonalMovementEnabled,
            long straightKey, long diagonalKey)
        {
            var straight = Math.Max(straightKey, 0L);

            if (!isDiagonalMovementEnabled)
            {
                return (straight, straight);
            }

            var diagonal = Math.Max(diagonalKey, 0L);

            if (diagonal < straight)
            {
                return (diagonal, 0L);
            }

            return (straight, Math.Min(diagonal, 2L * straight) - straight);
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
