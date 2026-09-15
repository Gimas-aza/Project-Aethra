using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Project.World.Meshing
{
    /// <summary>
    /// Naive Surface Nets over a binary solid field. One vertex per sign-changing cell, placed at the
    /// average of its crossing edge midpoints; one quad per crossing sample edge.
    /// <para>
    /// The field covers voxels -1..Size and cells cover -1..Size-1, so a chunk owns each surface edge
    /// exactly once: neighbouring chunks meet without seams and without duplicated triangles.
    /// </para>
    /// </summary>
    [BurstCompile]
    public struct SurfaceNetsJob : IJob
    {
        /// <summary>Voxel extent of the volume being meshed (16 for a chunk).</summary>
        public int3 Size;

        [ReadOnly] public NativeArray<byte> Field;
        [ReadOnly] public NativeArray<byte> SolidFlags;
        [ReadOnly] public NativeArray<float4> MaterialColors;

        public NativeArray<int> CellVertex;

        public NativeList<float3> Vertices;
        public NativeList<float3> Normals;
        public NativeList<float4> Colors;
        public NativeList<int> Indices;

        public static int3 FieldExtent(int3 size)
        {
            return size + 2;
        }

        public static int3 CellExtent(int3 size)
        {
            return size + 1;
        }

        public void Execute()
        {
            Vertices.Clear();
            Normals.Clear();
            Colors.Clear();
            Indices.Clear();

            int3 cellExtent = CellExtent(Size);
            int cellCount = cellExtent.x * cellExtent.y * cellExtent.z;

            for (int i = 0; i < cellCount; i++)
            {
                CellVertex[i] = -1;
            }

            EmitVertices();
            EmitQuads();
            NormalizeNormals();
        }

        private int FieldIndex(int x, int y, int z)
        {
            int3 extent = FieldExtent(Size);
            return x + 1 + extent.x * (y + 1 + extent.y * (z + 1));
        }

        private int CellIndex(int x, int y, int z)
        {
            int3 extent = CellExtent(Size);
            return x + 1 + extent.x * (y + 1 + extent.y * (z + 1));
        }

        private void EmitVertices()
        {
            for (int cz = -1; cz < Size.z; cz++)
            {
                for (int cy = -1; cy < Size.y; cy++)
                {
                    for (int cx = -1; cx < Size.x; cx++)
                    {
                        int mask = 0;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            byte material = Field[FieldIndex(
                                cx + (corner & 1),
                                cy + ((corner >> 1) & 1),
                                cz + ((corner >> 2) & 1))];

                            if (SolidFlags[material] != 0)
                            {
                                mask |= 1 << corner;
                            }
                        }

                        if (mask == 0 || mask == 255)
                        {
                            continue;
                        }

                        float3 sum = float3.zero;
                        int crossings = 0;

                        // The 12 cell edges are the corner pairs differing in exactly one bit.
                        for (int corner = 0; corner < 8; corner++)
                        {
                            for (int axis = 0; axis < 3; axis++)
                            {
                                int bit = 1 << axis;
                                if ((corner & bit) != 0)
                                {
                                    continue;
                                }

                                int other = corner | bit;
                                bool a = (mask & (1 << corner)) != 0;
                                bool b = (mask & (1 << other)) != 0;
                                if (a == b)
                                {
                                    continue;
                                }

                                float3 pa = new float3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                                float3 pb = new float3(other & 1, (other >> 1) & 1, (other >> 2) & 1);
                                sum += (pa + pb) * 0.5f;
                                crossings++;
                            }
                        }

                        if (crossings == 0)
                        {
                            continue;
                        }

                        float4 color = float4.zero;
                        int solidCount = 0;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            if ((mask & (1 << corner)) == 0)
                            {
                                continue;
                            }

                            byte material = Field[FieldIndex(
                                cx + (corner & 1),
                                cy + ((corner >> 1) & 1),
                                cz + ((corner >> 2) & 1))];

                            color += MaterialColors[material];
                            solidCount++;
                        }

                        CellVertex[CellIndex(cx, cy, cz)] = Vertices.Length;
                        Vertices.Add(new float3(cx, cy, cz) + sum / crossings);
                        Normals.Add(float3.zero);
                        Colors.Add(solidCount > 0 ? color / solidCount : new float4(1f, 1f, 1f, 1f));
                    }
                }
            }
        }

        private void EmitQuads()
        {
            for (int qz = 0; qz < Size.z; qz++)
            {
                for (int qy = 0; qy < Size.y; qy++)
                {
                    for (int qx = 0; qx < Size.x; qx++)
                    {
                        bool solidAtOrigin = SolidFlags[Field[FieldIndex(qx, qy, qz)]] != 0;

                        for (int axis = 0; axis < 3; axis++)
                        {
                            int nx = qx + (axis == 0 ? 1 : 0);
                            int ny = qy + (axis == 1 ? 1 : 0);
                            int nz = qz + (axis == 2 ? 1 : 0);

                            bool solidAtNeighbour = SolidFlags[Field[FieldIndex(nx, ny, nz)]] != 0;
                            if (solidAtOrigin == solidAtNeighbour)
                            {
                                continue;
                            }

                            // The four cells sharing this sample edge, offset along the two axes
                            // orthogonal to it: b = (axis + 1) % 3, c = (axis + 2) % 3.
                            int bx = axis == 2 ? 1 : 0;
                            int by = axis == 0 ? 1 : 0;
                            int bz = axis == 1 ? 1 : 0;
                            int cx2 = axis == 1 ? 1 : 0;
                            int cy2 = axis == 2 ? 1 : 0;
                            int cz2 = axis == 0 ? 1 : 0;

                            int v00 = CellVertex[CellIndex(qx - bx - cx2, qy - by - cy2, qz - bz - cz2)];
                            int v10 = CellVertex[CellIndex(qx - cx2, qy - cy2, qz - cz2)];
                            int v11 = CellVertex[CellIndex(qx, qy, qz)];
                            int v01 = CellVertex[CellIndex(qx - bx, qy - by, qz - bz)];

                            if (v00 < 0 || v10 < 0 || v11 < 0 || v01 < 0)
                            {
                                continue;
                            }

                            if (solidAtOrigin)
                            {
                                AddTriangle(v00, v10, v11);
                                AddTriangle(v00, v11, v01);
                            }
                            else
                            {
                                AddTriangle(v00, v11, v10);
                                AddTriangle(v00, v01, v11);
                            }
                        }
                    }
                }
            }
        }

        private void AddTriangle(int a, int b, int c)
        {
            Indices.Add(a);
            Indices.Add(b);
            Indices.Add(c);

            float3 pa = Vertices[a];
            float3 normal = math.cross(Vertices[b] - pa, Vertices[c] - pa);

            Normals[a] = Normals[a] + normal;
            Normals[b] = Normals[b] + normal;
            Normals[c] = Normals[c] + normal;
        }

        private void NormalizeNormals()
        {
            for (int i = 0; i < Normals.Length; i++)
            {
                float3 normal = Normals[i];
                float length = math.length(normal);
                Normals[i] = length > 1e-6f ? normal / length : new float3(0f, 1f, 0f);
            }
        }
    }
}
