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
    /// Explicit tie scenarios for <see cref="TieBreaking"/> and the step counts on results. The property tests in
    /// <see cref="PathfinderOptimalityTests"/> check the same rules against a reference search on random grids.
    /// </summary>
    [TestFixture]
    public class PathfinderTieBreakingTests
    {
        private const int Width = 16;
        private const int Height = 12;

        private static readonly TieBreaking[] TieBreakingModes = { TieBreaking.FewerDiagonals, TieBreaking.FewerTurns };

        [Test]
        public void GetPath_OnOpenGroundWithEqualStepCosts_ReturnsTheStraightColumn(
            [ValueSource(nameof(TieBreakingModes))] TieBreaking tieBreaking,
            [Values(1f, 1.41f)] float diagonal)
        {
            using var pathfinder = CreatePathfinder(tieBreaking, 1f, diagonal);

            using var path = pathfinder.GetPath(new Coordinate(3, 2), new Coordinate(3, 7));

            path.Path.Should().Equal(Column(3, 3, 7));
            path.Steps.Should().Be(5);
            path.DiagonalSteps.Should().Be(0);
        }

        [Test]
        public void GetPath_WithFewerDiagonals_UsesTheFewestDiagonalsAndKeepsItsDirection()
        {
            using var pathfinder = CreatePathfinder(TieBreaking.FewerDiagonals, 1f, 1f);

            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(5, 3));

            path.Path.Should().Equal(C(1, 1), C(2, 2), C(3, 3), C(4, 3), C(5, 3));
            path.Steps.Should().Be(5);
            path.DiagonalSteps.Should().Be(3);
        }

        [Test]
        public void GetPath_WithFewerTurns_OnOpenGround_TurnsOnce()
        {
            using var pathfinder = CreatePathfinder(TieBreaking.FewerTurns, 1f, 1.41f);

            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(10, 5));

            path.Path.Should().Equal(C(1, 1), C(2, 2), C(3, 3), C(4, 4), C(5, 5),
                C(6, 5), C(7, 5), C(8, 5), C(9, 5), C(10, 5));
            path.DiagonalSteps.Should().Be(5);
        }

        [Test]
        public void GetPath_WithFewerTurns_WhenTheStraightLineIsBlocked_DetoursWithOneTurn()
        {
            var cells = CreateCells();
            SetWall(cells, 5, 0);
            SetWall(cells, 5, 1);
            SetWall(cells, 5, 2);
            using var pathfinder = new Pathfinder(cells, Width, Height,
                CreateSettings(TieBreaking.FewerTurns, 1f, 1f));

            using var path = pathfinder.GetPath(new Coordinate(0, 1), new Coordinate(10, 1));

            path.Get(4).Should().Be(C(5, 6), "a V below the wall is as short as the line, with one turn");
            CountTurns(new Coordinate(0, 1), path.Path.ToList()).Should().Be(1);
        }

        [Test]
        public void GetPath_WithoutTieBreaking_ReturnsTheRouteOfEarlierVersions()
        {
            using var pathfinder = CreatePathfinder(TieBreaking.None, 1f, 1.41f);

            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(10, 5));

            path.Path.Should().Equal(C(1, 0), C(2, 1), C(3, 1), C(4, 2), C(5, 2),
                C(6, 3), C(7, 3), C(8, 3), C(9, 4), C(10, 5));
        }

        [Test]
        public void GetPath_WithTieBreaking_IgnoresEditsAwayFromTheCompetingRoutes(
            [ValueSource(nameof(TieBreakingModes))] TieBreaking tieBreaking)
        {
            var from = new Coordinate(0, 0);
            var to = new Coordinate(10, 5);
            var settings = CreateSettings(tieBreaking, 1f, 1.41f);
            using var pathfinder = new Pathfinder(CreateCells(), Width, Height, settings);
            using var expected = pathfinder.GetPath(from, to);

            // The cheapest routes fill the parallelogram y <= x <= y + 5; these walls change what the search
            // queues, but none of them is on a cheapest route.
            var edited = CreateCells();
            foreach (var wall in new[] { C(7, 0), C(9, 1), C(1, 3), C(2, 5), C(12, 4), C(4, 8) })
            {
                SetWall(edited, wall.X, wall.Y);
            }

            using var editedPathfinder = new Pathfinder(edited, Width, Height, settings);
            using var path = editedPathfinder.GetPath(from, to);

            path.Path.Should().Equal(expected.Path);
        }

        [Test]
        public void GetPath_WithTieBreaking_ReturnsTheSameRouteForEveryGridRepresentation(
            [ValueSource(nameof(TieBreakingModes))] TieBreaking tieBreaking)
        {
            var settings = CreateSettings(tieBreaking, 1f, 1f);
            var cells = CreateCells();
            foreach (var wall in new[] { C(4, 2), C(4, 3), C(4, 4), C(8, 6), C(9, 6), C(10, 6) })
            {
                SetWall(cells, wall.X, wall.Y);
            }

            var random = new Random(7);
            var shuffled = cells.OrderBy(_ => random.Next()).ToArray();
            var matrix = new Cell[Width, Height];
            foreach (var cell in cells)
            {
                matrix[cell.Coordinate.X, cell.Coordinate.Y] = cell;
            }

            var from = new Coordinate(1, 3);
            var to = new Coordinate(12, 8);
            using var arrayPathfinder = new Pathfinder(cells, Width, Height, settings);
            using var shuffledPathfinder = new Pathfinder(shuffled, Width, Height, settings);
            using var matrixPathfinder = new Pathfinder(matrix, settings);

            using var expected = arrayPathfinder.GetPath(from, to);
            using var shuffledPath = shuffledPathfinder.GetPath(from, to);
            using var matrixPath = matrixPathfinder.GetPath(from, to);

            expected.IsSuccess.Should().BeTrue();
            shuffledPath.Path.Should().Equal(expected.Path);
            matrixPath.Path.Should().Equal(expected.Path);
        }

        [Test]
        public void GetPath_WithTieBreaking_WhenThePathfinderIsReused_MatchesAFreshPathfinder(
            [ValueSource(nameof(TieBreakingModes))] TieBreaking tieBreaking)
        {
            var settings = CreateSettings(tieBreaking, 1f, 1.41f);
            var cells = CreateCells();
            foreach (var wall in new[] { C(4, 2), C(4, 3), C(4, 4), C(8, 6), C(9, 6), C(10, 6) })
            {
                SetWall(cells, wall.X, wall.Y);
            }

            var random = new Random(3);
            using var reused = new Pathfinder(cells, Width, Height, settings);

            for (var query = 0; query < 40; query++)
            {
                var from = C(random.Next(Width), random.Next(Height));
                var to = C(random.Next(Width), random.Next(Height));
                using var fresh = new Pathfinder(cells, Width, Height, settings);

                using var expected = fresh.GetPath(from, to);
                using var path = reused.GetPath(from, to);
                using var expectedRange = fresh.GetReachable(from, 6f);
                using var range = reused.GetReachable(from, 6f);

                path.Path.Should().Equal(expected.Path, $"{from} -> {to}");
                range.Cells.Should().Equal(expectedRange.Cells, $"from {from}");
            }
        }

        [Test]
        public void GetPath_WithTieBreaking_WhenFractionalCostsSumInDifferentOrders_StillTies()
        {
            // 0.1 is not exact in binary: float sums of the same steps in different orders differ in the last bit,
            // which used to pick an arbitrary route. Exact keys tie them, so the fewest turns win.
            var cells = CreateCells();
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i].Weight = 0.1f;
            }

            using var pathfinder = new Pathfinder(cells, Width, Height, CreateSettings(TieBreaking.FewerTurns, 0.3f,
                0.7f, isCellWeightEnabled: true));

            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(9, 3));

            CountTurns(new Coordinate(0, 0), path.Path.ToList()).Should().Be(1);
        }

        [Test]
        public void GetPath_WithTieBreaking_WhenACellIsInfinitelyHeavy_ClampsItsCost()
        {
            var cells = CreateCells(width: 3, height: 1);
            cells[1].Weight = float.PositiveInfinity;
            using var pathfinder = new Pathfinder(cells, 3, 1, CreateSettings(TieBreaking.FewerDiagonals, 1f, 1f,
                isCellWeightEnabled: true));

            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(2, 0));
            using var reachable = pathfinder.GetReachable(new Coordinate(0, 0), float.MaxValue);

            path.Path.Should().Equal(C(1, 0), C(2, 0));
            reachable.TryGetCell(new Coordinate(2, 0), out var cell).Should().BeTrue();
            cell.Cost.Should().Be(16777216f + 2f, "a step costs at most 2^24");
        }

        [Test]
        public void GetReachable_WithFewerDiagonalsAndEqualStepCosts_CountsKingMoves()
        {
            var origin = new Coordinate(6, 5);
            using var pathfinder = CreatePathfinder(TieBreaking.FewerDiagonals, 1f, 1f);

            using var reachable = pathfinder.GetReachable(origin, 3f);

            reachable.Length.Should().Be(7 * 7);
            foreach (var cell in reachable.Cells)
            {
                var dx = Math.Abs(cell.Coordinate.X - origin.X);
                var dy = Math.Abs(cell.Coordinate.Y - origin.Y);
                cell.Cost.Should().Be(Math.Max(dx, dy), cell.ToString());
                cell.Steps.Should().Be(Math.Max(dx, dy), cell.ToString());
                cell.DiagonalSteps.Should().Be(Math.Min(dx, dy), cell.ToString());
            }
        }

        [Test]
        public void GetReachable_WithTieBreaking_WhenTheBudgetEqualsARouteCost_IncludesTheCell()
        {
            using var pathfinder = CreatePathfinder(TieBreaking.FewerDiagonals, 1f, 1.41f);

            using var reachable = pathfinder.GetReachable(new Coordinate(2, 2), 3 * 1.41f);

            reachable.TryGetCell(new Coordinate(5, 5), out var cell).Should().BeTrue();
            cell.Steps.Should().Be(3);
            cell.DiagonalSteps.Should().Be(3);
            cell.Cost.Should().Be(4.23f);
            reachable.Contains(new Coordinate(6, 5)).Should().BeFalse();
        }

        [Test]
        public void GetReachable_WithoutTieBreaking_ReportsTheStepCountsOfTheCheapestRoute()
        {
            var origin = new Coordinate(6, 5);
            using var pathfinder = CreatePathfinder(TieBreaking.None, 1f, 1.41f);

            using var reachable = pathfinder.GetReachable(origin, 5f);

            reachable.TryGetCell(new Coordinate(9, 6), out var cell).Should().BeTrue();
            cell.Steps.Should().Be(3);
            cell.DiagonalSteps.Should().Be(1);
            reachable.TryGetCell(origin, out var originCell).Should().BeTrue();
            originCell.Steps.Should().Be(0);
        }

        [Test]
        public void BeginStepwiseSearch_WithTieBreaking_FindsTheGetPathRouteWithConsistentScores(
            [ValueSource(nameof(TieBreakingModes))] TieBreaking tieBreaking)
        {
            var from = new Coordinate(1, 1);
            var to = new Coordinate(11, 7);
            using var pathfinder = CreatePathfinder(tieBreaking, 1f, 1.41f);
            List<Coordinate> expected;
            using (var path = pathfinder.GetPath(from, to))
            {
                expected = path.Path.ToList();
            }

            using var search = pathfinder.BeginStepwiseSearch(from, to);
            var final = search.RunToCompletion();

            final.State.Should().Be(SearchState.Success);
            final.Path.Should().Equal(expected);
            foreach (var node in final.Searched)
            {
                node.ScoreF.Should().BeApproximately(node.ScoreG + node.ScoreH, 0.0001f, node.ToString());
            }
        }

        [Test]
        public void GetPath_WithSmoothing_ReportsTheStepsOfTheUnsmoothedRoute()
        {
            var settings = CreateSettings(TieBreaking.FewerTurns, 1f, 1.41f);
            settings.PathSmoothingMethod = PathSmoothingMethod.Simple;
            using var pathfinder = new Pathfinder(CreateCells(), Width, Height, settings);

            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(10, 5));

            path.Length.Should().BeLessThan(10);
            path.Steps.Should().Be(10);
            path.DiagonalSteps.Should().Be(5);
        }

        [Test]
        public void GetPath_FromThePathCache_KeepsTheStepCounts()
        {
            using var pathfinder = CreatePathfinder(TieBreaking.FewerDiagonals, 1f, 1f).EnablePathCaching();
            using var first = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(5, 3));

            using var cached = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(5, 3));

            cached.Steps.Should().Be(5);
            cached.DiagonalSteps.Should().Be(3);
        }

        [Test]
        public void TieBreaking_ByDefault_IsNone()
        {
            IPathfinderSettings custom = new SettingsWithoutTieBreaking();

            new PathfinderSettings().TieBreaking.Should().Be(TieBreaking.None);
            custom.TieBreaking.Should().Be(TieBreaking.None);
        }

        [Test]
        public void Success_WithStepCounts_ValidatesThem()
        {
            var path = new Coordinate[16];

            using var result = PathResult.Success(path, 2, 5, 1);

            result.Steps.Should().Be(5);
            result.DiagonalSteps.Should().Be(1);
            FluentActions.Invoking(() => PathResult.Success(path, 2, -1, 0))
                .Should().Throw<ArgumentOutOfRangeException>();
            FluentActions.Invoking(() => PathResult.Success(path, 2, 1, 2))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        private static Pathfinder CreatePathfinder(TieBreaking tieBreaking, float straight, float diagonal)
        {
            return new Pathfinder(CreateCells(), Width, Height, CreateSettings(tieBreaking, straight, diagonal));
        }

        private static PathfinderSettings CreateSettings(TieBreaking tieBreaking, float straight, float diagonal,
            bool isCellWeightEnabled = false)
        {
            return new PathfinderSettings
            {
                TieBreaking = tieBreaking,
                StraightMovementMultiplier = straight,
                DiagonalMovementMultiplier = diagonal,
                IsCellWeightEnabled = isCellWeightEnabled
            };
        }

        private static Cell[] CreateCells(int width = Width, int height = Height)
        {
            var cells = new Cell[width * height];
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    cells[x * height + y] = new Cell { Coordinate = C(x, y), IsWalkable = true };
                }
            }

            return cells;
        }

        private static void SetWall(Cell[] cells, int x, int y)
        {
            cells[x * Height + y].IsWalkable = false;
        }

        private static IEnumerable<Coordinate> Column(int x, int fromY, int toY)
        {
            return Enumerable.Range(fromY, toY - fromY + 1).Select(y => C(x, y));
        }

        private static int CountTurns(Coordinate from, IReadOnlyList<Coordinate> path)
        {
            var turns = 0;
            var previous = from;
            (int, int)? previousStep = null;

            foreach (var next in path)
            {
                var step = (next.X - previous.X, next.Y - previous.Y);
                if (previousStep.HasValue && previousStep.Value != step)
                {
                    turns++;
                }

                previousStep = step;
                previous = next;
            }

            return turns;
        }

        private static Coordinate C(int x, int y) => new Coordinate(x, y);

        private sealed class SettingsWithoutTieBreaking : IPathfinderSettings
        {
            public bool IsDiagonalMovementEnabled => true;
            public bool IsCalculatingOccupiedCells => true;
            public bool IsMovementBetweenCornersEnabled => false;
            public bool IsCellWeightEnabled => true;
            public float StraightMovementMultiplier => 1f;
            public float DiagonalMovementMultiplier => 1.41f;
            public PathSmoothingMethod PathSmoothingMethod => PathSmoothingMethod.None;
            public int? InitialBufferSize => null;
        }
    }
}
