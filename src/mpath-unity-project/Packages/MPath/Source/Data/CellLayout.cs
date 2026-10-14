namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Which index of a <see cref="PathCell"/>[] or <see cref="Interfaces.ICellHolder"/>[] array holds the cell at (X, Y).
    /// Set through <see cref="Interfaces.IPathfinderSettings.CellLayout"/>. A cell's slot is its position: only the
    /// first <c>fieldWidth * fieldHeight</c> elements of the array are the grid, and queries never reorder or
    /// otherwise write the caller's array.
    /// </summary>
    /// <remarks>
    /// Has no effect on the <see cref="PathCell"/>[,] and <see cref="Interfaces.ICellHolder"/>[,] constructors: the cell
    /// at (X, Y) is the element <c>[X, Y]</c>. Any other value is rejected with an
    /// <see cref="System.ArgumentOutOfRangeException"/> when the pathfinder is built.
    /// </remarks>
    public enum CellLayout
    {
        /// <summary>
        /// The cell at (X, Y) is at index <c>X * fieldHeight + Y</c>, the order of a <see cref="PathCell"/>[,] matrix in
        /// memory. The default.
        /// </summary>
        ColumnMajor = 1,

        /// <summary>
        /// The cell at (X, Y) is at index <c>Y * fieldWidth + X</c>, the usual <c>y * width + x</c> order.
        /// </summary>
        RowMajor = 2
    }
}
