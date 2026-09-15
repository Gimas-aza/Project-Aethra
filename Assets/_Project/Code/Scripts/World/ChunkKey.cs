using System;
using Unity.Mathematics;

namespace Project.World
{
    public readonly struct ChunkKey : IEquatable<ChunkKey>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public ChunkKey(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static ChunkKey FromWorldVoxel(int voxelX, int voxelY, int voxelZ)
        {
            int size = WorldMetrics.ChunkSize;
            return new ChunkKey(
                FloorDiv(voxelX, size),
                FloorDiv(voxelY, size),
                FloorDiv(voxelZ, size));
        }

        public int3 OriginVoxel => new int3(
            X * WorldMetrics.ChunkSize,
            Y * WorldMetrics.ChunkSize,
            Z * WorldMetrics.ChunkSize);

        public bool Equals(ChunkKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public override string ToString()
        {
            return $"({X},{Y},{Z})";
        }

        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            int r = value % divisor;
            if (r < 0)
            {
                q--;
            }

            return q;
        }
    }
}
