using System;
using System.Collections.Generic;
using System.Linq;
using Migs.MPath.Core.Data;
using Unity.AI.Navigation;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace Migs.MPath.Editor
{
    /// <summary>
    /// Compile-time check: <see cref="PathCell"/> is not ambiguous (CS0104) next to the namespaces a Unity caller
    /// commonly imports. If this project compiles, the name is free there. Never called.
    /// </summary>
    internal static class PathCellNameClashCheck
    {
        internal static PathCell[,] CreateGrid(int width, int height)
        {
            var cells = new PathCell[width, height];
            var row = Enumerable.Repeat(new PathCell { IsWalkable = true }, width).ToList();
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    cells[x, y] = row[x];
                }
            }

            return cells;
        }
    }
}
