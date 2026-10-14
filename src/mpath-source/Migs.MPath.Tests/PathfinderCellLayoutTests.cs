using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// Pins <see cref="CellLayout"/>: a <see cref="Cell"/>[] array in column-major, row-major or any order gives the
    /// same results as the matching <see cref="Cell"/>[,] matrix, queries never reorder or write the caller's array,
    /// direct edits reach the next query (in place, or through the snapshot of an unordered array), a declared layout
    /// is checked, and <see cref="Pathfinder.GetIndex"/> follows the layout.
    /// </summary>
    [TestFixture]
    public class PathfinderCellLayoutTests
    {
        private const int Width = 13;
        private const int Height = 7;
        private const int Seed = 42;
        private const float WallRatio = 0.25f;

        public enum Order
        {
            ColumnMajor,
            RowMajor,
            Shuffled
        }

        public static IEnumerable<TestCaseData> MatchingLayouts()
        {
            yield return new TestCaseData(Order.ColumnMajor, CellLayout.Auto);
            yield return new TestCaseData(Order.RowMajor, CellLayout.Auto);
            yield return new TestCaseData(Order.Shuffled, CellLayout.Auto);
            yield return new TestCaseData(Order.ColumnMajor, CellLayout.ColumnMajor);
            yield return new TestCaseData(Order.RowMajor, CellLayout.RowMajor);
            yield return new TestCaseData(Order.Shuffled, CellLayout.Unordered);
            yield return new TestCaseData(Order.RowMajor, CellLayout.Unordered);
        }

        [TestCaseSource(nameof(MatchingLayouts))]
        public void Queries_OnAnArray_MatchTheMatrix(Order order, CellLayout layout)
        {
            // Arrange
            var matrix = CreateRandomMatrix();
            using var reference = new Pathfinder(matrix);
            using var pathfinder = new Pathfinder(ToArray(matrix, order), Width, Height, Settings(layout));
            var random = new Random(Seed);

            for (var i = 0; i < 40; i++)
            {
                var from = new Coordinate(random.Next(Width), random.Next(Height));
                var to = new Coordinate(random.Next(Width), random.Next(Height));

                // Act
                using var expectedPath = reference.GetPath(from, to);
                using var path = pathfinder.GetPath(from, to);
                using var expectedRange = reference.GetReachable(from, 6f);
                using var range = pathfinder.GetReachable(from, 6f);

                // Assert
                path.IsSuccess.Should().Be(expectedPath.IsSuccess);
                path.Path.Should().Equal(expectedPath.Path);
                ToList(range).Should().Equal(ToList(expectedRange));
                pathfinder.HasLineOfSight(from, to).Should().Be(reference.HasLineOfSight(from, to));
            }
        }

        [TestCaseSource(nameof(MatchingLayouts))]
        public void Queries_NeverChangeTheCallersArray(Order order, CellLayout layout)
        {
            // Arrange
            var cells = ToArray(CreateRandomMatrix(), order);
            var before = (Cell[])cells.Clone();
            using var pathfinder = new Pathfinder(cells, Width, Height, Settings(layout));
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, Height - 1);

            // Act
            using (pathfinder.GetPath(from, to))
            using (pathfinder.GetReachable(from, 8f))
            using (var search = pathfinder.BeginStepwiseSearch(from, to))
            {
                pathfinder.HasLineOfSight(from, to);
                search.RunToCompletion();
            }

            // Assert
            cells.Should().Equal(before);
        }

        [TestCase(Order.ColumnMajor)]
        [TestCase(Order.RowMajor)]
        [TestCase(Order.Shuffled)]
        public void DirectEdit_BetweenQueries_IsSeenByTheNextQuery(Order order)
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), order);
            using var pathfinder = new Pathfinder(cells, Width, Height);
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, 0);

            using (pathfinder.GetPath(from, to))
            {
            }

            // Act
            cells[IndexOf(cells, to)].IsWalkable = false;

            // Assert
            using var result = pathfinder.GetPath(from, to);
            result.IsSuccess.Should().BeFalse();
        }

        [Test]
        public void UnorderedManualSnapshot_DirectEdit_IsSeenOnlyAfterRefresh()
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), Order.Shuffled);
            using var pathfinder = new Pathfinder(cells, Width, Height, new PathfinderSettings
            {
                GridSnapshotMode = GridSnapshotMode.Manual
            });
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, 0);

            using (pathfinder.GetPath(from, to))
            {
            }

            cells[IndexOf(cells, to)].IsWalkable = false;

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
        public void UnorderedManualSnapshot_SetWalkable_WritesTheArrayAndTheNextQuery()
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), Order.Shuffled);
            using var pathfinder = new Pathfinder(cells, Width, Height, new PathfinderSettings
            {
                GridSnapshotMode = GridSnapshotMode.Manual
            });
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, 0);

            using (pathfinder.GetPath(from, to))
            {
            }

            // Act
            pathfinder.SetWalkable(to, false);

            // Assert
            using var result = pathfinder.GetPath(from, to);
            result.IsSuccess.Should().BeFalse();
            cells[IndexOf(cells, to)].IsWalkable.Should().BeFalse();
        }

        [Test]
        public void AutoLayout_ArrayReorderedAndRefreshed_IsDetectedAgain()
        {
            // Arrange
            var matrix = CreateRandomMatrix();
            var cells = ToArray(matrix, Order.RowMajor);
            using var reference = new Pathfinder(matrix);
            using var pathfinder = new Pathfinder(cells, Width, Height);
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, Height - 1);

            using (pathfinder.GetPath(from, to))
            {
            }

            ToArray(matrix, Order.Shuffled).CopyTo(cells, 0);

            // Act
            pathfinder.Refresh();
            using var result = pathfinder.GetPath(from, to);

            // Assert
            using var expected = reference.GetPath(from, to);
            result.Path.Should().Equal(expected.Path);
        }

        [TestCase(Order.RowMajor, CellLayout.ColumnMajor)]
        [TestCase(Order.ColumnMajor, CellLayout.RowMajor)]
        [TestCase(Order.Shuffled, CellLayout.RowMajor)]
        public void DeclaredLayout_ArrayInAnotherOrder_ThrowsOnTheFirstQuery(Order order, CellLayout layout)
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), order);
            using var pathfinder = new Pathfinder(cells, Width, Height, Settings(layout));

            // Act
            var action = () => pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(1, 0));

            // Assert
            action.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void GetIndex_DeclaredRowMajor_IsTheRowMajorIndexBeforeAnyQuery()
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), Order.RowMajor);
            using var pathfinder = new Pathfinder(cells, Width, Height, Settings(CellLayout.RowMajor));

            // Act
            var index = pathfinder.GetIndex(new Coordinate(3, 2));

            // Assert
            index.Should().Be(2 * Width + 3);
        }

        [Test]
        public void GetIndex_AutoRowMajor_IsTheRowMajorIndexAfterTheFirstQuery()
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), Order.RowMajor);
            using var pathfinder = new Pathfinder(cells, Width, Height);

            using (pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(1, 0)))
            {
            }

            // Act
            var index = pathfinder.GetIndex(new Coordinate(3, 2));

            // Assert
            index.Should().Be(2 * Width + 3);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GetIndex_Unordered_IsTheCellsIndexInTheCallersArray(bool afterAQuery)
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), Order.Shuffled);
            using var pathfinder = new Pathfinder(cells, Width, Height, Settings(CellLayout.Unordered));
            var target = new Coordinate(3, 2);

            if (afterAQuery)
            {
                using (pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(1, 0)))
                {
                }
            }

            // Act
            var index = pathfinder.GetIndex(target);

            // Assert
            cells[index].Coordinate.Should().Be(target);
        }

        [Test]
        public void Unordered_ArrayWithoutACell_TreatsTheMissingCellAsAWall()
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), Order.Shuffled);
            var missing = new Coordinate(4, 0);
            cells[IndexOf(cells, missing)].Coordinate = new Coordinate(0, 0);
            using var pathfinder = new Pathfinder(cells, Width, Height);

            // Act
            using var result = pathfinder.GetReachable(new Coordinate(0, 0), 100f);

            // Assert
            result.Contains(missing).Should().BeFalse();
            result.Contains(new Coordinate(5, 0)).Should().BeTrue();
        }

        [Test]
        public void Unordered_CellOutsideTheGrid_ThrowsOnTheQuery()
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), Order.Shuffled);
            cells[3].Coordinate = new Coordinate(Width, 0);
            using var pathfinder = new Pathfinder(cells, Width, Height);

            // Act
            var action = () => pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(1, 0));

            // Assert
            action.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void Constructor_ArrayShorterThanTheField_ThrowsArgumentException()
        {
            // Arrange
            var cells = new Cell[Width * Height - 1];

            // Act
            var action = () => new Pathfinder(cells, Width, Height);

            // Assert
            action.Should().Throw<ArgumentException>();
        }

        [TestCase(Order.RowMajor, GridSnapshotMode.PerQuery)]
        [TestCase(Order.ColumnMajor, GridSnapshotMode.PerQuery)]
        [TestCase(Order.Shuffled, GridSnapshotMode.PerQuery)]
        [TestCase(Order.Shuffled, GridSnapshotMode.Manual)]
        public void GetPath_OnAnArray_AllocatesOnlyTheResult(Order order, GridSnapshotMode snapshotMode)
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), order);
            using var pathfinder = new Pathfinder(cells, Width, Height,
                new PathfinderSettings { GridSnapshotMode = snapshotMode });
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, Height - 1);

            using (pathfinder.GetPath(from, to))
            {
            }

            // Act
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var i = 0; i < 100; i++)
            {
                using (pathfinder.GetPath(from, to))
                {
                }
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            allocated.Should().BeLessThan(100 * 64);
        }

        private static PathfinderSettings Settings(CellLayout layout)
        {
            return new PathfinderSettings { CellLayout = layout };
        }

        private static List<ReachableCell> ToList(RangeResult range)
        {
            var list = new List<ReachableCell>();

            for (var i = 0; i < range.Length; i++)
            {
                list.Add(range.Get(i));
            }

            return list.OrderBy(c => c.Coordinate.X).ThenBy(c => c.Coordinate.Y).ToList();
        }

        private static int IndexOf(Cell[] cells, Coordinate coordinate)
        {
            return Array.FindIndex(cells, c => c.Coordinate == coordinate);
        }

        private static Cell[] ToArray(Cell[,] matrix, Order order)
        {
            var cells = new Cell[Width * Height];

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    cells[order == Order.RowMajor ? y * Width + x : x * Height + y] = matrix[x, y];
                }
            }

            if (order == Order.Shuffled)
            {
                var random = new Random(Seed);

                for (var i = cells.Length - 1; i > 0; i--)
                {
                    var j = random.Next(i + 1);
                    (cells[i], cells[j]) = (cells[j], cells[i]);
                }
            }

            return cells;
        }

        private static Cell[,] CreateOpenMatrix()
        {
            var matrix = new Cell[Width, Height];

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    matrix[x, y] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
                }
            }

            return matrix;
        }

        private static Cell[,] CreateRandomMatrix()
        {
            var random = new Random(Seed);
            var matrix = CreateOpenMatrix();

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    matrix[x, y].IsWalkable = random.NextDouble() >= WallRatio;
                    matrix[x, y].Weight = random.Next(3);
                }
            }

            matrix[0, 0].IsWalkable = true;
            matrix[Width - 1, Height - 1].IsWalkable = true;

            return matrix;
        }
    }
}
