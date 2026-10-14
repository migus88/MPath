using System;
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
        [field: SerializeField] public bool IsMovementBetweenCornersEnabled { get; set; } = false;
        /// <summary>
        /// When enabled, <see cref="CornerBlockingOverride"/> is used as <see cref="CornerBlocking"/> and
        /// <see cref="IsMovementBetweenCornersEnabled"/> is ignored.
        /// </summary>
        [field: SerializeField] public bool IsCornerBlockingOverridden { get; set; } = false;
        /// <summary>
        /// The corner-cutting rule used while <see cref="IsCornerBlockingOverridden"/> is enabled.
        /// </summary>
        [field: SerializeField] public CornerBlocking CornerBlockingOverride { get; set; } = CornerBlocking.BetweenBlockedCells;
        /// <inheritdoc/>
        [field: SerializeField] public bool IsCellWeightEnabled { get; set; } = true;
        /// <inheritdoc/>
        [field: SerializeField] public float StraightMovementMultiplier { get; set; } = 1f;
        /// <inheritdoc/>
        [field: SerializeField] public float DiagonalMovementMultiplier { get; set; } = 1.41f;
        /// <inheritdoc/>
        [field: SerializeField] public PathSmoothingMethod PathSmoothingMethod { get; set; } = PathSmoothingMethod.None;
        /// <inheritdoc/>
        [field: SerializeField] public GridSnapshotMode GridSnapshotMode { get; set; } = GridSnapshotMode.PerQuery;
        [field: SerializeField] public TieBreaking TieBreaking { get; set; } = TieBreaking.None;
        /// <inheritdoc cref="InitialBufferSize"/>
        [Obsolete("Ignored: the open set is sized to the grid when the pathfinder is created and never grows. This member will be removed in the next major version.")]
        [field: SerializeField] public int InitialBufferSizeSerialized { get; set; } = -1;

        /// <inheritdoc/>
        /// <remarks>
        /// <see cref="CornerBlockingOverride"/> while <see cref="IsCornerBlockingOverridden"/> is enabled; otherwise
        /// derived from <see cref="IsMovementBetweenCornersEnabled"/>. Setting it enables the override.
        /// </remarks>
        public CornerBlocking CornerBlocking
        {
            get
            {
                if (IsCornerBlockingOverridden)
                {
                    return CornerBlockingOverride;
                }

                return IsMovementBetweenCornersEnabled ? CornerBlocking.None : CornerBlocking.BetweenBlockedCells;
            }
            set
            {
                IsCornerBlockingOverridden = true;
                CornerBlockingOverride = value;
            }
        }

        /// <inheritdoc/>
        [Obsolete("Ignored: the open set is sized to the grid when the pathfinder is created and never grows. This member will be removed in the next major version.")]
        public int? InitialBufferSize
        {
            get => InitialBufferSizeSerialized == -1 ? null : InitialBufferSizeSerialized;
            set => InitialBufferSizeSerialized = value ?? -1;
        }
    }
}