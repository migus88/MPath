using System;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using NSubstitute;
using NUnit.Framework;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathfinderConstructorTests
    {
        private const int TestWidth = 10;
        private const int TestHeight = 10;
        
        [Test]
        public void Constructor_WithCellArray_ShouldInitializePathfinder()
        {
            // Arrange
            var cells = new Cell[TestWidth * TestHeight];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = new Cell
                {
                    Coordinate = new Coordinate(i / TestHeight, i % TestHeight),
                    IsWalkable = true
                };
            }

            // Act & Assert - should not throw
            var action = () => 
            {
                var pathfinder = new Pathfinder(cells, TestWidth, TestHeight);
                pathfinder.Dispose();
            };
            action.Should().NotThrow();
        }
        
        [Test]
        public void Constructor_WithNullCellArray_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var action = () => new Pathfinder((Cell[])null!, TestWidth, TestHeight);
            action.Should().Throw<ArgumentNullException>()
                  .WithParameterName("cells");
        }
        
        [Test]
        public void Constructor_WithNegativeWidth_ShouldThrowArgumentException()
        {
            // Arrange
            var cells = new Cell[TestWidth * TestHeight];

            // Act & Assert
            var action = () => new Pathfinder(cells, -1, TestHeight);
            action.Should().Throw<ArgumentException>()
                  .WithParameterName("fieldWidth");
        }
        
        [Test]
        public void Constructor_WithNegativeHeight_ShouldThrowArgumentException()
        {
            // Arrange
            var cells = new Cell[TestWidth * TestHeight];

            // Act & Assert
            var action = () => new Pathfinder(cells, TestWidth, -1);
            action.Should().Throw<ArgumentException>()
                  .WithParameterName("fieldHeight");
        }
        
        [Test]
        public void Constructor_WithCellHolderArray_ShouldInitializePathfinder()
        {
            // Arrange
            var cellHolders = new ICellHolder[TestWidth * TestHeight];
            for (var i = 0; i < cellHolders.Length; i++)
            {
                var cell = new Cell
                {
                    Coordinate = new Coordinate(i / TestHeight, i % TestHeight),
                    IsWalkable = true
                };
                
                var cellHolder = Substitute.For<ICellHolder>();
                cellHolder.CellData.Returns(cell);
                cellHolders[i] = cellHolder;
            }

            // Act & Assert - should not throw
            var action = () => 
            {
                var pathfinder = new Pathfinder(cellHolders, TestWidth, TestHeight);
                pathfinder.Dispose();
            };
            action.Should().NotThrow();
        }
        
        [Test]
        public void Constructor_WithNullCellHolderArray_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var action = () => new Pathfinder((ICellHolder[])null!, TestWidth, TestHeight);
            action.Should().Throw<ArgumentNullException>()
                  .WithParameterName("holders");
        }
        
        [Test]
        public void Constructor_WithCellsMatrix_ShouldInitializePathfinder()
        {
            // Arrange
            var cellsMatrix = new Cell[TestWidth, TestHeight];
            for (var x = 0; x < TestWidth; x++)
            {
                for (var y = 0; y < TestHeight; y++)
                {
                    cellsMatrix[x, y] = new Cell
                    {
                        Coordinate = new Coordinate(x, y),
                        IsWalkable = true
                    };
                }
            }

            // Act & Assert - should not throw
            var action = () => 
            {
                var pathfinder = new Pathfinder(cellsMatrix);
                pathfinder.Dispose();
            };
            action.Should().NotThrow();
        }
        
        [Test]
        public void Constructor_WithNullCellsMatrix_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var action = () => new Pathfinder((Cell[,])null!);
            action.Should().Throw<ArgumentNullException>()
                  .WithParameterName("cellsMatrix");
        }
        
        [Test]
        public void Constructor_WithCellHoldersMatrix_ShouldInitializePathfinder()
        {
            // Arrange
            var cellHoldersMatrix = new ICellHolder[TestWidth, TestHeight];
            for (var x = 0; x < TestWidth; x++)
            {
                for (var y = 0; y < TestHeight; y++)
                {
                    var cell = new Cell
                    {
                        Coordinate = new Coordinate(x, y),
                        IsWalkable = true
                    };
                    
                    var cellHolder = Substitute.For<ICellHolder>();
                    cellHolder.CellData.Returns(cell);
                    cellHoldersMatrix[x, y] = cellHolder;
                }
            }

            // Act & Assert - should not throw
            var action = () => 
            {
                var pathfinder = new Pathfinder(cellHoldersMatrix);
                pathfinder.Dispose();
            };
            action.Should().NotThrow();
        }
        
        [Test]
        public void Constructor_WithNullCellHoldersMatrix_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var action = () => new Pathfinder((ICellHolder[,])null!);
            action.Should().Throw<ArgumentNullException>()
                  .WithParameterName("cellHoldersMatrix");
        }
        
        [Test]
        public void Dispose_ShouldNotThrow()
        {
            // Arrange
            var cells = new Cell[TestWidth * TestHeight];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = new Cell
                {
                    Coordinate = new Coordinate(i / TestHeight, i % TestHeight),
                    IsWalkable = true
                };
            }
            
            var pathfinder = new Pathfinder(cells, TestWidth, TestHeight);
            
            // Act & Assert
            var action = () => pathfinder.Dispose();
            action.Should().NotThrow();
        }
    }
} 