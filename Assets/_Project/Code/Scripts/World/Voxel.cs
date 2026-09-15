using System;

namespace Project.World
{
    public struct Voxel : IEquatable<Voxel>
    {
        public byte MaterialId;
        public byte Damage;

        public static Voxel Air => default;

        public bool IsAir => MaterialId == VoxelIds.Air;

        public bool Equals(Voxel other)
        {
            return MaterialId == other.MaterialId && Damage == other.Damage;
        }

        public override bool Equals(object obj)
        {
            return obj is Voxel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return MaterialId | (Damage << 8);
        }
    }
}
