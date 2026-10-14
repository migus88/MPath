using System;
using System.Collections.Generic;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// The allocation-free ways to read a <see cref="PathResult"/> and a <see cref="RangeResult"/> (span, indexer,
    /// struct enumerator), the O(1) range lookups and <see cref="RangeResult.CopyCostsTo"/>.
    /// </summary>
    [TestFixture]
    public class ResultAccessTests
    {
        private const int Width = 23;
        private const int Height = 17;
        private const int LookupRounds = 3;

        private static readonly int[] Seeds = { 1, 7, 42, 1234 };

        [Test]
        public void PathResult_SpanIndexerAndForeach_MatchGet()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));
            using var result = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(Width - 1, Height - 3));
            var expected = new List<Coordinate>();

            for (var i = 0; i < result.Length; i++)
            {
                expected.Add(result.Get(i));
            }

            // Act
            var fromSpan = result.AsSpan().ToArray();
            var fromIndexer = new List<Coordinate>();
            var fromForeach = new List<Coordinate>();

            for (var i = 0; i < result.Length; i++)
            {
                fromIndexer.Add(result[i]);
            }

            foreach (var coordinate in result)
            {
                fromForeach.Add(coordinate);
            }

            // Assert
            expected.Should().NotBeEmpty();
            fromSpan.Should().Equal(expected);
            fromIndexer.Should().Equal(expected);
            fromForeach.Should().Equal(expected);
        }

        [Test]
        public void RangeResult_SpanIndexerAndForeach_MatchGet()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 3, wallChance: 0.2));
            using var result = pathfinder.GetReachable(new Coordinate(5, 5), 6f);
            var expected = new List<ReachableCell>();

            for (var i = 0; i < result.Length; i++)
            {
                expected.Add(result.Get(i));
            }

            // Act
            var fromSpan = result.AsSpan().ToArray();
            var fromIndexer = new List<ReachableCell>();
            var fromForeach = new List<ReachableCell>();

            for (var i = 0; i < result.Length; i++)
            {
                fromIndexer.Add(result[i]);
            }

            foreach (var cell in result)
            {
                fromForeach.Add(cell);
            }

            // Assert
            expected.Count.Should().BeGreaterThan(1);
            fromSpan.Should().Equal(expected);
            fromIndexer.Should().Equal(expected);
            fromForeach.Should().Equal(expected);
        }

        [Test]
        public void FailedAndEmptyResults_AreEmptySequences()
        {
            // Arrange
            var failure = PathResult.Failure();
            var empty = RangeResult.Empty();
            var visited = 0;

            // Act
            foreach (var _ in failure)
            {
                visited++;
            }

            foreach (var _ in empty)
            {
                visited++;
            }

            // Assert
            visited.Should().Be(0);
            failure.AsSpan().IsEmpty.Should().BeTrue();
            empty.AsSpan().IsEmpty.Should().BeTrue();
            empty.Contains(new Coordinate(0, 0)).Should().BeFalse();
            ((Action)(() => _ = failure[0])).Should().Throw<ArgumentOutOfRangeException>();
            ((Action)(() => _ = empty[0])).Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Indexers_OutOfRange_Throw([Values(-1, 1)] int offsetFromEnd)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));
            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(3, 0));
            using var range = pathfinder.GetReachable(new Coordinate(0, 0), 1f);
            var pathIndex = offsetFromEnd < 0 ? -1 : path.Length;
            var rangeIndex = offsetFromEnd < 0 ? -1 : range.Length;

            // Act
            var readPath = () => _ = path[pathIndex];
            var readRange = () => _ = range[rangeIndex];

            // Assert
            readPath.Should().Throw<ArgumentOutOfRangeException>();
            readRange.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void PathResult_AfterDispose_NewAccessorsThrow()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));
            var result = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(3, 3));
            result.Dispose();

            // Act & Assert
            ((Action)(() => result.AsSpan())).Should().Throw<ObjectDisposedException>();
            ((Action)(() => _ = result[0])).Should().Throw<ObjectDisposedException>();
            ((Action)(() => result.GetEnumerator())).Should().Throw<ObjectDisposedException>();
        }

        [Test]
        public void RangeResult_AfterDispose_NewAccessorsThrow()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));
            var result = pathfinder.GetReachable(new Coordinate(3, 3), 2f);
            result.Contains(new Coordinate(3, 3)).Should().BeTrue();
            result.Dispose();
            var costs = new float[Width * Height];

            // Act & Assert
            ((Action)(() => result.AsSpan())).Should().Throw<ObjectDisposedException>();
            ((Action)(() => _ = result[0])).Should().Throw<ObjectDisposedException>();
            ((Action)(() => result.GetEnumerator())).Should().Throw<ObjectDisposedException>();
            ((Action)(() => result.Contains(new Coordinate(3, 3)))).Should().Throw<ObjectDisposedException>();
            ((Action)(() => result.CopyCostsTo(costs))).Should().Throw<ObjectDisposedException>();
        }

        [Test]
        public void Foreach_OverResults_AllocatesNothing()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));
            using var path = pathfinder.GetPath(new Coordinate(0, 0), new Coordinate(Width - 1, Height - 1));
            using var range = pathfinder.GetReachable(new Coordinate(5, 5), 4f);
            var sum = SumBoth(path, range);

            // Act
            var before = GC.GetAllocatedBytesForCurrentThread();
            sum += SumBoth(path, range);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            sum.Should().BeGreaterThan(0);
            allocated.Should().Be(0);
        }

        [Test]
        public void Lookups_AfterTheFirst_AllocateNothing()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));

            using (var warmUp = pathfinder.GetReachable(new Coordinate(5, 5), 4f))
            {
                warmUp.Contains(new Coordinate(5, 5));
            }

            using var range = pathfinder.GetReachable(new Coordinate(5, 5), 4f);
            range.Contains(new Coordinate(5, 5)).Should().BeTrue();
            var hits = 0;

            // Act
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var x = -1; x <= Width; x++)
            {
                for (var y = -1; y <= Height; y++)
                {
                    if (range.TryGetCost(new Coordinate(x, y), out _))
                    {
                        hits++;
                    }
                }
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            hits.Should().Be(range.Length);
            allocated.Should().Be(0);
        }

        [Test]
        public void TryGetCell_OnSeededGrids_AgreesWithLinearScan(
            [ValueSource(nameof(Seeds))] int seed,
            [Values] TieBreaking tieBreaking)
        {
            // Arrange
            var settings = new PathfinderSettings { TieBreaking = tieBreaking, IsCellWeightEnabled = true };
            using var pathfinder = new Pathfinder(CreateGrid(seed, wallChance: 0.25), settings);
            var random = new Random(seed);
            var origin = new Coordinate(random.Next(Width), random.Next(Height));
            pathfinder.SetWalkable(origin, true);
            using var range = pathfinder.GetReachable(origin, 9f);
            var expected = new Dictionary<Coordinate, ReachableCell>();

            for (var i = 0; i < range.Length; i++)
            {
                expected.Add(range.Get(i).Coordinate, range.Get(i));
            }

            // Act & Assert
            for (var round = 0; round < LookupRounds; round++)
            {
                for (var x = -2; x < Width + 2; x++)
                {
                    for (var y = -2; y < Height + 2; y++)
                    {
                        var coordinate = new Coordinate(x, y);
                        var isExpected = expected.TryGetValue(coordinate, out var expectedCell);

                        range.TryGetCell(coordinate, out var cell).Should().Be(isExpected, $"{coordinate}");
                        range.Contains(coordinate).Should().Be(isExpected);
                        range.TryGetCost(coordinate, out var cost).Should().Be(isExpected);

                        if (isExpected)
                        {
                            cell.Should().Be(expectedCell);
                            cost.Should().Be(expectedCell.Cost);
                        }
                    }
                }
            }
        }

        [Test]
        public void TryGetCell_OnCreatedResult_FindsItsCells()
        {
            // Arrange
            var cells = new[]
            {
                new ReachableCell(new Coordinate(-5, 3), 1f),
                new ReachableCell(new Coordinate(1000, -7), 2f),
                new ReachableCell(new Coordinate(4, 4), 3f)
            };

            // Not disposed: the array is not from the pool.
            var range = RangeResult.Create(cells, 2);

            // Act & Assert
            range.TryGetCost(new Coordinate(1000, -7), out var cost).Should().BeTrue();
            cost.Should().Be(2f);
            range.Contains(new Coordinate(-5, 3)).Should().BeTrue();
            range.Contains(new Coordinate(4, 4)).Should().BeFalse();
        }

        [Test]
        public void Lookups_AfterLaterQueriesAndPathfinderDispose_StillAnswer()
        {
            // Arrange
            var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));
            using var range = pathfinder.GetReachable(new Coordinate(2, 2), 2f);

            using (pathfinder.GetReachable(new Coordinate(15, 10), 3f))
            {
            }

            pathfinder.Dispose();

            // Act
            var isOriginReached = range.TryGetCost(new Coordinate(2, 2), out var originCost);
            var isFarCellReached = range.Contains(new Coordinate(15, 10));
            var isNeighborReached = range.TryGetCost(new Coordinate(3, 2), out var neighborCost);

            // Assert
            isOriginReached.Should().BeTrue();
            originCost.Should().Be(0f);
            isFarCellReached.Should().BeFalse();
            isNeighborReached.Should().BeTrue();
            neighborCost.Should().BeApproximately(1f, 1e-5f);
        }

        [Test]
        public void CopyCostsTo_WritesCostsAtLayoutIndexAndUnreachedElsewhere([ValueSource(nameof(Seeds))] int seed)
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed, wallChance: 0.2));
            var origin = new Coordinate(Width / 2, Height / 2);
            pathfinder.SetWalkable(origin, true);
            using var range = pathfinder.GetReachable(origin, 5f);
            var costs = new float[Width * Height + 3];
            const float unreached = -7f;

            // Act
            var written = range.CopyCostsTo(costs, unreached);

            // Assert
            written.Should().Be(range.Length);

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    var coordinate = new Coordinate(x, y);
                    var expected = range.TryGetCost(coordinate, out var cost) ? cost : unreached;
                    costs[pathfinder.GetIndex(coordinate)].Should().Be(expected);
                }
            }

            costs[Width * Height].Should().Be(unreached);
            costs[Width * Height + 2].Should().Be(unreached);
        }

        [Test]
        public void CopyCostsTo_DefaultUnreached_IsMinusOne()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));
            using var range = pathfinder.GetReachable(new Coordinate(0, 0), 0f);
            var costs = new float[Width * Height];

            // Act
            range.CopyCostsTo(costs);

            // Assert
            costs[pathfinder.GetIndex(new Coordinate(0, 0))].Should().Be(0f);
            costs[pathfinder.GetIndex(new Coordinate(1, 0))].Should().Be(-1f);
        }

        [Test]
        public void CopyCostsTo_SpanShorterThanGrid_Throws()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));
            using var range = pathfinder.GetReachable(new Coordinate(0, 0), 1f);
            var costs = new float[Width * Height - 1];

            // Act
            var copy = () => range.CopyCostsTo(costs);

            // Assert
            copy.Should().Throw<ArgumentException>();
        }

        [Test]
        public void CopyCostsTo_EmptyResult_FillsUnreached()
        {
            // Arrange
            using var pathfinder = new Pathfinder(CreateGrid(seed: 0, wallChance: 0));
            using var range = pathfinder.GetReachable(new Coordinate(0, 0), -1f);
            var costs = new float[4];

            // Act
            var written = range.CopyCostsTo(costs, 9f);

            // Assert
            written.Should().Be(0);
            costs.Should().AllBeEquivalentTo(9f);
        }

        [Test]
        public void CopyCostsTo_CreatedResultWithCells_Throws()
        {
            // Arrange
            // Not disposed: the array is not from the pool.
            var range = RangeResult.Create(new[] { new ReachableCell(new Coordinate(0, 0), 0f) }, 1);
            var costs = new float[16];

            // Act
            var copy = () => range.CopyCostsTo(costs);

            // Assert
            copy.Should().Throw<InvalidOperationException>();
        }

        private static float SumBoth(PathResult path, RangeResult range)
        {
            var sum = 0f;

            foreach (var coordinate in path)
            {
                sum += coordinate.X;
            }

            foreach (var cell in range)
            {
                sum += cell.Cost;
            }

            return sum;
        }

        private static Cell[,] CreateGrid(int seed, double wallChance)
        {
            var random = new Random(seed);
            var cells = new Cell[Width, Height];

            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    cells[x, y] = new Cell
                    {
                        Coordinate = new Coordinate(x, y),
                        IsWalkable = random.NextDouble() >= wallChance,
                        Weight = wallChance > 0 ? random.Next(3) : 0
                    };
                }
            }

            return cells;
        }
    }
}
