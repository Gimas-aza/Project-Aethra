using Unity.Mathematics;
using UnityEngine;

namespace Project.World.Editing
{
    /// <summary>
    /// Amanatides-Woo traversal of the voxel grid. Used instead of a physics raycast so aiming lines
    /// up with voxels rather than with the smoothed Surface Nets surface.
    /// </summary>
    public static class VoxelRaycaster
    {
        private const int MaxSteps = 512;

        /// <summary>
        /// Finds the first non-liquid solid voxel along the ray.
        /// <paramref name="placement"/> is the empty voxel just before it.
        /// </summary>
        public static bool Raycast(
            VoxelEditService edit,
            VoxelMaterialCatalog catalog,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            out int3 hit,
            out int3 placement)
        {
            hit = default;
            placement = default;

            Vector3 dir = direction.normalized;
            if (dir.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            int x = Mathf.FloorToInt(origin.x);
            int y = Mathf.FloorToInt(origin.y);
            int z = Mathf.FloorToInt(origin.z);

            int stepX = dir.x > 0f ? 1 : dir.x < 0f ? -1 : 0;
            int stepY = dir.y > 0f ? 1 : dir.y < 0f ? -1 : 0;
            int stepZ = dir.z > 0f ? 1 : dir.z < 0f ? -1 : 0;

            float tMaxX = BoundaryDistance(origin.x, dir.x, x);
            float tMaxY = BoundaryDistance(origin.y, dir.y, y);
            float tMaxZ = BoundaryDistance(origin.z, dir.z, z);

            float tDeltaX = dir.x != 0f ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity;
            float tDeltaY = dir.y != 0f ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity;
            float tDeltaZ = dir.z != 0f ? Mathf.Abs(1f / dir.z) : float.PositiveInfinity;

            var previous = new int3(x, y, z);
            float travelled = 0f;

            for (int step = 0; step < MaxSteps && travelled <= maxDistance; step++)
            {
                var current = new int3(x, y, z);

                if (edit.TryGet(current, out Voxel voxel) &&
                    !voxel.IsAir &&
                    (catalog == null || !catalog.IsLiquid(voxel.MaterialId)))
                {
                    hit = current;
                    placement = previous;
                    return true;
                }

                previous = current;

                if (tMaxX < tMaxY && tMaxX < tMaxZ)
                {
                    travelled = tMaxX;
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else if (tMaxY < tMaxZ)
                {
                    travelled = tMaxY;
                    y += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    travelled = tMaxZ;
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }

            return false;
        }

        private static float BoundaryDistance(float origin, float direction, int cell)
        {
            if (direction > 0f)
            {
                return (cell + 1 - origin) / direction;
            }

            if (direction < 0f)
            {
                return (cell - origin) / direction;
            }

            return float.PositiveInfinity;
        }
    }
}
