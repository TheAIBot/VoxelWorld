﻿using OpenGL;
using System;
using System.Buffers;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace VoxelWorld
{
    internal class VoxelGrid
    {
        private Vector3 GridCenter;
        VoxelSystemData GenData;
        private readonly sbyte[] GridSign;
        private readonly Vector3[] VoxelPoints;
        private readonly bool[] IsUsingVoxelPoint;
        private int TriangleCount = 0;

        public VoxelGrid(Vector3 center, VoxelSystemData voxelSystemData)
        {
            this.GenData = voxelSystemData;
            this.GridCenter = center;

            this.GridSign = new sbyte[GenData.GridSize * GenData.GridSize * GenData.GridSize];
            this.VoxelPoints = new Vector3[(GenData.GridSize - 1) * (GenData.GridSize - 1) * (GenData.GridSize - 1)];
            this.IsUsingVoxelPoint = new bool[VoxelPoints.Length];
        }

        public void Repurpose(Vector3 newCenter, VoxelSystemData genData)
        {
            GridCenter = newCenter;
            GenData = genData;
            Array.Fill(IsUsingVoxelPoint, false);
        }

        public BitArray GetCompressed()
        {
            BitArray signs = new BitArray(GridSign.Length);
            for (int i = 0; i < GridSign.Length; i++)
            {
                signs[i] = GridSign[i] > 0;
            }

            return signs;
        }

        public void Restore(BitArray compressedGrid)
        {
            for (int i = 0; i < compressedGrid.Length; i++)
            {
                GridSign[i] = (sbyte)(compressedGrid[i] ? 1 : -1);
            }
        }

        private Vector4 GetTopLeftCorner()
        {
            float distanceFromCenter = (((float)GenData.GridSize - 1.0f) / 2.0f) * GenData.VoxelSize;
            return new Vector4(GridCenter + new Vector3(distanceFromCenter, distanceFromCenter, distanceFromCenter), 0.0f);
        }

        public unsafe void Randomize()
        {
            if (Avx.IsSupported)
            {
                float* prods = stackalloc float[GenData.WeightGen.Seeds.GetSeedsCount()];
                fixed (float* seedsPtr = GenData.WeightGen.Seeds.Seeds)
                {
                    Vector4 topLeftCorner = GetTopLeftCorner();
                    int index = 0;
                    for (int z = 0; z < GenData.GridSize; z++)
                    {
                        for (int y = 0; y < GenData.GridSize; y++)
                        {
                            for (int x = 0; x < GenData.GridSize; x++)
                            {
                                Vector4 pos = topLeftCorner - new Vector4(x, y, z, 0.0f) * GenData.VoxelSize;

                                float noise = GenData.WeightGen.GenerateWeight(pos, seedsPtr, prods);
                                GridSign[index++] = (sbyte)(noise > 0.0f ? 1 : -1);
                            }
                        }
                    }
                }
            }
            else
            {
                throw new Exception("I was too lazy so you need AVX in order to run this program.");
            }
        }

        public bool IsEmpty()
        {
            return TriangleCount == 0;
        }

        public bool EdgePointsUsed()
        {
            int VPToIndex(int x, int y, int z)
            {
                return z * (GenData.GridSize - 1) * (GenData.GridSize - 1) + y * (GenData.GridSize - 1) + x;
            }

            bool pointsAtEdge = false;
            for (int y = 0; y < GenData.GridSize - 1; y++)
            {
                for (int x = 0; x < GenData.GridSize - 1; x++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(x, y, 0)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int y = 0; y < GenData.GridSize - 1; y++)
            {
                for (int x = 0; x < GenData.GridSize - 1; x++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(x, y, GenData.GridSize - 2)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int z = 0; z < GenData.GridSize - 1; z++)
            {
                for (int x = 0; x < GenData.GridSize - 1; x++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(x, 0, z)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int z = 0; z < GenData.GridSize - 1; z++)
            {
                for (int x = 0; x < GenData.GridSize - 1; x++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(x, GenData.GridSize - 2, z)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int z = 0; z < GenData.GridSize - 1; z++)
            {
                for (int y = 0; y < GenData.GridSize - 1; y++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(0, y, z)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int z = 0; z < GenData.GridSize - 1; z++)
            {
                for (int y = 0; y < GenData.GridSize - 1; y++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(GenData.GridSize - 2, y, z)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            return false;
        }

        public void Interpolate()
        {
            var topLeft = GetTopLeftCorner();
            Vector3 topLeftCorner = new Vector3(topLeft.X, topLeft.Y, topLeft.Z) - (new Vector3(GenData.VoxelSize) * 0.5f);

            Vector3 xIncrement = new Vector3(1, 0, 0) * GenData.VoxelSize;
            Vector3 yIncrement = new Vector3(0, 1, 0) * GenData.VoxelSize;
            Vector3 zIncrement = new Vector3(0, 0, 1) * GenData.VoxelSize;

            Vector3 voxelPos = topLeftCorner;

            int index = 0;
            for (int vpZ = 0; vpZ < GenData.GridSize - 1; vpZ++)
            {
                voxelPos.Y = topLeftCorner.Y;
                for (int vpY = 0; vpY < GenData.GridSize - 1; vpY++)
                {
                    voxelPos.X = topLeftCorner.X;
                    for (int vpX = 0; vpX < GenData.GridSize - 1; vpX++)
                    {
                        VoxelPoints[index++] = voxelPos;
                        voxelPos -= xIncrement;
                    }
                    voxelPos -= yIncrement;
                }
                voxelPos -= zIncrement;
            }
        }

        public void Smooth(int iterations)
        {
            int VPToIndex(int x, int y, int z)
            {
                return z * (GenData.GridSize - 1) * (GenData.GridSize - 1) + y * (GenData.GridSize - 1) + x;
            }

            for (int i = 0; i < iterations; i++)
            {
                for (int z = 1; z < GenData.GridSize - 2; z++)
                {
                    for (int y = 1; y < GenData.GridSize - 2; y++)
                    {
                        for (int x = 1; x < GenData.GridSize - 2; x++)
                        {
                            if (!IsUsingVoxelPoint[VPToIndex(x, y, z)])
                            {
                                continue;
                            }

                            int points = 0;
                            Vector3 center = new Vector3(0, 0, 0);

                            if (IsUsingVoxelPoint[VPToIndex(x - 1, y, z)])
                            {
                                center += VoxelPoints[VPToIndex(x - 1, y, z)];
                                points++;
                            }
                            if (IsUsingVoxelPoint[VPToIndex(x + 1, y, z)])
                            {
                                center += VoxelPoints[VPToIndex(x + 1, y, z)];
                                points++;
                            }

                            if (IsUsingVoxelPoint[VPToIndex(x, y - 1, z)])
                            {
                                center += VoxelPoints[VPToIndex(x, y - 1, z)];
                                points++;
                            }
                            if (IsUsingVoxelPoint[VPToIndex(x, y + 1, z)])
                            {
                                center += VoxelPoints[VPToIndex(x, y + 1, z)];
                                points++;
                            }

                            if (IsUsingVoxelPoint[VPToIndex(x, y, z - 1)])
                            {
                                center += VoxelPoints[VPToIndex(x, y, z - 1)];
                                points++;
                            }
                            if (IsUsingVoxelPoint[VPToIndex(x, y, z + 1)])
                            {
                                center += VoxelPoints[VPToIndex(x, y, z + 1)];
                                points++;
                            }


                            VoxelPoints[VPToIndex(x, y, z)] += ((center / points) - VoxelPoints[VPToIndex(x, y, z)]);
                        }
                    }
                }

            }
        }

        public void PreCalculateGeometryData()
        {
            int GridToVP(int x, int y, int z)
            {
                return (z - 1) * (GenData.GridSize - 1) * (GenData.GridSize - 1) + (y - 1) * (GenData.GridSize - 1) + (x - 1);
            }

            int PosToGridIndex(int x, int y, int z)
            {
                return z * GenData.GridSize * GenData.GridSize + y * GenData.GridSize + x;
            }

            TriangleCount = 0;
            for (int z = 1; z < GenData.GridSize - 1; z++)
            {
                for (int y = 1; y < GenData.GridSize - 1; y++)
                {
                    for (int x = 1; x < GenData.GridSize - 1; x++)
                    {
                        int centerSign = GridSign[PosToGridIndex(x, y, z)];
                        if (centerSign < 0)
                        {
                            continue;
                        }

                        int x0y0z0 = GridToVP(x + 0, y + 0, z + 0);
                        int x0y0z1 = GridToVP(x + 0, y + 0, z + 1);
                        int x0y1z0 = GridToVP(x + 0, y + 1, z + 0);
                        int x0y1z1 = GridToVP(x + 0, y + 1, z + 1);
                        int x1y0z0 = GridToVP(x + 1, y + 0, z + 0);
                        int x1y0z1 = GridToVP(x + 1, y + 0, z + 1);
                        int x1y1z0 = GridToVP(x + 1, y + 1, z + 0);
                        int x1y1z1 = GridToVP(x + 1, y + 1, z + 1);

                        if (centerSign > GridSign[PosToGridIndex(x - 1, y, z)])
                        {
                            IsUsingVoxelPoint[x0y0z0] = true;
                            IsUsingVoxelPoint[x0y0z1] = true;
                            IsUsingVoxelPoint[x0y1z0] = true;
                            IsUsingVoxelPoint[x0y1z1] = true;
                            TriangleCount += 2;
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y - 1, z)])
                        {
                            IsUsingVoxelPoint[x1y0z1] = true;
                            IsUsingVoxelPoint[x1y0z0] = true;
                            IsUsingVoxelPoint[x0y0z1] = true;
                            IsUsingVoxelPoint[x0y0z0] = true;
                            TriangleCount += 2;
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y, z - 1)])
                        {
                            IsUsingVoxelPoint[x0y0z0] = true;
                            IsUsingVoxelPoint[x0y1z0] = true;
                            IsUsingVoxelPoint[x1y0z0] = true;
                            IsUsingVoxelPoint[x1y1z0] = true;
                            TriangleCount += 2;
                        }

                        if (centerSign > GridSign[PosToGridIndex(x + 1, y, z)])
                        {
                            IsUsingVoxelPoint[x1y0z0] = true;
                            IsUsingVoxelPoint[x1y0z1] = true;
                            IsUsingVoxelPoint[x1y1z0] = true;
                            IsUsingVoxelPoint[x1y1z1] = true;
                            TriangleCount += 2;
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y + 1, z)])
                        {
                            IsUsingVoxelPoint[x1y1z1] = true;
                            IsUsingVoxelPoint[x1y1z0] = true;
                            IsUsingVoxelPoint[x0y1z1] = true;
                            IsUsingVoxelPoint[x0y1z0] = true;
                            TriangleCount += 2;
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y, z + 1)])
                        {
                            IsUsingVoxelPoint[x0y0z1] = true;
                            IsUsingVoxelPoint[x0y1z1] = true;
                            IsUsingVoxelPoint[x1y0z1] = true;
                            IsUsingVoxelPoint[x1y1z1] = true;
                            TriangleCount += 2;
                        }
                    }
                }
            }
        }

        public BoundingCircle GetBoundingCircle()
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < VoxelPoints.Length; i++)
            {
                if (!IsUsingVoxelPoint[i])
                {
                    continue;
                }

                Vector3 vp = VoxelPoints[i];
                min = Vector3.Min(min, vp);
                max = Vector3.Max(max, vp);
            }
            Vector3 center = (max + min) * 0.5f;
            float radius = (max - center).Length();
            return new BoundingCircle(center, radius);
        }
        
        public GridNormal GetGridNormal()
        {
            int PosToGridIndex(int x, int y, int z)
            {
                return z * GenData.GridSize * GenData.GridSize + y * GenData.GridSize + x;
            }

            GridNormal normal = new GridNormal();

            for (int z = 1; z < GenData.GridSize - 1; z++)
            {
                for (int y = 1; y < GenData.GridSize - 1; y++)
                {
                    for (int x = 1; x < GenData.GridSize - 1; x++)
                    {
                        int centerSign = GridSign[PosToGridIndex(x, y, z)];
                        if (centerSign < 0)
                        {
                            continue;
                        }

                        normal.Xp |= centerSign > GridSign[PosToGridIndex(x + 1, y, z)];
                        normal.Xm |= centerSign > GridSign[PosToGridIndex(x - 1, y, z)];
                        normal.Yp |= centerSign > GridSign[PosToGridIndex(x, y + 1, z)];
                        normal.Ym |= centerSign > GridSign[PosToGridIndex(x, y - 1, z)];
                        normal.Zp |= centerSign > GridSign[PosToGridIndex(x, y, z + 1)];
                        normal.Zm |= centerSign > GridSign[PosToGridIndex(x, y, z - 1)];
                    }
                }
            }

            return normal;
        }

        private static int CountTruesWithPopCnt(bool[] bools)
        {
            ReadOnlySpan<ulong> longs = MemoryMarshal.Cast<bool, ulong>(bools);
            int sum = 0;
            for (int i = 0; i < longs.Length; i++)
            {
                sum += (int)Popcnt.X64.PopCount(longs[i]);
            }

            ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<bool, byte>(bools);
            for (int i = longs.Length * sizeof(ulong); i < bytes.Length; i++)
            {
                sum += bytes[i];
            }

            return sum;
        }


        public GeometryData Triangulize()
        {
            int GridToVP(int x, int y, int z)
            {
                return (z - 1) * (GenData.GridSize - 1) * (GenData.GridSize - 1) + (y - 1) * (GenData.GridSize - 1) + (x - 1);
            }

            int PosToGridIndex(int x, int y, int z)
            {
                return z * GenData.GridSize * GenData.GridSize + y * GenData.GridSize + x;
            }

            int vertexCount = CountTruesWithPopCnt(IsUsingVoxelPoint);

            int triangleIndiceCount = TriangleCount * 3;
            GeometryData geoData = new GeometryData(vertexCount, triangleIndiceCount);
            Span<uint> indices = geoData.Indices;


            int indiceIndex = 0;

            void AddRectangleTriangles(Span<uint> indices, uint a, uint b, uint c, uint d)
            {
                indices[indiceIndex++] = c;
                indices[indiceIndex++] = a;
                indices[indiceIndex++] = b;

                indices[indiceIndex++] = b;
                indices[indiceIndex++] = d;
                indices[indiceIndex++] = c;
            }


            for (int z = 1; z < GenData.GridSize - 1; z++)
            {
                for (int y = 1; y < GenData.GridSize - 1; y++)
                {
                    for (int x = 1; x < GenData.GridSize - 1; x++)
                    {
                        int centerSign = GridSign[PosToGridIndex(x, y, z)];
                        if (centerSign < 0)
                        {
                            continue;
                        }

                        uint x0y0z0 = (uint)GridToVP(x + 0, y + 0, z + 0);
                        uint x0y0z1 = (uint)GridToVP(x + 0, y + 0, z + 1);
                        uint x0y1z0 = (uint)GridToVP(x + 0, y + 1, z + 0);
                        uint x0y1z1 = (uint)GridToVP(x + 0, y + 1, z + 1);
                        uint x1y0z0 = (uint)GridToVP(x + 1, y + 0, z + 0);
                        uint x1y0z1 = (uint)GridToVP(x + 1, y + 0, z + 1);
                        uint x1y1z0 = (uint)GridToVP(x + 1, y + 1, z + 0);
                        uint x1y1z1 = (uint)GridToVP(x + 1, y + 1, z + 1);


                        if (centerSign > GridSign[PosToGridIndex(x - 1, y, z)])
                        {
                            AddRectangleTriangles(indices, x0y0z1, x0y0z0, x0y1z1, x0y1z0);
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y - 1, z)])
                        {
                            AddRectangleTriangles(indices, x0y0z0, x0y0z1, x1y0z0, x1y0z1);
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y, z - 1)])
                        {
                            AddRectangleTriangles(indices, x0y1z0, x0y0z0, x1y1z0, x1y0z0);
                        }

                        if (centerSign > GridSign[PosToGridIndex(x + 1, y, z)])
                        {
                            AddRectangleTriangles(indices, x1y0z0, x1y0z1, x1y1z0, x1y1z1);
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y + 1, z)])
                        {
                            AddRectangleTriangles(indices, x0y1z1, x0y1z0, x1y1z1, x1y1z0);
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y, z + 1)])
                        {
                            AddRectangleTriangles(indices, x0y0z1, x0y1z1, x1y0z1, x1y1z1);
                        }
                    }
                }
            }

            using (var indexConverterArr = new RentedArray<uint>(VoxelPoints.Length))
            {
                Span<uint> indexConverter = indexConverterArr.AsSpan();
                indexConverter.Fill(uint.MaxValue);

                Span<Vector3> vertices = geoData.Vertices;
                int vpIndex = 0;

                for (int i = 0; i < indices.Length; i++)
                {
                    int oldIndex = (int)indices[i];
                    uint newIndex = indexConverter[oldIndex];
                    if (newIndex == uint.MaxValue)
                    {
                        newIndex = (uint)vpIndex;
                        indexConverter[oldIndex] = newIndex;
                        vertices[vpIndex++] = VoxelPoints[oldIndex];
                    }
                    indices[i] = newIndex;
                }
            }

            Geometry.CalculateNormals(geoData.Vertices, indices, geoData.Normals);

            return geoData;
        }
    }
}
