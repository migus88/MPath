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
        /// Simple smoothing that keeps only the first step, every turn point and the destination.
        /// Faster but less optimal than string pulling.
        /// </summary>
        Simple = 1,
        
        /// <summary>
        /// String pulling algorithm that removes unnecessary waypoints by taking straight shortcuts
        /// the agent can walk: every traced cell must fit the agent's footprint and every diagonal
        /// step must obey the corner rule. Creates more direct paths but is more computationally intensive.
        /// </summary>
        StringPulling = 2
    }
} 