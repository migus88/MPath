using System;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Tests.Implementations;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// Pins how <see cref="Pathfinder.GetPath"/> validates its endpoints: an origin outside the grid throws, and a
    /// destination the agent cannot enter fails before any cell is expanded.
    /// </summary>
    [TestFixture]
    public class PathfinderQueryValidationTests
    {
        private const int GridSize = 10;

        private static readonly Coordinate Origin = new Coordinate(1, 1);
        private static readonly Coordinate Destination = new Coordinate(5, 5);

        public enum Blocker
        {
            Wall,
            Occupied,
            NoClearanceForSize2,
        }

        [TestCase(-1, 3)]
        [TestCase(GridSize, 3)]
        [TestCase(3, -1)]
        [TestCase(3, GridSize)]
        public void GetPath_WithOriginOutsideGrid_ThrowsArgumentException(int x, int y)
        {
            using var pathfinder = new Pathfinder(CreateEmptyGrid(), GridSize, GridSize);

            var action = () => pathfinder.GetPath(new Agent { Size = 1 }, new Coordinate(x, y), Destination);

            action.Should().Throw<ArgumentException>().WithParameterName("from");
        }

        [TestCase(Blocker.Wall)]
        [TestCase(Blocker.Occupied)]
        [TestCase(Blocker.NoClearanceForSize2)]
        public void GetPath_WithNonEnterableDestination_ReturnsFailure(Blocker blocker)
        {
            var (cells, agent) = CreateBlockedScenario(blocker);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, OccupiedCellsBlock());

            using var result = pathfinder.GetPath(agent, Origin, Destination);

            result.IsSuccess.Should().BeFalse();
        }

        [TestCase(Blocker.Wall)]
        [TestCase(Blocker.Occupied)]
        [TestCase(Blocker.NoClearanceForSize2)]
        public void BeginStepwiseSearch_WithNonEnterableDestination_FailsWithoutExpanding(Blocker blocker)
        {
            var (cells, agent) = CreateBlockedScenario(blocker);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, OccupiedCellsBlock());

            using var search = pathfinder.BeginStepwiseSearch(agent, Origin, Destination);
            var final = search.Tick();

            search.IsComplete.Should().BeTrue();
            final.State.Should().Be(SearchState.Failure);
            final.Iteration.Should().Be(0);
            final.ClosedCount.Should().Be(0);
        }

        [Test]
        public void GetPath_WithOccupiedDestinationAndOccupancyIgnored_FindsPath()
        {
            var (cells, agent) = CreateBlockedScenario(Blocker.Occupied);
            var settings = new PathfinderSettings { IsCalculatingOccupiedCells = false };
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);

            using var result = pathfinder.GetPath(agent, Origin, Destination);

            result.IsSuccess.Should().BeTrue();
            result.Get(result.Length - 1).Should().Be(Destination);
        }

        [Test]
        public void GetPath_FromOccupiedCellToItself_SucceedsWithEmptyPath()
        {
            var cells = CreateEmptyGrid();
            cells[Index(Origin)].IsOccupied = true;
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, OccupiedCellsBlock());

            using var result = pathfinder.GetPath(new Agent { Size = 1 }, Origin, Origin);

            result.IsSuccess.Should().BeTrue();
            result.Length.Should().Be(0);
        }

        private static (Cell[] Cells, Agent Agent) CreateBlockedScenario(Blocker blocker)
        {
            var cells = CreateEmptyGrid();

            switch (blocker)
            {
                case Blocker.Wall:
                    cells[Index(Destination)].IsWalkable = false;
                    return (cells, new Agent { Size = 1 });
                case Blocker.Occupied:
                    cells[Index(Destination)].IsOccupied = true;
                    return (cells, new Agent { Size = 1 });
                case Blocker.NoClearanceForSize2:
                    cells[Index(new Coordinate(Destination.X + 1, Destination.Y + 1))].IsWalkable = false;
                    return (cells, new Agent { Size = 2 });
                default:
                    throw new ArgumentOutOfRangeException(nameof(blocker), blocker, null);
            }
        }

        private static PathfinderSettings OccupiedCellsBlock()
        {
            return new PathfinderSettings { IsCalculatingOccupiedCells = true };
        }

        private static int Index(Coordinate coordinate)
        {
            return coordinate.X * GridSize + coordinate.Y;
        }

        private static Cell[] CreateEmptyGrid()
        {
            var cells = new Cell[GridSize * GridSize];
            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                {
                    cells[x * GridSize + y] = new Cell
                    {
                        Coordinate = new Coordinate(x, y),
                        IsWalkable = true,
                    };
                }
            }

            return cells;
        }
    }
}
