using System;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// Pins the per-query <see cref="QueryOptions"/>: the occupancy override and the always-enterable cells apply to
    /// <see cref="Pathfinder.GetPath(Coordinate, Coordinate, int)"/>,
    /// <see cref="Pathfinder.GetReachable(Coordinate, float, int)"/> and the stepwise
    /// search alike, for that query only, and default options reproduce the queries without options.
    /// </summary>
    [TestFixture]
    public class PathfinderQueryOptionsTests
    {
        private const float Budget = 100f;

        private static readonly Coordinate CorridorStart = new Coordinate(0, 0);

        public enum Query
        {
            Path,
            Reachable,
            Stepwise
        }

        [TestCase(Query.Path, true, OccupancyRule.UseSettings, false)]
        [TestCase(Query.Path, true, OccupancyRule.Ignore, true)]
        [TestCase(Query.Path, false, OccupancyRule.UseSettings, true)]
        [TestCase(Query.Path, false, OccupancyRule.Block, false)]
        [TestCase(Query.Reachable, true, OccupancyRule.Ignore, true)]
        [TestCase(Query.Reachable, false, OccupancyRule.Block, false)]
        [TestCase(Query.Stepwise, true, OccupancyRule.Ignore, true)]
        [TestCase(Query.Stepwise, false, OccupancyRule.Block, false)]
        public void Occupancy_OverridesSettingsForTheQuery(Query query, bool isCalculatingOccupiedCells,
            OccupancyRule occupancy, bool expectedReach)
        {
            // Arrange
            var cells = CreateCorridor(6, new Coordinate(2, 0));
            var settings = new PathfinderSettings { IsCalculatingOccupiedCells = isCalculatingOccupiedCells };
            using var pathfinder = new Pathfinder(cells, settings);

            // Act
            var isReached = CanReach(query, pathfinder, CorridorStart, new Coordinate(5, 0),
                new QueryOptions(occupancy));

            // Assert
            isReached.Should().Be(expectedReach);
        }

        [TestCase(Query.Path)]
        [TestCase(Query.Reachable)]
        [TestCase(Query.Stepwise)]
        public void AlwaysEnterable_EntersListedCellsOnly(Query query)
        {
            // Arrange
            var cells = CreateCorridor(6, new Coordinate(2, 0), new Coordinate(4, 0));
            using var pathfinder = new Pathfinder(cells);
            ReadOnlySpan<Coordinate> enterable = stackalloc Coordinate[] { new Coordinate(2, 0) };
            var options = new QueryOptions(alwaysEnterable: enterable);

            // Act
            var isBeforeSecondUnitReached = CanReach(query, pathfinder, CorridorStart,
                new Coordinate(3, 0), options);
            var isBehindSecondUnitReached = CanReach(query, pathfinder, CorridorStart,
                new Coordinate(5, 0), options);

            // Assert
            isBeforeSecondUnitReached.Should().BeTrue();
            isBehindSecondUnitReached.Should().BeFalse();
        }

        [Test]
        public void AlwaysEnterable_LeavesGridAndLaterQueriesUnchanged()
        {
            // Arrange
            var unit = new Coordinate(2, 0);
            var cells = CreateCorridor(6, unit);
            using var pathfinder = new Pathfinder(cells);
            var destination = new Coordinate(5, 0);

            // Act
            using (pathfinder.GetPath(CorridorStart, destination,
                       new QueryOptions(alwaysEnterable: new[] { unit })))
            {
            }

            using var later = pathfinder.GetPath(CorridorStart, destination);

            // Assert
            cells[unit.X, unit.Y].IsOccupied.Should().BeTrue();
            later.IsSuccess.Should().BeFalse();
        }

        [Test]
        public void AlwaysEnterable_DoesNotOpenWalls()
        {
            // Arrange
            var wall = new Coordinate(2, 0);
            var cells = CreateCorridor(6);
            cells[wall.X, wall.Y].IsWalkable = false;
            using var pathfinder = new Pathfinder(cells);

            // Act
            using var result = pathfinder.GetPath(CorridorStart, new Coordinate(5, 0),
                new QueryOptions(OccupancyRule.Ignore, new[] { wall }, true));

            // Assert
            result.IsSuccess.Should().BeFalse();
        }

        [TestCase(Query.Path, false, false)]
        [TestCase(Query.Path, true, true)]
        [TestCase(Query.Stepwise, false, false)]
        [TestCase(Query.Stepwise, true, true)]
        public void DestinationAlwaysEnterable_DecidesWhetherOccupiedDestinationIsReached(Query query,
            bool isDestinationAlwaysEnterable, bool expectedReach)
        {
            // Arrange
            var target = new Coordinate(4, 0);
            using var pathfinder = new Pathfinder(CreateCorridor(6, target));

            // Act
            var isReached = CanReach(query, pathfinder, CorridorStart, target,
                new QueryOptions(isDestinationAlwaysEnterable: isDestinationAlwaysEnterable));

            // Assert
            isReached.Should().Be(expectedReach);
        }

        [Test]
        public void DestinationAlwaysEnterable_IsIgnoredByReachable()
        {
            // Arrange
            var unit = new Coordinate(4, 0);
            using var pathfinder = new Pathfinder(CreateCorridor(6, unit));

            // Act
            using var range = pathfinder.GetReachable(CorridorStart, Budget,
                new QueryOptions(isDestinationAlwaysEnterable: true));

            // Assert
            range.Contains(unit).Should().BeFalse();
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void AlwaysEnterable_CountsAsClearForMultiCellAgent(bool isOwnFootprintEnterable, bool expectedReach)
        {
            // Arrange
            var ownFootprint = new[]
            {
                new Coordinate(0, 0), new Coordinate(1, 0), new Coordinate(0, 1), new Coordinate(1, 1)
            };
            var cells = CreateGrid(8, 2, ownFootprint);
            using var pathfinder = new Pathfinder(cells);
            var enterable = isOwnFootprintEnterable ? ownFootprint : Array.Empty<Coordinate>();

            // Act
            using var result = pathfinder.GetPath(CorridorStart, new Coordinate(6, 0),
                new QueryOptions(alwaysEnterable: enterable), 2);

            // Assert
            result.IsSuccess.Should().Be(expectedReach);
        }

        [TestCase(OccupancyRule.UseSettings, false, 2)]
        [TestCase(OccupancyRule.UseSettings, true, 1)]
        [TestCase(OccupancyRule.Ignore, false, 1)]
        public void Options_ReclassifyFlankForCornerRule(OccupancyRule occupancy, bool isFlankEnterable,
            int expectedLength)
        {
            // Arrange
            var flank = new Coordinate(1, 0);
            var cells = CreateGrid(3, 3, flank);
            var settings = new PathfinderSettings { CornerBlocking = CornerBlocking.All };
            using var pathfinder = new Pathfinder(cells, settings);
            var enterable = isFlankEnterable ? new[] { flank } : Array.Empty<Coordinate>();

            // Act
            using var result = pathfinder.GetPath(CorridorStart, new Coordinate(1, 1),
                new QueryOptions(occupancy, enterable));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Length.Should().Be(expectedLength);
        }

        [Test]
        public void GetPath_WithOptions_BypassesPathCache()
        {
            // Arrange
            var cells = CreateCorridor(6, new Coordinate(2, 0));
            using var pathfinder = new Pathfinder(cells).EnablePathCaching();
            var destination = new Coordinate(5, 0);

            // Act
            using var withOptions = pathfinder.GetPath(CorridorStart, destination,
                new QueryOptions(OccupancyRule.Ignore));
            using var withoutOptions = pathfinder.GetPath(CorridorStart, destination);

            // Assert
            withOptions.IsSuccess.Should().BeTrue();
            withoutOptions.IsSuccess.Should().BeFalse();
        }

        [Test]
        public void DefaultOptions_MatchQueriesWithoutOptions()
        {
            // Arrange
            var cells = CreateScatteredGrid(24);
            using var pathfinder = new Pathfinder(cells);
            var destination = new Coordinate(23, 23);

            // Act
            using var plainPath = pathfinder.GetPath(CorridorStart, destination);
            var expectedPath = plainPath.Path.ToArray();
            using var optionsPath = pathfinder.GetPath(CorridorStart, destination, default(QueryOptions));
            using var plainRange = pathfinder.GetReachable(CorridorStart, 12f);
            var expectedRange = plainRange.Cells.ToArray();
            using var optionsRange = pathfinder.GetReachable(CorridorStart, 12f, default(QueryOptions));

            // Assert
            plainPath.IsSuccess.Should().BeTrue();
            optionsPath.Path.Should().Equal(expectedPath);
            optionsRange.Cells.Should().Equal(expectedRange);
        }

        [Test]
        public void GetPath_WithOptions_AllocatesNoMoreThanWithout()
        {
            // Arrange
            var unit = new Coordinate(2, 1);
            using var pathfinder = new Pathfinder(CreateGrid(6, 2, unit));
            var destination = new Coordinate(5, 0);

            for (var i = 0; i < 3; i++)
            {
                QueryWithOptions(pathfinder, destination, unit);
                QueryWithoutOptions(pathfinder, destination);
            }

            // Act
            var before = GC.GetAllocatedBytesForCurrentThread();
            QueryWithOptions(pathfinder, destination, unit);
            var withOptions = GC.GetAllocatedBytesForCurrentThread() - before;

            before = GC.GetAllocatedBytesForCurrentThread();
            QueryWithoutOptions(pathfinder, destination);
            var withoutOptions = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            withOptions.Should().BeLessOrEqualTo(withoutOptions);
        }

        private static void QueryWithOptions(Pathfinder pathfinder, Coordinate destination,
            Coordinate unit)
        {
            Span<Coordinate> enterable = stackalloc Coordinate[] { unit };
            using var result = pathfinder.GetPath(CorridorStart, destination,
                new QueryOptions(OccupancyRule.Block, enterable, true));
        }

        private static void QueryWithoutOptions(Pathfinder pathfinder, Coordinate destination)
        {
            using var result = pathfinder.GetPath(CorridorStart, destination);
        }

        private static bool CanReach(Query query, Pathfinder pathfinder, Coordinate from,
            Coordinate to, QueryOptions options)
        {
            switch (query)
            {
                case Query.Path:
                {
                    using var result = pathfinder.GetPath(from, to, options);
                    return result.IsSuccess;
                }
                case Query.Reachable:
                {
                    using var range = pathfinder.GetReachable(from, Budget, options);
                    return range.Contains(to);
                }
                case Query.Stepwise:
                {
                    using var search = pathfinder.BeginStepwiseSearch(from, to, options);
                    return search.RunToCompletion().State == SearchState.Success;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(query), query, null);
            }
        }

        private static Cell[,] CreateCorridor(int length, params Coordinate[] occupied)
        {
            return CreateGrid(length, 1, occupied);
        }

        private static Cell[,] CreateGrid(int width, int height, params Coordinate[] occupied)
        {
            var cells = new Cell[width, height];

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    cells[x, y] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
                }
            }

            foreach (var coordinate in occupied)
            {
                cells[coordinate.X, coordinate.Y].IsOccupied = true;
            }

            return cells;
        }

        private static Cell[,] CreateScatteredGrid(int size)
        {
            var cells = CreateGrid(size, size);

            for (var x = 1; x < size - 1; x++)
            {
                for (var y = 1; y < size - 1; y++)
                {
                    var pattern = (x * 7 + y * 13) % 11;
                    cells[x, y].IsWalkable = pattern != 0;
                    cells[x, y].IsOccupied = pattern == 5;
                    cells[x, y].Weight = pattern % 3;
                }
            }

            return cells;
        }
    }
}
