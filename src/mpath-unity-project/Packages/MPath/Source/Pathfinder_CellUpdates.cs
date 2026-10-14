using System;
using System.Runtime.CompilerServices;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core
{
    public sealed unsafe partial class Pathfinder
    {
        /// <summary>
        /// Returns the index of the cell at <paramref name="coordinate"/>. For a <see cref="Cell"/>[] or
        /// <see cref="ICellHolder"/>[] array it is the cell's index in the caller's array, by
        /// <see cref="IPathfinderSettings.CellLayout"/>: <c>X * fieldHeight + Y</c> (column-major) or
        /// <c>Y * fieldWidth + X</c> (row-major). For a matrix it is the column-major index
        /// <c>X * fieldHeight + Y</c>. It is always the index <see cref="RangeResult.CopyCostsTo"/> writes the cell's
        /// cost at.
        /// </summary>
        /// <remarks>O(1). Queries never reorder the caller's array, so <c>cells[GetIndex(c)]</c> stays valid.</remarks>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <returns>The index of the cell at <paramref name="coordinate"/>.</returns>
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
        /// <see cref="ICellHolder.CellData"/> (a <c>null</c> holder reads as a <c>default</c>, non-walkable cell).
        /// </summary>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <returns>A copy of the cell.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="coordinate"/> is outside the grid.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public Cell GetCell(Coordinate coordinate)
        {
            ThrowIfDisposed();
            ThrowIfOutsideGrid(coordinate);

            ICellHolder holder;

            switch (_initializationMode)
            {
                case InitializationMode.CellsArray:
                    return _cells[GetLayoutIndex(coordinate.X, coordinate.Y)];
                case InitializationMode.CellsMatrix:
                    return _cellsMatrix[coordinate.X, coordinate.Y];
                case InitializationMode.CellHoldersMatrix:
                    holder = _cellHoldersMatrix[coordinate.X, coordinate.Y];
                    break;
                default:
                    holder = _cellHolders[GetLayoutIndex(coordinate.X, coordinate.Y)];
                    break;
            }

            return holder == null ? default : holder.CellData;
        }

        /// <summary>
        /// Replaces the walkability, occupancy and weight of the cell at <paramref name="coordinate"/> with those of
        /// <paramref name="cell"/>.
        /// </summary>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <param name="cell">The new cell values. Its <see cref="Cell.Weight"/> must not be negative.</param>
        /// <inheritdoc cref="SetWalkable" path="/remarks"/>
        /// <inheritdoc cref="SetWalkable" path="/exception"/>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell's weight is negative.</exception>
        public void SetCell(Coordinate coordinate, in Cell cell)
        {
            ThrowIfNegativeWeight(cell.Weight, nameof(cell));

            ref var target = ref GetWritableCell(coordinate);

            UpdateCell(ref target, coordinate, cell.IsWalkable, cell.IsOccupied, cell.Weight);
        }

        /// <summary>
        /// Sets whether the cell at <paramref name="coordinate"/> can be traversed.
        /// </summary>
        /// <param name="coordinate">A coordinate inside the grid.</param>
        /// <param name="isWalkable">The new walkability.</param>
        /// <remarks>
        /// O(1) and allocation-free. The caller's grid is never reordered. The change is written to the caller's
        /// <see cref="Cell"/>[] or <see cref="Cell"/>[,], so the caller's grid and the pathfinder stay in sync, and
        /// the next query sees it. When the value changes in a way cached paths depend on, the path cache is
        /// cleared, as by <see cref="InvalidateCache"/>; setting the current value again does nothing. Not
        /// supported by the <see cref="ICellHolder"/> constructors: the holders are the source the pathfinder reads,
        /// so update the holder itself (and call <see cref="Refresh"/> with <see cref="GridSnapshotMode.Manual"/>, and
        /// <see cref="InvalidateCache"/> when caching is enabled).
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="coordinate"/> is outside the grid.</exception>
        /// <exception cref="NotSupportedException">Thrown when the pathfinder was built from <see cref="ICellHolder"/>s.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the pathfinder has been disposed.</exception>
        public void SetWalkable(Coordinate coordinate, bool isWalkable)
        {
            ref var target = ref GetWritableCell(coordinate);

            UpdateCell(ref target, coordinate, isWalkable, target.IsOccupied, target.Weight);
        }

        /// <summary>
        /// Sets whether the cell at <paramref name="coordinate"/> is occupied. Occupancy only affects queries while
        /// <see cref="IPathfinderSettings.AreOccupiedCellsBlocking"/> (or a per-query <see cref="OccupancyRule"/>)
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
        /// <param name="weight">The new weight: 0 or more, in the same units as the step costs.</param>
        /// <inheritdoc cref="SetWalkable" path="/remarks"/>
        /// <inheritdoc cref="SetWalkable" path="/exception"/>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="weight"/> is negative.</exception>
        public void SetWeight(Coordinate coordinate, int weight)
        {
            ThrowIfNegativeWeight(weight, nameof(weight));

            ref var target = ref GetWritableCell(coordinate);

            UpdateCell(ref target, coordinate, target.IsWalkable, target.IsOccupied, weight);
        }

        /// <summary>
        /// Writes the user fields to the source cell (and, in matrix mode, to the snapshot the running or next query
        /// reads), then clears the path cache when a field cached paths depend on changed. Occupancy and
        /// weight only matter to cached paths when the settings enable them, since cached queries always run under
        /// the settings' own rules.
        /// </summary>
        private void UpdateCell(ref Cell target, Coordinate coordinate, bool isWalkable, bool isOccupied, int weight)
        {
            var isCacheAffected = target.IsWalkable != isWalkable
                || (_settings.AreOccupiedCellsBlocking && target.IsOccupied != isOccupied)
                || (_settings.IsCellWeightEnabled && target.Weight != weight);

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

        private static void ThrowIfNegativeWeight(int weight, string paramName)
        {
            if (weight < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, weight, "A cell weight must not be negative.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUserFields(ref Cell cell, bool isWalkable, bool isOccupied, int weight)
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
                    return ref _cells[GetLayoutIndex(coordinate.X, coordinate.Y)];
                case InitializationMode.CellsMatrix:
                    return ref _cellsMatrix[coordinate.X, coordinate.Y];
                default:
                    throw new NotSupportedException(
                        "Cell holders are the source the pathfinder reads: update the holder instead " +
                        "(and call Refresh with GridSnapshotMode.Manual, and InvalidateCache when path caching is enabled).");
            }
        }

        private void ThrowIfOutsideGrid(Coordinate coordinate)
        {
            if (!IsPositionValid(coordinate.X, coordinate.Y))
            {
                throw new ArgumentException($"Coordinate {coordinate} is outside the valid field range",
                    nameof(coordinate));
            }
        }
    }
}
