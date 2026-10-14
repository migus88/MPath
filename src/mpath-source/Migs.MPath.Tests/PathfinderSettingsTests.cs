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
            settings.CornerBlocking.Should().Be(CornerBlocking.BetweenBlockedCells);
            settings.IsCellWeightEnabled.Should().BeTrue();
            settings.StraightMovementMultiplier.Should().Be(1.0f);
            settings.DiagonalMovementMultiplier.Should().Be(1.41f);
            settings.PathSmoothingMethod.Should().Be(PathSmoothingMethod.None);
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
            
            // Act
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = isDiagonalEnabled,
                IsCalculatingOccupiedCells = isCalculatingOccupied,
                IsMovementBetweenCornersEnabled = isCornerMovementEnabled,
                IsCellWeightEnabled = isCellWeightEnabled,
                StraightMovementMultiplier = straightMultiplier,
                DiagonalMovementMultiplier = diagonalMultiplier,
                PathSmoothingMethod = PathSmoothingMethod.StringPulling
            };
            
            // Assert
            settings.IsDiagonalMovementEnabled.Should().Be(isDiagonalEnabled);
            settings.IsCalculatingOccupiedCells.Should().Be(isCalculatingOccupied);
            settings.IsMovementBetweenCornersEnabled.Should().Be(isCornerMovementEnabled);
            settings.IsCellWeightEnabled.Should().Be(isCellWeightEnabled);
            settings.StraightMovementMultiplier.Should().Be(straightMultiplier);
            settings.DiagonalMovementMultiplier.Should().Be(diagonalMultiplier);
            settings.PathSmoothingMethod.Should().Be(PathSmoothingMethod.StringPulling);
        }
        
        [TestCase(true, CornerBlocking.None)]
        [TestCase(false, CornerBlocking.BetweenBlockedCells)]
        public void IsMovementBetweenCornersEnabled_SetFromDefault_MapsToCornerBlocking(bool value, CornerBlocking expected)
        {
            // Arrange
            var settings = new PathfinderSettings();

            // Act
            settings.IsMovementBetweenCornersEnabled = value;

            // Assert
            settings.CornerBlocking.Should().Be(expected);
        }

        [Test]
        public void IsMovementBetweenCornersEnabled_SetFalse_KeepsCustomCornerBlocking()
        {
            // Arrange
            var settings = new PathfinderSettings { CornerBlocking = CornerBlocking.BetweenWalls };

            // Act
            settings.IsMovementBetweenCornersEnabled = false;

            // Assert
            settings.CornerBlocking.Should().Be(CornerBlocking.BetweenWalls);
            settings.IsMovementBetweenCornersEnabled.Should().BeFalse();
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