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
            settings.AreOccupiedCellsBlocking.Should().BeTrue();
            settings.CornerBlocking.Should().Be(CornerBlocking.BetweenBlockedCells);
            settings.IsCellWeightEnabled.Should().BeTrue();
            settings.StraightStepCost.Should().Be(100);
            settings.DiagonalStepCost.Should().Be(141);
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
            const bool areOccupiedCellsBlocking = false;
            const CornerBlocking cornerBlocking = CornerBlocking.None;
            const bool isCellWeightEnabled = false;
            const int straightStepCost = 2;
            const int diagonalStepCost = 3;
            
            // Act
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = isDiagonalEnabled,
                AreOccupiedCellsBlocking = areOccupiedCellsBlocking,
                CornerBlocking = cornerBlocking,
                IsCellWeightEnabled = isCellWeightEnabled,
                StraightStepCost = straightStepCost,
                DiagonalStepCost = diagonalStepCost,
                PathSmoothingMethod = PathSmoothingMethod.StringPulling
            };
            
            // Assert
            settings.IsDiagonalMovementEnabled.Should().Be(isDiagonalEnabled);
            settings.AreOccupiedCellsBlocking.Should().Be(areOccupiedCellsBlocking);
            settings.CornerBlocking.Should().Be(cornerBlocking);
            settings.IsCellWeightEnabled.Should().Be(isCellWeightEnabled);
            settings.StraightStepCost.Should().Be(straightStepCost);
            settings.DiagonalStepCost.Should().Be(diagonalStepCost);
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