using System;
using System.Buffers;
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
        /// Enumerates the reachable cells, each paired with the cost of the cheapest path to it.
        /// </summary>
        public IEnumerable<ReachableCell> Cells
        {
            get
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(nameof(RangeResult));
                }

                if (Length <= 0 || _cells == null || _cells.Length < Length)
                {
                    yield break;
                }

                for (var i = 0; i < Length; i++)
                {
                    yield return _cells[i];
                }
            }
        }

        private readonly ReachableCell[] _cells;
        private bool _isDisposed;

        private RangeResult(ReachableCell[] cells, int length, bool isSuccess)
        {
            _cells = cells;
            Length = length;
            IsSuccess = isSuccess;
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
        /// Determines whether the specified coordinate is reachable within the budget.
        /// Performs a linear scan; for repeated lookups consider building a set from <see cref="Cells"/>.
        /// </summary>
        /// <param name="coordinate">The coordinate to look for.</param>
        /// <returns><c>true</c> if the coordinate is reachable; otherwise, <c>false</c>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        public bool Contains(Coordinate coordinate)
        {
            return TryGetCost(coordinate, out _);
        }

        /// <summary>
        /// Gets the cheapest path cost to the specified coordinate, if it is reachable.
        /// Performs a linear scan; for repeated lookups consider building a dictionary from <see cref="Cells"/>.
        /// </summary>
        /// <param name="coordinate">The coordinate to look for.</param>
        /// <param name="cost">When this method returns, contains the cheapest path cost if reachable; otherwise, zero.</param>
        /// <returns><c>true</c> if the coordinate is reachable; otherwise, <c>false</c>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the result has been disposed.</exception>
        public bool TryGetCost(Coordinate coordinate, out float cost)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RangeResult));
            }

            if (_cells != null)
            {
                for (var i = 0; i < Length; i++)
                {
                    if (_cells[i].Coordinate != coordinate)
                    {
                        continue;
                    }

                    cost = _cells[i].Cost;
                    return true;
                }
            }

            cost = 0f;
            return false;
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
            _isDisposed = true;
        }
    }
}
