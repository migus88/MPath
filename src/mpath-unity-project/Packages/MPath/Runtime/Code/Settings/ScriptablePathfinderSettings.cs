using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using UnityEngine;

namespace Migs.MPath.Settings
{
    [CreateAssetMenu(fileName = "PathfinderSettings", menuName = "MPath/Pathfinder Settings", order = 0)]
    public class ScriptablePathfinderSettings : ScriptableObject, IPathfinderSettings
    {
        /// <inheritdoc/>
        [field: SerializeField] public bool IsDiagonalMovementEnabled { get; set; } = true;
        /// <inheritdoc/>
        [field: SerializeField] public bool IsCalculatingOccupiedCells { get; set; } = true;
        /// <inheritdoc/>
        [field: SerializeField] public CornerBlocking CornerBlocking { get; set; } = CornerBlocking.BetweenBlockedCells;
        /// <inheritdoc/>
        [field: SerializeField] public bool IsCellWeightEnabled { get; set; } = true;
        /// <inheritdoc/>
        [field: SerializeField] public int StraightStepCost { get; set; } = 100;
        /// <inheritdoc/>
        [field: SerializeField] public int DiagonalStepCost { get; set; } = 141;
        /// <inheritdoc/>
        [field: SerializeField] public PathSmoothingMethod PathSmoothingMethod { get; set; } = PathSmoothingMethod.None;
        /// <inheritdoc/>
        [field: SerializeField] public GridSnapshotMode GridSnapshotMode { get; set; } = GridSnapshotMode.PerQuery;
        /// <inheritdoc/>
        [field: SerializeField] public TieBreaking TieBreaking { get; set; } = TieBreaking.None;
        /// <inheritdoc/>
        [field: SerializeField] public CellLayout CellLayout { get; set; } = CellLayout.Auto;
    }
}