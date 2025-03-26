using Migs.MPath.Core.Interfaces;
using UnityEngine;

namespace Code.Settings
{
    [CreateAssetMenu(fileName = "PathfinderSettings", menuName = "Atomic/Pathfinder/Settings", order = 0)]
    public class ScriptablePathfinderSettings : ScriptableObject, IPathfinderSettings
    {
        /// <inheritdoc/>
        [field: SerializeField] public bool IsDiagonalMovementEnabled { get; set; } = true;
        /// <inheritdoc/>
        [field: SerializeField] public bool IsCalculatingOccupiedCells { get; set; } = true;
        /// <inheritdoc/>
        [field: SerializeField] public bool IsMovementBetweenCornersEnabled { get; set; } = false;
        /// <inheritdoc/>
        [field: SerializeField] public bool IsCellWeightEnabled { get; set; } = true;
        /// <inheritdoc/>
        [field: SerializeField] public float StraightMovementMultiplier { get; set; } = 1f;
        /// <inheritdoc/>
        [field: SerializeField] public float DiagonalMovementMultiplier { get; set; } = 1.41f;
        /// <inheritdoc cref="InitialBufferSize"/>
        [field: SerializeField] public int InitialBufferSizeSerialized { get; set; } = -1;

        /// <inheritdoc/>
        public int? InitialBufferSize
        {
            get => InitialBufferSizeSerialized == -1 ? null : InitialBufferSizeSerialized;
            set => InitialBufferSizeSerialized = value ?? -1;
        }
    }
}