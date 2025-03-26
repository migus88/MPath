using FluentAssertions;
using Migs.MPath.Core.Data;
using NUnit.Framework;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class CoordinateTests
    {
        [Test]
        public void DefaultConstructor_ShouldInitializeToZero()
        {
            // Act
            var coordinate = new Coordinate();
            
            // Assert
            coordinate.X.Should().Be(0);
            coordinate.Y.Should().Be(0);
        }
        
        [Test]
        public void Constructor_WithXAndY_ShouldInitializeToSpecifiedValues()
        {
            // Arrange
            const int x = 5;
            const int y = 10;
            
            // Act
            var coordinate = new Coordinate(x, y);
            
            // Assert
            coordinate.X.Should().Be(x);
            coordinate.Y.Should().Be(y);
        }
        
        [Test]
        public void Equals_WithSameCoordinates_ShouldReturnTrue()
        {
            // Arrange
            var coordinate1 = new Coordinate(5, 10);
            var coordinate2 = new Coordinate(5, 10);
            
            // Act & Assert
            coordinate1.Equals(coordinate2).Should().BeTrue();
            (coordinate1 == coordinate2).Should().BeTrue();
            (coordinate1 != coordinate2).Should().BeFalse();
        }
        
        [Test]
        public void Equals_WithDifferentCoordinates_ShouldReturnFalse()
        {
            // Arrange
            var coordinate1 = new Coordinate(5, 10);
            var coordinate2 = new Coordinate(5, 11);
            
            // Act & Assert
            coordinate1.Equals(coordinate2).Should().BeFalse();
            (coordinate1 == coordinate2).Should().BeFalse();
            (coordinate1 != coordinate2).Should().BeTrue();
        }
        
        [Test]
        public void Equals_WithBoxedObject_ShouldReturnTrueForSameCoordinates()
        {
            // Arrange
            var coordinate = new Coordinate(5, 10);
            object boxedCoordinate = new Coordinate(5, 10);
            
            // Act & Assert
            coordinate.Equals(boxedCoordinate).Should().BeTrue();
        }
        
        [Test]
        public void Equals_WithDifferentType_ShouldReturnFalse()
        {
            // Arrange
            var coordinate = new Coordinate(5, 10);
            object differentType = "Not a coordinate";
            
            // Act & Assert
            coordinate.Equals(differentType).Should().BeFalse();
        }
        
        [Test]
        public void Equals_WithNull_ShouldReturnFalse()
        {
            // Arrange
            var coordinate = new Coordinate(5, 10);
            
            // Act & Assert
            coordinate.Equals(null).Should().BeFalse();
        }
        
        [Test]
        public void GetHashCode_ForSameCoordinates_ShouldReturnSameValue()
        {
            // Arrange
            var coordinate1 = new Coordinate(5, 10);
            var coordinate2 = new Coordinate(5, 10);
            
            // Act
            var hashCode1 = coordinate1.GetHashCode();
            var hashCode2 = coordinate2.GetHashCode();
            
            // Assert
            hashCode1.Should().Be(hashCode2);
        }
        
        [Test]
        public void GetHashCode_ForDifferentCoordinates_ShouldReturnDifferentValues()
        {
            // Arrange
            var coordinate1 = new Coordinate(5, 10);
            var coordinate2 = new Coordinate(10, 5);
            
            // Act
            var hashCode1 = coordinate1.GetHashCode();
            var hashCode2 = coordinate2.GetHashCode();
            
            // Assert
            hashCode1.Should().NotBe(hashCode2);
        }
        
        [Test]
        public void Reset_ShouldSetCoordinatesToZero()
        {
            // Arrange
            var coordinate = new Coordinate(5, 10);
            
            // Act
            coordinate.Reset();
            
            // Assert
            coordinate.X.Should().Be(0);
            coordinate.Y.Should().Be(0);
        }
        
        [Test]
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var coordinate = new Coordinate(5, 10);
            var expected = "5:10";
            
            // Act
            var result = coordinate.ToString();
            
            // Assert
            result.Should().Be(expected);
        }
    }
} 