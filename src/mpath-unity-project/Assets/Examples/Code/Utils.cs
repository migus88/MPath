namespace Migs.Mpath.Examples
{
    public static class Utils
    {
        public static int FieldCellComparison(FieldCell a, FieldCell b)
        {
            // Column-major (X, then Y): the default CellLayout, so a holder's index is its position.
            var result = a.Position.x.CompareTo(b.Position.x);
            return result == 0 ? a.Position.y.CompareTo(b.Position.y) : result;
        }
    }
}