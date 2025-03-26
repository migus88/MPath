using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Interfaces
{
    public interface IAgent
    {
        /// <summary>
        /// The square size of the agent, measured in occupied cells <br/>
        /// For example, a size of 2 means that the agent occupies a 2x2 square of cells
        /// </summary>
        int Size { get; }
    }
}
