using System;
using FluentAssertions;
using Migs.MPath.Core.Data;
using NUnit.Framework;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class CellTests
    {
        [Test]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var coordinate = new Coordinate(5, 10);
            var cell = new Cell { Coordinate = coordinate };

            // Assert
            cell.Coordinate.Should().Be(coordinate);
            // Check the actual defaults from the implementation
            cell.IsWalkable.Should().BeFalse();
            cell.IsOccupied.Should().BeFalse();
            cell.Weight.Should().Be(0f);
        }

        [Test]
        public void Constructor_WithNonDefaultValues_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var coordinate = new Coordinate(5, 10);
            var isWalkable = true;
            var isOccupied = true;
            var weight = 2.5f;

            // Act
            var cell = new Cell
            {
                Coordinate = coordinate,
                IsWalkable = isWalkable,
                IsOccupied = isOccupied,
                Weight = weight
            };

            // Assert
            cell.Coordinate.Should().Be(coordinate);
            cell.IsWalkable.Should().Be(isWalkable);
            cell.IsOccupied.Should().Be(isOccupied);
            cell.Weight.Should().Be(weight);
        }

        [Test]
        public void Reset_IsObsolete_LeavesTheCellUnchanged()
        {
            // Arrange
            var cell = new Cell
            {
                Coordinate = new Coordinate(5, 10),
                IsWalkable = true,
                IsOccupied = true,
                Weight = 2.5f
            };
            var before = cell;

            // Act
#pragma warning disable CS0618
            cell.Reset();
#pragma warning restore CS0618

            // Assert
            cell.Should().Be(before);
        }
    }
}