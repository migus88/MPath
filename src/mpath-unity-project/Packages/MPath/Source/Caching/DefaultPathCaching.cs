using System;
using System.Collections.Generic;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core.Caching
{
    /// <summary>
    /// Default implementation of path caching that stores calculated paths in memory.
    /// </summary>
    public class DefaultPathCaching : IPathCaching
    {
        private readonly Dictionary<CacheKey, PathResult> _cache = new();
        private bool _isDisposed;

        /// <summary>
        /// Tries to get a cached path result for the specified parameters.
        /// </summary>
        /// <param name="agent">The agent for which the path was calculated.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="pathResult">The cached path result, if found.</param>
        /// <returns>True if a cached path was found, otherwise false.</returns>
        public bool TryGetCachedPath(IAgent agent, Coordinate from, Coordinate to, out PathResult pathResult)
        {
            ThrowIfDisposed();
            
            var key = new CacheKey(agent.Size, from, to);
            return _cache.TryGetValue(key, out pathResult);
        }

        /// <summary>
        /// Caches a path result for the specified parameters.
        /// </summary>
        /// <param name="agent">The agent for which the path was calculated.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="pathResult">The path result to cache.</param>
        public void CachePath(IAgent agent, Coordinate from, Coordinate to, PathResult pathResult)
        {
            ThrowIfDisposed();
            
            // Don't cache failed paths
            if (!pathResult.IsSuccess)
            {
                return;
            }
                
            var key = new CacheKey(agent.Size, from, to);
            
            // If we already have a path for this key, remove it first
            if (_cache.TryGetValue(key, out var existingPath))
            {
                existingPath.Dispose();
                _cache.Remove(key);
            }
            
            _cache[key] = pathResult;
        }

        /// <summary>
        /// Clears all cached paths.
        /// </summary>
        public void ClearCache()
        {
            ThrowIfDisposed();
            
            foreach (var cachedPath in _cache.Values)
            {
                cachedPath.Dispose();
            }
            
            _cache.Clear();
        }

        /// <summary>
        /// Throws an ObjectDisposedException if this instance has been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(DefaultPathCaching));
            }
        }

        /// <summary>
        /// Disposes the DefaultPathCaching instance and releases all cached paths.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
                
            ClearCache();
            _isDisposed = true;
        }

        /// <summary>
        /// Struct to use as a key for the path cache.
        /// </summary>
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
                       _from == other._from &&
                       _to == other._to;
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