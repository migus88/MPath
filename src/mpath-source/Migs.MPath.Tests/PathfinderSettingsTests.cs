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
            settings.CornerBlocking.Should().Be(CornerBlocking.BetweenBlockedCells);
            settings.IsCellWeightEnabled.Should().BeTrue();
            settings.StraightMovementMultiplier.Should().Be(1.0f);
            settings.DiagonalMovementMultiplier.Should().Be(1.41f);
            settings.PathSmoothingMethod.Should().Be(PathSmoothingMethod.None);
            settings.GridSnapshotMode.Should().Be(GridSnapshotMode.PerQuery);
            settings.CellLayout.Should().Be(CellLayout.Auto);
            settings.TieBreaking.Should().Be(TieBreaking.None);
        }
        
        [Test]
        public void Properties_ShouldSetAndGetCustomValues()
        {
            // Arrange
            const bool isDiagonalEnabled = false;
            const bool isCalculatingOccupied = false;
            const CornerBlocking cornerBlocking = CornerBlocking.None;
            const bool isCellWeightEnabled = false;
            const float straightMultiplier = 2.0f;
            const float diagonalMultiplier = 3.0f;
            
            // Act
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = isDiagonalEnabled,
                IsCalculatingOccupiedCells = isCalculatingOccupied,
                CornerBlocking = cornerBlocking,
                IsCellWeightEnabled = isCellWeightEnabled,
                StraightMovementMultiplier = straightMultiplier,
                DiagonalMovementMultiplier = diagonalMultiplier,
                PathSmoothingMethod = PathSmoothingMethod.StringPulling
            };
            
            // Assert
            settings.IsDiagonalMovementEnabled.Should().Be(isDiagonalEnabled);
            settings.IsCalculatingOccupiedCells.Should().Be(isCalculatingOccupied);
            settings.CornerBlocking.Should().Be(cornerBlocking);
            settings.IsCellWeightEnabled.Should().Be(isCellWeightEnabled);
            settings.StraightMovementMultiplier.Should().Be(straightMultiplier);
            settings.DiagonalMovementMultiplier.Should().Be(diagonalMultiplier);
            settings.PathSmoothingMethod.Should().Be(PathSmoothingMethod.StringPulling);
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