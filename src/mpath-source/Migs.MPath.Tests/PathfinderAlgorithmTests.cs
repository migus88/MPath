using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Tests.Implementations;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathfinderAlgorithmTests
    {
        private const int GridSize = 10;
        
        [Test]
        public void GetPath_WithDiagonalMovementDisabled_ShouldNotUseDiagonalPaths()
        {
            // Arrange
            var cells = CreateEmptyGrid(GridSize, GridSize);
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = false,
                IsCalculatingOccupiedCells = true,
                IsMovementBetweenCornersEnabled = false,
                IsCellWeightEnabled = false
            };
            
            var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };
            var start = new Coordinate(0, 0);
            var destination = new Coordinate(GridSize - 1, GridSize - 1);
            
            // Act
            var result = pathfinder.GetPath(agent, start, destination);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            
            // Check no diagonal moves are used
            for (var i = 0; i < result.Length - 1; i++)
            {
                var current = result.Get(i);
                var next = result.Get(i + 1);
                
                // Only one coordinate should change between consecutive points
                var diffX = Math.Abs(next.X - current.X);
                var diffY = Math.Abs(next.Y - current.Y);
                
                // In a non-diagonal path, either diffX or diffY should be 0, but not both
                (diffX == 0 || diffY == 0).Should().BeTrue();
                (diffX == 1 && diffY == 1).Should().BeFalse(); // No diagonal moves
            }
        }
        
        [Test]
        public void GetPath_ShouldFindPath()
        {
            // Arrange
            var cells = CreateEmptyGrid(GridSize, GridSize);
            var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };
            var start = new Coordinate(0, 0);
            var destination = new Coordinate(GridSize - 1, GridSize - 1);
            
            // Act
            var result = pathfinder.GetPath(agent, start, destination);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Length.Should().BeGreaterThan(0);
        }
        
        [Test]
        public void GetPath_OccupiedCellsConsideredAsBlocked_ShouldFindPathAroundOccupiedCells()
        {
            // Arrange
            var cells = CreateEmptyGrid(GridSize, GridSize);
            
            // Create a horizontal line of occupied cells with a gap
            for (var x = 0; x < GridSize; x++)
            {
                if (x != GridSize / 2) // Leave a gap
                {
                    var cellIndex = x * GridSize + (GridSize / 2);
                    cells[cellIndex].IsOccupied = true;
                }
            }
            
            var settings = new PathfinderSettings
            {
                IsCalculatingOccupiedCells = true // Consider occupied cells as blocked
            };
            
            var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };
            var start = new Coordinate(GridSize / 4, GridSize / 4); // Top left quadrant
            var destination = new Coordinate(3 * GridSize / 4, 3 * GridSize / 4); // Bottom right quadrant
            
            // Act
            var result = pathfinder.GetPath(agent, start, destination);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
        }
        
        [Test]
        public void GetPath_OccupiedCellsIgnored_ShouldPathThroughOccupiedCells()
        {
            // Arrange
            var cells = CreateEmptyGrid(GridSize, GridSize);
            
            // Create a horizontal line of occupied cells with no gaps
            for (var x = 0; x < GridSize; x++)
            {
                var cellIndex = x * GridSize + (GridSize / 2);
                cells[cellIndex].IsOccupied = true;
            }
            
            var settings = new PathfinderSettings
            {
                IsCalculatingOccupiedCells = false // Ignore occupied cells
            };
            
            var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };
            var start = new Coordinate(GridSize / 4, GridSize / 4); // Top left quadrant
            var destination = new Coordinate(3 * GridSize / 4, 3 * GridSize / 4); // Bottom right quadrant
            
            // Act
            var result = pathfinder.GetPath(agent, start, destination);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
        }
        
        [Test]
        public void GetPath_WithObstacles_ShouldFindPathAroundObstacles()
        {
            // Arrange
            var cells = CreateEmptyGrid(GridSize, GridSize);
            
            // Create a horizontal wall in the middle with a gap
            for (var x = 0; x < GridSize; x++)
            {
                if (x != GridSize / 2) // Leave a gap
                {
                    var cellIndex = x * GridSize + (GridSize / 2);
                    cells[cellIndex].IsWalkable = false;
                }
            }
            
            var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };
            var start = new Coordinate(GridSize / 4, GridSize / 4); // Top left quadrant
            var destination = new Coordinate(3 * GridSize / 4, 3 * GridSize / 4); // Bottom right quadrant
            
            // Act
            var result = pathfinder.GetPath(agent, start, destination);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Length.Should().BeGreaterThan(0);
        }
        
        [Test]
        public void GetPath_WithNoValidPath_ShouldReturnFailure()
        {
            // Create a completely blocked grid with no valid paths
            var cells = CreateEmptyGrid(GridSize, GridSize);
            
            // Make all cells unwalkable
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i].IsWalkable = false;
            }
            
            // Make just the start and end points walkable
            var start = new Coordinate(0, 0);
            var destination = new Coordinate(GridSize - 1, GridSize - 1);
            
            cells[start.X * GridSize + start.Y].IsWalkable = true;
            cells[destination.X * GridSize + destination.Y].IsWalkable = true;
            
            var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };
            
            // Act
            var result = pathfinder.GetPath(agent, start, destination);
            
            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Length.Should().Be(0);
        }
        
        [Test]
        public void GetPath_WithCellWeights_ShouldPreferLowerWeightCells()
        {
            // Arrange
            var cells = CreateEmptyGrid(GridSize, GridSize);
            
            // Set a high weight on the direct path
            for (var i = 1; i < GridSize - 1; i++)
            {
                var cellIndex = i * GridSize + i; // Diagonal line
                cells[cellIndex].Weight = 10f; // High weight
            }
            
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = true,
                IsCalculatingOccupiedCells = true,
                IsMovementBetweenCornersEnabled = true,
                IsCellWeightEnabled = true // Enable cell weights
            };
            
            var pathfinder = new Pathfinder(cells, GridSize, GridSize, settings);
            var agent = new Agent { Size = 1 };
            var start = new Coordinate(0, 0);
            var destination = new Coordinate(GridSize - 1, GridSize - 1);
            
            // Act
            var result = pathfinder.GetPath(agent, start, destination);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Length.Should().BeGreaterThan(0);
        }
        
        [Test]
        public void GetPath_WithLargerAgent_ShouldRequireMoreClearance()
        {
            // Arrange
            var cells = CreateEmptyGrid(GridSize, GridSize);
            
            // Create a narrow passage with a width of 1 cell
            for (var y = 0; y < GridSize; y++)
            {
                if (y != GridSize / 2) // Leave a 1-cell gap
                {
                    var leftWallIndex = (GridSize / 2 - 1) * GridSize + y;
                    var rightWallIndex = (GridSize / 2 + 1) * GridSize + y;
                    cells[leftWallIndex].IsWalkable = false;
                    cells[rightWallIndex].IsWalkable = false;
                }
            }
            
            var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            
            // Test with a 1x1 agent (should pass through the gap)
            var smallAgent = new Agent { Size = 1 };
            var start = new Coordinate(0, GridSize / 2);
            var destination = new Coordinate(GridSize - 1, GridSize / 2);
            
            // Act & Assert
            var smallAgentResult = pathfinder.GetPath(smallAgent, start, destination);
            smallAgentResult.IsSuccess.Should().BeTrue();
            
            // Test with a 2x2 agent (shouldn't fit through the gap)
            var largeAgent = new Agent { Size = 2 };
            var largeAgentResult = pathfinder.GetPath(largeAgent, start, destination);
            largeAgentResult.IsSuccess.Should().BeFalse();
        }
        
        [Test]
        public void GetPath_SameStartAndDestination_ShouldReturnEmptyPath()
        {
            // Arrange
            var cells = CreateEmptyGrid(GridSize, GridSize);
            var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            var agent = new Agent { Size = 1 };
            var position = new Coordinate(GridSize / 2, GridSize / 2);
            
            // Act
            var result = pathfinder.GetPath(agent, position, position);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Length.Should().Be(0); // No path is needed
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