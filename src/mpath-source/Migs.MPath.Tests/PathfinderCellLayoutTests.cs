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
    /// Pins that a cell's slot is its position: every grid representation (a column- or row-major <see cref="PathCell"/>[]
    /// or <see cref="ICellHolder"/>[] under <see cref="CellLayout"/>, a <see cref="PathCell"/>[,] or
    /// <see cref="ICellHolder"/>[,] at <c>[x, y]</c>) gives the same results, every coordinate a query reports is the
    /// slot it came from, a <c>null</c> holder is a wall whatever the pooled buffer held, queries never write the
    /// caller's array, and <see cref="Pathfinder.GetIndex"/> and <see cref="RangeResult.CopyCostsTo"/> share one index
    /// space.
    /// </summary>
    [TestFixture]
    public class PathfinderCellLayoutTests
    {
        private const int Width = 13;
        private const int Height = 7;
        private const int Seed = 42;
        private const float WallRatio = 0.25f;

        public enum Representation
        {
            ColumnMajorArray,
            RowMajorArray,
            ColumnMajorHolders,
            RowMajorHolders,
            Matrix,
            HolderMatrix
        }

        public static IEnumerable<Representation> Representations()
        {
            return Enum.GetValues(typeof(Representation)).Cast<Representation>();
        }

        [TestCaseSource(nameof(Representations))]
        public void Queries_OnEveryRepresentation_MatchTheColumnMajorArray(Representation representation)
        {
            // Arrange
            var matrix = CreateRandomMatrix();
            using var reference = new Pathfinder(ToArray(matrix, CellLayout.ColumnMajor), Width, Height);
            using var pathfinder = Create(matrix, representation);
            var random = new Random(Seed);

            for (var i = 0; i < 40; i++)
            {
                var from = new Coordinate(random.Next(Width), random.Next(Height));
                var to = new Coordinate(random.Next(Width), random.Next(Height));

                // Act
                using var expectedPath = reference.GetPath(from, to);
                using var path = pathfinder.GetPath(from, to);
                using var expectedRange = reference.GetReachable(from, 600);
                using var range = pathfinder.GetReachable(from, 600);

                // Assert
                path.IsSuccess.Should().Be(expectedPath.IsSuccess);
                path.Path.Should().Equal(expectedPath.Path);
                ToList(range).Should().Equal(ToList(expectedRange));
                pathfinder.HasLineOfSight(from, to).Should().Be(reference.HasLineOfSight(from, to));
            }
        }

        [TestCaseSource(nameof(Representations))]
        public void Queries_ReportTheCoordinatesOfTheSlotsTheyCameFrom(Representation representation)
        {
            // Arrange: only a cross is walkable (row y = 2 and column x = 3), so a transposed slot would be off it.
            var matrix = new PathCell[Width, Height];
            var cross = new HashSet<Coordinate>();

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    if (y == 2 || x == 3)
                    {
                        matrix[x, y].IsWalkable = true;
                        cross.Add(new Coordinate(x, y));
                    }
                }
            }

            using var pathfinder = Create(matrix, representation);
            var from = new Coordinate(Width - 1, 2);
            var to = new Coordinate(3, Height - 1);

            // Act
            using var path = pathfinder.GetPath(from, to);
            using var range = pathfinder.GetReachable(from, int.MaxValue);
            SearchStep step;
            using (var search = pathfinder.BeginStepwiseSearch(from, to))
            {
                step = search.RunToCompletion();
            }

            // Assert
            path.IsSuccess.Should().BeTrue();
            path.Path.Last().Should().Be(to);
            path.Path.Should().OnlyContain(c => cross.Contains(c));
            range.Cells.Select(c => c.Coordinate).Should().BeEquivalentTo(cross);
            step.State.Should().Be(SearchState.Success);
            step.Path.Should().Equal(path.Path);
            step.Searched.Select(n => n.Coordinate).Should().OnlyContain(c => cross.Contains(c));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NullHolder_AfterTheCellPoolHeldWalkableCells_IsAWall(bool isMatrix)
        {
            // Arrange: a fully walkable matrix pathfinder fills a pooled cell buffer of the same size and returns it.
            var open = CreateOpenMatrix();
            using (var dirty = new Pathfinder(open))
            using (dirty.GetPath(new Coordinate(0, 0), new Coordinate(1, 0)))
            {
            }

            var wall = new Coordinate(4, 0);
            var holders = ToHolderMatrix(open);
            holders[wall.X, wall.Y] = null!;

            using var pathfinder = isMatrix
                ? new Pathfinder(holders)
                : new Pathfinder(ToHolderArray(holders, CellLayout.ColumnMajor), Width, Height);

            // Act
            using var range = pathfinder.GetReachable(new Coordinate(0, 0), int.MaxValue);

            // Assert
            range.Contains(wall).Should().BeFalse();
            range.Length.Should().Be(Width * Height - 1);
            pathfinder.GetCell(wall).IsWalkable.Should().BeFalse();
        }

        [TestCase(CellLayout.ColumnMajor)]
        [TestCase(CellLayout.RowMajor)]
        public void Queries_NeverChangeTheCallersArray(CellLayout layout)
        {
            // Arrange
            var cells = ToArray(CreateRandomMatrix(), layout);
            var before = (PathCell[])cells.Clone();
            using var pathfinder = new Pathfinder(cells, Width, Height, Settings(layout));
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, Height - 1);

            // Act
            using (pathfinder.GetPath(from, to))
            using (pathfinder.GetReachable(from, 800))
            using (var search = pathfinder.BeginStepwiseSearch(from, to))
            {
                pathfinder.HasLineOfSight(from, to);
                search.RunToCompletion();
            }

            // Assert
            cells.Should().Equal(before);
        }

        [TestCase(CellLayout.ColumnMajor)]
        [TestCase(CellLayout.RowMajor)]
        public void DirectEdit_BetweenQueries_IsSeenByTheNextQuery(CellLayout layout)
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), layout);
            using var pathfinder = new Pathfinder(cells, Width, Height, Settings(layout));
            var from = new Coordinate(0, 0);
            var to = new Coordinate(Width - 1, 0);

            using (pathfinder.GetPath(from, to))
            {
            }

            // Act
            cells[pathfinder.GetIndex(to)].IsWalkable = false;

            // Assert
            using var result = pathfinder.GetPath(from, to);
            result.IsSuccess.Should().BeFalse();
        }

        [TestCase(CellLayout.ColumnMajor, 3 * Height + 2)]
        [TestCase(CellLayout.RowMajor, 2 * Width + 3)]
        public void GetIndex_IsTheIndexUnderTheDeclaredLayout(CellLayout layout, int expected)
        {
            // Arrange
            using var pathfinder = new Pathfinder(ToArray(CreateOpenMatrix(), layout), Width, Height,
                Settings(layout));

            // Act
            var index = pathfinder.GetIndex(new Coordinate(3, 2));

            // Assert
            index.Should().Be(expected);
        }

        [TestCase(Representation.RowMajorArray)]
        [TestCase(Representation.RowMajorHolders)]
        [TestCase(Representation.ColumnMajorArray)]
        [TestCase(Representation.HolderMatrix)]
        public void CopyCostsTo_WritesEachCostAtGetIndexOfItsCell(Representation representation)
        {
            // Arrange
            using var pathfinder = Create(CreateRandomMatrix(), representation);
            using var range = pathfinder.GetReachable(new Coordinate(0, 0), 600);
            var costs = new int[Width * Height];

            // Act
            range.CopyCostsTo(costs);

            // Assert
            range.Length.Should().BeGreaterThan(1);
            var expected = Enumerable.Repeat(-1, costs.Length).ToArray();
            foreach (var cell in range)
            {
                expected[pathfinder.GetIndex(cell.Coordinate)] = cell.Cost;
            }

            costs.Should().Equal(expected);
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(-1)]
        public void Constructor_WithALayoutOtherThanColumnOrRowMajor_ThrowsArgumentOutOfRange(int layout)
        {
            // Arrange
            var settings = Settings((CellLayout)layout);

            // Act
            var array = () => new Pathfinder(new PathCell[Width * Height], Width, Height, settings);
            var matrix = () => new Pathfinder(new PathCell[Width, Height], settings);

            // Assert
            array.Should().Throw<ArgumentOutOfRangeException>();
            matrix.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Constructor_ArrayShorterThanTheField_ThrowsArgumentException()
        {
            // Act
            var cells = () => new Pathfinder(new PathCell[Width * Height - 1], Width, Height);
            var holders = () => new Pathfinder(new ICellHolder[Width * Height - 1], Width, Height);

            // Assert
            cells.Should().Throw<ArgumentException>();
            holders.Should().Throw<ArgumentException>();
        }

        [TestCase(CellLayout.ColumnMajor)]
        [TestCase(CellLayout.RowMajor)]
        public void GetPath_OnAnArray_AllocatesOnlyTheResult(CellLayout layout)
        {
            // Arrange
            var cells = ToArray(CreateOpenMatrix(), layout);
            using var pathfinder = new Pathfinder(cells, Width, Height, Settings(layout));
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

        private static Pathfinder Create(PathCell[,] matrix, Representation representation)
        {
            switch (representation)
            {
                case Representation.ColumnMajorArray:
                    return new Pathfinder(ToArray(matrix, CellLayout.ColumnMajor), Width, Height);
                case Representation.RowMajorArray:
                    return new Pathfinder(ToArray(matrix, CellLayout.RowMajor), Width, Height,
                        Settings(CellLayout.RowMajor));
                case Representation.ColumnMajorHolders:
                    return new Pathfinder(ToHolderArray(ToHolderMatrix(matrix), CellLayout.ColumnMajor), Width,
                        Height);
                case Representation.RowMajorHolders:
                    return new Pathfinder(ToHolderArray(ToHolderMatrix(matrix), CellLayout.RowMajor), Width, Height,
                        Settings(CellLayout.RowMajor));
                case Representation.Matrix:
                    return new Pathfinder(matrix);
                default:
                    return new Pathfinder(ToHolderMatrix(matrix));
            }
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

        private static int GetIndex(int x, int y, CellLayout layout)
        {
            return layout == CellLayout.RowMajor ? y * Width + x : x * Height + y;
        }

        private static PathCell[] ToArray(PathCell[,] matrix, CellLayout layout)
        {
            var cells = new PathCell[Width * Height];

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    cells[GetIndex(x, y, layout)] = matrix[x, y];
                }
            }

            return cells;
        }

        private static ICellHolder[,] ToHolderMatrix(PathCell[,] matrix)
        {
            var holders = new ICellHolder[Width, Height];

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    holders[x, y] = new Holder { CellData = matrix[x, y] };
                }
            }

            return holders;
        }

        private static ICellHolder[] ToHolderArray(ICellHolder[,] matrix, CellLayout layout)
        {
            var holders = new ICellHolder[Width * Height];

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    holders[GetIndex(x, y, layout)] = matrix[x, y];
                }
            }

            return holders;
        }

        private static PathCell[,] CreateOpenMatrix()
        {
            var matrix = new PathCell[Width, Height];

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    matrix[x, y] = new PathCell { IsWalkable = true };
                }
            }

            return matrix;
        }

        private static PathCell[,] CreateRandomMatrix()
        {
            var random = new Random(Seed);
            var matrix = CreateOpenMatrix();

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    matrix[x, y].IsWalkable = random.NextDouble() >= WallRatio;
                    matrix[x, y].Weight = random.Next(3) * 100;
                }
            }

            matrix[0, 0].IsWalkable = true;
            matrix[Width - 1, Height - 1].IsWalkable = true;

            return matrix;
        }

        private sealed class Holder : ICellHolder
        {
            public PathCell CellData { get; set; }
        }
    }
}
