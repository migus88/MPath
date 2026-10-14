using System;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Interfaces
{
    /// <summary>
    /// Interface for path caching functionality that allows storing and retrieving calculated paths
    /// based on start, destination, and agent properties.
    /// </summary>
    /// <remarks>
    /// Ownership: the <see cref="PathResult"/> passed to <see cref="CachePath"/> belongs to the cache, which disposes
    /// it when it evicts or clears the entry. The instance returned by <see cref="TryGetCachedPath"/> stays owned by
    /// the cache: the <see cref="Pathfinder"/> copies it before handing a result to its caller, so callers can
    /// dispose every result they receive without affecting the cache.
    /// <para>
    /// Queries made with the <c>agentSize</c> overloads (for example
    /// <see cref="Pathfinder.GetPath(Coordinate, Coordinate, int)"/>) pass an internal <see cref="IAgent"/> whose
    /// <see cref="IAgent.Size"/> is the query's size; it equals any other such agent of the same size. Key the cache
    /// on <see cref="IAgent.Size"/> (as <see cref="Caching.DefaultPathCaching"/> does) so those queries share entries
    /// with <see cref="IAgent"/> queries of the same size.
    /// </para>
    /// </remarks>
    public interface IPathCaching : IDisposable
    {
        /// <summary>
        /// Tries to get a cached path result for the specified parameters.
        /// </summary>
        /// <param name="agent">The agent for which the path was calculated.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="pathResult">The cached path result, if found. It remains owned by the cache.</param>
        /// <returns>True if a cached path was found, otherwise false.</returns>
        bool TryGetCachedPath(IAgent agent, Coordinate from, Coordinate to, out PathResult pathResult);
        
        /// <summary>
        /// Caches a path result for the specified parameters.
        /// </summary>
        /// <param name="agent">The agent for which the path was calculated.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="pathResult">The path result to cache. The cache takes ownership and disposes it on eviction.</param>
        void CachePath(IAgent agent, Coordinate from, Coordinate to, PathResult pathResult);
        
        /// <summary>
        /// Clears all cached paths.
        /// </summary>
        void ClearCache();
    }
} 