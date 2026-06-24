using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Tests.Implementations;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathfinderStepwiseTests
    {
        private const int GridSize = 10;

        [Test]
        public void BeginStepwiseSearch_RunToCompletion_ProducesSamePathAsGetPath()
        {
            var from = new Coordinate(1, 1);
            var to = new Coordinate(8, 6);
            var agent = new Agent { Size = 1 };

            // Default settings use no smoothing, so GetPath returns the raw A* path the stepwise search traces.
            List<Coordinate> batchPath;
            using (var batchPathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize))
            using (var batchResult = batchPathfinder.GetPath(agent, from, to))
            {
                batchResult.IsSuccess.Should().BeTrue();
                batchPath = batchResult.Path.ToList();
            }

            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);
            using var search = pathfinder.BeginStepwiseSearch(agent, from, to);

            var final = search.RunToCompletion();

            final.State.Should().Be(SearchState.Success);
            final.IsComplete.Should().BeTrue();
            final.Path.Should().Equal(batchPath);
        }

        [Test]
        public void RunToCompletion_OnSuccess_PathReachesDestinationAndIsContiguous()
        {
            var from = new Coordinate(0, 0);
            var to = new Coordinate(9, 9);
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);

            using var search = pathfinder.BeginStepwiseSearch(new Agent { Size = 1 }, from, to);
            var final = search.RunToCompletion();

            final.State.Should().Be(SearchState.Success);

            // Following the PathResult convention, the origin is excluded and the path ends at the destination.
            final.Path.Should().NotBeEmpty();
            final.Path.Last().Should().Be(to);
            final.Path.Should().NotContain(from);

            // Every consecutive pair must be adjacent (Chebyshev distance 1).
            var steps = new[] { from }.Concat(final.Path).ToList();
            for (var i = 1; i < steps.Count; i++)
            {
                Pathfinder.GetChebyshevDistance(steps[i - 1], steps[i]).Should().Be(1);
            }
        }

        [Test]
        public void Tick_AccumulatesSearchedAreaMonotonically()
        {
            var from = new Coordinate(5, 5);
            var to = new Coordinate(9, 9);
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);
            using var search = pathfinder.BeginStepwiseSearch(new Agent { Size = 1 }, from, to);

            var previousSearched = 0;
            SearchStep step;

            do
            {
                step = search.Tick();

                // The searched area never shrinks as the search advances.
                step.Searched.Count.Should().BeGreaterThanOrEqualTo(previousSearched);
                previousSearched = step.Searched.Count;

                // Open + Closed always partitions the searched set.
                (step.OpenCount + step.ClosedCount).Should().Be(step.Searched.Count);
            }
            while (!step.IsComplete);

            step.State.Should().Be(SearchState.Success);
            step.Searched.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public void Tick_WhileInProgress_ClosesExactlyOneCellPerExpansion()
        {
            var from = new Coordinate(0, 0);
            var to = new Coordinate(9, 9);
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);
            using var search = pathfinder.BeginStepwiseSearch(new Agent { Size = 1 }, from, to);

            SearchStep step;
            do
            {
                step = search.Tick();

                if (step.State == SearchState.InProgress)
                {
                    // Each in-progress expansion closes exactly one (previously open) cell.
                    step.ClosedCount.Should().Be(step.Iteration);

                    // The most recently expanded cell is part of the searched area and marked closed.
                    var currentNode = step.Searched.Single(n => n.Coordinate == step.Current);
                    currentNode.State.Should().Be(SearchNodeState.Closed);
                }
            }
            while (!step.IsComplete);
        }

        [Test]
        public void Searched_ExposesAStarScoresForTheOrigin()
        {
            var from = new Coordinate(3, 4);
            var to = new Coordinate(7, 7);
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);
            using var search = pathfinder.BeginStepwiseSearch(new Agent { Size = 1 }, from, to);

            var first = search.Tick();

            var originNode = first.Searched.Single(n => n.Coordinate == from);
            originNode.ScoreG.Should().Be(0f);
            // f = g + h is the priority by which the open set is ordered.
            originNode.ScoreF.Should().BeApproximately(originNode.ScoreG + originNode.ScoreH, 0.0001f);
        }

        [Test]
        public void BeginStepwiseSearch_WithUnreachableDestination_ReportsFailure()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            var settings = new PathfinderSettings { IsDiagonalMovementEnabled = false };

            // Fence the destination (9,9) off from the rest of the grid.
            SetWalkable(cells, 8, 9, false);
            SetWalkable(cells, 9, 8, false);

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            using var search = pathfinder.BeginStepwiseSearch(new Agent { Size = 1 },
                new Coordinate(0, 0), new Coordinate(9, 9));

            var final = search.RunToCompletion();

            final.State.Should().Be(SearchState.Failure);
            final.IsComplete.Should().BeTrue();
            final.Path.Should().BeEmpty();

            // A failed search exhausts the frontier: everything reachable ends up closed.
            final.OpenCount.Should().Be(0);
            final.ClosedCount.Should().Be(final.Searched.Count);
            final.Searched.Should().NotContain(n => n.Coordinate == new Coordinate(9, 9));
        }

        [Test]
        public void StepwiseSearch_WhenOriginEqualsDestination_SucceedsOnFirstTick()
        {
            var origin = new Coordinate(4, 4);
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);
            using var search = pathfinder.BeginStepwiseSearch(new Agent { Size = 1 }, origin, origin);

            var step = search.Tick();

            step.State.Should().Be(SearchState.Success);
            // Matching GetPath, an already-at-destination search yields an empty path.
            step.Path.Should().BeEmpty();
        }

        [Test]
        public void Tick_AfterCompletion_IsIdempotent()
        {
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);
            using var search = pathfinder.BeginStepwiseSearch(new Agent { Size = 1 },
                new Coordinate(0, 0), new Coordinate(2, 2));

            var final = search.RunToCompletion();
            var iterationAtCompletion = final.Iteration;

            var again = search.Tick();

            again.Should().BeSameAs(final);
            again.Iteration.Should().Be(iterationAtCompletion);
        }

        [Test]
        public void BeginStepwiseSearch_WhileAnotherSearchIsActive_Throws()
        {
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);
            var agent = new Agent { Size = 1 };

            var first = pathfinder.BeginStepwiseSearch(agent, new Coordinate(0, 0), new Coordinate(5, 5));

            var startSecond = () => pathfinder.BeginStepwiseSearch(agent, new Coordinate(0, 0), new Coordinate(5, 5));
            startSecond.Should().Throw<InvalidOperationException>();

            // Disposing the first session frees the instance for a new search.
            first.Dispose();
            using var second = pathfinder.BeginStepwiseSearch(agent, new Coordinate(0, 0), new Coordinate(5, 5));
            second.RunToCompletion().State.Should().Be(SearchState.Success);
        }

        [Test]
        public void Pathfinder_CanRunGetPathAfterAStepwiseSearchIsDisposed()
        {
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);
            var agent = new Agent { Size = 1 };
            var from = new Coordinate(0, 0);
            var to = new Coordinate(7, 7);

            using (var search = pathfinder.BeginStepwiseSearch(agent, from, to))
            {
                search.RunToCompletion().State.Should().Be(SearchState.Success);
            }

            // The pinned buffer is released on dispose, so the regular API works again.
            using var result = pathfinder.GetPath(agent, from, to);
            result.IsSuccess.Should().BeTrue();
        }

        [Test]
        public void Tick_AfterDisposal_ThrowsObjectDisposedException()
        {
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);
            var search = pathfinder.BeginStepwiseSearch(new Agent { Size = 1 },
                new Coordinate(0, 0), new Coordinate(5, 5));

            search.Dispose();

            var action = () => search.Tick();
            action.Should().Throw<ObjectDisposedException>();
        }

        [Test]
        public void BeginStepwiseSearch_WithNullAgent_ThrowsArgumentNullException()
        {
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);

            var action = () => pathfinder.BeginStepwiseSearch(null!, new Coordinate(0, 0), new Coordinate(5, 5));

            action.Should().Throw<ArgumentNullException>().WithParameterName("agent");
        }

        [Test]
        public void BeginStepwiseSearch_WithInvalidOrigin_ThrowsArgumentException()
        {
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);

            var action = () => pathfinder.BeginStepwiseSearch(new Agent { Size = 1 },
                new Coordinate(GridSize + 3, 0), new Coordinate(5, 5));

            action.Should().Throw<ArgumentException>().WithParameterName("from");
        }

        [Test]
        public void BeginStepwiseSearch_WithInvalidDestination_ThrowsArgumentException()
        {
            using var pathfinder = new Pathfinder(CreateEmptyGrid(GridSize, GridSize), GridSize, GridSize);

            var action = () => pathfinder.BeginStepwiseSearch(new Agent { Size = 1 },
                new Coordinate(0, 0), new Coordinate(GridSize + 3, 0));

            action.Should().Throw<ArgumentException>().WithParameterName("to");
        }

        [Test]
        public void RunToCompletion_AroundAWall_FindsTheSamePathAsGetPath()
        {
            var from = new Coordinate(0, 5);
            var to = new Coordinate(9, 5);
            var agent = new Agent { Size = 1 };

            static Cell[] BuildWalledGrid()
            {
                var cells = CreateEmptyGrid(GridSize, GridSize);
                // Vertical wall down the middle with a single gap at the top.
                for (var y = 1; y < GridSize; y++)
                {
                    SetWalkable(cells, 5, y, false);
                }

                return cells;
            }

            var settings = new PathfinderSettings { IsDiagonalMovementEnabled = false };

            List<Coordinate> batchPath;
            using (var batchPathfinder = new Pathfinder(BuildWalledGrid(), GridSize, GridSize, settings))
            using (var batchResult = batchPathfinder.GetPath(agent, from, to))
            {
                batchResult.IsSuccess.Should().BeTrue();
                batchPath = batchResult.Path.ToList();
            }

            using var pathfinder = new Pathfinder(BuildWalledGrid(), GridSize, GridSize, settings);
            using var search = pathfinder.BeginStepwiseSearch(agent, from, to);
            var final = search.RunToCompletion();

            final.State.Should().Be(SearchState.Success);
            final.Path.Should().Equal(batchPath);

            // The wall cells must never appear in the searched area.
            final.Searched.Should().NotContain(n => n.Coordinate.X == 5 && n.Coordinate.Y >= 1);
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
