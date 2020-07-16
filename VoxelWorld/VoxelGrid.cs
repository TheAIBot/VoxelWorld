using OpenGL;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VoxelWorld
{
    internal class VoxelGrid
    {
        private readonly int Size;
        private Vector3 GridCenter;
        private float VoxelSize;
        private readonly Func<Vector3, float> WeightGen;
        private readonly float[] Grid;
        private readonly sbyte[] GridSign;
        private readonly Vector3[] VoxelPoints;
        private readonly bool[] IsUsingVoxelPoint;
        private int TriangleCount = 0;

        public VoxelGrid(int size, Vector3 center, float voxelSize, Func<Vector3, float> gen)
        {
            this.Size = size;
            this.GridCenter = center;
            this.VoxelSize = voxelSize;
            this.WeightGen = gen;

            this.Grid = new float[Size * Size * Size];
            this.GridSign = new sbyte[Size * Size * Size];
            this.VoxelPoints = new Vector3[(Size - 1) * (Size - 1) * (Size - 1)];
            this.IsUsingVoxelPoint = new bool[VoxelPoints.Length];
        }

        public void Repurpose(Vector3 newCenter, float newVoxelSize)
        {
            GridCenter = newCenter;
            VoxelSize = newVoxelSize;
            Array.Fill(IsUsingVoxelPoint, false);
        }

        private Vector3 GetTopLeftCorner()
        {
            float distanceFromCenter = (((float)Size - 1.0f) / 2.0f) * VoxelSize;
            return GridCenter + new Vector3(distanceFromCenter, distanceFromCenter, distanceFromCenter);
        }

        public void Randomize()
        {
            int IndexFromPos(int x, int y, int z)
            {
                return z * Size * Size + y * Size + x;
            }

            Vector3 topLeftCorner = GetTopLeftCorner();
            for (int z = 0; z < Size; z++)
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Vector3 pos = topLeftCorner - new Vector3(VoxelSize * x, VoxelSize * y, VoxelSize * z);

                        float noise = WeightGen(pos);

                        Grid[IndexFromPos(x, y, z)] = noise;
                        GridSign[IndexFromPos(x, y, z)] = (sbyte)MathF.Sign(noise);
                    }
                }
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
                return z * (Size - 1) * (Size - 1) + y * (Size - 1) + x;
            }

            bool pointsAtEdge = false;
            for (int y = 0; y < Size - 1; y++)
            {
                for (int x = 0; x < Size - 1; x++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(x, y, 0)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int y = 0; y < Size - 1; y++)
            {
                for (int x = 0; x < Size - 1; x++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(x, y, Size - 2)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int z = 0; z < Size - 1; z++)
            {
                for (int x = 0; x < Size - 1; x++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(x, 0, z)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int z = 0; z < Size - 1; z++)
            {
                for (int x = 0; x < Size - 1; x++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(x, Size - 2, z)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int z = 0; z < Size - 1; z++)
            {
                for (int y = 0; y < Size - 1; y++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(0, y, z)];
                }
            }
            if (pointsAtEdge)
            {
                return true;
            }

            for (int z = 0; z < Size - 1; z++)
            {
                for (int y = 0; y < Size - 1; y++)
                {
                    pointsAtEdge |= IsUsingVoxelPoint[VPToIndex(Size - 2, y, z)];
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
            Vector3 topLeftCorner = GetTopLeftCorner();

            int index = 0;
            for (int vpZ = 0; vpZ < Size - 1; vpZ++)
            {
                for (int vpY = 0; vpY < Size - 1; vpY++)
                {
                    for (int vpX = 0; vpX < Size - 1; vpX++)
                    {
                        VoxelPoints[index++] = topLeftCorner - new Vector3(vpX, vpY, vpZ) * VoxelSize - (new Vector3(VoxelSize) * 0.5f);
                    }
                }
            }
        }

        public void Smooth(int iterations)
        {
            int VPToIndex(int x, int y, int z)
            {
                return z * (Size - 1) * (Size - 1) + y * (Size - 1) + x;
            }

            for (int i = 0; i < iterations; i++)
            {
                for (int z = 1; z < Size - 2; z++)
                {
                    for (int y = 1; y < Size - 2; y++)
                    {
                        for (int x = 1; x < Size - 2; x++)
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
                return (z - 1) * (Size - 1) * (Size - 1) + (y - 1) * (Size - 1) + (x - 1);
            }

            int PosToGridIndex(int x, int y, int z)
            {
                return z * Size * Size + y * Size + x;
            }

            TriangleCount = 0;
            for (int z = 1; z < Size - 1; z++)
            {
                for (int y = 1; y < Size - 1; y++)
                {
                    for (int x = 1; x < Size - 1; x++)
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

        public AxisAlignedBoundingBox GetBoundingBox()
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

            return new AxisAlignedBoundingBox(min, max);
        }
        
        public GridNormal GetGridNormal()
        {
            int PosToGridIndex(int x, int y, int z)
            {
                return z * Size * Size + y * Size + x;
            }

            GridNormal normal = new GridNormal();

            for (int z = 1; z < Size - 1; z++)
            {
                for (int y = 1; y < Size - 1; y++)
                {
                    for (int x = 1; x < Size - 1; x++)
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


        public GeometryData Triangulize()
        {
            int GridToVP(int x, int y, int z)
            {
                return (z - 1) * (Size - 1) * (Size - 1) + (y - 1) * (Size - 1) + (x - 1);
            }

            int PosToGridIndex(int x, int y, int z)
            {
                return z * Size * Size + y * Size + x;
            }

            uint[] indices = new uint[TriangleCount * 3];
            int indiceIndex = 0;

            void AddRectangleTriangles(uint a, uint b, uint c, uint d)
            {
                indices[indiceIndex++] = c;
                indices[indiceIndex++] = a;
                indices[indiceIndex++] = b;

                indices[indiceIndex++] = b;
                indices[indiceIndex++] = d;
                indices[indiceIndex++] = c;
            }


            for (int z = 1; z < Size - 1; z++)
            {
                for (int y = 1; y < Size - 1; y++)
                {
                    for (int x = 1; x < Size - 1; x++)
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
                            AddRectangleTriangles(x0y0z1, x0y0z0, x0y1z1, x0y1z0);
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y - 1, z)])
                        {
                            AddRectangleTriangles(x0y0z0, x0y0z1, x1y0z0, x1y0z1);
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y, z - 1)])
                        {
                            AddRectangleTriangles(x0y1z0, x0y0z0, x1y1z0, x1y0z0);
                        }

                        if (centerSign > GridSign[PosToGridIndex(x + 1, y, z)])
                        {
                            AddRectangleTriangles(x1y0z0, x1y0z1, x1y1z0, x1y1z1);
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y + 1, z)])
                        {
                            AddRectangleTriangles(x0y1z1, x0y1z0, x1y1z1, x1y1z0);
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y, z + 1)])
                        {
                            AddRectangleTriangles(x0y0z1, x0y1z1, x1y0z1, x1y1z1);
                        }
                    }
                }
            }

            Span<byte> usedVP = MemoryMarshal.Cast<bool, byte>(IsUsingVoxelPoint);
            int vpUsedCount = 0;
            for (int i = 0; i < usedVP.Length; i++)
            {
                vpUsedCount += usedVP[i];
            }

            uint[] indexConverter = new uint[VoxelPoints.Length];
            Vector3[] prunedPoints = new Vector3[vpUsedCount];
            int vpIndex = 0;

            Array.Fill(indexConverter, uint.MaxValue);

            for (int i = 0; i < indices.Length; i++)
            {
                uint oldIndex = indices[i];
                uint newIndex = indexConverter[oldIndex];
                if (newIndex == uint.MaxValue)
                {
                    newIndex = (uint)vpIndex;
                    indexConverter[oldIndex] = newIndex;
                    prunedPoints[vpIndex++] = VoxelPoints[oldIndex];
                }
                indices[i] = newIndex;
            }

            Vector3[] normals = Geometry.CalculateNormals(prunedPoints, indices);

            return new GeometryData(prunedPoints, normals, indices);
        }
    }
}
