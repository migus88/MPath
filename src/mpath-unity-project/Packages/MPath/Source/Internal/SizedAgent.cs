using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Core.Internal
{
    /// <summary>
    /// The <see cref="IAgent"/> handed to an <see cref="IPathCaching"/> for a query made with an <c>agentSize</c>
    /// overload. Instances for common sizes are shared, so the cache sees the same instance for the same size, and
    /// two instances are equal when their sizes are.
    /// </summary>
    internal sealed class SizedAgent : IAgent
    {
        private const int SharedSizes = 64;

        private static readonly SizedAgent[] Shared = new SizedAgent[SharedSizes];

        public int Size { get; }

        private SizedAgent(int size)
        {
            Size = size;
        }

        /// <summary>
        /// Returns an agent of <paramref name="size"/>, reusing the shared instance when there is one.
        /// <paramref name="size"/> must be at least 1.
        /// </summary>
        public static IAgent Of(int size)
        {
            if (size > SharedSizes)
            {
                return new SizedAgent(size);
            }

            return Shared[size - 1] ??= new SizedAgent(size);
        }

        public override bool Equals(object obj)
        {
            return obj is SizedAgent other && other.Size == Size;
        }

        public override int GetHashCode()
        {
            return Size;
        }
    }
}
