using System;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Tests.Implementations;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathfinderReachabilityTests
    {
        private const int GridSize = 10;

        [Test]
        public void GetReachable_WithZeroBudget_ReturnsOnlyOrigin()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };
            var origin = new Coordinate(5, 5);

            using var result = pathfinder.GetReachable(agent, origin, 0f);

            result.IsSuccess.Should().BeTrue();
            result.Length.Should().Be(1);
            result.Get(0).Coordinate.Should().Be(origin);
            result.Get(0).Cost.Should().Be(0f);
        }

        [Test]
        public void GetReachable_WithNegativeBudget_ReturnsEmpty()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };

            using var result = pathfinder.GetReachable(agent, new Coordinate(5, 5), -1f);

            result.IsSuccess.Should().BeFalse();
            result.Length.Should().Be(0);
        }

        [Test]
        public void GetReachable_WithoutDiagonals_ReturnsManhattanDiamond()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = false,
                IsCellWeightEnabled = false
            };

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };
            var origin = new Coordinate(5, 5);

            using var result = pathfinder.GetReachable(agent, origin, 2f);

            // All cells with Manhattan distance <= 2: 1 + 4 + 8 = 13
            result.Length.Should().Be(13);

            // Every reachable cell's cost must equal its Manhattan distance (cheapest path),
            // which also verifies Dijkstra picks the minimal cost for each cell.
            foreach (var cell in result.Cells)
            {
                var manhattan = Math.Abs(cell.Coordinate.X - origin.X) +
                                Math.Abs(cell.Coordinate.Y - origin.Y);
                cell.Cost.Should().BeApproximately(manhattan, 0.0001f);
                cell.Cost.Should().BeLessThanOrEqualTo(2f);
            }
        }

        [Test]
        public void GetReachable_WithDiagonals_ReachesDiagonalNeighbors()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = true,
                IsCellWeightEnabled = false,
                DiagonalMovementMultiplier = 1.41f,
                StraightMovementMultiplier = 1f
            };

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };
            var origin = new Coordinate(5, 5);

            // Budget covers the 4 cardinal (1.0) and 4 diagonal (1.41) neighbours, but not two steps (>= 2.0).
            using var result = pathfinder.GetReachable(agent, origin, 1.45f);

            result.Length.Should().Be(9);
            result.Contains(new Coordinate(6, 6)).Should().BeTrue();
            result.TryGetCost(new Coordinate(6, 6), out var diagonalCost).Should().BeTrue();
            diagonalCost.Should().BeApproximately(1.41f, 0.0001f);
        }

        [Test]
        public void GetReachable_FromAsymmetricOrigin_ReportsTrueManhattanCosts()
        {
            // Guards against grid transposition: costs/coordinates must reflect the real grid layout,
            // not a swapped (y, x) one. Uses an off-diagonal origin and off-axis targets.
            var cells = CreateEmptyGrid(GridSize, GridSize);
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = false,
                IsCellWeightEnabled = false
            };

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };
            var origin = new Coordinate(2, 1);

            using var result = pathfinder.GetReachable(agent, origin, 3f);

            result.TryGetCost(new Coordinate(5, 1), out var eastCost).Should().BeTrue();
            eastCost.Should().BeApproximately(3f, 0.0001f);

            result.TryGetCost(new Coordinate(2, 4), out var southCost).Should().BeTrue();
            southCost.Should().BeApproximately(3f, 0.0001f);

            result.TryGetCost(new Coordinate(4, 2), out var lCost).Should().BeTrue();
            lCost.Should().BeApproximately(3f, 0.0001f);

            // (6,1) is Manhattan distance 4 from (2,1) - outside the budget.
            result.Contains(new Coordinate(6, 1)).Should().BeFalse();
        }

        [Test]
        public void GetReachable_AllCostsAreWithinBudget()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };

            const float budget = 5f;
            using var result = pathfinder.GetReachable(agent, new Coordinate(5, 5), budget);

            result.Length.Should().BeGreaterThan(1);
            foreach (var cell in result.Cells)
            {
                cell.Cost.Should().BeLessThanOrEqualTo(budget);
            }
        }

        [Test]
        public void GetReachable_WithWalls_DoesNotReachBlockedCells()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = false,
                IsCellWeightEnabled = false
            };

            // Wall the origin (5,5) in completely (its four cardinal neighbours).
            SetWalkable(cells, 4, 5, false);
            SetWalkable(cells, 6, 5, false);
            SetWalkable(cells, 5, 4, false);
            SetWalkable(cells, 5, 6, false);

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };

            using var result = pathfinder.GetReachable(agent, new Coordinate(5, 5), 10f);

            // Only the origin itself is reachable.
            result.Length.Should().Be(1);
            result.Contains(new Coordinate(4, 5)).Should().BeFalse();
        }

        [Test]
        public void GetReachable_WithCellWeight_IncreasesCostAndCanExcludeCells()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = false,
                IsCellWeightEnabled = true
            };

            // Make the cell directly east of the origin expensive to enter.
            cells[6 * GridSize + 5].Weight = 5f;

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };

            using var result = pathfinder.GetReachable(agent, new Coordinate(5, 5), 2f);

            // Weight is additive: entering (6,5) costs travel(1) + weight(5) = 6 (> budget), and (7,5)
            // is only reachable through it within budget.
            result.Contains(new Coordinate(6, 5)).Should().BeFalse();
            result.Contains(new Coordinate(7, 5)).Should().BeFalse();
            // A normal neighbour still costs travel(1) + weight(1) = 2 and is reachable.
            result.TryGetCost(new Coordinate(4, 5), out var westCost).Should().BeTrue();
            westCost.Should().BeApproximately(2f, 0.0001f);
        }

        [Test]
        public void GetReachable_WithDefaultZeroWeightAndWeightingEnabled_StaysWithinBudget()
        {
            // Regression: Cell.Weight defaults to 0 and IsCellWeightEnabled defaults to true.
            // A multiplicative weight model would make every step cost travel * 0 = 0, so the budget
            // would never bite and the flood fill would return the entire walkable board.
            var cells = new Cell[GridSize * GridSize];
            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                {
                    cells[x * GridSize + y] = new Cell
                    {
                        Coordinate = new Coordinate(x, y),
                        IsWalkable = true,
                        IsOccupied = false
                        // Weight intentionally left at its default of 0.
                    };
                }
            }

            var settings = new PathfinderSettings { IsDiagonalMovementEnabled = false };
            settings.IsCellWeightEnabled.Should().BeTrue(); // guard the default this test depends on

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };

            using var result = pathfinder.GetReachable(agent, new Coordinate(5, 5), 2f);

            // Step cost is travel(1) + weight(0) = 1, so budget 2 yields the Manhattan diamond (13 cells),
            // NOT the whole 100-cell board.
            result.Length.Should().Be(13);
            result.Length.Should().BeLessThan(GridSize * GridSize);
        }

        [Test]
        public void GetReachable_WithLargerAgent_ReachesFewerCellsNearBorder()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var origin = new Coordinate(0, 0);

            using var smallResult = pathfinder.GetReachable(new Agent { Size = 1 }, origin, 1000f);
            using var largeResult = pathfinder.GetReachable(new Agent { Size = 2 }, origin, 1000f);

            // A 1x1 agent can stand on every cell of the open grid.
            smallResult.Length.Should().Be(GridSize * GridSize);

            // A 2x2 agent needs clearance, so the last row/column are unreachable.
            largeResult.Length.Should().Be((GridSize - 1) * (GridSize - 1));
            largeResult.Contains(new Coordinate(GridSize - 1, GridSize - 1)).Should().BeFalse();
        }

        [Test]
        public void GetReachable_WithInvalidOrigin_ThrowsArgumentException()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };

            var action = () => pathfinder.GetReachable(agent, new Coordinate(GridSize + 5, GridSize + 5), 5f);

            action.Should().Throw<ArgumentException>()
                  .WithParameterName("from");
        }

        [Test]
        public void GetReachable_WithNullAgent_ThrowsArgumentNullException()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            var action = () => pathfinder.GetReachable(null!, new Coordinate(0, 0), 5f);

            action.Should().Throw<ArgumentNullException>()
                  .WithParameterName("agent");
        }

        [Test]
        public void GetReachable_AfterDisposal_ThrowsObjectDisposedException()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };

            var result = pathfinder.GetReachable(agent, new Coordinate(5, 5), 3f);
            result.Dispose();

            var action = () => result.Get(0);
            action.Should().Throw<ObjectDisposedException>();
        }

        [Test]
        public void GetReachable_CanBeCalledRepeatedlyOnSameInstance()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };
            var origin = new Coordinate(5, 5);

            using (var first = pathfinder.GetReachable(agent, origin, 3f))
            {
                first.Length.Should().BeGreaterThan(1);
            }

            using var second = pathfinder.GetReachable(agent, origin, 3f);
            second.Length.Should().BeGreaterThan(1);
            second.Contains(origin).Should().BeTrue();
        }

        private static void SetWalkable(Cell[] cells, int x, int y, bool walkable)
        {
            cells[x * GridSize + y].IsWalkable = walkable;
        }

        private static Cell[] CreateEmptyGrid(int width, int height)
        {
            var cells = new Cell[width * height];
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    var index = x * height + y;
                    cells[index] = new Cell
                    {
                        Coordinate = new Coordinate(x, y),
                        IsWalkable = true,
                        IsOccupied = false,
                        Weight = 1.0f
                    };
                }
            }

            return cells;
        }
    }
}
