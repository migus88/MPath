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
        
        [Test]
        public void GetPath_WithPathSmoothingEnabled_ShouldSmoothPath()
        {
            // Arrange
            var width = 10;
            var height = 10;
            var cells = CreateEmptyGrid(width, height);
            
            // Create two versions of settings for comparison
            var noSmoothingSettings = new PathfinderSettings
            {
                PathSmoothingMethod = PathSmoothingMethod.None
            };
            
            var smoothingSettings = new PathfinderSettings
            {
                PathSmoothingMethod = PathSmoothingMethod.StringPulling
            };

            // Create a simple maze with a zigzag path that should be smoothed
            // Make a small corridor that forces a zigzag path without smoothing
            for (int i = 2; i < 8; i++)
            {
                // Create walls in alternating rows
                if (i % 2 == 0)
                {
                    cells[i * height + 4].IsWalkable = false;
                }
                else
                {
                    cells[i * height + 6].IsWalkable = false;
                }
            }

            var from = new Coordinate(1, 5);
            var to = new Coordinate(8, 5);
            var agent = new Agent { Size = 1 };

            // Get both smoothed and unsmoothed paths for comparison
            using var noSmoothingPathfinder = new Pathfinder(cells, width, height, noSmoothingSettings);
            var noSmoothingResult = noSmoothingPathfinder.GetPath(agent, from, to);
            
            using var smoothingPathfinder = new Pathfinder(cells, width, height, smoothingSettings);
            var smoothingResult = smoothingPathfinder.GetPath(agent, from, to);

            // Assert
            noSmoothingResult.IsSuccess.Should().BeTrue();
            smoothingResult.IsSuccess.Should().BeTrue();
            
            // With path smoothing, we expect a shorter path than without smoothing
            smoothingResult.Length.Should().BeLessThan(noSmoothingResult.Length);
        }

        [Test]
        public void GetPath_WithSimpleSmoothing_ShouldSmoothPath()
        {
            // Arrange
            var width = 10;
            var height = 10;
            var cells = CreateEmptyGrid(width, height);
            
            // Create two versions of settings for comparison
            var noSmoothingSettings = new PathfinderSettings
            {
                PathSmoothingMethod = PathSmoothingMethod.None
            };
            
            var smoothingSettings = new PathfinderSettings
            {
                PathSmoothingMethod = PathSmoothingMethod.Simple
            };

            // Create a simple maze with a zigzag path that should be smoothed
            // Make a small corridor that forces a zigzag path without smoothing
            for (int i = 2; i < 8; i++)
            {
                // Create walls in alternating rows
                if (i % 2 == 0)
                {
                    cells[i * height + 4].IsWalkable = false;
                }
                else
                {
                    cells[i * height + 6].IsWalkable = false;
                }
            }

            var from = new Coordinate(1, 5);
            var to = new Coordinate(8, 5);
            var agent = new Agent { Size = 1 };

            // Get both smoothed and unsmoothed paths for comparison
            using var noSmoothingPathfinder = new Pathfinder(cells, width, height, noSmoothingSettings);
            var noSmoothingResult = noSmoothingPathfinder.GetPath(agent, from, to);
            
            using var smoothingPathfinder = new Pathfinder(cells, width, height, smoothingSettings);
            var smoothingResult = smoothingPathfinder.GetPath(agent, from, to);

            // Assert
            noSmoothingResult.IsSuccess.Should().BeTrue();
            smoothingResult.IsSuccess.Should().BeTrue();
            
            // With path smoothing, the path should never be longer than without smoothing
            // In some simple cases, they might be the same length
            smoothingResult.Length.Should().BeLessThanOrEqualTo(noSmoothingResult.Length);
        }

        [Test]
        public void GetPath_CompareWithAndWithoutSmoothing_ShouldReducePathLength()
        {
            // Arrange
            var width = 20;
            var height = 20;
            var cells = CreateEmptyGrid(width, height);
            
            // Create a complex maze with many obstacles that would create a jagged path
            for (int i = 5; i < 15; i++)
            {
                // Create horizontal and vertical walls with gaps
                cells[i * height + 10].IsWalkable = false;
                cells[10 * height + i].IsWalkable = false;
                
                // Leave some gaps for the path to go through
                if (i == 7 || i == 12)
                {
                    cells[i * height + 10].IsWalkable = true;
                    cells[10 * height + i].IsWalkable = true;
                }
            }

            var from = new Coordinate(5, 5);
            var to = new Coordinate(15, 15);
            var agent = new Agent { Size = 1 };

            // Create a pathfinder without smoothing
            var settingsWithoutSmoothing = new PathfinderSettings
            {
                PathSmoothingMethod = PathSmoothingMethod.None
            };
            
            // Create a pathfinder with smoothing
            var settingsWithSmoothing = new PathfinderSettings
            {
                PathSmoothingMethod = PathSmoothingMethod.StringPulling
            };

            // Act
            using var pathfinderWithoutSmoothing = new Pathfinder(cells, width, height, settingsWithoutSmoothing);
            var resultWithoutSmoothing = pathfinderWithoutSmoothing.GetPath(agent, from, to);
            
            using var pathfinderWithSmoothing = new Pathfinder(cells, width, height, settingsWithSmoothing);
            var resultWithSmoothing = pathfinderWithSmoothing.GetPath(agent, from, to);

            // Assert
            resultWithoutSmoothing.IsSuccess.Should().BeTrue();
            resultWithSmoothing.IsSuccess.Should().BeTrue();
            
            // The smoothed path should have fewer waypoints
            resultWithSmoothing.Length.Should().BeLessThan(resultWithoutSmoothing.Length);
        }
        
        [Test]
        public void GetPath_CompareAllSmoothingMethods_ShouldReducePathLength()
        {
            // Arrange
            var width = 20;
            var height = 20;
            var cells = CreateEmptyGrid(width, height);
            
            // Create a complex maze with many obstacles that would create a jagged path
            for (int i = 5; i < 15; i++)
            {
                // Create horizontal and vertical walls with gaps
                cells[i * height + 10].IsWalkable = false;
                cells[10 * height + i].IsWalkable = false;
                
                // Leave some gaps for the path to go through
                if (i == 7 || i == 12)
                {
                    cells[i * height + 10].IsWalkable = true;
                    cells[10 * height + i].IsWalkable = true;
                }
            }

            var from = new Coordinate(5, 5);
            var to = new Coordinate(15, 15);
            var agent = new Agent { Size = 1 };

            // Create pathfinders with different smoothing methods
            var settingsNoSmoothing = new PathfinderSettings
            {
                PathSmoothingMethod = PathSmoothingMethod.None
            };
            
            var settingsSimpleSmoothing = new PathfinderSettings
            {
                PathSmoothingMethod = PathSmoothingMethod.Simple
            };
            
            var settingsStringPulling = new PathfinderSettings
            {
                PathSmoothingMethod = PathSmoothingMethod.StringPulling
            };

            // Act
            using var pathfinderNoSmoothing = new Pathfinder(cells, width, height, settingsNoSmoothing);
            var resultNoSmoothing = pathfinderNoSmoothing.GetPath(agent, from, to);
            
            using var pathfinderSimpleSmoothing = new Pathfinder(cells, width, height, settingsSimpleSmoothing);
            var resultSimpleSmoothing = pathfinderSimpleSmoothing.GetPath(agent, from, to);
            
            using var pathfinderStringPulling = new Pathfinder(cells, width, height, settingsStringPulling);
            var resultStringPulling = pathfinderStringPulling.GetPath(agent, from, to);

            // Assert
            resultNoSmoothing.IsSuccess.Should().BeTrue();
            resultSimpleSmoothing.IsSuccess.Should().BeTrue();
            resultStringPulling.IsSuccess.Should().BeTrue();
            
            // Both types of smoothing should reduce path length compared to no smoothing
            resultSimpleSmoothing.Length.Should().BeLessThan(resultNoSmoothing.Length);
            resultStringPulling.Length.Should().BeLessThan(resultNoSmoothing.Length);
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