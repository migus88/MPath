using System;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Caching;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using NSubstitute;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// Pins the <c>agentSize</c> overloads of <see cref="Pathfinder.GetPath(Coordinate, Coordinate, int)"/>,
    /// <see cref="Pathfinder.GetReachable(Coordinate, float, int)"/> and
    /// <see cref="Pathfinder.BeginStepwiseSearch(Coordinate, Coordinate, int)"/> (with and without
    /// <see cref="QueryOptions"/>): they match the <see cref="IAgent"/> overloads, default to a single-cell agent,
    /// reject sizes below 1 and share path cache entries with <see cref="IAgent"/> queries of the same size.
    /// </summary>
    [TestFixture]
    public class PathfinderAgentSizeTests
    {
        private const int GridSize = 12;
        private const float Budget = 9f;

        private static readonly Coordinate From = new Coordinate(0, 0);
        private static readonly Coordinate To = new Coordinate(1, 6);
        private static readonly Coordinate Unit = new Coordinate(3, 3);

        public enum Query
        {
            Path,
            PathWithOptions,
            Reachable,
            ReachableWithOptions,
            Stepwise,
            StepwiseWithOptions,
        }

        [Test]
        public void AgentSizeOverloads_MatchAgentOverloads_ForSizesOneAndTwo(
            [Values] Query query, [Values(1, 2)] int agentSize)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid());
            var agent = new SquareAgent(agentSize);

            // Act
            var expected = RunWithAgent(query, pathfinder, agent);
            var actual = RunWithAgentSize(query, pathfinder, agentSize);

            // Assert
            actual.Should().Equal(expected);
        }

        [TestCase(Query.Path)]
        [TestCase(Query.PathWithOptions)]
        [TestCase(Query.Reachable)]
        [TestCase(Query.ReachableWithOptions)]
        [TestCase(Query.Stepwise)]
        [TestCase(Query.StepwiseWithOptions)]
        public void AgentSizeOverloads_WithoutSize_UseSingleCellAgent(Query query)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid());

            // Act
            var expected = RunWithAgent(query, pathfinder, new SquareAgent(1));
            var actual = RunWithDefaultSize(query, pathfinder);

            // Assert
            actual.Should().Equal(expected);
        }

        [TestCase(Query.Path)]
        [TestCase(Query.PathWithOptions)]
        [TestCase(Query.Reachable)]
        [TestCase(Query.ReachableWithOptions)]
        [TestCase(Query.Stepwise)]
        [TestCase(Query.StepwiseWithOptions)]
        public void AgentSizeOverloads_WithSizeZero_ThrowArgumentOutOfRangeException(Query query)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid());

            // Act
            var action = () => RunWithAgentSize(query, pathfinder, 0);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("agentSize");
        }

        [Test]
        public void AgentOverloads_WithSizeZero_KeepTreatingAgentAsSingleCell()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid());

            // Act
            var expected = RunWithAgent(Query.Path, pathfinder, new SquareAgent(1));
            var actual = RunWithAgent(Query.Path, pathfinder, new SquareAgent(0));

            // Assert
            actual.Should().Equal(expected);
        }

        [Test]
        public void GetPath_WithAgentSize_SharesCacheEntriesWithAgentQueriesOfSameSize()
        {
            // Arrange
            var cells = CreateGrid();
            using var pathfinder = new Pathfinder(cells).EnablePathCaching();
            using var cached = pathfinder.GetPath(From, To, 2);
            var cachedPath = cached.Path.ToArray();
            cells[To.X, To.Y].IsWalkable = false;

            // Act
            using var hit = pathfinder.GetPath(new SquareAgent(2), From, To);

            // Assert
            hit.IsSuccess.Should().BeTrue();
            hit.Path.Should().Equal(cachedPath);
        }

        [Test]
        public void GetPath_WithAgentSize_PassesCacheAnAgentOfThatSize()
        {
            // Arrange
            var cache = Substitute.For<IPathCaching>();
            using var pathfinder = new Pathfinder(CreateGrid()).EnablePathCaching(cache);

            // Act
            using (pathfinder.GetPath(From, To, 2))
            {
            }

            using (pathfinder.GetPath(From, To, 2))
            {
            }

            // Assert
            var agents = cache.ReceivedCalls()
                .Where(call => call.GetMethodInfo().Name == nameof(IPathCaching.CachePath))
                .Select(call => (IAgent)call.GetArguments()[0]!)
                .ToArray();
            agents.Should().HaveCount(2);
            agents[0].Size.Should().Be(2);
            agents[1].Should().Be(agents[0]);
        }

        private static object[] RunWithAgent(Query query, Pathfinder pathfinder, IAgent agent)
        {
            switch (query)
            {
                case Query.Path:
                    return ToItems(pathfinder.GetPath(agent, From, To));
                case Query.PathWithOptions:
                    return ToItems(pathfinder.GetPath(agent, From, To, Options()));
                case Query.Reachable:
                    return ToItems(pathfinder.GetReachable(agent, From, Budget));
                case Query.ReachableWithOptions:
                    return ToItems(pathfinder.GetReachable(agent, From, Budget, Options()));
                case Query.Stepwise:
                    return ToItems(pathfinder.BeginStepwiseSearch(agent, From, To));
                case Query.StepwiseWithOptions:
                    return ToItems(pathfinder.BeginStepwiseSearch(agent, From, To, Options()));
                default:
                    throw new ArgumentOutOfRangeException(nameof(query), query, null);
            }
        }

        private static object[] RunWithAgentSize(Query query, Pathfinder pathfinder, int agentSize)
        {
            switch (query)
            {
                case Query.Path:
                    return ToItems(pathfinder.GetPath(From, To, agentSize));
                case Query.PathWithOptions:
                    return ToItems(pathfinder.GetPath(From, To, Options(), agentSize));
                case Query.Reachable:
                    return ToItems(pathfinder.GetReachable(From, Budget, agentSize));
                case Query.ReachableWithOptions:
                    return ToItems(pathfinder.GetReachable(From, Budget, Options(), agentSize));
                case Query.Stepwise:
                    return ToItems(pathfinder.BeginStepwiseSearch(From, To, agentSize));
                case Query.StepwiseWithOptions:
                    return ToItems(pathfinder.BeginStepwiseSearch(From, To, Options(), agentSize));
                default:
                    throw new ArgumentOutOfRangeException(nameof(query), query, null);
            }
        }

        private static object[] RunWithDefaultSize(Query query, Pathfinder pathfinder)
        {
            switch (query)
            {
                case Query.Path:
                    return ToItems(pathfinder.GetPath(From, To));
                case Query.PathWithOptions:
                    return ToItems(pathfinder.GetPath(From, To, Options()));
                case Query.Reachable:
                    return ToItems(pathfinder.GetReachable(From, Budget));
                case Query.ReachableWithOptions:
                    return ToItems(pathfinder.GetReachable(From, Budget, Options()));
                case Query.Stepwise:
                    return ToItems(pathfinder.BeginStepwiseSearch(From, To));
                case Query.StepwiseWithOptions:
                    return ToItems(pathfinder.BeginStepwiseSearch(From, To, Options()));
                default:
                    throw new ArgumentOutOfRangeException(nameof(query), query, null);
            }
        }

        private static QueryOptions Options()
        {
            return new QueryOptions(OccupancyRule.Ignore);
        }

        private static object[] ToItems(PathResult result)
        {
            using (result)
            {
                result.IsSuccess.Should().BeTrue();
                return result.Path.Cast<object>().ToArray();
            }
        }

        private static object[] ToItems(RangeResult result)
        {
            using (result)
            {
                return result.Cells.Cast<object>().ToArray();
            }
        }

        private static object[] ToItems(Pathfinder.StepwiseSearch search)
        {
            using (search)
            {
                var step = search.RunToCompletion();
                step.State.Should().Be(SearchState.Success);
                return step.Path.Cast<object>().Concat(step.Searched.Cast<object>()).ToArray();
            }
        }

        // A wall row at y = 2 with a one-cell gap at x = 3 and a two-cell gap at x = 7..8, so a size-2 agent takes
        // a different path than a single-cell one. An occupied cell behind the narrow gap makes the options matter.
        private static Cell[,] CreateGrid()
        {
            var cells = new Cell[GridSize, GridSize];

            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                {
                    cells[x, y] = new Cell
                    {
                        Coordinate = new Coordinate(x, y),
                        IsWalkable = y != 2 || x == 3 || x == 7 || x == 8
                    };
                }
            }

            cells[Unit.X, Unit.Y].IsOccupied = true;

            return cells;
        }

        private sealed class SquareAgent : IAgent
        {
            public SquareAgent(int size)
            {
                Size = size;
            }

            public int Size { get; }
        }
    }
}
