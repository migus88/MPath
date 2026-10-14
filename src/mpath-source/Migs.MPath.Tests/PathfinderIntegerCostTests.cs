using System;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using NUnit.Framework;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// The integer cost contract: step cost validation at construction, negative and huge weights.
    /// </summary>
    [TestFixture]
    public class PathfinderIntegerCostTests
    {
        private const int Width = 8;
        private const int Height = 6;
        private const int MaxCost = int.MaxValue / 2;

        [TestCase(0, 141, "StraightStepCost")]
        [TestCase(-5, 141, "StraightStepCost")]
        [TestCase(100, 0, "DiagonalStepCost")]
        [TestCase(100, -1, "DiagonalStepCost")]
        public void Constructor_WithAStepCostBelowOne_Throws(int straight, int diagonal, string parameter)
        {
            var settings = new PathfinderSettings { StraightStepCost = straight, DiagonalStepCost = diagonal };

            var create = () => new Pathfinder(CreateCells(), Width, Height, settings);

            create.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameter);
        }

        [Test]
        public void Constructor_WhenCrossingTheGridAtTheDearerStepCostPassesTheHeadroom_Throws()
        {
            var settings = new PathfinderSettings { StraightStepCost = 1, DiagonalStepCost = MaxCost / (Width + Height) + 1 };

            var create = () => new Pathfinder(CreateCells(), Width, Height, settings);

            create.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("DiagonalStepCost");
        }

        [Test]
        public void Constructor_WhenCrossingTheGridAtTheDearerStepCostFitsTheHeadroom_Succeeds()
        {
            var settings = new PathfinderSettings { StraightStepCost = 1, DiagonalStepCost = MaxCost / (Width + Height) };

            var create = () => new Pathfinder(CreateCells(), Width, Height, settings).Dispose();

            create.Should().NotThrow();
        }

        [Test]
        public void Constructor_WithoutDiagonalMovement_IgnoresTheDiagonalCostForTheHeadroom()
        {
            var settings = new PathfinderSettings
            {
                IsDiagonalMovementEnabled = false, StraightStepCost = 1, DiagonalStepCost = int.MaxValue
            };

            var create = () => new Pathfinder(CreateCells(), Width, Height, settings).Dispose();

            create.Should().NotThrow();
        }

        [Test]
        public void SetWeight_WithANegativeWeight_ThrowsAndKeepsTheCell()
        {
            using var pathfinder = new Pathfinder(CreateCells(), Width, Height);
            var target = new Coordinate(2, 2);

            var setWeight = () => pathfinder.SetWeight(target, -1);

            setWeight.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("weight");
            pathfinder.GetCell(target).Weight.Should().Be(0);
        }

        [Test]
        public void SetCell_WithANegativeWeight_Throws()
        {
            using var pathfinder = new Pathfinder(CreateCells(), Width, Height);

            var setCell = () => pathfinder.SetCell(new Coordinate(2, 2), new Cell { IsWalkable = true, Weight = -3 });

            setCell.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("cell");
        }

        [Test]
        public void GetReachable_WithANegativeWeightWrittenIntoTheGrid_CountsItAsZero(
            [Values(TieBreaking.None, TieBreaking.FewerDiagonals)] TieBreaking tieBreaking)
        {
            var cells = CreateCells();
            cells[3 * Height + 0].Weight = -50;
            var settings = new PathfinderSettings { IsDiagonalMovementEnabled = false, TieBreaking = tieBreaking };
            using var pathfinder = new Pathfinder(cells, Width, Height, settings);

            using var range = pathfinder.GetReachable(new Coordinate(0, 0), 400);

            range.TryGetCost(new Coordinate(3, 0), out var cost).Should().BeTrue();
            cost.Should().Be(300);
            range.TryGetCost(new Coordinate(4, 0), out var beyond).Should().BeTrue();
            beyond.Should().Be(400);
        }

        private static Cell[] CreateCells()
        {
            var cells = new Cell[Width * Height];
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    cells[x * Height + y] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
                }
            }

            return cells;
        }
    }
}
