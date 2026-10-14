using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// The search state lives in the pathfinder and is reset lazily (a generation stamp per query), and the matrix and
    /// holder snapshots follow <see cref="GridSnapshotMode"/>. Leftover state from earlier queries must never leak
    /// into a later one, a search must not write into the caller's grid, and a <see cref="GridSnapshotMode.Manual"/>
    /// snapshot changes only through the cell-update methods or <see cref="Pathfinder.Refresh"/>.
    /// </summary>
    [TestFixture]
    public class PathfinderSearchStateTests
    {
        private const int Width = 24;
        private const int Height = 18;
        private const int QueryCount = 60;

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void MixedQueries_OnOnePathfinder_MatchFreshPathfinders(int seed)
        {
            // Arrange
            var random = new Random(seed);
            var grid = CreateRandomGrid(random);
            using var shared = new Pathfinder(grid);

            for (var i = 0; i < QueryCount; i++)
            {
                // Act + Assert
                AssertSameAsFresh(shared, grid, random, i % 3);
            }
        }

        [Test]
        public void GenerationWrapAround_NodesStampedBeforeTheWrap_DoNotLeakIntoLaterQueries()
        {
            // Arrange
            var random = new Random(7);
            var grid = CreateRandomGrid(random);
            using var shared = new Pathfinder(grid);

            // Stamp nodes with generation 1, the first one used again after the wrap.
            using (shared.GetReachable(new Coordinate(Width / 2, Height / 2), int.MaxValue))
            {
            }

            shared.SearchGeneration = uint.MaxValue - 1;

            for (var i = 0; i < 6; i++)
            {
                // Act + Assert
                AssertSameAsFresh(shared, grid, random, i % 3);
            }

            shared.SearchGeneration.Should().BeLessThan(10);
        }

        [Test]
        public void Queries_InArrayMode_DoNotWriteIntoTheCallerCells()
        {
            // Arrange
            var random = new Random(11);
            var grid = CreateRandomGrid(random);
            var cells = new Cell[Width * Height];

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    cells[x * Height + y] = grid[x, y];
                }
            }

            var before = (Cell[])cells.Clone();
            using var pathfinder = new Pathfinder(cells, Width, Height);

            // Act
            using (pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(Width - 1, Height - 1)))
            {
            }

            using (pathfinder.GetReachable(new Coordinate(Width / 2, Height / 2), 1000))
            {
            }

            using (var search = pathfinder.BeginStepwiseSearch(new Coordinate(0, 0), new Coordinate(Width - 1, 0)))
            {
                search.RunToCompletion();
            }

            // Assert
            cells.Should().Equal(before);
        }

        [TestCase(SnapshotSource.Matrix)]
        [TestCase(SnapshotSource.HolderArray)]
        [TestCase(SnapshotSource.HolderMatrix)]
        public void ManualSnapshot_DirectEdit_IsSeenOnlyAfterRefresh(SnapshotSource source)
        {
            // Arrange
            var grid = new EditableGrid(source);
            using var pathfinder = grid.CreatePathfinder(new PathfinderSettings
            {
                GridSnapshotMode = GridSnapshotMode.Manual
            });
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, Height - 1);

            using (pathfinder.GetPath(from, to))
            {
            }

            grid.SetWalkable(to, false);

            // Act
            bool isSuccessBeforeRefresh;
            using (var result = pathfinder.GetPath(from, to))
            {
                isSuccessBeforeRefresh = result.IsSuccess;
            }

            pathfinder.Refresh();

            bool isSuccessAfterRefresh;
            using (var result = pathfinder.GetPath(from, to))
            {
                isSuccessAfterRefresh = result.IsSuccess;
            }

            // Assert
            isSuccessBeforeRefresh.Should().BeTrue();
            isSuccessAfterRefresh.Should().BeFalse();
        }

        [Test]
        public void ManualSnapshot_CellUpdateMethod_IsSeenByTheNextQuery()
        {
            // Arrange
            var grid = new EditableGrid(SnapshotSource.Matrix);
            using var pathfinder = grid.CreatePathfinder(new PathfinderSettings
            {
                GridSnapshotMode = GridSnapshotMode.Manual
            });
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, Height - 1);

            using (pathfinder.GetPath(from, to))
            {
            }

            // Act
            pathfinder.SetWalkable(to, false);

            // Assert
            using var result = pathfinder.GetPath(from, to);
            result.IsSuccess.Should().BeFalse();
        }

        [Test]
        public void ManualSnapshot_EditBeforeTheFirstQuery_IsSeenWithoutRefresh()
        {
            // Arrange
            var grid = new EditableGrid(SnapshotSource.HolderArray);
            using var pathfinder = grid.CreatePathfinder(new PathfinderSettings
            {
                GridSnapshotMode = GridSnapshotMode.Manual
            });
            var to = new Coordinate(Width - 1, Height - 1);

            // Act
            grid.SetWalkable(to, false);

            // Assert
            using var result = pathfinder.GetPath(new Coordinate(0, 0), to);
            result.IsSuccess.Should().BeFalse();
        }

        [TestCase(SnapshotSource.Matrix)]
        [TestCase(SnapshotSource.HolderArray)]
        public void DefaultSettings_DirectEdit_IsSeenByTheNextQuery(SnapshotSource source)
        {
            // Arrange
            var grid = new EditableGrid(source);
            using var pathfinder = grid.CreatePathfinder(new PathfinderSettings());
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, Height - 1);

            using (pathfinder.GetPath(from, to))
            {
            }

            // Act
            grid.SetWalkable(to, false);

            // Assert
            using var result = pathfinder.GetPath(from, to);
            result.IsSuccess.Should().BeFalse();
        }

        [Test]
        public void Refresh_AfterDispose_Throws()
        {
            // Arrange
            var pathfinder = new EditableGrid(SnapshotSource.Matrix).CreatePathfinder(new PathfinderSettings());
            pathfinder.Dispose();

            // Act
            var action = () => pathfinder.Refresh();

            // Assert
            action.Should().Throw<ObjectDisposedException>();
        }

        /// <summary>
        /// Runs one query of the given kind (0 = path, 1 = reachability, 2 = stepwise) with random arguments on
        /// <paramref name="shared"/> and on a fresh pathfinder over the same grid, and asserts identical results.
        /// </summary>
        private static void AssertSameAsFresh(Pathfinder shared, Cell[,] grid, Random random, int kind)
        {
            var from = RandomCoordinate(random);
            var to = RandomCoordinate(random);
            using var fresh = new Pathfinder(grid);

            switch (kind)
            {
                case 0:
                {
                    using var expected = fresh.GetPath(from, to);
                    using var actual = shared.GetPath(from, to);

                    actual.IsSuccess.Should().Be(expected.IsSuccess);
                    actual.Path.Should().Equal(expected.Path);
                    break;
                }
                case 1:
                {
                    var budget = random.Next(1, 30);
                    using var expected = fresh.GetReachable(from, budget);
                    using var actual = shared.GetReachable(from, budget);

                    ToDictionary(actual).Should().Equal(ToDictionary(expected));
                    break;
                }
                default:
                {
                    SearchStep expected;
                    using (var search = fresh.BeginStepwiseSearch(from, to))
                    {
                        expected = search.RunToCompletion();
                    }

                    SearchStep actual;
                    using (var search = shared.BeginStepwiseSearch(from, to))
                    {
                        actual = search.RunToCompletion();
                    }

                    actual.State.Should().Be(expected.State);
                    actual.Path.Should().Equal(expected.Path);
                    actual.Searched.Should().Equal(expected.Searched);
                    break;
                }
            }
        }

        private static Dictionary<Coordinate, int> ToDictionary(RangeResult result)
        {
            return result.Cells.ToDictionary(cell => cell.Coordinate, cell => cell.Cost);
        }

        private static Coordinate RandomCoordinate(Random random)
        {
            return new Coordinate(random.Next(Width), random.Next(Height));
        }

        private static Cell[,] CreateRandomGrid(Random random)
        {
            var grid = new Cell[Width, Height];

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    grid[x, y] = new Cell
                    {
                        Coordinate = new Coordinate(x, y),
                        IsWalkable = random.NextDouble() >= 0.25,
                        IsOccupied = random.NextDouble() < 0.05,
                        Weight = random.Next(3) * 100
                    };
                }
            }

            return grid;
        }

        public enum SnapshotSource
        {
            Matrix,
            HolderArray,
            HolderMatrix
        }

        /// <summary>
        /// An open grid in one of the snapshot-taking representations, editable directly (behind the pathfinder's
        /// back).
        /// </summary>
        private sealed class EditableGrid
        {
            private readonly SnapshotSource _source;
            private readonly Cell[,] _matrix = new Cell[Width, Height];
            private readonly Holder[,] _holders = new Holder[Width, Height];

            public EditableGrid(SnapshotSource source)
            {
                _source = source;

                for (var x = 0; x < Width; x++)
                {
                    for (var y = 0; y < Height; y++)
                    {
                        var cell = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
                        _matrix[x, y] = cell;
                        _holders[x, y] = new Holder { CellData = cell };
                    }
                }
            }

            public Pathfinder CreatePathfinder(IPathfinderSettings settings)
            {
                switch (_source)
                {
                    case SnapshotSource.Matrix:
                        return new Pathfinder(_matrix, settings);
                    case SnapshotSource.HolderArray:
                        return new Pathfinder(_holders.Cast<ICellHolder>().ToArray(), Width, Height, settings);
                    default:
                        var holders = new ICellHolder[Width, Height];
                        Array.Copy(_holders, holders, _holders.Length);
                        return new Pathfinder(holders, settings);
                }
            }

            public void SetWalkable(Coordinate coordinate, bool isWalkable)
            {
                _matrix[coordinate.X, coordinate.Y].IsWalkable = isWalkable;

                var holder = _holders[coordinate.X, coordinate.Y];
                var cell = holder.CellData;
                cell.IsWalkable = isWalkable;
                holder.CellData = cell;
            }
        }

        private sealed class Holder : ICellHolder
        {
            public Cell CellData { get; set; }
        }

    }
}
