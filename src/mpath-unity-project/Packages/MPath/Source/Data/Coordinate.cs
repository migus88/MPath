using System;
using System.Runtime.CompilerServices;

namespace Migs.MPath.Core.Data
{
    public struct Coordinate : IEquatable<Coordinate>
    {
        private const int HashMultiplier = 397;
        
        public static readonly Coordinate Zero = new Coordinate(0, 0);
        
        public int X { get; private set; }
        public int Y { get; private set; }
        public bool IsInitialized { get; private set; }

        public Coordinate(int x, int y)
        {
            X = x;
            Y = y;
            IsInitialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            X = 0;
            Y = 0;
            IsInitialized = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Coordinate left, Coordinate right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Coordinate left, Coordinate right)
        {
            return !(left == right);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Coordinate operator +(Coordinate left, Coordinate right)
        {
            return new Coordinate(left.X + right.X, left.Y + right.Y);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Coordinate operator -(Coordinate left, Coordinate right)
        {
            return new Coordinate(left.X - right.X, left.Y - right.Y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Coordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj)
        {
            return obj is Coordinate other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * HashMultiplier) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{X}:{Y}";
        }

        public static implicit operator (int x, int y)(Coordinate coordinate) => (coordinate.X, coordinate.Y);
        public static explicit operator Coordinate((int x, int y) tuple) => new Coordinate(tuple.x, tuple.y);
    }
}