using System;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Interfaces
{
    /// <summary>
    /// Interface for path caching functionality that allows storing and retrieving calculated paths
    /// based on the agent size, the start and the destination.
    /// </summary>
    /// <remarks>
    /// Ownership: the <see cref="PathResult"/> passed to <see cref="CachePath"/> belongs to the cache, which disposes
    /// it when it evicts or clears the entry. The instance returned by <see cref="TryGetCachedPath"/> stays owned by
    /// the cache: the <see cref="Pathfinder"/> copies it before handing a result to its caller, so callers can
    /// dispose every result they receive without affecting the cache.
    /// <para>
    /// Entries are keyed by <c>(agentSize, from, to)</c>: a size-1 and a size-2 query of the same pair are different
    /// entries. Only queries that run under the pathfinder's own settings reach the cache; a query whose
    /// <see cref="QueryOptions"/> change the rules neither reads nor fills it.
    /// </para>
    /// </remarks>
    public interface IPathCaching : IDisposable
    {
        /// <summary>
        /// Tries to get a cached path result for the specified parameters.
        /// </summary>
        /// <param name="agentSize">The square size, in cells, of the agent the path was calculated for.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="pathResult">The cached path result, if found. It remains owned by the cache.</param>
        /// <returns>True if a cached path was found, otherwise false.</returns>
        bool TryGetCachedPath(int agentSize, Coordinate from, Coordinate to, out PathResult pathResult);
        
        /// <summary>
        /// Caches a path result for the specified parameters.
        /// </summary>
        /// <param name="agentSize">The square size, in cells, of the agent the path was calculated for.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="pathResult">The path result to cache. The cache takes ownership and disposes it on eviction.</param>
        void CachePath(int agentSize, Coordinate from, Coordinate to, PathResult pathResult);
        
        /// <summary>
        /// Clears all cached paths.
        /// </summary>
        void ClearCache();
    }
} 