using System;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Interfaces
{
    /// <summary>
    /// Interface for path caching functionality that allows storing and retrieving calculated paths
    /// based on start, destination, and agent properties.
    /// </summary>
    public interface IPathCaching : IDisposable
    {
        /// <summary>
        /// Tries to get a cached path result for the specified parameters.
        /// </summary>
        /// <param name="agent">The agent for which the path was calculated.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="pathResult">The cached path result, if found.</param>
        /// <returns>True if a cached path was found, otherwise false.</returns>
        bool TryGetCachedPath(IAgent agent, Coordinate from, Coordinate to, out PathResult pathResult);
        
        /// <summary>
        /// Caches a path result for the specified parameters.
        /// </summary>
        /// <param name="agent">The agent for which the path was calculated.</param>
        /// <param name="from">The starting coordinate.</param>
        /// <param name="to">The destination coordinate.</param>
        /// <param name="pathResult">The path result to cache.</param>
        void CachePath(IAgent agent, Coordinate from, Coordinate to, PathResult pathResult);
        
        /// <summary>
        /// Clears all cached paths.
        /// </summary>
        void ClearCache();
    }
} 