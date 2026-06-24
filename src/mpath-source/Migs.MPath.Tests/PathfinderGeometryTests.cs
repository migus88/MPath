using System;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathfinderGeometryTests
    {
        private const int GridSize = 10;

        // ---------------------------------------------------------------------
        // Manhattan distance
        // ---------------------------------------------------------------------

        [Test]
        public void GetManhattanDistance_ReturnsSumOfAxisDeltas()
        {
            Pathfinder.GetManhattanDistance(new Coordinate(0, 0), new Coordinate(3, 4)).Should().Be(7);
            Pathfinder.GetManhattanDistance(new Coordinate(2, 1), new Coordinate(5, 1)).Should().Be(3);
            Pathfinder.GetManhattanDistance(new Coordinate(1, 5), new Coordinate(1, 0)).Should().Be(5);
        }

        [Test]
        public void GetManhattanDistance_IsZeroForSameCoordinate()
        {
            Pathfinder.GetManhattanDistance(new Coordinate(4, 7), new Coordinate(4, 7)).Should().Be(0);
        }

        [Test]
        public void GetManhattanDistance_IsSymmetric()
        {
            var a = new Coordinate(2, 8);
            var b = new Coordinate(7, 3);

            Pathfinder.GetManhattanDistance(a, b)
                .Should().Be(Pathfinder.GetManhattanDistance(b, a));
        }

        // ---------------------------------------------------------------------
        // Chebyshev distance
        // ---------------------------------------------------------------------

        [Test]
        public void GetChebyshevDistance_ReturnsLargerAxisDelta()
        {
            Pathfinder.GetChebyshevDistance(new Coordinate(0, 0), new Coordinate(3, 4)).Should().Be(4);
            Pathfinder.GetChebyshevDistance(new Coordinate(2, 1), new Coordinate(5, 1)).Should().Be(3);
            Pathfinder.GetChebyshevDistance(new Coordinate(0, 0), new Coordinate(3, 3)).Should().Be(3);
        }

        [Test]
        public void GetChebyshevDistance_IsZeroForSameCoordinate()
        {
            Pathfinder.GetChebyshevDistance(new Coordinate(4, 7), new Coordinate(4, 7)).Should().Be(0);
        }

        [Test]
        public void GetChebyshevDistance_NeverExceedsManhattanDistance()
        {
            var a = new Coordinate(1, 2);
            var b = new Coordinate(8, 6);

            var chebyshev = Pathfinder.GetChebyshevDistance(a, b);
            var manhattan = Pathfinder.GetManhattanDistance(a, b);

            chebyshev.Should().BeLessThanOrEqualTo(manhattan);
            chebyshev.Should().Be(7); // max(7, 4)
        }

        // ---------------------------------------------------------------------
        // Line of sight
        // ---------------------------------------------------------------------

        [Test]
        public void HasLineOfSight_OnEmptyGrid_ReturnsTrueForStraightLine()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(9, 0)).Should().BeTrue();
        }

        [Test]
        public void HasLineOfSight_OnEmptyGrid_ReturnsTrueForDiagonalLine()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(9, 9)).Should().BeTrue();
        }

        [Test]
        public void HasLineOfSight_ToSelf_ReturnsTrue()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            pathfinder.HasLineOfSight(new Coordinate(3, 3), new Coordinate(3, 3)).Should().BeTrue();
        }

        [Test]
        public void HasLineOfSight_WithWallOnTheLine_ReturnsFalse()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            SetWalkable(cells, 2, 0, false); // directly between (0,0) and (4,0)

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0)).Should().BeFalse();
        }

        [Test]
        public void HasLineOfSight_WithWallBesideTheLine_ReturnsTrue()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            SetWalkable(cells, 2, 1, false); // off the (0,0)->(4,0) horizontal line

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0)).Should().BeTrue();
        }

        [Test]
        public void HasLineOfSight_DoesNotTestEndpoints()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            // Block both endpoints but leave the cells between them clear.
            SetWalkable(cells, 0, 0, false);
            SetWalkable(cells, 4, 0, false);

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0)).Should().BeTrue();
        }

        [Test]
        public void HasLineOfSight_WithOccupiedCell_RespectsOccupancySetting()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            SetOccupied(cells, 2, 0, true); // occupied but still walkable, on the line

            // Default settings calculate occupied cells, so the occupant blocks sight.
            using (var blocking = new Pathfinder(cells, GridSize, GridSize))
            {
                blocking.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0)).Should().BeFalse();
            }

            // Disabling occupancy lets sight pass through the occupant.
            var settings = new PathfinderSettings { IsCalculatingOccupiedCells = false };
            using var ignoring = new Pathfinder(cells, GridSize, GridSize, settings);
            ignoring.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0)).Should().BeTrue();
        }

        [Test]
        public void HasLineOfSight_WithIgnoreUnwalkableMode_SeesThroughWalls()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            SetWalkable(cells, 2, 0, false); // wall directly on the (0,0)->(4,0) line

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            // Default mode is blocked by the wall...
            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0))
                      .Should().BeFalse();

            // ...but IgnoreUnwalkableCells treats the wall as transparent.
            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0),
                          LineOfSightMode.IgnoreUnwalkableCells)
                      .Should().BeTrue();
        }

        [Test]
        public void HasLineOfSight_WithIgnoreUnwalkableMode_StillBlockedByOccupiedCell()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            SetOccupied(cells, 2, 0, true); // occupant on the line; occupancy is on by default

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            // Ignoring unwalkable cells does NOT ignore occupants - units still block sight.
            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0),
                          LineOfSightMode.IgnoreUnwalkableCells)
                      .Should().BeFalse();
        }

        [Test]
        public void HasLineOfSight_WithIgnoreUnwalkableMode_AndOccupancyDisabled_SeesThroughEverything()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            SetWalkable(cells, 2, 0, false);  // wall on the line
            SetOccupied(cells, 3, 0, true);   // occupant on the line

            var settings = new PathfinderSettings { IsCalculatingOccupiedCells = false };
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);

            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0),
                          LineOfSightMode.IgnoreUnwalkableCells)
                      .Should().BeTrue();
        }

        [Test]
        public void HasLineOfSight_WithExplicitBlockedMode_MatchesDefault()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            SetWalkable(cells, 2, 0, false);

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(4, 0),
                          LineOfSightMode.BlockedByUnwalkableCells)
                      .Should().BeFalse();
        }

        [Test]
        public void HasLineOfSight_IsSymmetric()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            SetWalkable(cells, 5, 5, false);

            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            var forward = pathfinder.HasLineOfSight(new Coordinate(2, 2), new Coordinate(8, 8));
            var backward = pathfinder.HasLineOfSight(new Coordinate(8, 8), new Coordinate(2, 2));

            forward.Should().Be(backward);
            forward.Should().BeFalse();
        }

        [Test]
        public void HasLineOfSight_CanBeCalledRepeatedlyOnSameInstance()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            pathfinder.HasLineOfSight(new Coordinate(0, 0), new Coordinate(9, 9)).Should().BeTrue();
            pathfinder.HasLineOfSight(new Coordinate(0, 9), new Coordinate(9, 0)).Should().BeTrue();
        }

        [Test]
        public void HasLineOfSight_WithInvalidOrigin_ThrowsArgumentException()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            var action = () => pathfinder.HasLineOfSight(new Coordinate(-1, 0), new Coordinate(5, 5));

            action.Should().Throw<ArgumentException>().WithParameterName("from");
        }

        [Test]
        public void HasLineOfSight_WithInvalidTarget_ThrowsArgumentException()
        {
            var cells = CreateEmptyGrid(GridSize, GridSize);
            using var pathfinder = new Pathfinder(cells, GridSize, GridSize);

            var action = () => pathfinder.HasLineOfSight(new Coordinate(5, 5), new Coordinate(GridSize, GridSize));

            action.Should().Throw<ArgumentException>().WithParameterName("to");
        }

        private static void SetWalkable(Cell[] cells, int x, int y, bool walkable)
        {
            cells[x * GridSize + y].IsWalkable = walkable;
        }

        private static void SetOccupied(Cell[] cells, int x, int y, bool occupied)
        {
            cells[x * GridSize + y].IsOccupied = occupied;
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
