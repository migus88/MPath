using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Internal
{
    /// <summary>
    /// The occupancy rules of the running query: the settings' defaults, or a <see cref="QueryOptions"/>
    /// resolved against them. Holds a pointer to the caller's always-enterable cells, which is only valid while
    /// the query that set it is running.
    /// </summary>
    internal readonly unsafe struct QueryRules
    {
        public readonly bool AreOccupiedCellsBlocking;

        private readonly Coordinate* _enterableCells;
        private readonly int _enterableCellCount;
        private readonly bool _isDestinationEnterable;
        private readonly Coordinate _destination;

        private QueryRules(bool areOccupiedCellsBlocking, Coordinate* enterableCells, int enterableCellCount,
            bool isDestinationEnterable, Coordinate destination)
        {
            AreOccupiedCellsBlocking = areOccupiedCellsBlocking;
            _enterableCells = enterableCells;
            _enterableCellCount = enterableCellCount;
            _isDestinationEnterable = isDestinationEnterable;
            _destination = destination;
        }

        /// <summary>
        /// Whether these rules behave exactly like the settings' defaults (so, for example, cached paths stay valid).
        /// </summary>
        public bool IsDefault(in FastPathfinderSettings settings)
        {
            return AreOccupiedCellsBlocking == settings.AreOccupiedCellsBlocking
                && (!AreOccupiedCellsBlocking || (_enterableCellCount == 0 && !_isDestinationEnterable));
        }

        public static QueryRules FromSettings(in FastPathfinderSettings settings)
        {
            return new QueryRules(settings.AreOccupiedCellsBlocking, null, 0, false, default);
        }

        /// <param name="enterableCells">The pinned <see cref="QueryOptions.AlwaysEnterable"/> cells.</param>
        /// <param name="enterableCellCount">The number of <paramref name="enterableCells"/>.</param>
        /// <param name="destination">The query's destination, or <c>null</c> for a query without one.</param>
        public static QueryRules FromOptions(in FastPathfinderSettings settings, OccupancyRule occupancy,
            Coordinate* enterableCells, int enterableCellCount, bool isDestinationEnterable, Coordinate? destination)
        {
            var areOccupiedCellsBlocking = occupancy switch
            {
                OccupancyRule.Block => true,
                OccupancyRule.Ignore => false,
                _ => settings.AreOccupiedCellsBlocking
            };

            return new QueryRules(areOccupiedCellsBlocking, enterableCells, enterableCellCount,
                isDestinationEnterable && destination.HasValue, destination ?? default);
        }

        /// <summary>
        /// Whether the occupied cell at (<paramref name="x"/>, <paramref name="y"/>) may be treated as free.
        /// Called for occupied cells only, so free cells never pay for the scan.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsAlwaysEnterable(int x, int y)
        {
            if (_isDestinationEnterable && _destination.X == x && _destination.Y == y)
            {
                return true;
            }

            for (var i = 0; i < _enterableCellCount; i++)
            {
                var cell = _enterableCells + i;
                if (cell->X == x && cell->Y == y)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
