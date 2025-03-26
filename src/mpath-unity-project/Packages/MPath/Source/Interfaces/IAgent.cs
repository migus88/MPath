using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Interfaces
{
    /// <summary>
    /// Represents an entity that can move through the pathfinding grid.
    /// This interface is used by the pathfinding system to determine agent properties
    /// such as size constraints for valid paths.
    /// </summary>
    public interface IAgent
    {
        /// <summary>
        /// Gets the square size of the agent, measured in occupied cells.
        /// For example, a size of 2 means that the agent occupies a 2x2 square of cells.
        /// A size of 1 represents a single-cell agent.
        /// </summary>
        int Size { get; }
    }
}
