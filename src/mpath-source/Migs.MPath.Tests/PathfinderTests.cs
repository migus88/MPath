using System.Diagnostics;
using System.Reflection;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Tests.Implementations;
using Migs.MPath.Tools;
using FluentAssertions;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathfinderTests
    {
        private const string ResultsDirectory = "Results/";

        [SetUp]
        public void Setup()
        {
            // Create results directory if it doesn't exist
            if (!Directory.Exists(ResultsDirectory))
            {
                Directory.CreateDirectory(ResultsDirectory);
            }
        }

        [Test]
        public void GetPath_WithLargerAgent_ShouldFindPathAroundObstacles()
        {
            // Arrange
            var maze = new Maze("Maze/Conditions/001.png");
            
            var start = maze.Start;
            var destination = maze.Destination;
            var agent = new Agent { Size = 2 };

            var pathfinder = new Pathfinder(maze.Cells);
            
            // Act
            var result = pathfinder.GetPath(agent, start, destination);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Length.Should().BeGreaterThan(0);
            
            // Visualization (not part of the test assertion)
            maze.AddPath(result.Path.ToArray());
            maze.SaveImage($"{ResultsDirectory}001.png", 100);
        }
        
        [Test]
        public void GetPath_ThroughCavern_ShouldFindPath()
        {
            // Arrange
            var maze = new Maze("Maze/Conditions/000.gif");
            var start = new Coordinate(10, 10);
            var destination = new Coordinate(502, 374);
            
            maze.SetStart(start);
            maze.SetDestination(destination);
            var agent = new Agent { Size = 1 };
            
            var pathfinder = new Pathfinder(maze.Cells);
            
            // Act
            var result = pathfinder.GetPath(agent, start, destination);
            
            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Length.Should().BeGreaterThan(0);
            
            // Visualization (not part of the test assertion)
            if (result.IsSuccess)
            {
                maze.AddPath(result.Path.ToArray());
            }
            
            maze.SaveImage($"{ResultsDirectory}000.png", 4);
        }
        
        [Test]
        public void GetPath_WithInvalidDestination_ShouldThrowArgumentException()
        {
            // Arrange
            var maze = new Maze("Maze/Conditions/001.png");
            var start = maze.Start;
            var invalidDestination = new Coordinate(maze.Width + 10, maze.Height + 10); // Outside maze bounds
            var agent = new Agent { Size = 1 };
            
            var pathfinder = new Pathfinder(maze.Cells);
            
            // Act & Assert
            var action = () => pathfinder.GetPath(agent, start, invalidDestination);
            action.Should().Throw<ArgumentException>()
                  .WithMessage("*outside the valid field range*");
        }
        
        [Test]
        public void GetPath_WithNullAgent_ShouldThrowArgumentNullException()
        {
            // Arrange
            var maze = new Maze("Maze/Conditions/001.png");
            var start = maze.Start;
            var destination = maze.Destination;
            
            var pathfinder = new Pathfinder(maze.Cells);
            
            // Act & Assert
            var action = () => pathfinder.GetPath(null!, start, destination);
            action.Should().Throw<ArgumentNullException>()
                  .WithParameterName("agent");
        }
    }
}