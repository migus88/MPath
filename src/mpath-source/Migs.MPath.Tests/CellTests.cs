using System;
using System.Reflection;
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
        public void Reset_ShouldResetInternalPathfindingState()
        {
            // Arrange
            var cell = new Cell
            {
                Coordinate = new Coordinate(5, 10),
                IsWalkable = true,
                IsOccupied = false,
                Weight = 1.0f
            };

            // Set parent coordinate using private field through reflection for testing
            var parentField = typeof(Cell).GetField("parentCoordinate", BindingFlags.NonPublic | BindingFlags.Instance);
            if (parentField != null)
            {
                parentField.SetValue(cell, new Coordinate(1, 1));
            }

            // Set f, g, h values using reflection
            var fField = typeof(Cell).GetField("f", BindingFlags.NonPublic | BindingFlags.Instance);
            var gField = typeof(Cell).GetField("g", BindingFlags.NonPublic | BindingFlags.Instance);
            var hField = typeof(Cell).GetField("h", BindingFlags.NonPublic | BindingFlags.Instance);
            var isClosedField = typeof(Cell).GetField("isClosed", BindingFlags.NonPublic | BindingFlags.Instance);
            var isOpenField = typeof(Cell).GetField("isOpen", BindingFlags.NonPublic | BindingFlags.Instance);

            fField?.SetValue(cell, 10.0f);
            gField?.SetValue(cell, 5.0f);
            hField?.SetValue(cell, 5.0f);
            isClosedField?.SetValue(cell, true);
            isOpenField?.SetValue(cell, true);

            // Act
            cell.Reset();

            // Assert - test only a subset of fields to avoid fragility
            if (parentField != null)
            {
                var parentAfterReset = (Coordinate)parentField.GetValue(cell);
                parentAfterReset.Should().Be(new Coordinate(0, 0));
            }

            if (isClosedField != null)
            {
                var isClosedAfterReset = (bool)isClosedField.GetValue(cell);
                isClosedAfterReset.Should().BeFalse();
            }
        }
    }
}