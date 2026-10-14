using System;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// The search itself allocates nothing once a pathfinder exists: the open set is sized to the grid at
    /// construction, so a query with a much wider frontier than any before it still only allocates its result.
    /// </summary>
    [TestFixture]
    public class PathfinderAllocationTests
    {
        private const int GridSize = 120;
        private const int MaxResultBytes = 256;

        [Test]
        public void GetReachable_WiderFrontierThanWarmUp_AllocatesOnlyTheResult()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateOpenGrid());
            var center = new Coordinate(GridSize / 2, GridSize / 2);

            using (pathfinder.GetReachable(center, 1f))
            {
            }

            // Act
            var before = GC.GetAllocatedBytesForCurrentThread();

            using (pathfinder.GetReachable(center, float.MaxValue))
            {
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            allocated.Should().BeLessThan(MaxResultBytes);
        }

        [Test]
        public void GetPath_WiderFrontierThanWarmUp_AllocatesOnlyTheResult()
        {
            // Arrange
            var grid = CreateOpenGrid();
            var wall = GridSize / 2;

            for (var y = 0; y < GridSize - 1; y++)
            {
                grid[wall, y].IsWalkable = false;
            }

            using var pathfinder = new Pathfinder(grid);
            var from = new Coordinate(wall - 1, 0);
            var to = new Coordinate(wall + 1, 0);

            using (var poolWarmUp = new Pathfinder(grid))
            using (poolWarmUp.GetPath(from, to))
            {
            }

            using (pathfinder.GetPath(from, new Coordinate(wall - 1, 1)))
            {
            }

            // Act
            var before = GC.GetAllocatedBytesForCurrentThread();
            bool isSuccess;

            using (var result = pathfinder.GetPath(from, to))
            {
                isSuccess = result.IsSuccess;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            isSuccess.Should().BeTrue();
            allocated.Should().BeLessThan(MaxResultBytes);
        }

        private static Cell[,] CreateOpenGrid()
        {
            var cells = new Cell[GridSize, GridSize];

            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                {
                    cells[x, y] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
                }
            }

            return cells;
        }
    }
}
