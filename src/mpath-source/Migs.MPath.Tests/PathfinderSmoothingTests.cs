using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathfinderSmoothingTests
    {
        [Test]
        public void GetPath_WithSimpleSmoothingOnStraightPath_ReturnsFirstAndLastOnly()
        {
            // Arrange
            var cells = CreateEmptyGrid(10, 10);
            using var pathfinder = new Pathfinder(cells, 10, 10, Settings(PathSmoothingMethod.Simple));

            // Act
            using var result = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(5, 0));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Path.Should().Equal(new Coordinate(1, 0), new Coordinate(5, 0));
        }

        [Test]
        public void GetPath_WithSimpleSmoothingOnLShapedPath_ReturnsFirstCornerAndLast()
        {
            // Arrange: only the bottom row and the right column are walkable
            var cells = CreateEmptyGrid(6, 6);
            foreach (var index in Enumerable.Range(0, cells.Length))
            {
                var coordinate = cells[index].Coordinate;
                cells[index].IsWalkable = coordinate.Y == 0 || coordinate.X == 5;
            }

            var settings = Settings(PathSmoothingMethod.Simple);
            settings.IsDiagonalMovementEnabled = false;
            using var pathfinder = new Pathfinder(cells, 6, 6, settings);

            // Act
            using var result = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(5, 5));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Path.Should().Equal(new Coordinate(1, 0), new Coordinate(5, 0), new Coordinate(5, 5));
        }

        [Test]
        public void GetPath_WithSimpleSmoothingAndTurnBeforeLastStep_KeepsThatCorner()
        {
            // Arrange: the bottom row plus one cell above its end
            var cells = CreateEmptyGrid(6, 6);
            foreach (var index in Enumerable.Range(0, cells.Length))
            {
                var coordinate = cells[index].Coordinate;
                cells[index].IsWalkable = coordinate.Y == 0 || (coordinate.X == 5 && coordinate.Y == 1);
            }

            var settings = Settings(PathSmoothingMethod.Simple);
            settings.IsDiagonalMovementEnabled = false;
            using var pathfinder = new Pathfinder(cells, 6, 6, settings);

            // Act
            using var result = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(5, 1));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Path.Should().Equal(new Coordinate(1, 0), new Coordinate(5, 0), new Coordinate(5, 1));
        }

        [Test]
        public void GetPath_WithSimpleSmoothingOnRandomGrids_KeepsEndpointsAndHasNoConsecutiveDuplicates()
        {
            const int size = 16;
            var random = new Random(40);

            for (var run = 0; run < 50; run++)
            {
                // Arrange
                var cells = CreateRandomGrid(size, size, random, 0.25);
                var from = new Coordinate(0, 0);
                var to = new Coordinate(size - 1, size - 1);
                using var raw = new Pathfinder(cells, size, size, Settings(PathSmoothingMethod.None));
                using var simple = new Pathfinder(cells, size, size, Settings(PathSmoothingMethod.Simple));

                // Act
                using var rawResult = raw.GetPath(from, to);
                using var smoothedResult = simple.GetPath(from, to);

                // Assert
                smoothedResult.IsSuccess.Should().Be(rawResult.IsSuccess);

                if (!rawResult.IsSuccess || rawResult.Length <= 2)
                {
                    continue;
                }

                var smoothed = smoothedResult.Path.ToArray();
                smoothed.First().Should().Be(rawResult.Get(0));
                smoothed.Last().Should().Be(to);

                for (var i = 1; i < smoothed.Length; i++)
                {
                    smoothed[i].Should().NotBe(smoothed[i - 1], $"run {run} repeats waypoint {i}");
                }
            }
        }

        [Test]
        public void GetPath_WithStringPullingAndCornerCuttingDisabled_DoesNotSqueezeBetweenTouchingWalls()
        {
            // Arrange: two walls touching at a corner on the main diagonal
            var cells = CreateEmptyGrid(7, 7);
            SetWall(cells, 7, 3, 2);
            SetWall(cells, 7, 2, 3);
            var settings = Settings(PathSmoothingMethod.StringPulling);
            using var pathfinder = new Pathfinder(cells, 7, 7, settings);
            var from = new Coordinate(0, 0);

            // Act
            using var result = pathfinder.GetPath(from, new Coordinate(6, 6));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Path.Should().NotEqual(new[] { new Coordinate(1, 1), new Coordinate(6, 6) });
            AssertEverySegmentIsStraightlyWalkable(cells, 7, 7, settings, 1, from, result);
        }

        [Test]
        public void GetPath_WithStringPullingAndCornerCuttingEnabled_PullsBetweenTouchingWalls()
        {
            // Arrange
            var cells = CreateEmptyGrid(7, 7);
            SetWall(cells, 7, 3, 2);
            SetWall(cells, 7, 2, 3);
            var settings = Settings(PathSmoothingMethod.StringPulling);
            settings.IsMovementBetweenCornersEnabled = true;
            using var pathfinder = new Pathfinder(cells, 7, 7, settings);

            // Act
            using var result = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(6, 6));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Path.Should().Equal(new Coordinate(1, 1), new Coordinate(6, 6));
        }

        [TestCase(CornerBlocking.BetweenWalls, true)]
        [TestCase(CornerBlocking.BetweenBlockedCells, false)]
        public void GetPath_WithStringPullingBetweenWallAndUnit_FollowsCornerBlocking(CornerBlocking cornerBlocking,
            bool isPulledThroughCorner)
        {
            // Arrange: a wall and an occupied cell touching at a corner on the main diagonal
            var cells = CreateEmptyGrid(7, 7);
            SetWall(cells, 7, 3, 2);
            cells[2 * 7 + 3].IsOccupied = true;
            var settings = Settings(PathSmoothingMethod.StringPulling);
            settings.CornerBlocking = cornerBlocking;
            using var pathfinder = new Pathfinder(cells, 7, 7, settings);

            // Act
            using var result = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(6, 6));

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Path.SequenceEqual(new[] { new Coordinate(1, 1), new Coordinate(6, 6) })
                .Should().Be(isPulledThroughCorner);
        }

        [Test]
        public void GetPath_WithStringPullingAndDiagonalMovementDisabled_NeverSqueezesBetweenTouchingWalls()
        {
            const int size = 16;
            var random = new Random(29);

            for (var run = 0; run < 30; run++)
            {
                // Arrange: the corner setting cannot open a diagonal squeeze the 4-connected search never makes
                var cells = CreateRandomGrid(size, size, random, 0.3);
                var settings = Settings(PathSmoothingMethod.StringPulling);
                settings.IsDiagonalMovementEnabled = false;
                settings.IsMovementBetweenCornersEnabled = true;
                using var pathfinder = new Pathfinder(cells, size, size, settings);
                var from = new Coordinate(0, 0);

                // Act
                using var result = pathfinder.GetPath(from, new Coordinate(size - 1, size - 1));

                // Assert
                if (result.IsSuccess)
                {
                    AssertEverySegmentIsStraightlyWalkable(cells, size, size, settings, 1, from, result);
                }
            }
        }

        [Test]
        public void GetPath_WithStringPullingAndSizeTwoAgent_DoesNotPullThroughOneCellGap()
        {
            // Arrange: wall row y=4 with a 1-wide gap at x=3 and a 2-wide gap at x=8..9
            const int size = 12;
            var cells = CreateEmptyGrid(size, size);
            for (var x = 0; x < size; x++)
            {
                if (x != 3 && x != 8 && x != 9)
                {
                    SetWall(cells, size, x, 4);
                }
            }

            var settings = Settings(PathSmoothingMethod.StringPulling);
            using var pathfinder = new Pathfinder(cells, size, size, settings);
            var agentSize = 2;
            var from = new Coordinate(0, 0);

            // Act
            using var result = pathfinder.GetPath(from, new Coordinate(2, 8), agentSize);

            // Assert
            result.IsSuccess.Should().BeTrue();
            AssertEverySegmentIsStraightlyWalkable(cells, size, size, settings, agentSize, from, result);
        }

        [Test]
        public void GetPath_WithStringPullingOnRandomGrids_KeepsEverySegmentWalkableForLargeAgents()
        {
            const int size = 20;
            var random = new Random(27);
            var successes = 0;

            for (var run = 0; run < 30; run++)
            {
                // Arrange
                var cells = CreateRandomGrid(size, size, random, 0.06);
                var settings = Settings(PathSmoothingMethod.StringPulling);
                using var pathfinder = new Pathfinder(cells, size, size, settings);
                var agentSize = 2;
                var from = new Coordinate(0, 0);

                // Act
                using var result = pathfinder.GetPath(from, new Coordinate(size - 2, size - 2), agentSize);

                // Assert
                if (result.IsSuccess)
                {
                    successes++;
                    AssertEverySegmentIsStraightlyWalkable(cells, size, size, settings, agentSize, from, result);
                }
            }

            successes.Should().BeGreaterThan(10);
        }

        // A segment is walkable in a straight line when the unsmoothed search, under the same settings and
        // agent, connects its ends in the minimum number of moves (Chebyshev with diagonals, else Manhattan).
        private static void AssertEverySegmentIsStraightlyWalkable(Cell[] cells, int width, int height,
            PathfinderSettings settings, int agentSize, Coordinate from, PathResult result)
        {
            var rawSettings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = settings.IsDiagonalMovementEnabled,
                IsMovementBetweenCornersEnabled = settings.IsMovementBetweenCornersEnabled,
                IsCalculatingOccupiedCells = settings.IsCalculatingOccupiedCells,
                PathSmoothingMethod = PathSmoothingMethod.None
            };

            using var oracle = new Pathfinder(cells, width, height, rawSettings);
            var waypoints = new List<Coordinate> { from };
            waypoints.AddRange(result.Path);

            for (var i = 1; i < waypoints.Count; i++)
            {
                var start = waypoints[i - 1];
                var end = waypoints[i];
                var minimumMoves = settings.IsDiagonalMovementEnabled
                    ? Pathfinder.GetChebyshevDistance(start, end)
                    : Pathfinder.GetManhattanDistance(start, end);

                using var segment = oracle.GetPath(start, end, agentSize);

                segment.IsSuccess.Should().BeTrue($"segment {start} -> {end} must be reachable");
                segment.Length.Should().Be(minimumMoves, $"segment {start} -> {end} must be a straight walk");
            }
        }

        private static PathfinderSettings Settings(PathSmoothingMethod method)
        {
            return new PathfinderSettings { PathSmoothingMethod = method };
        }

        private static void SetWall(Cell[] cells, int height, int x, int y)
        {
            cells[x * height + y].IsWalkable = false;
        }

        private static Cell[] CreateRandomGrid(int width, int height, Random random, double wallChance)
        {
            var cells = CreateEmptyGrid(width, height);

            for (var i = 0; i < cells.Length; i++)
            {
                cells[i].IsWalkable = random.NextDouble() >= wallChance;
            }

            cells[0].IsWalkable = true;
            cells[cells.Length - 1].IsWalkable = true;
            return cells;
        }

        private static Cell[] CreateEmptyGrid(int width, int height)
        {
            var cells = new Cell[width * height];

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    cells[x * height + y] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
                }
            }

            return cells;
        }
    }
}
