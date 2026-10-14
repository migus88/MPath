using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathfinderCornerBlockingTests
    {
        private const int Diagonal = 141;

        public enum Flank
        {
            Open,
            Wall,
            Occupied
        }

        private static readonly Flank[] Flanks = { Flank.Open, Flank.Wall, Flank.Occupied };

        private static IEnumerable<TestCaseData> FlankPairsAndSizes()
        {
            foreach (var size in new[] { 1, 2 })
            {
                foreach (var first in Flanks)
                {
                    foreach (var second in Flanks)
                    {
                        yield return new TestCaseData(size, first, second)
                            .SetName($"DiagonalStep_Size{size}_{first}And{second}_FollowsEveryCornerBlockingMask");
                    }
                }
            }
        }

        [TestCaseSource(nameof(FlankPairsAndSizes))]
        public void DiagonalStep_FollowsEveryCornerBlockingMask(int agentSize, Flank east, Flank south)
        {
            var allFlags = (int)CornerBlocking.All;

            for (var mask = 0; mask <= allFlags; mask++)
            {
                // Arrange
                var cornerBlocking = (CornerBlocking)mask;
                var expected = (cornerBlocking & FlagFor(east, south)) == 0;

                // Act
                var isDiagonalTaken = IsDiagonalStepTaken(agentSize, east, south,
                    new PathfinderSettings { CornerBlocking = cornerBlocking });

                // Assert
                isDiagonalTaken.Should().Be(expected, $"mask {cornerBlocking} with flanks {east}/{south}");
            }
        }

        [Test]
        public void DefaultSettings_BlockDiagonalOnlyBetweenTwoBlockedCells()
        {
            foreach (var east in Flanks)
            {
                foreach (var south in Flanks)
                {
                    // Arrange
                    var expected = east == Flank.Open || south == Flank.Open;

                    // Act
                    var isDiagonalTaken = IsDiagonalStepTaken(1, east, south, new PathfinderSettings());

                    // Assert
                    isDiagonalTaken.Should().Be(expected, $"default settings with flanks {east}/{south}");
                }
            }
        }

        [Test]
        public void BetweenWalls_UnitsDoNotBlockCorners_ButCannotBeEntered()
        {
            // Arrange
            var settings = new PathfinderSettings { CornerBlocking = CornerBlocking.BetweenWalls };

            // Act & Assert
            IsDiagonalStepTaken(1, Flank.Wall, Flank.Wall, settings).Should().BeFalse();
            IsDiagonalStepTaken(1, Flank.Wall, Flank.Occupied, settings).Should().BeTrue();
            IsDiagonalStepTaken(1, Flank.Occupied, Flank.Occupied, settings).Should().BeTrue();
            IsDiagonalStepTaken(1, Flank.Wall, Flank.Open, settings).Should().BeTrue();

            var cells = CreateGrid(2, Flank.Occupied, Flank.Occupied, 1);
            using var pathfinder = new Pathfinder(cells, 2, 2, settings);
            using var toUnit = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(1, 0));
            toUnit.IsSuccess.Should().BeFalse();
        }

        [Test]
        public void OccupiedFlanks_WhenOccupancyIsNotCalculated_CountAsOpen()
        {
            // Arrange
            var settings = new PathfinderSettings
            {
                CornerBlocking = CornerBlocking.All,
                IsCalculatingOccupiedCells = false
            };

            // Act
            var isDiagonalTaken = IsDiagonalStepTaken(1, Flank.Occupied, Flank.Occupied, settings);

            // Assert
            isDiagonalTaken.Should().BeTrue();
        }

        [TestCase(CornerBlocking.BetweenWalls, true)]
        [TestCase(CornerBlocking.BetweenBlockedCells, false)]
        public void GetReachable_AppliesCornerBlocking(CornerBlocking cornerBlocking, bool isCornerReachable)
        {
            // Arrange
            var cells = CreateGrid(2, Flank.Wall, Flank.Occupied, 1);
            using var pathfinder = new Pathfinder(cells, 2, 2,
                new PathfinderSettings { CornerBlocking = cornerBlocking, IsCellWeightEnabled = false });

            // Act
            using var range = pathfinder.GetReachable(new Coordinate(0, 0), 1000);

            // Assert
            var reached = Enumerable.Range(0, range.Length).Select(i => range.Get(i).Coordinate).ToList();
            reached.Contains(new Coordinate(1, 1)).Should().Be(isCornerReachable);
            reached.Should().NotContain(new Coordinate(0, 1));
        }

        [TestCase(CornerBlocking.BetweenWalls, SearchState.Success)]
        [TestCase(CornerBlocking.BetweenBlockedCells, SearchState.Failure)]
        public void StepwiseSearch_AppliesCornerBlocking(CornerBlocking cornerBlocking, SearchState expectedState)
        {
            // Arrange
            var cells = CreateGrid(2, Flank.Wall, Flank.Occupied, 1);
            using var pathfinder = new Pathfinder(cells, 2, 2, new PathfinderSettings { CornerBlocking = cornerBlocking });

            // Act
            using var search = pathfinder.BeginStepwiseSearch(new Coordinate(0, 0), new Coordinate(1, 1));
            var final = search.RunToCompletion();

            // Assert
            final.State.Should().Be(expectedState);
        }

        [Test]
        public void CustomSettings_CornerBlockingNone_AllowsCuttingBetweenTwoWalls()
        {
            // Arrange
            var settings = new CustomSettings { CornerBlocking = CornerBlocking.None };

            // Act
            var isDiagonalTaken = IsDiagonalStepTaken(1, Flank.Wall, Flank.Wall, settings);

            // Assert
            isDiagonalTaken.Should().BeTrue();
        }

        /// <summary>
        /// Runs <c>GetPath</c> from (0,0) to (1,1) with the two flanking positions set up as requested and
        /// reports whether the single diagonal step was taken.
        /// </summary>
        private static bool IsDiagonalStepTaken(int agentSize, Flank east, Flank south, IPathfinderSettings settings)
        {
            var gridSize = agentSize + 1;
            var cells = CreateGrid(gridSize, east, south, agentSize);

            using var pathfinder = new Pathfinder(cells, gridSize, gridSize, settings);
            using var result = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(1, 1), agentSize);

            return result.IsSuccess && result.Length == 1;
        }

        /// <summary>
        /// Creates an open square grid where the only cell of the agent's east flank footprint that is not
        /// shared with the start or destination footprint, (<paramref name="agentSize"/>, 0), and its south
        /// counterpart, (0, <paramref name="agentSize"/>), are set to the requested flank kinds.
        /// </summary>
        private static Cell[] CreateGrid(int gridSize, Flank east, Flank south, int agentSize)
        {
            var cells = new Cell[gridSize * gridSize];
            for (var x = 0; x < gridSize; x++)
            {
                for (var y = 0; y < gridSize; y++)
                {
                    cells[x * gridSize + y] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
                }
            }

            Apply(cells, agentSize * gridSize, east);
            Apply(cells, agentSize, south);
            return cells;
        }

        private static void Apply(Cell[] cells, int index, Flank flank)
        {
            cells[index].IsWalkable = flank != Flank.Wall;
            cells[index].IsOccupied = flank == Flank.Occupied;
        }

        private static CornerBlocking FlagFor(Flank first, Flank second)
        {
            var pair = first <= second ? (first, second) : (second, first);

            return pair switch
            {
                (Flank.Wall, Flank.Wall) => CornerBlocking.WallAndWall,
                (Flank.Wall, Flank.Occupied) => CornerBlocking.WallAndOccupied,
                (Flank.Occupied, Flank.Occupied) => CornerBlocking.OccupiedAndOccupied,
                (Flank.Open, Flank.Wall) => CornerBlocking.WallAndOpen,
                (Flank.Open, Flank.Occupied) => CornerBlocking.OccupiedAndOpen,
                _ => CornerBlocking.None
            };
        }

        private sealed class CustomSettings : IPathfinderSettings
        {
            public bool IsDiagonalMovementEnabled => true;
            public bool IsCalculatingOccupiedCells => true;
            public CornerBlocking CornerBlocking { get; set; }
            public bool IsCellWeightEnabled => true;
            public int StraightStepCost => 100;
            public int DiagonalStepCost => Diagonal;
            public PathSmoothingMethod PathSmoothingMethod => PathSmoothingMethod.None;
            public GridSnapshotMode GridSnapshotMode => GridSnapshotMode.PerQuery;
            public CellLayout CellLayout => CellLayout.Auto;
            public TieBreaking TieBreaking => TieBreaking.None;
        }
    }
}
