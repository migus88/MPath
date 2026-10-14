using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Migs.MPath.Core.Data;
using NUnit.Framework;

namespace Migs.MPath.Tests
{
    /// <summary>
    /// Compile-time check: <see cref="PathCell"/> is not ambiguous (CS0104) next to the namespaces a .NET caller
    /// commonly imports. If this file compiles, the name is free there.
    /// </summary>
    [TestFixture]
    public class PathCellNameClashTests
    {
        [Test]
        public void PathCell_WithCommonSystemNamespacesImported_ResolvesUnqualified()
        {
            PathCell cell = new PathCell { IsWalkable = true };
            List<PathCell> cells = Enumerable.Repeat(cell, 2).ToList();

            cells.Should().OnlyContain(c => c.IsWalkable);
        }
    }
}
