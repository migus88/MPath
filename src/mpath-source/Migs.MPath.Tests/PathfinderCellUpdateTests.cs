using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Tests.Implementations;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// Pins the cell-update API (<see cref="Pathfinder.SetWalkable"/>, <see cref="Pathfinder.SetOccupied"/>,
    /// <see cref="Pathfinder.SetWeight"/>, <see cref="Pathfinder.SetCell"/>, <see cref="Pathfinder.GetCell"/>,
    /// <see cref="Pathfinder.GetIndex"/>): updates reach the next query in the writable modes whatever the order of
    /// the caller's array, land in the caller's own grid, and are rejected in the holder modes.
    /// </summary>
    [TestFixture]
    public class PathfinderCellUpdateTests
    {
        private const int Width = 5;
        private const int Height = 3;
        private const float Budget = 100f;

        private static readonly Coordinate Start = new Coordinate(0, 1);
        private static readonly Coordinate End = new Coordinate(4, 1);

        public enum Mode
        {
            UnorderedArray,
            OrderedArray,
            Matrix,
            HolderArray,
            HolderMatrix
        }

        [TestCase(Mode.UnorderedArray)]
        [TestCase(Mode.OrderedArray)]
        [TestCase(Mode.Matrix)]
        public void SetWalkable_BlockingAColumn_BlocksTheNextPathAndReachableQuery(Mode mode)
        {
            // Arrange
            using var pathfinder = Create(mode, out _, out _);

            // Act
            for (var y = 0; y < Height; y++)
            {
                pathfinder.SetWalkable(new Coordinate(2, y), false);
            }

            // Assert
            using var path = pathfinder.GetPath(new Agent(), Start, End);
            using var reachable = pathfinder.GetReachable(new Agent(), Start, Budget);
            path.IsSuccess.Should().BeFalse();
            reachable.Contains(new Coordinate(2, 1)).Should().BeFalse();
            reachable.Contains(End).Should().BeFalse();
        }

        [TestCase(Mode.UnorderedArray)]
        [TestCase(Mode.Matrix)]
        public void SetWalkable_AfterAQuery_IsSeenByTheNextQuery(Mode mode)
        {
            // Arrange
            using var pathfinder = Create(mode, out _, out _);
            using (var before = pathfinder.GetPath(new Agent(), Start, End))
            {
                before.IsSuccess.Should().BeTrue();
            }

            // Act
            for (var y = 0; y < Height; y++)
            {
                pathfinder.SetWalkable(new Coordinate(2, y), false);
            }

            // Assert
            using var after = pathfinder.GetPath(new Agent(), Start, End);
            after.IsSuccess.Should().BeFalse();
        }

        [TestCase(Mode.HolderArray)]
        [TestCase(Mode.HolderMatrix)]
        public void Setters_InHolderModes_ThrowNotSupportedException(Mode mode)
        {
            // Arrange
            using var pathfinder = Create(mode, out _, out _);
            var target = new Coordinate(2, 1);

            // Act
            var setters = new Action[]
            {
                () => pathfinder.SetWalkable(target, false),
                () => pathfinder.SetOccupied(target, true),
                () => pathfinder.SetWeight(target, 3f),
                () => pathfinder.SetCell(target, new Cell())
            };

            // Assert
            foreach (var setter in setters)
            {
                setter.Should().Throw<NotSupportedException>();
            }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void SetOccupied_BlocksOnlyWhenOccupancyIsCalculated(bool isCalculatingOccupiedCells, bool expectedReach)
        {
            // Arrange
            var settings = new PathfinderSettings { IsCalculatingOccupiedCells = isCalculatingOccupiedCells };
            using var pathfinder = new Pathfinder(CreateRowMajorArray(), Width, Height, settings);

            // Act
            for (var y = 0; y < Height; y++)
            {
                pathfinder.SetOccupied(new Coordinate(2, y), true);
            }

            // Assert
            using var path = pathfinder.GetPath(new Agent(), Start, End);
            path.IsSuccess.Should().Be(expectedReach);
        }

        [Test]
        public void SetWeight_WithCellWeightEnabled_RoutesAroundTheHeavyCell()
        {
            // Arrange
            var settings = new PathfinderSettings { IsDiagonalMovementEnabled = false };
            using var pathfinder = new Pathfinder(CreateMatrix(), settings);
            var heavy = new Coordinate(2, 1);
            using (var before = pathfinder.GetPath(new Agent(), Start, End))
            {
                before.Path.Should().Contain(heavy);
            }

            // Act
            pathfinder.SetWeight(heavy, 50f);

            // Assert
            using var after = pathfinder.GetPath(new Agent(), Start, End);
            after.IsSuccess.Should().BeTrue();
            after.Path.Should().NotContain(heavy);
        }

        [Test]
        public void SetCell_CopiesTheUserFieldsAndKeepsTheCoordinate()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateMatrix());
            var target = new Coordinate(3, 2);

            // Act
            pathfinder.SetCell(target, new Cell { Coordinate = new Coordinate(0, 0), IsOccupied = true, Weight = 4f });

            // Assert
            var cell = pathfinder.GetCell(target);
            cell.Coordinate.Should().Be(target);
            cell.IsWalkable.Should().BeFalse();
            cell.IsOccupied.Should().BeTrue();
            cell.Weight.Should().BeApproximately(4f, 0.0001f);
            pathfinder.GetCell(new Coordinate(0, 0)).IsWalkable.Should().BeTrue();
        }

        [Test]
        public void SetOccupied_InArrayMode_IsVisibleInTheCallersArrayAtGetIndex()
        {
            // Arrange
            var cells = CreateRowMajorArray();
            using var pathfinder = new Pathfinder(cells, Width, Height);
            using (pathfinder.GetPath(new Agent(), Start, End))
            {
            }

            var target = new Coordinate(3, 2);

            // Act
            pathfinder.SetOccupied(target, true);

            // Assert
            var index = pathfinder.GetIndex(target);
            cells[index].Coordinate.Should().Be(target);
            cells[index].IsOccupied.Should().BeTrue();
        }

        [Test]
        public void SetWalkable_OnAnUnorderedArray_UpdatesTheCellWithoutReorderingTheArray()
        {
            // Arrange
            var cells = CreateRowMajorArray();
            var order = cells.Select(c => c.Coordinate).ToArray();
            using var pathfinder = new Pathfinder(cells, Width, Height);
            var target = new Coordinate(3, 2);

            // Act
            pathfinder.SetWalkable(target, false);

            // Assert
            cells.Select(c => c.Coordinate).Should().Equal(order);
            cells[target.Y * Width + target.X].IsWalkable.Should().BeFalse();
            cells.Count(c => !c.IsWalkable).Should().Be(1);
        }

        [Test]
        public void SetWalkable_InMatrixMode_WritesTheCallersMatrix()
        {
            // Arrange
            var matrix = CreateMatrix();
            using var pathfinder = new Pathfinder(matrix);

            // Act
            pathfinder.SetWalkable(new Coordinate(3, 2), false);

            // Assert
            matrix[3, 2].IsWalkable.Should().BeFalse();
        }

        [TestCase(Mode.UnorderedArray)]
        [TestCase(Mode.Matrix)]
        [TestCase(Mode.HolderArray)]
        [TestCase(Mode.HolderMatrix)]
        public void GetCell_ReturnsTheCurrentCellOfTheCallersGrid(Mode mode)
        {
            // Arrange
            using var pathfinder = Create(mode, out var cells, out var holders);
            var target = new Coordinate(3, 2);

            // Act
            if (holders != null)
            {
                holders.Single(h => h.CellData.Coordinate == target).IsOccupied = true;
            }
            else if (cells != null)
            {
                cells[target.Y * Width + target.X].IsOccupied = true;
            }
            else
            {
                pathfinder.SetOccupied(target, true);
            }

            // Assert
            var cell = pathfinder.GetCell(target);
            cell.Coordinate.Should().Be(target);
            cell.IsOccupied.Should().BeTrue();
        }

        [Test]
        public void GetIndex_ReturnsTheColumnMajorLayoutIndex()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateMatrix());

            // Act
            var index = pathfinder.GetIndex(new Coordinate(3, 2));

            // Assert
            index.Should().Be(3 * Height + 2);
        }

        [Test]
        public void SetWalkable_WhenTheArrayHasNoSuchCell_ThrowsInvalidOperationException()
        {
            // Arrange
            var cells = CreateRowMajorArray();
            cells[cells.Length - 1].Coordinate = new Coordinate(0, 0);
            using var pathfinder = new Pathfinder(cells, Width, Height);

            // Act
            var action = () => pathfinder.SetWalkable(new Coordinate(Width - 1, Height - 1), false);

            // Assert
            action.Should().Throw<InvalidOperationException>();
        }

        [TestCase(Mode.OrderedArray)]
        [TestCase(Mode.Matrix)]
        public void Setters_InLayout_DoNotAllocate(Mode mode)
        {
            // Arrange
            using var pathfinder = Create(mode, out _, out _);
            var target = new Coordinate(3, 2);
            pathfinder.SetWalkable(target, false);
            pathfinder.SetOccupied(target, false);
            pathfinder.SetWeight(target, 0f);

            // Act
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                pathfinder.SetWalkable(target, i % 2 == 0);
                pathfinder.SetOccupied(target, i % 2 == 0);
                pathfinder.SetWeight(target, i);
                pathfinder.GetCell(target);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            allocated.Should().Be(0);
        }

        public static IEnumerable<TestCaseData> OutsideGridCalls()
        {
            var outside = new[] { new Coordinate(-1, 0), new Coordinate(Width, 0), new Coordinate(0, Height) };

            foreach (var coordinate in outside)
            {
                yield return new TestCaseData((Action<Pathfinder>)(p => p.SetWalkable(coordinate, false)))
                    .SetName($"SetWalkable_Outside{coordinate}_ThrowsArgumentException");
                yield return new TestCaseData((Action<Pathfinder>)(p => p.GetCell(coordinate)))
                    .SetName($"GetCell_Outside{coordinate}_ThrowsArgumentException");
                yield return new TestCaseData((Action<Pathfinder>)(p => p.GetIndex(coordinate)))
                    .SetName($"GetIndex_Outside{coordinate}_ThrowsArgumentException");
            }

            yield return new TestCaseData((Action<Pathfinder>)(p => p.SetOccupied(new Coordinate(Width, 0), true)))
                .SetName("SetOccupied_OutsideGrid_ThrowsArgumentException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.SetWeight(new Coordinate(Width, 0), 1f)))
                .SetName("SetWeight_OutsideGrid_ThrowsArgumentException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.SetCell(new Coordinate(Width, 0), new Cell())))
                .SetName("SetCell_OutsideGrid_ThrowsArgumentException");
        }

        [TestCaseSource(nameof(OutsideGridCalls))]
        public void CellApi_OutsideTheGrid_ThrowsArgumentException(Action<Pathfinder> call)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateMatrix());

            // Act
            var action = () => call(pathfinder);

            // Assert
            action.Should().Throw<ArgumentException>();
        }

        private static Pathfinder Create(Mode mode, out Cell[] cells, out List<Holder> holders)
        {
            cells = null;
            holders = null;

            switch (mode)
            {
                case Mode.UnorderedArray:
                    cells = CreateRowMajorArray();
                    return new Pathfinder(cells, Width, Height);
                case Mode.OrderedArray:
                    var ordered = CreateRowMajorArray().OrderBy(c => c.Coordinate.X).ThenBy(c => c.Coordinate.Y).ToArray();
                    return new Pathfinder(ordered, Width, Height);
                case Mode.Matrix:
                    return new Pathfinder(CreateMatrix());
                case Mode.HolderArray:
                    holders = CreateRowMajorArray().Select(c => new Holder(c)).ToList();
                    return new Pathfinder(holders.Cast<ICellHolder>().ToArray(), Width, Height);
                default:
                    holders = new List<Holder>();
                    var holderMatrix = new ICellHolder[Width, Height];
                    foreach (var cell in CreateRowMajorArray())
                    {
                        var holder = new Holder(cell);
                        holders.Add(holder);
                        holderMatrix[cell.Coordinate.X, cell.Coordinate.Y] = holder;
                    }

                    return new Pathfinder(holderMatrix);
            }
        }

        private static Cell[] CreateRowMajorArray()
        {
            var cells = new Cell[Width * Height];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    cells[y * Width + x] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
                }
            }

            return cells;
        }

        private static Cell[,] CreateMatrix()
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

        public sealed class Holder : ICellHolder
        {
            private Cell _cell;

            public Holder(Cell cell)
            {
                _cell = cell;
            }

            public Cell CellData => _cell;

            public bool IsOccupied
            {
                set => _cell.IsOccupied = value;
            }
        }
    }
}
