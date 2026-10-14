using System.Runtime.CompilerServices;
using FluentAssertions;
using Migs.MPath.Core.Data;
using NUnit.Framework;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathCellTests
    {
        [Test]
        public void Constructor_ShouldInitializeWithDefaultValues()
        {
            // Arrange & Act
            var cell = new PathCell();

            // Assert
            cell.IsWalkable.Should().BeFalse();
            cell.IsOccupied.Should().BeFalse();
            cell.Weight.Should().Be(0);
        }

        [Test]
        public void Constructor_WithNonDefaultValues_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var isWalkable = true;
            var isOccupied = true;
            var weight = 250;

            // Act
            var cell = new PathCell
            {
                IsWalkable = isWalkable,
                IsOccupied = isOccupied,
                Weight = weight
            };

            // Assert
            cell.IsWalkable.Should().Be(isWalkable);
            cell.IsOccupied.Should().Be(isOccupied);
            cell.Weight.Should().Be(weight);
        }

        [Test]
        public void Layout_ShouldBeEightBytes()
        {
            // A cell carries no coordinate: its slot in the grid is its position.
            Unsafe.SizeOf<PathCell>().Should().Be(8);
        }
    }
}
