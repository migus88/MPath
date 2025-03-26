using System.Runtime.CompilerServices;

namespace Migs.MPath.Core.Data;

public struct CellPathfindingData
{  
    internal bool IsClosed { get; set; }
    internal float ScoreF { get; set; }
    internal float ScoreH { get; set; }
    internal float ScoreG { get; set; }
    internal int Depth { get; set; }
    internal Coordinate ParentCoordinate { get; set; }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Reset()
    {
        IsClosed = false;
        ScoreF = 0;
        ScoreH = 0;
        ScoreG = 0;
        Depth = 0;
        ParentCoordinate.Reset();
    }
}