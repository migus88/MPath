using Migs.MPath.Core.Data;

namespace Migs.MPath.Core.Interfaces
{
    /// <summary>
    /// Represents an object that contains cell data used in the pathfinding system.
    /// Typically implemented by game objects or entities that need to expose cell information
    /// to the pathfinding system without directly managing the cell data themselves.
    /// </summary>
    /// <remarks>
    /// A holder's slot is its cell's position: <c>[x, y]</c> in an <see cref="ICellHolder"/>[,] matrix, the index
    /// under <see cref="IPathfinderSettings.CellLayout"/> in an <see cref="ICellHolder"/>[] array. A <c>null</c>
    /// holder is a non-walkable cell.
    /// </remarks>
    public interface ICellHolder
    {
        /// <summary>
        /// Gets the cell data associated with this holder.
        /// This data contains information such as walkability, occupancy and weight.
        /// </summary>
        Cell CellData { get; }
    }
}