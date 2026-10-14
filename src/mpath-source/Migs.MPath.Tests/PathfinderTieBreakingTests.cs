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
            [Values(100, 141)] int diagonal)
        {
            using var pathfinder = CreatePathfinder(tieBreaking, 100, diagonal);

            using var path = pathfinder.GetPath(new Coordinate(3, 2), new Coordinate(3, 7));

            path.Path.Should().Equal(Column(3, 3, 7));
            path.Steps.Should().Be(5);
            path.DiagonalSteps.Should().Be(0);
        }

        [Test]
        public void GetPath_WithFewerDiagonals_UsesTheFewestDiagonalsAndKeepsItsDirection()
        {
            using var pathfinder = CreatePathfinder(TieBreaking.FewerDiagonals, 1, 1);

            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(5, 3));

            path.Path.Should().Equal(C(1, 1), C(2, 2), C(3, 3), C(4, 3), C(5, 3));
            path.Steps.Should().Be(5);
            path.DiagonalSteps.Should().Be(3);
        }

        [Test]
        public void GetPath_WithFewerTurns_OnOpenGround_TurnsOnce()
        {
            using var pathfinder = CreatePathfinder(TieBreaking.FewerTurns, 100, 141);

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
                CreateSettings(TieBreaking.FewerTurns, 1, 1));

            using var path = pathfinder.GetPath(new Coordinate(0, 1), new Coordinate(10, 1));

            path.Get(4).Should().Be(C(5, 6), "a V below the wall is as short as the line, with one turn");
            CountTurns(new Coordinate(0, 1), path.Path.ToList()).Should().Be(1);
        }

        [Test]
        public void GetPath_WithoutTieBreaking_ReturnsTheRouteOfEarlierVersions()
        {
            using var pathfinder = CreatePathfinder(TieBreaking.None, 100, 141);

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
            var settings = CreateSettings(tieBreaking, 100, 141);
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
            var settings = CreateSettings(tieBreaking, 1, 1);
            var cells = CreateCells();
            foreach (var wall in new[] { C(4, 2), C(4, 3), C(4, 4), C(8, 6), C(9, 6), C(10, 6) })
            {
                SetWall(cells, wall.X, wall.Y);
            }

            var rowMajor = new PathCell[Width * Height];
            var matrix = new PathCell[Width, Height];
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    rowMajor[y * Width + x] = cells[x * Height + y];
                    matrix[x, y] = cells[x * Height + y];
                }
            }

            var rowMajorSettings = CreateSettings(tieBreaking, 1, 1);
            rowMajorSettings.CellLayout = CellLayout.RowMajor;

            var from = new Coordinate(1, 3);
            var to = new Coordinate(12, 8);
            using var arrayPathfinder = new Pathfinder(cells, Width, Height, settings);
            using var rowMajorPathfinder = new Pathfinder(rowMajor, Width, Height, rowMajorSettings);
            using var matrixPathfinder = new Pathfinder(matrix, settings);

            using var expected = arrayPathfinder.GetPath(from, to);
            using var rowMajorPath = rowMajorPathfinder.GetPath(from, to);
            using var matrixPath = matrixPathfinder.GetPath(from, to);

            expected.IsSuccess.Should().BeTrue();
            rowMajorPath.Path.Should().Equal(expected.Path);
            matrixPath.Path.Should().Equal(expected.Path);
        }

        [Test]
        public void GetPath_WithTieBreaking_WhenThePathfinderIsReused_MatchesAFreshPathfinder(
            [ValueSource(nameof(TieBreakingModes))] TieBreaking tieBreaking)
        {
            var settings = CreateSettings(tieBreaking, 100, 141);
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
                using var expectedRange = fresh.GetReachable(from, 600);
                using var range = reused.GetReachable(from, 600);

                path.Path.Should().Equal(expected.Path, $"{from} -> {to}");
                range.Cells.Should().Equal(expectedRange.Cells, $"from {from}");
            }
        }

        [Test]
        public void GetPath_WithTieBreaking_WhenWeightedCostsSumInDifferentOrders_StillTies()
        {
            // Integer costs add up exactly in any order, so every route of equal cost ties and the fewest turns win.
            var cells = CreateCells();
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i].Weight = 1;
            }

            using var pathfinder = new Pathfinder(cells, Width, Height, CreateSettings(TieBreaking.FewerTurns, 3,
                7, isCellWeightEnabled: true));

            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(9, 3));

            CountTurns(new Coordinate(0, 0), path.Path.ToList()).Should().Be(1);
        }

        [Test]
        public void GetPath_WhenACellWeighsMoreThanTheCostHeadroom_TreatsWhatLiesBehindItAsUnreachable(
            [Values(TieBreaking.None, TieBreaking.FewerDiagonals)] TieBreaking tieBreaking)
        {
            var cells = CreateCells(width: 4, height: 1);
            cells[2].Weight = int.MaxValue;
            using var pathfinder = new Pathfinder(cells, 4, 1, CreateSettings(tieBreaking, 1, 1,
                isCellWeightEnabled: true));

            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(3, 0));
            using var reachable = pathfinder.GetReachable(new Coordinate(0, 0), int.MaxValue);

            path.IsSuccess.Should().BeFalse("a route past int.MaxValue / 2 is never taken");
            reachable.Cells.Select(cell => cell.Coordinate).Should().Equal(C(0, 0), C(1, 0));
            reachable.Cells.Should().OnlyContain(cell => cell.Cost >= 0, "no cost wraps around");
        }

        [Test]
        public void GetReachable_WhenRoutesCostUpToTheHeadroom_ReachesThemWithoutWrapping(
            [Values(TieBreaking.None, TieBreaking.FewerDiagonals)] TieBreaking tieBreaking)
        {
            const int headroom = int.MaxValue / 2;
            var cells = CreateCells(width: 4, height: 1);
            cells[1].Weight = headroom - 2;
            using var pathfinder = new Pathfinder(cells, 4, 1, CreateSettings(tieBreaking, 1, 1,
                isCellWeightEnabled: true));

            using var reachable = pathfinder.GetReachable(new Coordinate(0, 0), int.MaxValue);

            reachable.TryGetCost(new Coordinate(2, 0), out var cost).Should().BeTrue();
            cost.Should().Be(headroom, "the route costs exactly the headroom");
            reachable.Contains(new Coordinate(3, 0)).Should().BeFalse("one more step would pass it");
        }

        [Test]
        public void GetReachable_WithFewerDiagonalsAndEqualStepCosts_CountsKingMoves()
        {
            var origin = new Coordinate(6, 5);
            using var pathfinder = CreatePathfinder(TieBreaking.FewerDiagonals, 1, 1);

            using var reachable = pathfinder.GetReachable(origin, 3);

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
        public void GetReachable_WhenTheBudgetEqualsARouteCost_IncludesTheCell(
            [Values(TieBreaking.None, TieBreaking.FewerDiagonals)] TieBreaking tieBreaking)
        {
            using var pathfinder = CreatePathfinder(tieBreaking, 100, 141);

            using var reachable = pathfinder.GetReachable(new Coordinate(2, 2), 3 * 141);

            reachable.TryGetCell(new Coordinate(5, 5), out var cell).Should().BeTrue();
            cell.Steps.Should().Be(3);
            cell.DiagonalSteps.Should().Be(3);
            cell.Cost.Should().Be(423);
            reachable.Contains(new Coordinate(6, 5)).Should().BeFalse();
        }

        [Test]
        public void GetReachable_WhenTheBudgetIsOneBelowARouteCost_ExcludesTheCell(
            [Values(TieBreaking.None, TieBreaking.FewerDiagonals)] TieBreaking tieBreaking)
        {
            using var pathfinder = CreatePathfinder(tieBreaking, 100, 141);

            using var reachable = pathfinder.GetReachable(new Coordinate(2, 2), 3 * 141 - 1);

            reachable.Contains(new Coordinate(5, 5)).Should().BeFalse();
            reachable.Contains(new Coordinate(4, 4)).Should().BeTrue();
        }

        [Test]
        public void GetReachable_WithoutTieBreaking_ReportsTheStepCountsOfTheCheapestRoute()
        {
            var origin = new Coordinate(6, 5);
            using var pathfinder = CreatePathfinder(TieBreaking.None, 100, 141);

            using var reachable = pathfinder.GetReachable(origin, 500);

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
            using var pathfinder = CreatePathfinder(tieBreaking, 100, 141);
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
                node.ScoreF.Should().Be(node.ScoreG + node.ScoreH, node.ToString());
            }
        }

        [Test]
        public void GetPath_WithSmoothing_ReportsTheStepsOfTheUnsmoothedRoute()
        {
            var settings = CreateSettings(TieBreaking.FewerTurns, 100, 141);
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
            using var pathfinder = CreatePathfinder(TieBreaking.FewerDiagonals, 1, 1).EnablePathCaching();
            using var first = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(5, 3));

            using var cached = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(5, 3));

            cached.Steps.Should().Be(5);
            cached.DiagonalSteps.Should().Be(3);
        }

        [Test]
        public void TieBreaking_ByDefault_IsNone()
        {
            new PathfinderSettings().TieBreaking.Should().Be(TieBreaking.None);
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

        private static Pathfinder CreatePathfinder(TieBreaking tieBreaking, int straight, int diagonal)
        {
            return new Pathfinder(CreateCells(), Width, Height, CreateSettings(tieBreaking, straight, diagonal));
        }

        private static PathfinderSettings CreateSettings(TieBreaking tieBreaking, int straight, int diagonal,
            bool isCellWeightEnabled = false)
        {
            return new PathfinderSettings
            {
                TieBreaking = tieBreaking,
                StraightStepCost = straight,
                DiagonalStepCost = diagonal,
                IsCellWeightEnabled = isCellWeightEnabled
            };
        }

        private static PathCell[] CreateCells(int width = Width, int height = Height)
        {
            var cells = new PathCell[width * height];
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    cells[x * height + y] = new PathCell { IsWalkable = true };
                }
            }

            return cells;
        }

        private static void SetWall(PathCell[] cells, int x, int y)
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

    }
}
