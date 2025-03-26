using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Interfaces
{
    /// <summary>
    /// Represents an object that contains cell data used in the pathfinding system.
    /// Typically implemented by game objects or entities that need to expose cell information
    /// to the pathfinding system without directly managing the cell data themselves.
    /// </summary>
    public interface ICellHolder
    {
        /// <summary>
        /// Gets the cell data associated with this holder.
        /// This data contains information such as walkability, weight, and coordinates.
        /// </summary>
        Cell CellData { get; }
    }
}