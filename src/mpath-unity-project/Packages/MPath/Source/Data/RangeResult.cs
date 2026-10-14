using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;

namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Represents the result of a movement-range (reachability) query.
    /// Contains every cell whose cheapest path cost from the origin is within the supplied budget.
    /// Implements <see cref="IDisposable"/> to release the backing array to the array pool.
    /// </summary>
    public sealed class RangeResult : IDisposable
    {
        /// <summary>
        /// A result from <see cref="Create"/> uses the lookup index only when its bounding box spans no more entries
        /// than this many per reached cell (or <see cref="MinIndexAreaLimit"/>), so a sparse hand-made result never
        /// rents a huge map.
        /// </summary>
        private const int MaxIndexAreaPerCell = 64;
        private const int MinIndexAreaLimit = 4096;

        private static readonly RangeResult EmptyResult = new(null, 0, false);

        /// <summary>
        /// Gets a value indicating whether the query produced at least one reachable cell.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the number of reachable cells in the result.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// The reachable cells, each paired with the cost of the cheapest path to it, as a sequence.
        /// <c>foreach (var c in range.Cells)</c> uses the same struct <see cref="Enumerator"/> as
        /// <c>foreach (var c in range)</c> and allocates nothing; as an <see cref="IEnumerable{T}"/> (LINQ, an
        /// <c>IEnumerable&lt;ReachableCell&gt;</c> variable) it is boxed.
        /// </summary>
        /// <remarks>Enumerating a disposed result throws <see cref="ObjectDisposedException"/>.</remarks>
        public CellEnumerable Cells => new CellEnumerable(this);

        /// <summary>
        /// Gets the reachable cell at the specified index, like <see cref="Get"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the reachable cell to get.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public ReachableCell this[int index]
        {
            get
            {
                ThrowIfDisposed();

                if ((uint)index >= (uint)Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _cells[index];
            }
        }

        private readonly ReachableCell[] _cells;

        // The grid the result came from, set only for pathfinder results: CopyCostsTo writes cell (x, y) at
        // x * _strideX + y * _strideY, the pathfinder's layout index, into the first _gridSize entries.
        private readonly int _gridSize;
        private readonly int _strideX;
        private readonly int _strideY;

        // The lookup index, built on the first lookup: the bounding box of the reached cells, and for each box cell
        // (row-major from _minX/_minY) the result index of that cell. The map is rented and never cleared: an entry
        // counts only when it is a valid result index whose cell has that coordinate.
        private int[] _indexByCell;
        private int _minX;
        private int _minY;
        private int _boxWidth;
        private int _boxHeight;
        private bool _isIndexBuilt;
        private bool _isDisposed;

        private RangeResult(ReachableCell[] cells, int length, bool isSuccess, int gridSize = 0, int strideX = 0,
            int strideY = 0)
        {
            _cells = cells;
            Length = length;
            IsSuccess = isSuccess;
            _gridSize = gridSize;
            _strideX = strideX;
            _strideY = strideY;
            _isDisposed = false;
        }

        /// <summary>
        /// Creates a successful range result backed by the specified array.
        /// </summary>
        /// <param name="cells">The array of reachable cells.</param>
        /// <param name="length">The number of valid entries in the array.</param>
        /// <returns>A new <see cref="RangeResult"/> wrapping the supplied cells.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="length"/> is negative or greater than the array length.</exception>
        public static RangeResult Create(ReachableCell[] cells, int length)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (length < 0 || length > cells.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return new RangeResult(cells, length, length > 0);
        }

        /// <summary>
        /// Creates the result of a pathfinder query, which knows its grid's layout so <see cref="CopyCostsTo"/> can
        /// write a per-cell field.
        /// </summary>
        /// <param name="cells">The pooled array of reachable cells.</param>
        /// <param name="length">The number of valid entries in the array.</param>
        /// <param name="gridSize">The number of cells in the grid.</param>
        /// <param name="strideX">How far the layout index moves per step in X.</param>
        /// <param name="strideY">How far the layout index moves per step in Y.</param>
        internal static RangeResult Create(ReachableCell[] cells, int length, int gridSize, int strideX, int strideY)
        {
            return new RangeResult(cells, length, length > 0, gridSize, strideX, strideY);
        }

        /// <summary>
        /// Returns an empty range result (no reachable cells).
        /// </summary>
        /// <returns>A shared empty <see cref="RangeResult"/>.</returns>
        public static RangeResult Empty()
        {
            return EmptyResult;
        }

        /// <summary>
        /// Gets the reachable cell at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the reachable cell to get.</param>
        /// <returns>The reachable cell at the specified index.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is out of range.</exception>
        public ReachableCell Get(int index)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RangeResult));
            }

            if (index < 0 || index >= Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _cells[index];
        }

        /// <summary>
        /// Determines whether the specified coordinate is reachable within the budget, in O(1) (see
        /// <see cref="TryGetCell"/>).
        /// </summary>
        /// <param name="coordinate">The coordinate to look for.</param>
        /// <returns><c>true</c> if the coordinate is reachable; otherwise, <c>false</c>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        public bool Contains(Coordinate coordinate)
        {
            return TryGetCost(coordinate, out _);
        }

        /// <summary>
        /// Gets the cheapest path cost to the specified coordinate, if it is reachable, in O(1) (see
        /// <see cref="TryGetCell"/>).
        /// </summary>
        /// <param name="coordinate">The coordinate to look for.</param>
        /// <param name="cost">When this method returns, contains the cheapest path cost if reachable; otherwise, zero.</param>
        /// <returns><c>true</c> if the coordinate is reachable; otherwise, <c>false</c>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        public bool TryGetCost(Coordinate coordinate, out int cost)
        {
            var isReachable = TryGetCell(coordinate, out var cell);
            cost = cell.Cost;
            return isReachable;
        }

        /// <summary>
        /// Gets the reachable cell (cost and step counts) at the specified coordinate, if it is reachable.
        /// </summary>
        /// <remarks>
        /// The first lookup builds an index over the reached cells in O(<see cref="Length"/>), rented from the array
        /// pool and sized to their bounding box (at most the grid); every lookup after it is O(1) and allocates
        /// nothing. The index belongs to the result: it stays valid after later queries and after the pathfinder is
        /// disposed, and is returned to the pool by <see cref="Dispose"/>. A result from <see cref="Create"/> whose
        /// cells are spread over a box much larger than their count is searched linearly instead.
        /// </remarks>
        /// <param name="coordinate">The coordinate to look for.</param>
        /// <param name="cell">When this method returns, contains the reachable cell if reachable; otherwise, the
        /// default value.</param>
        /// <returns><c>true</c> if the coordinate is reachable; otherwise, <c>false</c>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        public bool TryGetCell(Coordinate coordinate, out ReachableCell cell)
        {
            ThrowIfDisposed();

            if (!_isIndexBuilt)
            {
                BuildIndex();
            }

            int index;

            if (_indexByCell != null)
            {
                var x = coordinate.X - _minX;
                var y = coordinate.Y - _minY;

                if ((uint)x < (uint)_boxWidth && (uint)y < (uint)_boxHeight)
                {
                    index = _indexByCell[y * _boxWidth + x];

                    if ((uint)index < (uint)Length && _cells[index].Coordinate == coordinate)
                    {
                        cell = _cells[index];
                        return true;
                    }
                }

                cell = default;
                return false;
            }

            for (index = 0; index < Length; index++)
            {
                if (_cells[index].Coordinate != coordinate)
                {
                    continue;
                }

                cell = _cells[index];
                return true;
            }

            cell = default;
            return false;
        }

        /// <summary>
        /// Returns the reachable cells as a span over the pooled array, without copying. Empty for an empty result.
        /// </summary>
        /// <remarks>The span is only valid until the result is disposed: the array then goes back to the pool and
        /// is reused by later queries.</remarks>
        /// <returns>The reachable cells, in the order the search settled them (the origin first).</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        public ReadOnlySpan<ReachableCell> AsSpan()
        {
            ThrowIfDisposed();

            return _cells == null
                ? ReadOnlySpan<ReachableCell>.Empty
                : new ReadOnlySpan<ReachableCell>(_cells, 0, Length);
        }

        /// <summary>
        /// Returns an allocation-free enumerator over the reachable cells, so <c>foreach (var c in range)</c>
        /// allocates nothing.
        /// </summary>
        /// <remarks>Do not dispose the result while enumerating it.</remarks>
        /// <returns>An enumerator over the reachable cells.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        public Enumerator GetEnumerator()
        {
            ThrowIfDisposed();

            return new Enumerator(_cells, Length);
        }

        /// <summary>
        /// Writes the result into a dense per-cell cost field: the cost of each reachable cell at its layout index
        /// (<see cref="Pathfinder.GetIndex"/>), and <paramref name="unreached"/> in every other entry of
        /// <paramref name="costByCellIndex"/>. For a pathfinder over an array the field is in the array's own
        /// <see cref="CellLayout"/>, for one over a matrix it is column-major (<c>X * fieldHeight + Y</c>).
        /// </summary>
        /// <param name="costByCellIndex">The field to fill, at least as long as the grid has cells.</param>
        /// <param name="unreached">The value written for the cells that are not reachable.</param>
        /// <returns>The number of costs written (<see cref="Length"/>).</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="costByCellIndex"/> is shorter than the
        /// grid.</exception>
        /// <exception cref="InvalidOperationException">Thrown for a non-empty result made with
        /// <see cref="Create"/>, which does not know a grid layout.</exception>
        public int CopyCostsTo(Span<int> costByCellIndex, int unreached = -1)
        {
            ThrowIfDisposed();

            if (Length > 0 && _gridSize == 0)
            {
                throw new InvalidOperationException(
                    "Only a result returned by a Pathfinder knows the grid layout to copy costs into");
            }

            if (costByCellIndex.Length < _gridSize)
            {
                throw new ArgumentException("The span is shorter than the number of cells in the grid",
                    nameof(costByCellIndex));
            }

            costByCellIndex.Fill(unreached);

            for (var i = 0; i < Length; i++)
            {
                ref readonly var cell = ref _cells[i];
                var coordinate = cell.Coordinate;
                costByCellIndex[coordinate.X * _strideX + coordinate.Y * _strideY] = cell.Cost;
            }

            return Length;
        }

        /// <summary>
        /// Releases the resources used by the <see cref="RangeResult"/>.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed || _cells == null)
            {
                return;
            }

            ArrayPool<ReachableCell>.Shared.Return(_cells);

            if (_indexByCell != null)
            {
                ArrayPool<int>.Shared.Return(_indexByCell);
                _indexByCell = null;
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Builds the lookup index over the reached cells, or leaves it out (linear lookups) when there are none or
        /// their bounding box is too sparse.
        /// </summary>
        private void BuildIndex()
        {
            _isIndexBuilt = true;

            if (Length == 0 || _cells == null)
            {
                return;
            }

            var first = _cells[0].Coordinate;
            int minX = first.X, maxX = first.X, minY = first.Y, maxY = first.Y;

            for (var i = 1; i < Length; i++)
            {
                var coordinate = _cells[i].Coordinate;
                minX = Math.Min(minX, coordinate.X);
                maxX = Math.Max(maxX, coordinate.X);
                minY = Math.Min(minY, coordinate.Y);
                maxY = Math.Max(maxY, coordinate.Y);
            }

            var boxWidth = (long)maxX - minX + 1;
            var boxHeight = (long)maxY - minY + 1;
            var area = boxWidth * boxHeight;
            var areaLimit = _gridSize > 0 ? _gridSize : Math.Max((long)Length * MaxIndexAreaPerCell, MinIndexAreaLimit);

            if (area > areaLimit)
            {
                return;
            }

            _minX = minX;
            _minY = minY;
            _boxWidth = (int)boxWidth;
            _boxHeight = (int)boxHeight;
            _indexByCell = ArrayPool<int>.Shared.Rent((int)area);

            for (var i = 0; i < Length; i++)
            {
                var coordinate = _cells[i].Coordinate;
                _indexByCell[(coordinate.Y - minY) * _boxWidth + (coordinate.X - minX)] = i;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RangeResult));
            }
        }

        /// <summary>
        /// Enumerates the cells of a <see cref="RangeResult"/> without allocating. Returned by
        /// <see cref="RangeResult.GetEnumerator"/>; not meant to be used directly.
        /// </summary>
        public struct Enumerator : IEnumerator<ReachableCell>
        {
            private readonly ReachableCell[] _cells;
            private readonly int _length;
            private int _index;

            internal Enumerator(ReachableCell[] cells, int length)
            {
                _cells = cells;
                _length = cells == null ? 0 : length;
                _index = -1;
            }

            /// <summary>
            /// Gets the reachable cell at the current position.
            /// </summary>
            public ReachableCell Current => _cells[_index];

            object IEnumerator.Current => Current;

            /// <summary>
            /// Advances to the next reachable cell.
            /// </summary>
            /// <returns><c>true</c> while there is a cell to read.</returns>
            public bool MoveNext()
            {
                return ++_index < _length;
            }

            void IEnumerator.Reset()
            {
                _index = -1;
            }

            void IDisposable.Dispose()
            {
            }
        }

        /// <summary>
        /// The cells of a <see cref="RangeResult"/> as a sequence, returned by <see cref="RangeResult.Cells"/>.
        /// <c>foreach</c> over it allocates nothing; used as an <see cref="IEnumerable{T}"/> it is boxed.
        /// </summary>
        public readonly struct CellEnumerable : IEnumerable<ReachableCell>
        {
            private readonly RangeResult _result;

            internal CellEnumerable(RangeResult result)
            {
                _result = result;
            }

            /// <summary>
            /// Returns an allocation-free enumerator over the cells, like <see cref="RangeResult.GetEnumerator"/>.
            /// </summary>
            /// <returns>An enumerator over the reachable cells; empty for a <c>default</c> instance.</returns>
            /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
            public Enumerator GetEnumerator()
            {
                return _result == null ? default : _result.GetEnumerator();
            }

            IEnumerator<ReachableCell> IEnumerable<ReachableCell>.GetEnumerator()
            {
                return GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
