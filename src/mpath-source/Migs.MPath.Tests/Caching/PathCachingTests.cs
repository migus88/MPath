using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core;
using Migs.MPath.Core.Caching;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using NSubstitute;
using NUnit.Framework;

namespace Migs.MPath.Tests.Caching
{
    [TestFixture]
    public class PathCachingTests
    {
        private const int FieldSize = 10;
        
        private Cell[] _cells;
        private Pathfinder _pathfinder;
        
        [SetUp]
        public void SetUp()
        {
            _cells = new Cell[FieldSize * FieldSize];
            
            // Initialize all cells as walkable
            for (var y = 0; y < FieldSize; y++)
            {
                for (var x = 0; x < FieldSize; x++)
                {
                    var cell = new Cell
                    {
                        Coordinate = new Coordinate(x, y),
                        IsWalkable = true
                    };
                    _cells[y * FieldSize + x] = cell;
                }
            }
            
            _pathfinder = new Pathfinder(_cells, FieldSize, FieldSize);
        }
        
        [TearDown]
        public void TearDown()
        {
            _pathfinder.Dispose();
        }
        
        [Test]
        public void EnablePathCaching_DefaultImplementation_ReturnsPathfinderInstance()
        {
            // Act
            var result = _pathfinder.EnablePathCaching();
            
            // Assert
            result.Should().BeSameAs(_pathfinder);
        }
        
        [Test]
        public void DisablePathCaching_AfterEnabling_ReturnsPathfinderInstance()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            
            // Act
            var result = _pathfinder.DisablePathCaching();
            
            // Assert
            result.Should().BeSameAs(_pathfinder);
        }
        
