namespace Migs.MPath.Core.Internal
{
    /// <summary>
    /// What stops an agent from entering a position. The values are row/column indexes into the 3x3
    /// corner-blocking table built by <see cref="FastPathfinderSettings"/>.
    /// </summary>
    internal enum CellBlocker
    {
        Open = 0,
        Wall = 1,
        Occupied = 2
    }
}
