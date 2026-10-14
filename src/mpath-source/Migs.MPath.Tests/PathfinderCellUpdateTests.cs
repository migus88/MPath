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
    /// Pins the cell-update API (<see cref="Pathfinder.SetWalkable"/>, <see cref="Pathfinder.SetOccupied"/>,
    /// <see cref="Pathfinder.SetWeight"/>, <see cref="Pathfinder.SetCell"/>, <see cref="Pathfinder.GetCell"/>,
    /// <see cref="Pathfinder.GetIndex"/>): updates reach the next query in the writable modes whatever the layout of
    /// the caller's array (row-major or column-major), land in the caller's own grid, and are rejected in the holder
    /// modes.
    /// </summary>
    [TestFixture]
    public class PathfinderCellUpdateTests
    {
        private const int Width = 5;
        private const int Height = 3;
        private const int Budget = 10000;

        private static readonly Coordinate Start = new Coordinate(0, 1);
        private static readonly Coordinate End = new Coordinate(4, 1);

        public enum Mode
        {
            RowMajorArray,
            ColumnMajorArray,
            Matrix,
            HolderArray,
            HolderMatrix
        }

        [TestCase(Mode.RowMajorArray)]
        [TestCase(Mode.ColumnMajorArray)]
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
            using var path = pathfinder.GetPath(Start, End);
            using var reachable = pathfinder.GetReachable(Start, Budget);
            path.IsSuccess.Should().BeFalse();
            reachable.Contains(new Coordinate(2, 1)).Should().BeFalse();
            reachable.Contains(End).Should().BeFalse();
        }

        [TestCase(Mode.RowMajorArray)]
        [TestCase(Mode.Matrix)]
        public void SetWalkable_AfterAQuery_IsSeenByTheNextQuery(Mode mode)
        {
            // Arrange
            using var pathfinder = Create(mode, out _, out _);
            using (var before = pathfinder.GetPath(Start, End))
            {
                before.IsSuccess.Should().BeTrue();
            }

            // Act
            for (var y = 0; y < Height; y++)
            {
                pathfinder.SetWalkable(new Coordinate(2, y), false);
            }

            // Assert
            using var after = pathfinder.GetPath(Start, End);
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
                () => pathfinder.SetWeight(target, 300),
                () => pathfinder.SetCell(target, new PathCell())
            };

            // Assert
            foreach (var setter in setters)
            {
                setter.Should().Throw<NotSupportedException>();
            }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void SetOccupied_BlocksOnlyWhenOccupiedCellsBlock(bool areOccupiedCellsBlocking, bool expectedReach)
        {
            // Arrange
            var settings = new PathfinderSettings { AreOccupiedCellsBlocking = areOccupiedCellsBlocking };
            using var pathfinder = new Pathfinder(CreateOpenArray(), Width, Height, settings);

            // Act
            for (var y = 0; y < Height; y++)
            {
                pathfinder.SetOccupied(new Coordinate(2, y), true);
            }

            // Assert
            using var path = pathfinder.GetPath(Start, End);
            path.IsSuccess.Should().Be(expectedReach);
        }

        [Test]
        public void SetWeight_WithCellWeightEnabled_RoutesAroundTheHeavyCell()
        {
            // Arrange
            var settings = new PathfinderSettings { IsDiagonalMovementEnabled = false };
            using var pathfinder = new Pathfinder(CreateMatrix(), settings);
            var heavy = new Coordinate(2, 1);
            using (var before = pathfinder.GetPath(Start, End))
            {
                before.Path.Should().Contain(heavy);
            }

            // Act
            pathfinder.SetWeight(heavy, 5000);

            // Assert
            using var after = pathfinder.GetPath(Start, End);
            after.IsSuccess.Should().BeTrue();
            after.Path.Should().NotContain(heavy);
        }

        [Test]
        public void SetCell_CopiesTheUserFieldsIntoThatCellOnly()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateMatrix());
            var target = new Coordinate(3, 2);

            // Act
            pathfinder.SetCell(target, new PathCell { IsOccupied = true, Weight = 400 });

            // Assert
            var cell = pathfinder.GetCell(target);
            cell.IsWalkable.Should().BeFalse();
            cell.IsOccupied.Should().BeTrue();
            cell.Weight.Should().Be(400);
            pathfinder.GetCell(new Coordinate(0, 0)).IsWalkable.Should().BeTrue();
        }

        [Test]
        public void SetOccupied_InArrayMode_IsVisibleInTheCallersArrayAtGetIndex()
        {
            // Arrange
            var cells = CreateOpenArray();
            using var pathfinder = new Pathfinder(cells, Width, Height, RowMajor);
            using (pathfinder.GetPath(Start, End))
            {
            }

            var target = new Coordinate(3, 2);

            // Act
            pathfinder.SetOccupied(target, true);

            // Assert
            var index = pathfinder.GetIndex(target);
            index.Should().Be(target.Y * Width + target.X);
            cells[index].IsOccupied.Should().BeTrue();
            cells.Count(c => c.IsOccupied).Should().Be(1);
        }

        [Test]
        public void SetWalkable_OnARowMajorArray_UpdatesTheCellAtItsRowMajorIndex()
        {
            // Arrange
            var cells = CreateOpenArray();
            using var pathfinder = new Pathfinder(cells, Width, Height, RowMajor);
            var target = new Coordinate(3, 2);

            // Act
            pathfinder.SetWalkable(target, false);

            // Assert
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

        [TestCase(Mode.RowMajorArray)]
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
                holders[target.X, target.Y].IsOccupied = true;
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
            pathfinder.GetCell(target).IsOccupied.Should().BeTrue();
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

        [TestCase(Mode.ColumnMajorArray)]
        [TestCase(Mode.Matrix)]
        public void Setters_InLayout_DoNotAllocate(Mode mode)
        {
            // Arrange
            using var pathfinder = Create(mode, out _, out _);
            var target = new Coordinate(3, 2);
            pathfinder.SetWalkable(target, false);
            pathfinder.SetOccupied(target, false);
            pathfinder.SetWeight(target, 0);

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
            yield return new TestCaseData((Action<Pathfinder>)(p => p.SetWeight(new Coordinate(Width, 0), 100)))
                .SetName("SetWeight_OutsideGrid_ThrowsArgumentException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.SetCell(new Coordinate(Width, 0), new PathCell())))
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

        private static readonly PathfinderSettings RowMajor = new PathfinderSettings
        {
            CellLayout = CellLayout.RowMajor
        };

        private static Pathfinder Create(Mode mode, out PathCell[] cells, out Holder[,] holders)
        {
            cells = null!;
            holders = null!;

            switch (mode)
            {
                case Mode.RowMajorArray:
                    cells = CreateOpenArray();
                    return new Pathfinder(cells, Width, Height, RowMajor);
                case Mode.ColumnMajorArray:
                    return new Pathfinder(CreateOpenArray(), Width, Height);
                case Mode.Matrix:
                    return new Pathfinder(CreateMatrix());
                case Mode.HolderArray:
                    holders = CreateHolders();
                    var holderArray = new ICellHolder[Width * Height];
                    for (var x = 0; x < Width; x++)
                    {
                        for (var y = 0; y < Height; y++)
                        {
                            holderArray[x * Height + y] = holders[x, y];
                        }
                    }

                    return new Pathfinder(holderArray, Width, Height);
                default:
                    holders = CreateHolders();
                    return new Pathfinder((ICellHolder[,])holders);
            }
        }

        private static Holder[,] CreateHolders()
        {
            var holders = new Holder[Width, Height];
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    holders[x, y] = new Holder(new PathCell { IsWalkable = true });
                }
            }

            return holders;
        }

        private static PathCell[] CreateOpenArray()
        {
            var cells = new PathCell[Width * Height];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = new PathCell { IsWalkable = true };
            }

            return cells;
        }

        private static PathCell[,] CreateMatrix()
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

        public sealed class Holder : ICellHolder
        {
            private PathCell _cell;

            public Holder(PathCell cell)
            {
                _cell = cell;
            }

            public PathCell CellData => _cell;

            public bool IsOccupied
            {
                set => _cell.IsOccupied = value;
            }
        }
    }
}
