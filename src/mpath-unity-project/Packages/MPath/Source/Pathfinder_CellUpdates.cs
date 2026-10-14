using System;
using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core
{
    public sealed unsafe partial class Pathfinder
    {
        /// <summary>
        /// Returns the position of <paramref name="coordinate"/> in the cell layout the pathfinder searches:
        /// <c>X * fieldHeight + Y</c> (column-major).
        /// </summary>
        /// <remarks>
        /// In <see cref="Cell"/>[] mode this is where a query expects the cell in the caller's array; every query
        /// puts the array into this order, so writing <c>cells[GetIndex(c)]</c> directly is valid once the array is
        /// in layout (after the first query, or when it was built in this order). The coordinate-based setters
        /// work in any order.
        /// </remarks>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <returns>The layout index of <paramref name="coordinate"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="coordinate"/> is outside the grid.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public int GetIndex(Coordinate coordinate)
        {
            ThrowIfDisposed();
            ThrowIfOutsideGrid(coordinate);

            return GetLayoutIndex(coordinate.X, coordinate.Y);
        }

        /// <summary>
        /// Returns the current cell data at <paramref name="coordinate"/>, read from the grid the pathfinder was
        /// built from: the caller's <see cref="Cell"/>[] or <see cref="Cell"/>[,], or the matching
        /// <see cref="ICellHolder.CellData"/>.
        /// </summary>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <returns>A copy of the cell.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="coordinate"/> is outside the grid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the grid holds no cell with that coordinate.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public Cell GetCell(Coordinate coordinate)
        {
            ThrowIfDisposed();
            ThrowIfOutsideGrid(coordinate);

            switch (_initializationMode)
            {
                case InitializationMode.CellsArray:
                    return _cells[FindArrayIndex(coordinate)];
                case InitializationMode.CellsMatrix:
                    FindMatrixPosition(coordinate, out var x, out var y);
                    return _cellsMatrix[x, y];
                default:
                    return FindHolder(coordinate).CellData;
            }
        }

        /// <summary>
        /// Replaces the walkability, occupancy and weight of the cell at <paramref name="coordinate"/> with those of
        /// <paramref name="cell"/>. The cell keeps its coordinate: <c>cell.Coordinate</c> is ignored.
        /// </summary>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <param name="cell">The new cell values.</param>
        /// <inheritdoc cref="SetWalkable" path="/remarks"/>
        /// <inheritdoc cref="SetWalkable" path="/exception"/>
        public void SetCell(Coordinate coordinate, in Cell cell)
        {
            ref var target = ref GetWritableCell(coordinate);

            UpdateCell(ref target, coordinate, cell.IsWalkable, cell.IsOccupied, cell.Weight);
        }

        /// <summary>
        /// Sets whether the cell at <paramref name="coordinate"/> can be traversed.
        /// </summary>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <param name="isWalkable">The new walkability.</param>
        /// <remarks>
        /// O(1) and allocation-free while the grid is in layout (see <see cref="GetIndex"/>); a <see cref="Cell"/>[]
        /// in another order is searched once per call and never reordered. The change is written to the caller's
        /// <see cref="Cell"/>[] or <see cref="Cell"/>[,], so the caller's grid and the pathfinder stay in sync, and
        /// the next query sees it. When the value changes in a way cached paths depend on, the path cache is
        /// cleared, as by <see cref="InvalidateCache"/>; setting the current value again does nothing. Not
        /// supported by the <see cref="ICellHolder"/> constructors: the holders are the source the pathfinder reads,
        /// so update the holder itself (and call <see cref="Refresh"/> with <see cref="GridSnapshotMode.Manual"/>, and
        /// <see cref="InvalidateCache"/> when caching is enabled).
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="coordinate"/> is outside the grid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the grid holds no cell with that coordinate.</exception>
        /// <exception cref="NotSupportedException">Thrown when the pathfinder was built from <see cref="ICellHolder"/>s.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public void SetWalkable(Coordinate coordinate, bool isWalkable)
        {
            ref var target = ref GetWritableCell(coordinate);

            UpdateCell(ref target, coordinate, isWalkable, target.IsOccupied, target.Weight);
        }

        /// <summary>
        /// Sets whether the cell at <paramref name="coordinate"/> is occupied. Occupancy only affects queries while
        /// <see cref="IPathfinderSettings.IsCalculatingOccupiedCells"/> (or a per-query <see cref="OccupancyRule"/>)
        /// takes it into account.
        /// </summary>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <param name="isOccupied">The new occupancy.</param>
        /// <inheritdoc cref="SetWalkable" path="/remarks"/>
        /// <inheritdoc cref="SetWalkable" path="/exception"/>
        public void SetOccupied(Coordinate coordinate, bool isOccupied)
        {
            ref var target = ref GetWritableCell(coordinate);

            UpdateCell(ref target, coordinate, target.IsWalkable, isOccupied, target.Weight);
        }

        /// <summary>
        /// Sets the weight of the cell at <paramref name="coordinate"/>. Weight only affects queries while
        /// <see cref="IPathfinderSettings.IsCellWeightEnabled"/> is set.
        /// </summary>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <param name="weight">The new weight.</param>
        /// <inheritdoc cref="SetWalkable" path="/remarks"/>
        /// <inheritdoc cref="SetWalkable" path="/exception"/>
        public void SetWeight(Coordinate coordinate, float weight)
        {
            ref var target = ref GetWritableCell(coordinate);

            UpdateCell(ref target, coordinate, target.IsWalkable, target.IsOccupied, weight);
        }

        /// <summary>
        /// Writes the user fields to the source cell (and, in matrix mode, to the snapshot the running or next
        /// query reads), then clears the path cache when a field cached paths depend on changed. Occupancy and
        /// weight only matter to cached paths when the settings enable them, since cached queries always run under
        /// the settings' own rules.
        /// </summary>
        private void UpdateCell(ref Cell target, Coordinate coordinate, bool isWalkable, bool isOccupied, float weight)
        {
            var isCacheAffected = target.IsWalkable != isWalkable
                || (_settings.IsCalculatingOccupiedCells && target.IsOccupied != isOccupied)
                || (_settings.IsCellWeightEnabled && !target.Weight.Equals(weight));

            WriteUserFields(ref target, isWalkable, isOccupied, weight);

            if (_initializationMode == InitializationMode.CellsMatrix)
            {
                WriteUserFields(ref _cells[GetLayoutIndex(coordinate.X, coordinate.Y)], isWalkable, isOccupied, weight);
            }

            if (isCacheAffected && _isPathCachingEnabled)
            {
                _pathCaching?.ClearCache();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUserFields(ref Cell cell, bool isWalkable, bool isOccupied, float weight)
        {
            cell.IsWalkable = isWalkable;
            cell.IsOccupied = isOccupied;
            cell.Weight = weight;
        }

        /// <summary>
        /// The caller-owned cell a setter writes to: the element of the caller's <see cref="Cell"/>[] or
        /// <see cref="Cell"/>[,]. Holder modes have no writable source, since <see cref="ICellHolder.CellData"/> is
        /// read-only.
        /// </summary>
        private ref Cell GetWritableCell(Coordinate coordinate)
        {
            ThrowIfDisposed();
            ThrowIfOutsideGrid(coordinate);

            switch (_initializationMode)
            {
                case InitializationMode.CellsArray:
                    return ref _cells[FindArrayIndex(coordinate)];
                case InitializationMode.CellsMatrix:
                    FindMatrixPosition(coordinate, out var x, out var y);
                    return ref _cellsMatrix[x, y];
                default:
                    throw new NotSupportedException(
                        "Cell holders are the source the pathfinder reads: update the holder instead " +
                        "(and call Refresh with GridSnapshotMode.Manual, and InvalidateCache when path caching is enabled).");
            }
        }

        /// <summary>
        /// The index of <paramref name="coordinate"/> in the caller's <see cref="Cell"/>[]: its layout index when the
        /// array is in layout (every query sorts it so), otherwise a linear search that leaves the order untouched.
        /// </summary>
        private int FindArrayIndex(Coordinate coordinate)
        {
            var index = GetLayoutIndex(coordinate.X, coordinate.Y);

            if (index < _cells.Length && _cells[index].Coordinate == coordinate)
            {
                return index;
            }

            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].Coordinate == coordinate)
                {
                    return i;
                }
            }

            throw MissingCell(coordinate);
        }

        /// <summary>
        /// The position of <paramref name="coordinate"/> in the caller's <see cref="Cell"/>[,]: <c>[X, Y]</c> when
        /// that element carries the coordinate, otherwise a linear search (queries place cells by their own
        /// coordinate, not by their matrix position).
        /// </summary>
        private void FindMatrixPosition(Coordinate coordinate, out int x, out int y)
        {
            if (_cellsMatrix[coordinate.X, coordinate.Y].Coordinate == coordinate)
            {
                x = coordinate.X;
                y = coordinate.Y;
                return;
            }

            for (x = 0; x < Width; x++)
            {
                for (y = 0; y < Height; y++)
                {
                    if (_cellsMatrix[x, y].Coordinate == coordinate)
                    {
                        return;
                    }
                }
            }

            throw MissingCell(coordinate);
        }

        private ICellHolder FindHolder(Coordinate coordinate)
        {
            if (_initializationMode == InitializationMode.CellHoldersMatrix)
            {
                var holder = _cellHoldersMatrix[coordinate.X, coordinate.Y];

                if (holder != null && holder.CellData.Coordinate == coordinate)
                {
                    return holder;
                }

                foreach (var candidate in _cellHoldersMatrix)
                {
                    if (candidate != null && candidate.CellData.Coordinate == coordinate)
                    {
                        return candidate;
                    }
                }

                throw MissingCell(coordinate);
            }

            var index = GetLayoutIndex(coordinate.X, coordinate.Y);

            if (index < _cellHolders.Length && _cellHolders[index] != null &&
                _cellHolders[index].CellData.Coordinate == coordinate)
            {
                return _cellHolders[index];
            }

            foreach (var candidate in _cellHolders)
            {
                if (candidate != null && candidate.CellData.Coordinate == coordinate)
                {
                    return candidate;
                }
            }

            throw MissingCell(coordinate);
        }

        private void ThrowIfOutsideGrid(Coordinate coordinate)
        {
            if (!IsPositionValid(coordinate.X, coordinate.Y))
            {
                throw new ArgumentException($"Coordinate {coordinate} is outside the valid field range",
                    nameof(coordinate));
            }
        }

        private static InvalidOperationException MissingCell(Coordinate coordinate)
        {
            return new InvalidOperationException($"The grid holds no cell with coordinate {coordinate}.");
        }
    }
}
