namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Where a pathfinder built over a <see cref="Cell"/>[] array finds the cell at (X, Y). Set through
    /// <see cref="Interfaces.IPathfinderSettings.CellLayout"/>. Only the first <c>fieldWidth * fieldHeight</c> elements
    /// of the array are the grid. Queries never reorder or otherwise write the caller's array.
    /// </summary>
    /// <remarks>
    /// Has no effect on the <see cref="Cell"/>[,] and <see cref="Interfaces.ICellHolder"/> constructors: they always
    /// copy the grid into a buffer of their own (see <see cref="GridSnapshotMode"/>).
    /// </remarks>
    public enum CellLayout
    {
        /// <summary>
        /// Detect the layout at the first query (and again at the first query after <see cref="Pathfinder.Refresh"/>):
        /// <see cref="ColumnMajor"/> or <see cref="RowMajor"/> when every cell sits at its index under that layout,
        /// otherwise <see cref="Unordered"/>. One O(grid size) pass per detection. The default: arrays in any order
        /// keep working as in versions before this setting existed, without being sorted.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// The cell at (X, Y) is at index <c>X * fieldHeight + Y</c>. The array is searched in place, so a query
        /// costs only the cells it searches and direct edits to the array are seen by the next query. Checked once,
        /// at the first query (and after <see cref="Pathfinder.Refresh"/>).
        /// </summary>
        ColumnMajor = 1,

        /// <summary>
        /// The cell at (X, Y) is at index <c>Y * fieldWidth + X</c>, the usual <c>y * width + x</c> order. Searched
        /// in place like <see cref="ColumnMajor"/>.
        /// </summary>
        RowMajor = 2,

        /// <summary>
        /// The cells are in any order and each one's <see cref="Cell.Coordinate"/> says where it is. The pathfinder
        /// copies the array into a pooled buffer of its own, like a <see cref="Cell"/>[,] matrix: before every query
        /// with <see cref="GridSnapshotMode.PerQuery"/>, or once and after <see cref="Pathfinder.Refresh"/> with
        /// <see cref="GridSnapshotMode.Manual"/>. A grid cell the array does not hold is a wall.
        /// </summary>
        Unordered = 3
    }
}
