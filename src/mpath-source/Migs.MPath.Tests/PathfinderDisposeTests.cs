using System;
using System.Buffers;
using System.Collections.Generic;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Tests.Implementations;
using NSubstitute;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// Pins the <see cref="Pathfinder.Dispose"/> contract: it is idempotent (the pooled cell buffer goes back to the
    /// pool exactly once), every instance method throws <see cref="ObjectDisposedException"/> afterwards, and an
    /// active stepwise search is ended before its pinned buffer is returned.
    /// </summary>
    [TestFixture]
    public class PathfinderDisposeTests
    {
        private const int GridSize = 8;

        private static readonly Coordinate Origin = new Coordinate(0, 0);
        private static readonly Coordinate Destination = new Coordinate(5, 5);

        public static IEnumerable<TestCaseData> InstanceMethods()
        {
            var agent = new Agent { Size = 1 };

            yield return new TestCaseData((Action<Pathfinder>)(p => p.GetPath(agent, Origin, Destination)))
                .SetName("GetPath_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.GetReachable(agent, Origin, 5f)))
                .SetName("GetReachable_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.HasLineOfSight(Origin, Destination)))
                .SetName("HasLineOfSight_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.BeginStepwiseSearch(agent, Origin, Destination)))
                .SetName("BeginStepwiseSearch_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.EnablePathCaching()))
                .SetName("EnablePathCaching_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.DisablePathCaching()))
                .SetName("DisablePathCaching_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.InvalidateCache()))
                .SetName("InvalidateCache_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.GetIndex(Origin)))
                .SetName("GetIndex_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.GetCell(Origin)))
                .SetName("GetCell_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.SetCell(Origin, new Cell())))
                .SetName("SetCell_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.SetWalkable(Origin, false)))
                .SetName("SetWalkable_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.SetOccupied(Origin, true)))
                .SetName("SetOccupied_AfterDispose_ThrowsObjectDisposedException");
            yield return new TestCaseData((Action<Pathfinder>)(p => p.SetWeight(Origin, 1f)))
                .SetName("SetWeight_AfterDispose_ThrowsObjectDisposedException");
        }

        [Test]
        public void Dispose_CalledTwice_ReturnsThePooledBufferOnlyOnce()
        {
            var pathfinder = new Pathfinder(CreateMatrix());

            pathfinder.Dispose();
            pathfinder.Dispose();

            var first = ArrayPool<Cell>.Shared.Rent(GridSize * GridSize);
            var second = ArrayPool<Cell>.Shared.Rent(GridSize * GridSize);

            try
            {
                second.Should().NotBeSameAs(first);
            }
            finally
            {
                ArrayPool<Cell>.Shared.Return(first);
                ArrayPool<Cell>.Shared.Return(second);
            }
        }

        [Test]
        public void Dispose_CalledTwice_DisposesThePathCacheOnce()
        {
            var cache = Substitute.For<IPathCaching>();
            var pathfinder = new Pathfinder(CreateMatrix()).EnablePathCaching(cache);

            pathfinder.Dispose();
            pathfinder.Dispose();

            cache.Received(1).Dispose();
        }

        [TestCaseSource(nameof(InstanceMethods))]
        public void InstanceMethod_AfterDispose_ThrowsObjectDisposedException(Action<Pathfinder> call)
        {
            var pathfinder = new Pathfinder(CreateMatrix());
            pathfinder.Dispose();

            var action = () => call(pathfinder);

            action.Should().Throw<ObjectDisposedException>();
        }

        [Test]
        public void GetPath_AfterDisposeOfNonPooledArrayMode_ThrowsObjectDisposedException()
        {
            var cells = new Cell[GridSize * GridSize];
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = new Cell { Coordinate = new Coordinate(i / GridSize, i % GridSize), IsWalkable = true };
            }

            var pathfinder = new Pathfinder(cells, GridSize, GridSize);
            pathfinder.Dispose();

            var action = () => pathfinder.GetPath(new Agent { Size = 1 }, Origin, Destination);

            action.Should().Throw<ObjectDisposedException>();
        }

        [Test]
        public void Dispose_WithActiveStepwiseSearch_EndsTheSearch()
        {
            var pathfinder = new Pathfinder(CreateMatrix());
            var search = pathfinder.BeginStepwiseSearch(new Agent { Size = 1 }, Origin, Destination);
            search.Tick();

            pathfinder.Dispose();

            var tick = () => search.Tick();
            tick.Should().Throw<ObjectDisposedException>();

            var disposeSearch = () => search.Dispose();
            disposeSearch.Should().NotThrow();
        }

        private static Cell[,] CreateMatrix()
        {
            var matrix = new Cell[GridSize, GridSize];
            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                {
                    matrix[x, y] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
                }
            }

            return matrix;
        }
    }
}
