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
    /// Pins the <c>agentSize</c> argument of <see cref="Pathfinder.GetPath(Coordinate, Coordinate, int)"/>,
    /// <see cref="Pathfinder.GetReachable(Coordinate, int, int)"/> and
    /// <see cref="Pathfinder.BeginStepwiseSearch(Coordinate, Coordinate, int)"/> (with and without
    /// <see cref="QueryOptions"/>): it is always the third argument, defaults to a single-cell agent, rejects sizes
    /// below 1 (including a bare <c>default</c>) and keys the path cache.
    /// </summary>
    [TestFixture]
    public class PathfinderAgentSizeTests
    {
        private const int GridSize = 12;
        private const int Budget = 900;
        private const int WallRow = 2;
        private const int NarrowGapX = 3;
        private const int WideGapX = 7;

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

        [TestCase(Query.Path)]
        [TestCase(Query.Reachable)]
        [TestCase(Query.Stepwise)]
        public void Queries_WithoutSize_UseSingleCellAgent(Query query)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid());

            // Act
            var expected = Run(query, pathfinder, 1);
            var actual = RunWithDefaultSize(query, pathfinder);

            // Assert
            actual.Should().Equal(expected);
        }

        [TestCase(Query.Path, 1, WideGapX)]
        [TestCase(Query.Path, 2, WideGapX)]
        [TestCase(Query.PathWithOptions, 1, NarrowGapX)]
        [TestCase(Query.PathWithOptions, 2, WideGapX)]
        [TestCase(Query.Stepwise, 1, WideGapX)]
        [TestCase(Query.Stepwise, 2, WideGapX)]
        [TestCase(Query.StepwiseWithOptions, 1, NarrowGapX)]
        [TestCase(Query.StepwiseWithOptions, 2, WideGapX)]
        public void PathQueries_WithAgentSize_CrossTheWallWhereTheFootprintFits(Query query, int agentSize,
            int expectedGapX)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid());

            // Act
            var path = Run(query, pathfinder, agentSize).OfType<Coordinate>().ToArray();

            // Assert
            path.Where(coordinate => coordinate.Y == WallRow).Select(coordinate => coordinate.X)
                .Should().Equal(expectedGapX);
        }

        [TestCase(Query.Reachable, 1, true)]
        [TestCase(Query.Reachable, 2, false)]
        [TestCase(Query.ReachableWithOptions, 1, true)]
        [TestCase(Query.ReachableWithOptions, 2, false)]
        public void ReachableQueries_WithAgentSize_ReachTheNarrowGapOnlyForASingleCellAgent(Query query,
            int agentSize, bool expectedNarrowGapReached)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid());

            // Act
            var reached = Run(query, pathfinder, agentSize).Cast<ReachableCell>()
                .Select(cell => cell.Coordinate).ToArray();

            // Assert
            reached.Should().Contain(new Coordinate(WideGapX, WallRow));
            reached.Contains(new Coordinate(NarrowGapX, WallRow)).Should().Be(expectedNarrowGapReached);
        }

        [TestCase(Query.Path)]
        [TestCase(Query.PathWithOptions)]
        [TestCase(Query.Reachable)]
        [TestCase(Query.ReachableWithOptions)]
        [TestCase(Query.Stepwise)]
        [TestCase(Query.StepwiseWithOptions)]
        public void Queries_WithSizeZero_ThrowArgumentOutOfRangeException(Query query)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid());

            // Act
            var action = () => Run(query, pathfinder, 0);

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("agentSize");
        }

        [TestCase(Query.Path)]
        [TestCase(Query.Reachable)]
        [TestCase(Query.Stepwise)]
        public void Queries_WithBareDefaultAsThirdArgument_ThrowForAgentSize(Query query)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid());

            // Act
            Action action = query switch
            {
                Query.Path => () => pathfinder.GetPath(From, To, default),
                Query.Reachable => () => pathfinder.GetReachable(From, Budget, default),
                _ => () => pathfinder.BeginStepwiseSearch(From, To, default),
            };

            // Assert
            action.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("agentSize");
        }

        [Test]
        public void GetPath_WithCustomCache_PassesTheQueryAgentSize()
        {
            // Arrange
            var cache = Substitute.For<IPathCaching>();
            using var pathfinder = new Pathfinder(CreateGrid()).EnablePathCaching(cache);

            // Act
            using (pathfinder.GetPath(From, To, 2))
            {
            }

            // Assert
            cache.Received(1).TryGetCachedPath(2, From, To, out Arg.Any<PathResult>());
            cache.Received(1).CachePath(2, From, To, Arg.Any<PathResult>());
        }

        [Test]
        public void GetPath_WithDefaultCache_DoesNotShareEntriesBetweenSizes()
        {
            // Arrange
            using var reference = new Pathfinder(CreateGrid());
            using var uncached = reference.GetPath(From, To, 2);
            var expected = uncached.Path.ToArray();
            using var pathfinder = new Pathfinder(CreateGrid()).EnablePathCaching(new DefaultPathCaching());
            using var sizeOne = pathfinder.GetPath(From, To, 1);

            // Act
            using var sizeTwo = pathfinder.GetPath(From, To, 2);

            // Assert
            sizeTwo.Path.Should().Equal(expected);
            sizeTwo.Path.Should().NotEqual(sizeOne.Path);
        }

        private static object[] Run(Query query, Pathfinder pathfinder, int agentSize)
        {
            switch (query)
            {
                case Query.Path:
                    return ToItems(pathfinder.GetPath(From, To, agentSize));
                case Query.PathWithOptions:
                    return ToItems(pathfinder.GetPath(From, To, agentSize, Options()));
                case Query.Reachable:
                    return ToItems(pathfinder.GetReachable(From, Budget, agentSize));
                case Query.ReachableWithOptions:
                    return ToItems(pathfinder.GetReachable(From, Budget, agentSize, Options()));
                case Query.Stepwise:
                    return ToItems(pathfinder.BeginStepwiseSearch(From, To, agentSize));
                case Query.StepwiseWithOptions:
                    return ToItems(pathfinder.BeginStepwiseSearch(From, To, agentSize, Options()));
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
                case Query.Reachable:
                    return ToItems(pathfinder.GetReachable(From, Budget));
                case Query.Stepwise:
                    return ToItems(pathfinder.BeginStepwiseSearch(From, To));
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
                return step.Path.Cast<object>().ToArray();
            }
        }

        // A wall row at y = 2 with a one-cell gap at x = 3 and a two-cell gap at x = 7..8: only a single-cell agent
        // fits the narrow gap. The occupied cell behind it blocks that way unless the options ignore occupancy.
        private static Cell[,] CreateGrid()
        {
            var cells = new Cell[GridSize, GridSize];

            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                {
                    cells[x, y] = new Cell
                    {
                        IsWalkable = y != WallRow || x == NarrowGapX || x == WideGapX || x == WideGapX + 1
                    };
                }
            }

            cells[Unit.X, Unit.Y].IsOccupied = true;

            return cells;
        }
    }
}
