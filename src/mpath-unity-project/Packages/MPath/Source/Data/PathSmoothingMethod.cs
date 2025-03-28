namespace Migs.MPath.Core.Data
{
    /// <summary>
    /// Defines different methods for path smoothing.
    /// </summary>
    [System.Serializable]
    public enum PathSmoothingMethod
    {
        /// <summary>
        /// No path smoothing is applied. The original A* path is returned.
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Simple smoothing that removes redundant waypoints based on angle threshold.
        /// Faster but less optimal than string pulling.
        /// </summary>
        Simple = 1,
        
        /// <summary>
        /// String pulling algorithm that removes unnecessary waypoints by checking
        /// direct line-of-sight between points. Creates more direct paths but is more
        /// computationally intensive.
        /// </summary>
        StringPulling = 2
    }
} 