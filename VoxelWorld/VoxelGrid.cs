using OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using VoxelWorld.Shaders;
using static OpenGL.GenericVAO;

namespace VoxelWorld
{
    internal class VoxelGrid
    {
        private readonly int Size;
        public readonly Vector3 GridCenter;
        private readonly float VoxelSize;
        private readonly Func<Vector3, float> WeightGen;
        private readonly float[] Grid;
        private readonly sbyte[] GridSign;
        public readonly Vector3[] VoxelPoints;
        private readonly Vector3 TopLeftCorner;

        public VoxelGrid(int size, Vector3 center, float voxelSize, Func<Vector3, float> gen)
        {
            this.Size = size;
            this.GridCenter = center;
            this.VoxelSize = voxelSize;
            this.WeightGen = gen;

            float distanceFromCenter = (((float)size - 1.0f) / 2.0f) * voxelSize;
            this.TopLeftCorner = GridCenter + new Vector3(distanceFromCenter, distanceFromCenter, distanceFromCenter);

            this.Grid = new float[Size * Size * Size];
            this.GridSign = new sbyte[Size * Size * Size];
            this.VoxelPoints = new Vector3[(Size - 1) * (Size - 1) * (Size - 1)];
        }

        public void Randomize()
        {
            int IndexFromPos(int x, int y, int z)
            {
                return z * Size * Size + y * Size + x;
            }

            for (int z = 0; z < Size; z++)
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        Vector3 pos = TopLeftCorner - new Vector3(VoxelSize * x, VoxelSize * y, VoxelSize * z);

                        float noise = WeightGen(pos);

                        Grid[IndexFromPos(x, y, z)] = noise;
                        GridSign[IndexFromPos(x, y, z)] = (sbyte)MathF.Sign(noise);
                    }
                }
            }
        }

        public bool IsEmpty()
        {
            bool hasTrue = false;
            bool hasFalse = false;
            for (int i = 0; i < GridSign.Length; i++)
            {
                hasTrue |= GridSign[i] >= 0;
                hasFalse |= GridSign[i] < 0;
            }

            return !(hasTrue && hasFalse);
        }

        public bool EdgePointsUsed()
        {
            int VPToIndex(int x, int y, int z)
            {
                return z * (Size - 1) * (Size - 1) + y * (Size - 1) + x;
            }

            bool[] inUse = GetVPInUse();

            bool pointsAtEdge = false;
            for (int y = 0; y < Size - 1; y++)
            {
                for (int x = 0; x < Size - 1; x++)
                {
                    pointsAtEdge |= inUse[VPToIndex(x, y, 0)];
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
                    pointsAtEdge |= inUse[VPToIndex(x, y, Size - 2)];
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
                    pointsAtEdge |= inUse[VPToIndex(x, 0, z)];
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
                    pointsAtEdge |= inUse[VPToIndex(x, Size - 2, z)];
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
                    pointsAtEdge |= inUse[VPToIndex(0, y, z)];
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
                    pointsAtEdge |= inUse[VPToIndex(Size - 2, y, z)];
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
            int VPToGrid(int x, int y, int z)
            {
                return z * Size * Size + y * Size + x;
            }

            int VPToIndex(int x, int y, int z)
            {
                return z * (Size - 1) * (Size - 1) + y * (Size - 1) + x;
            }

            Vector3 middle = new Vector3(0.5f, 0.5f, 0.5f);
            Span<Vector3> offsets = stackalloc Vector3[8];
            offsets[0] = middle - new Vector3(0, 0, 0);
            offsets[1] = middle - new Vector3(0, 0, 1);
            offsets[2] = middle - new Vector3(0, 1, 0);
            offsets[3] = middle - new Vector3(0, 1, 1);
            offsets[4] = middle - new Vector3(1, 0, 0);
            offsets[5] = middle - new Vector3(1, 0, 1);
            offsets[6] = middle - new Vector3(1, 1, 0);
            offsets[7] = middle - new Vector3(1, 1, 1);

            Span<int> gridIndexOffsets = stackalloc int[8];
            gridIndexOffsets[0] = VPToGrid(0, 0, 0);
            gridIndexOffsets[1] = VPToGrid(0, 0, 1);
            gridIndexOffsets[2] = VPToGrid(0, 1, 0);
            gridIndexOffsets[3] = VPToGrid(0, 1, 1);
            gridIndexOffsets[4] = VPToGrid(1, 0, 0);
            gridIndexOffsets[5] = VPToGrid(1, 0, 1);
            gridIndexOffsets[6] = VPToGrid(1, 1, 0);
            gridIndexOffsets[7] = VPToGrid(1, 1, 1);

            bool[] inUse = GetVPInUse();

            for (int vpZ = 0; vpZ < Size - 1; vpZ++)
            {
                for (int vpY = 0; vpY < Size - 1; vpY++)
                {
                    for (int vpX = 0; vpX < Size - 1; vpX++)
                    {
                        Vector3 center = TopLeftCorner - new Vector3(vpX, vpY, vpZ) * VoxelSize - (new Vector3(VoxelSize) * 0.5f);

                        if (!inUse[VPToIndex(vpX, vpY, vpZ)])
                        {
                            VoxelPoints[VPToIndex(vpX, vpY, vpZ)] = center;
                            continue;
                        }



                        float maxWeight = -1;
                        float minWeight = 2;

                        Vector3 offset = Vector3.Zero;

                        int gridIndex = VPToGrid(vpX, vpY, vpZ);
                        for (int i = 0; i < gridIndexOffsets.Length; i++)
                        {
                            float weight = Grid[gridIndex + gridIndexOffsets[i]];

                            maxWeight = MathF.Max(maxWeight, weight);
                            minWeight = MathF.Min(minWeight, weight);

                            offset += offsets[i] * weight;
                        }

                        float weightScale = 1.0f / (maxWeight - minWeight);
                        VoxelPoints[VPToIndex(vpX, vpY, vpZ)] = center + offset * VoxelSize * weightScale;
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

            bool[] inUse = GetVPInUse();

            for (int i = 0; i < iterations; i++)
            {
                for (int z = 1; z < Size - 2; z++)
                {
                    for (int y = 1; y < Size - 2; y++)
                    {
                        for (int x = 1; x < Size - 2; x++)
                        {
                            if (!inUse[VPToIndex(x, y, z)])
                            {
                                continue;
                            }

                            int points = 0;
                            Vector3 center = new Vector3(0, 0, 0);

                            if (inUse[VPToIndex(x - 1, y, z)])
                            {
                                center += VoxelPoints[VPToIndex(x - 1, y, z)];
                                points++;
                            }
                            if (inUse[VPToIndex(x + 1, y, z)])
                            {
                                center += VoxelPoints[VPToIndex(x + 1, y, z)];
                                points++;
                            }

                            if (inUse[VPToIndex(x, y - 1, z)])
                            {
                                center += VoxelPoints[VPToIndex(x, y - 1, z)];
                                points++;
                            }
                            if (inUse[VPToIndex(x, y + 1, z)])
                            {
                                center += VoxelPoints[VPToIndex(x, y + 1, z)];
                                points++;
                            }

                            if (inUse[VPToIndex(x, y, z - 1)])
                            {
                                center += VoxelPoints[VPToIndex(x, y, z - 1)];
                                points++;
                            }
                            if (inUse[VPToIndex(x, y, z + 1)])
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

        private bool[] GetVPInUse()
        {
            int GridToVP(int x, int y, int z)
            {
                return (z - 1) * (Size - 1) * (Size - 1) + (y - 1) * (Size - 1) + (x - 1);
            }

            int PosToGridIndex(int x, int y, int z)
            {
                return z * Size * Size + y * Size + x;
            }

            bool[] inUse = new bool[(Size - 1) * (Size - 1) * (Size - 1)];

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
                            inUse[x0y0z0] = true;
                            inUse[x0y0z1] = true;
                            inUse[x0y1z0] = true;
                            inUse[x0y1z1] = true;
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y - 1, z)])
                        {
                            inUse[x1y0z1] = true;
                            inUse[x1y0z0] = true;
                            inUse[x0y0z1] = true;
                            inUse[x0y0z0] = true;
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y, z - 1)])
                        {
                            inUse[x0y0z0] = true;
                            inUse[x0y1z0] = true;
                            inUse[x1y0z0] = true;
                            inUse[x1y1z0] = true;
                        }

                        if (centerSign > GridSign[PosToGridIndex(x + 1, y, z)])
                        {
                            inUse[x1y0z0] = true;
                            inUse[x1y0z1] = true;
                            inUse[x1y1z0] = true;
                            inUse[x1y1z1] = true;
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y + 1, z)])
                        {
                            inUse[x1y1z1] = true;
                            inUse[x1y1z0] = true;
                            inUse[x0y1z1] = true;
                            inUse[x0y1z0] = true;
                        }
                        if (centerSign > GridSign[PosToGridIndex(x, y, z + 1)])
                        {
                            inUse[x0y0z1] = true;
                            inUse[x0y1z1] = true;
                            inUse[x1y0z1] = true;
                            inUse[x1y1z1] = true;
                        }
                    }
                }
            }

            return inUse;
        }

        public AxisAlignedBoundingBox GetBoundingBox()
        {
            bool[] inUse = GetVPInUse();

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < VoxelPoints.Length; i++)
            {
                if (!inUse[i])
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


        public void Triangulize(VoxelGridInfo vaoConv)
        {
            int GridToVP(int x, int y, int z)
            {
                return (z - 1) * (Size - 1) * (Size - 1) + (y - 1) * (Size - 1) + (x - 1);
            }

            int PosToGridIndex(int x, int y, int z)
            {
                return z * Size * Size + y * Size + x;
            }

            void AddRectangleTriangles(List<uint> indices, uint a, uint b, uint c, uint d)
            {
                indices.Add(c);
                indices.Add(a);
                indices.Add(b);

                indices.Add(b);
                indices.Add(d);
                indices.Add(c);
            }

            List<uint> indices = new List<uint>();

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

            uint[] indexConverter = new uint[VoxelPoints.Length];
            List<uint> indexes = new List<uint>();
            List<Vector3> vps = new List<Vector3>();

            Array.Fill(indexConverter, uint.MaxValue);

            for (int i = 0; i < indices.Count; i++)
            {
                uint oldIndex = indices[i];
                uint newIndex = indexConverter[oldIndex];
                if (newIndex == uint.MaxValue)
                {
                    newIndex = (uint)vps.Count;
                    vps.Add(VoxelPoints[indices[i]]);
                }
                indexes.Add(newIndex);
            }

            uint[] indicesArr = indexes.ToArray();
            Vector3[] prunedPoints = vps.ToArray();

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < prunedPoints.Length; i++)
            {
                min = Vector3.Min(min, prunedPoints[i]);
                max = Vector3.Max(max, prunedPoints[i]);
            }

            Vector3[] normals = Geometry.CalculateNormals(prunedPoints, indicesArr);
            //Console.WriteLine(indicesArr.Length);

            var createVao = new Action(() =>
            {
                VAO meshVao = null;
                VAO boxVao = null;

                {
                    VBO<uint> indiceBuffer = new VBO<uint>(indicesArr, BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticRead);
                    VBO<Vector3> posBuffer = new VBO<Vector3>(prunedPoints);
                    VBO<Vector3> normalBuffer = new VBO<Vector3>(normals);

                    var vbos = new IGenericVBO[]
                    {
                    new GenericVBO<Vector3>(posBuffer, "vertex_pos"),
                    new GenericVBO<Vector3>(normalBuffer, "vertex_normal"),
                    new GenericVBO<uint>(indiceBuffer)
                    };

                    meshVao = new VAO(SimpleShader.GetShader(), vbos);
                    meshVao.DisposeChildren = true;
                    meshVao.DisposeElementArray = true;
                }

                {
                    Vector3[] vertex = new Vector3[] 
                    {
                        new Vector3(min.X, min.Y, max.Z),
                        new Vector3(max.X, min.Y, max.Z),
                        new Vector3(min.X, max.Y, max.Z),
                        new Vector3(max.X, max.Y, max.Z),
                        new Vector3(max.X, min.Y, min.Z),
                        new Vector3(max.X, max.Y, min.Z),
                        new Vector3(min.X, max.Y, min.Z),
                        new Vector3(min.X, min.Y, min.Z)
                    };

                    uint[] element = new uint[] 
                    {
                        0, 1, 2, 1, 3, 2,
                        1, 4, 3, 4, 5, 3,
                        4, 7, 5, 7, 6, 5,
                        7, 0, 6, 0, 2, 6,
                        7, 4, 0, 4, 1, 0,
                        2, 3, 6, 3, 5, 6
                    };

                    Vector3[] normal = Geometry.CalculateNormals(vertex, element);

                    VBO<uint> indiceBuffer = new VBO<uint>(element, BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticRead);
                    VBO<Vector3> posBuffer = new VBO<Vector3>(vertex);
                    VBO<Vector3> normalBuffer = new VBO<Vector3>(normal);

                    var vbos = new IGenericVBO[]
                    {
                    new GenericVBO<Vector3>(posBuffer, "vertex_pos"),
                    new GenericVBO<Vector3>(normalBuffer, "vertex_normal"),
                    new GenericVBO<uint>(indiceBuffer)
                    };

                    boxVao = new VAO(SimpleShader.GetShader(), vbos);
                    boxVao.DisposeChildren = true;
                    boxVao.DisposeElementArray = true;
                }

                lock (vaoConv.DisposeLock)
                {
                    if (vaoConv.HasBeenDisposed)
                    {
                        meshVao.Dispose();
                        boxVao.Dispose();
                    }
                    else
                    {
                        vaoConv.meshVao = meshVao;
                        vaoConv.pointsVao = boxVao;
                    }
                }
            });

            MainThreadWork.QueueWork(createVao);
        }
    }
}
