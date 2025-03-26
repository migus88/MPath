namespace Migs.Mpath.Examples.Examples
{
    public static class Utils
    {
        public static int FieldCellComparison(FieldCell a, FieldCell b)
        {
            var result = a.CellData.Coordinate.Y.CompareTo(b.CellData.Coordinate.Y);
            return result == 0 ? a.CellData.Coordinate.X.CompareTo(b.CellData.Coordinate.X) : result;
        }
    }
}