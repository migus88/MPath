using Migs.MPath.Core.Data;
using UnityEngine;

namespace Code
{
    public static class PathfindingExtensions
    {
        public static Vector2Int ToVector2Int(this Coordinate coordinate) => new(coordinate.X, coordinate.Y);
        public static Coordinate ToCoordinate(this Vector2Int vector2Int) => new(vector2Int.x, vector2Int.y);
    }
}