        [Test]
        public void GetPath_WithCachingEnabled_ReturnsCachedResult()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 5);
            
            // Act
            var firstResult = _pathfinder.GetPath(from, to);
            var secondResult = _pathfinder.GetPath(from, to);
            
            // Assert
            firstResult.IsSuccess.Should().BeTrue();
            secondResult.IsSuccess.Should().BeTrue();
            secondResult.Length.Should().Be(firstResult.Length);
            
            // Paths should be identical
            for (var i = 0; i < firstResult.Length; i++)
            {
                secondResult.Get(i).Should().Be(firstResult.Get(i));
            }
        }
        
        [Test]
        public void GetPath_WithDifferentParameters_ReturnsDifferentPaths()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from1 = new Coordinate(0, 0);
            var to1 = new Coordinate(5, 5);
            var from2 = new Coordinate(0, 0);
            var to2 = new Coordinate(8, 8);
            
            // Act
            var result1 = _pathfinder.GetPath(from1, to1);
            var result2 = _pathfinder.GetPath(from2, to2);
            
            // Assert
            result1.IsSuccess.Should().BeTrue();
            result2.IsSuccess.Should().BeTrue();
            result2.Length.Should().NotBe(result1.Length);
        }
        
        [Test]
        public void GetPath_WithDifferentAgentSize_ReturnsDifferentPaths()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from = new Coordinate(0, 0);
            var to = new Coordinate(8, 8);

            // Create obstacles that will force a larger agent to take a different path
            // Add a narrow passage that only the size 1 agent can fit through
            _cells[2 * FieldSize + 2].IsWalkable = false;
            _cells[2 * FieldSize + 4].IsWalkable = false;
            _cells[3 * FieldSize + 2].IsWalkable = false;
            _cells[3 * FieldSize + 4].IsWalkable = false;
            _cells[4 * FieldSize + 2].IsWalkable = false;
            _cells[4 * FieldSize + 4].IsWalkable = false;

            var agent2Size = 2;
            
            // Act
            var result1 = _pathfinder.GetPath(from, to);
            var result2 = _pathfinder.GetPath(from, to, agent2Size);
            
            // Assert
            result1.IsSuccess.Should().BeTrue();
            result2.IsSuccess.Should().BeTrue();
            result2.Length.Should().NotBe(result1.Length);
        }
        
        [Test]
        public void GetPath_AfterDisablingCaching_CalculatesNewPath()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 5);
            
            // First calculation with caching
            var firstResult = _pathfinder.GetPath(from, to);
            
            // Disable caching
            _pathfinder.DisablePathCaching();
            
            // Act
            var secondResult = _pathfinder.GetPath(from, to);
            
            // Assert
            firstResult.IsSuccess.Should().BeTrue();
            secondResult.IsSuccess.Should().BeTrue();
            secondResult.Length.Should().Be(firstResult.Length);
            
            // Paths should be identical in values but not the same instance
            secondResult.Should().NotBeSameAs(firstResult);
        }
        
        [Test]
        public void InvalidateCache_ShouldClearExistingCacheEntries()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 5);
            
            // Cache a path
            var firstResult = _pathfinder.GetPath(from, to);
            
            // Act
            _pathfinder.InvalidateCache();
            var secondResult = _pathfinder.GetPath(from, to);
            
            // Assert
            firstResult.IsSuccess.Should().BeTrue();
            secondResult.IsSuccess.Should().BeTrue();
            secondResult.Length.Should().Be(firstResult.Length);
            
            // Should be a different instance since cache was invalidated
            secondResult.Should().NotBeSameAs(firstResult);
        }
        
        [Test]
        public void CustomPathCaching_CorrectlyUsed()
        {
            // Arrange
            var customCaching = new TestPathCaching();
            _pathfinder.EnablePathCaching(customCaching);
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 5);
            
            // Act
            _pathfinder.GetPath(from, to);
            _pathfinder.GetPath(from, to);
            
            // Assert
            customCaching.CachePathCalls.Should().Be(1);
            customCaching.TryGetCachedPathCalls.Should().Be(2);
        }
        
        [Test]
        public void GetPath_CacheHitAfterCallerDisposedFirstResult_ReturnsValidEqualPath()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 5);
            Coordinate[] expected;

            using (var first = _pathfinder.GetPath(from, to))
            {
                expected = first.Path.ToArray();
            }

            // Reuse whatever the disposed result gave back to the pool, as unrelated code would.
            var reused = ArrayPool<Coordinate>.Shared.Rent(expected.Length);
            Array.Fill(reused, new Coordinate(-1, -1));

            // Act
            using var second = _pathfinder.GetPath(from, to);

            // Assert
            second.IsSuccess.Should().BeTrue();
            second.Path.Should().Equal(expected);
            ArrayPool<Coordinate>.Shared.Return(reused);
        }

        [Test]
        public void GetPath_TwoCacheHits_ReturnDistinctInstancesThatDisposeIndependently()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 5);
            using var computed = _pathfinder.GetPath(from, to);
            var expected = computed.Path.ToArray();

            // Act
            var firstHit = _pathfinder.GetPath(from, to);
            using var secondHit = _pathfinder.GetPath(from, to);
            firstHit.Dispose();

            // Assert
            secondHit.Should().NotBeSameAs(firstHit);
            secondHit.Should().NotBeSameAs(computed);
            secondHit.Path.Should().Equal(expected);
            computed.Path.Should().Equal(expected);
        }

        [Test]
        public void InvalidateCache_WhileCallerHoldsResults_LeavesThemValid()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 5);
            using var computed = _pathfinder.GetPath(from, to);
            using var hit = _pathfinder.GetPath(from, to);
            var expected = computed.Path.ToArray();

            // Act
            _pathfinder.InvalidateCache();

            // Assert
            computed.Path.Should().Equal(expected);
            hit.Path.Should().Equal(expected);
        }

        [Test]
        public void GetPath_WithCustomCaching_NeverHandsOutTheInstanceTheCacheKeeps()
        {
            // Arrange
            var customCaching = new TestPathCaching();
            _pathfinder.EnablePathCaching(customCaching);
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 5);

            // Act
            using var computed = _pathfinder.GetPath(from, to);
            using var hit = _pathfinder.GetPath(from, to);

            // Assert
            customCaching.LastCached.Should().NotBeNull();
            computed.Should().NotBeSameAs(customCaching.LastCached);
            hit.Should().NotBeSameAs(customCaching.LastCached);
            hit.Path.Should().Equal(customCaching.LastCached!.Path);
        }

        [Test]
        public void GetPath_AfterSetWalkableBlocksACachedPath_RecomputesAroundTheCell()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 0);
            var blocked = new Coordinate(3, 0);

            using (var cached = _pathfinder.GetPath(from, to))
            {
                cached.Path.Should().Contain(blocked);
            }

            // Act
            _pathfinder.SetWalkable(blocked, false);
            using var result = _pathfinder.GetPath(from, to);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Path.Should().NotContain(blocked);
        }

        [Test]
        public void GetPath_AfterADirectEditAndRefresh_RecomputesAroundTheCell()
        {
            // Arrange
            _pathfinder.EnablePathCaching();
            var from = new Coordinate(0, 0);
            var to = new Coordinate(5, 0);
            var blocked = new Coordinate(3, 0);

            using (var cached = _pathfinder.GetPath(from, to))
            {
                cached.Path.Should().Contain(blocked);
            }

            _cells[blocked.Y * FieldSize + blocked.X].IsWalkable = false;

            // Act
            _pathfinder.Refresh();
            using var result = _pathfinder.GetPath(from, to);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Path.Should().NotContain(blocked);
        }

        [Test]
        public void Refresh_ClearsTheCache()
        {
            // Arrange
            var cache = Substitute.For<IPathCaching>();
            _pathfinder.EnablePathCaching(cache);

            // Act
            _pathfinder.Refresh();

            // Assert
            cache.Received(1).ClearCache();
        }

        [Test]
        public void SetWalkable_WithTheCurrentValue_KeepsTheCache()
        {
            // Arrange
            var cache = Substitute.For<IPathCaching>();
            _pathfinder.EnablePathCaching(cache);

            // Act
            _pathfinder.SetWalkable(new Coordinate(3, 0), true);

            // Assert
            cache.DidNotReceive().ClearCache();
        }

        [TestCase(false, false, 0)]
        [TestCase(true, false, 1)]
        [TestCase(false, true, 1)]
        public void SetOccupiedAndSetWeight_ClearTheCacheOnlyForFieldsTheSettingsUse(bool areOccupiedCellsBlocking,
            bool isCellWeightEnabled, int expectedClears)
        {
            // Arrange
            var settings = new PathfinderSettings
            {
                AreOccupiedCellsBlocking = areOccupiedCellsBlocking,
                IsCellWeightEnabled = isCellWeightEnabled
            };
            var cache = Substitute.For<IPathCaching>();
            using var pathfinder = new Pathfinder(_cells, FieldSize, FieldSize, settings).EnablePathCaching(cache);

            // Act
            pathfinder.SetOccupied(new Coordinate(3, 0), true);
            pathfinder.SetWeight(new Coordinate(3, 0), 500);

            // Assert
            cache.Received(expectedClears).ClearCache();
        }

        private class TestPathCaching : IPathCaching
        {
            private readonly Dictionary<CacheKey, PathResult> _cache = new();
            
            public int CachePathCalls { get; private set; }
            public int TryGetCachedPathCalls { get; private set; }
            public PathResult? LastCached { get; private set; }
            
            public bool TryGetCachedPath(int agentSize, Coordinate from, Coordinate to, out PathResult pathResult)
            {
                TryGetCachedPathCalls++;
                var key = new CacheKey(agentSize, from, to);
                return _cache.TryGetValue(key, out pathResult);
            }
            
            public void CachePath(int agentSize, Coordinate from, Coordinate to, PathResult pathResult)
            {
                CachePathCalls++;
                LastCached = pathResult;
                var key = new CacheKey(agentSize, from, to);
                _cache[key] = pathResult;
            }
            
            public void ClearCache()
            {
                _cache.Clear();
            }
            
            public void Dispose()
            {
                foreach (var result in _cache.Values)
                {
                    result.Dispose();
                }
                _cache.Clear();
            }
            
            private readonly struct CacheKey : IEquatable<CacheKey>
            {
                private readonly int _agentSize;
                private readonly Coordinate _from;
                private readonly Coordinate _to;

                public CacheKey(int agentSize, Coordinate from, Coordinate to)
                {
                    _agentSize = agentSize;
                    _from = from;
                    _to = to;
                }

                public bool Equals(CacheKey other)
                {
                    return _agentSize == other._agentSize &&
                           _from.Equals(other._from) &&
                           _to.Equals(other._to);
                }

                public override bool Equals(object obj)
                {
                    return obj is CacheKey other && Equals(other);
                }

                public override int GetHashCode()
                {
                    unchecked
                    {
                        var hashCode = _agentSize;
                        hashCode = (hashCode * 397) ^ _from.GetHashCode();
                        hashCode = (hashCode * 397) ^ _to.GetHashCode();
                        return hashCode;
                    }
                }
            }
        }
    }
} 