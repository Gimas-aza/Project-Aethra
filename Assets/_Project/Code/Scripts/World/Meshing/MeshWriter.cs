using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.World.Meshing
{
    /// <summary>Uploads native job output into a <see cref="Mesh"/> without going through managed arrays.</summary>
    public static class MeshWriter
    {
        public static bool Write(
            Mesh mesh,
            NativeList<float3> vertices,
            NativeList<float3> normals,
            NativeList<float4> colors,
            NativeList<int> indices)
        {
            mesh.Clear(false);

            if (!indices.IsCreated || indices.Length == 0)
            {
                return false;
            }

            mesh.indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices.AsArray().Reinterpret<Vector3>());

            if (normals.IsCreated && normals.Length == vertices.Length)
            {
                mesh.SetNormals(normals.AsArray().Reinterpret<Vector3>());
            }

            if (colors.IsCreated && colors.Length == vertices.Length)
            {
                mesh.SetColors(colors.AsArray().Reinterpret<Color>());
            }

            mesh.SetIndices(indices.AsArray(), MeshTopology.Triangles, 0, false);

            if (!normals.IsCreated || normals.Length != vertices.Length)
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            return true;
        }
    }
}
