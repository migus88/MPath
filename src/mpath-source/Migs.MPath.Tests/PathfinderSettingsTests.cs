using System;
using FluentAssertions;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Tests
{
    [TestFixture]
    public class PathfinderSettingsTests
    {
        [Test]
        public void DefaultConstructor_ShouldSetDefaultValues()
        {
            // Act
            var settings = new PathfinderSettings();
            
            // Assert
            settings.IsDiagonalMovementEnabled.Should().BeTrue();
            settings.IsCalculatingOccupiedCells.Should().BeTrue();
            settings.IsMovementBetweenCornersEnabled.Should().BeFalse();
            settings.IsCellWeightEnabled.Should().BeTrue();
            settings.StraightMovementMultiplier.Should().Be(1.0f);
            settings.DiagonalMovementMultiplier.Should().Be(1.41f);
            settings.PathSmoothingMethod.Should().Be(PathSmoothingMethod.None);
            settings.InitialBufferSize.Should().BeNull();
        }
        
        [Test]
        public void Properties_ShouldSetAndGetCustomValues()
        {
            // Arrange
            const bool isDiagonalEnabled = false;
            const bool isCalculatingOccupied = false;
            const bool isCornerMovementEnabled = true;
            const bool isCellWeightEnabled = false;
            const float straightMultiplier = 2.0f;
            const float diagonalMultiplier = 3.0f;
            const int bufferSize = 100;
            
            // Act
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = isDiagonalEnabled,
                IsCalculatingOccupiedCells = isCalculatingOccupied,
                IsMovementBetweenCornersEnabled = isCornerMovementEnabled,
                IsCellWeightEnabled = isCellWeightEnabled,
                StraightMovementMultiplier = straightMultiplier,
                DiagonalMovementMultiplier = diagonalMultiplier,
                PathSmoothingMethod = PathSmoothingMethod.StringPulling,
                InitialBufferSize = bufferSize
            };
            
            // Assert
            settings.IsDiagonalMovementEnabled.Should().Be(isDiagonalEnabled);
            settings.IsCalculatingOccupiedCells.Should().Be(isCalculatingOccupied);
            settings.IsMovementBetweenCornersEnabled.Should().Be(isCornerMovementEnabled);
            settings.IsCellWeightEnabled.Should().Be(isCellWeightEnabled);
            settings.StraightMovementMultiplier.Should().Be(straightMultiplier);
            settings.DiagonalMovementMultiplier.Should().Be(diagonalMultiplier);
            settings.PathSmoothingMethod.Should().Be(PathSmoothingMethod.StringPulling);
            settings.InitialBufferSize.Should().Be(bufferSize);
        }
        
        [Test]
        public void Settings_ShouldImplementIPathfinderSettings()
        {
            // Arrange
            var settings = new PathfinderSettings();
            
            // Act & Assert
            settings.Should().BeAssignableTo<IPathfinderSettings>();
        }
    }
} 