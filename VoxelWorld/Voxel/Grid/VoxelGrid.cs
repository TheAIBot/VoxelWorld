using OpenGL;
using System;
using System.Buffers;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using VoxelWorld.ShapeGenerators;
using VoxelWorld.Voxel;
using VoxelWorld.Voxel.System;

namespace VoxelWorld.Voxel.Grid
{
    internal class VoxelGrid
    {
        private Vector3 GridCenter;
        VoxelSystemData GenData;
        private readonly bool[] GridSign;
        private readonly Vector3[] VoxelPoints;
        private readonly bool[] IsUsingVoxelPoint;
        private int TriangleCount = 0;

        public VoxelGrid(Vector3 center, VoxelSystemData voxelSystemData)
        {
            GenData = voxelSystemData;
            GridCenter = center;

            GridSign = new bool[GenData.GridSize * GenData.GridSize * GenData.GridSize];
            VoxelPoints = new Vector3[(GenData.GridSize - 1) * (GenData.GridSize - 1) * (GenData.GridSize - 1)];
            IsUsingVoxelPoint = new bool[VoxelPoints.Length];
        }

        public void Repurpose(Vector3 newCenter, VoxelSystemData genData)
        {
            GridCenter = newCenter;
            GenData = genData;
            Array.Fill(IsUsingVoxelPoint, false);
        }

        public BitArray GetCompressed()
        {
            return new BitArray(GridSign);
        }

        public void Restore(BitArray compressedGrid)
        {
            compressedGrid.CopyTo(GridSign, 0);
        }

        private Vector4 GetTopLeftCorner()
        {
            float distanceFromCenter = (GenData.GridSize - 1.0f) / 2.0f * GenData.VoxelSize;
            return new Vector4(GridCenter + new Vector3(distanceFromCenter, distanceFromCenter, distanceFromCenter), 0.0f);
        }

        public unsafe void Randomize()
        {
            Vector128<float> YZToFloat128(int y, int z)
            {
                Vector128<int> zero = Vector128<int>.Zero;
                Vector128<int> yPos = Sse41.Insert(zero, y, 1);
                Vector128<int> yzPos = Sse41.Insert(yPos, z, 2);
                return Sse2.ConvertToVector128Single(yzPos);
            }

            if (Avx.IsSupported)
            {
                float* stackSpace = stackalloc float[CosApproxConsts.StackSpaceNeeded(GenData.WeightGen.Seeds)];

                fixed (float* seedsPtr = GenData.WeightGen.Seeds.Seeds)
                {
                    CosApproxConsts cosApprox = new CosApproxConsts(GenData.WeightGen.Seeds, GenData.WeightGen.NoiseFrequency, seedsPtr, stackSpace);

                    Vector4 voxelSizeX = Vector4.Zero;
                    voxelSizeX.X = GenData.VoxelSize;
                    cosApprox.BaseSeededXDiff(voxelSizeX);

                    Vector4 topLeftCorner = GetTopLeftCorner();
                    int index = 0;
                    int gridSize = GenData.GridSize;
                    for (int z = 0; z < gridSize; z++)
                    {
                        for (int y = 0; y < gridSize; y++)
                        {
                            Vector4 voxelPos = topLeftCorner - YZToFloat128(y, z).AsVector4() * GenData.VoxelSize;
                            cosApprox.MakeSeededBaseNoise(voxelPos);

                            for (int x = 0; x < gridSize; x++)
                            {
                                float noise = GenData.WeightGen.GenerateWeight(voxelPos, cosApprox);
                                GridSign[index++] = noise > 0.0f;

                                cosApprox.UpdateSeededNoiseWithXPosChange();

                                voxelPos = voxelPos - voxelSizeX;
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

        public GridSidePointsUsed EdgePointsUsed()
        {
            static int VPToIndex(int sideLength, int x, int y, int z)
            {
                return z * sideLength * sideLength + y * sideLength + x;
            }

            int sideLength = GenData.GridSize - 1;
            bool[] usedVoxelPoints = IsUsingVoxelPoint;

            GridSidePointsUsed sidesUsed = new GridSidePointsUsed();
            for (int b = 0; b < GenData.GridSize - 1; b++)
            {
                for (int a = 0; a < GenData.GridSize - 1; a++)
                {
                    int plusXIndex = VPToIndex(sideLength, sideLength - 1, a, b);
                    int minusXIndex = VPToIndex(sideLength, 0, a, b);
                    int plusYIndex = VPToIndex(sideLength, a, sideLength - 1, b);
                    int minusYIndex = VPToIndex(sideLength, a, 0, b);
                    int plusZIndex = VPToIndex(sideLength, a, b, sideLength - 1);
                    int minusZIndex = VPToIndex(sideLength, a, b, 0);

                    sidesUsed.PlusX |= usedVoxelPoints[plusXIndex];
                    sidesUsed.MinusX |= usedVoxelPoints[minusXIndex];
                    sidesUsed.PlusY |= usedVoxelPoints[plusYIndex];
                    sidesUsed.MinusY |= usedVoxelPoints[minusYIndex];
                    sidesUsed.PlusZ |= usedVoxelPoints[plusZIndex];
                    sidesUsed.MinusZ |= usedVoxelPoints[minusZIndex];
                }
            }

            return sidesUsed;
        }

        public bool SubGridEdgePointsUsed(in GridOffset subGrid)
        {
            static int VPToIndex(int sideLength, int x, int y, int z)
            {
                return z * sideLength * sideLength + y * sideLength + x;
            }

            int sideLength = GenData.GridSize - 1;
            int halfSideLength = sideLength / 2;
            int ceilHalfSideLength = halfSideLength + 1;

            int xOffset = subGrid.X * halfSideLength;
            int yOffset = subGrid.Y * halfSideLength;
            int zOffset = subGrid.Z * halfSideLength;

            for (int z = zOffset; z < zOffset + ceilHalfSideLength; z++)
            {
                for (int y = yOffset; y < yOffset + ceilHalfSideLength; y++)
                {
                    for (int x = xOffset; x < xOffset + ceilHalfSideLength; x++)
                    {
                        int voxelIndex = VPToIndex(sideLength, x, y, z);
                        if (IsUsingVoxelPoint[voxelIndex])
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void Interpolate()
        {
            var topLeft = GetTopLeftCorner();
            Vector3 topLeftCorner = new Vector3(topLeft.X, topLeft.Y, topLeft.Z) - new Vector3(GenData.VoxelSize) * 0.5f;

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


                            VoxelPoints[VPToIndex(x, y, z)] += center / points - VoxelPoints[VPToIndex(x, y, z)];
                        }
                    }
                }

            }
        }

        public unsafe void PreCalculateGeometryData()
        {
            int GridToVP(int x, int y, int z)
            {
                return (z - 1) * (GenData.GridSize - 1) * (GenData.GridSize - 1) + (y - 1) * (GenData.GridSize - 1) + (x - 1);
            }

            int PosToGridIndex(int x, int y, int z)
            {
                return z * GenData.GridSize * GenData.GridSize + y * GenData.GridSize + x;
            }

            fixed (bool* gridSifsgnPtr = GridSign)
            {
                sbyte* gridSignPtr = (sbyte*)gridSifsgnPtr;
                fixed (bool* isUsingVoxelBoolPtr = IsUsingVoxelPoint)
                {
                    sbyte* isUsingVoxelPtr = (sbyte*)isUsingVoxelBoolPtr;
                    TriangleCount = 0;
                    for (int z = 1; z < GenData.GridSize - 1; z++)
                    {
                        for (int y = 1; y < GenData.GridSize - 1; y++)
                        {
                            int x = 1;

                            int x0y0z0 = GridToVP(x + 0, y + 0, z + 0);
                            int x0y0z1 = GridToVP(x + 0, y + 0, z + 1);
                            int x0y1z0 = GridToVP(x + 0, y + 1, z + 0);
                            int x0y1z1 = GridToVP(x + 0, y + 1, z + 1);
                            int x1y0z0 = GridToVP(x + 1, y + 0, z + 0);
                            int x1y0z1 = GridToVP(x + 1, y + 0, z + 1);
                            int x1y1z0 = GridToVP(x + 1, y + 1, z + 0);
                            int x1y1z1 = GridToVP(x + 1, y + 1, z + 1);

                            int gridIdxCenter = PosToGridIndex(x, y, z);
                            int gridIdxxn1 = PosToGridIndex(x - 1, y, z);
                            int gridIdxyn1 = PosToGridIndex(x, y - 1, z);
                            int gridIdxzn1 = PosToGridIndex(x, y, z - 1);
                            int gridIdxxp1 = PosToGridIndex(x + 1, y, z);
                            int gridIdxyp1 = PosToGridIndex(x, y + 1, z);
                            int gridIdxzp1 = PosToGridIndex(x, y, z + 1);

                            int i = 0;
                            if (Avx.IsSupported && Popcnt.X64.IsSupported)
                            {
                                //Does the same as the non vectorized version but does it 16 points at a time
                                for (; i + Vector128<sbyte>.Count <= GenData.GridSize - 2; i += Vector128<sbyte>.Count)
                                {
                                    Vector128<sbyte> centerSigns = Sse2.LoadVector128(gridSignPtr + gridIdxCenter + i);

                                    Vector128<sbyte> gsXNeg = Sse2.LoadVector128(gridSignPtr + gridIdxxn1 + i);
                                    Vector128<sbyte> gsYNeg = Sse2.LoadVector128(gridSignPtr + gridIdxyn1 + i);
                                    Vector128<sbyte> gsZNeg = Sse2.LoadVector128(gridSignPtr + gridIdxzn1 + i);
                                    Vector128<sbyte> gsXPos = Sse2.LoadVector128(gridSignPtr + gridIdxxp1 + i);
                                    Vector128<sbyte> gsYPos = Sse2.LoadVector128(gridSignPtr + gridIdxyp1 + i);
                                    Vector128<sbyte> gsZPos = Sse2.LoadVector128(gridSignPtr + gridIdxzp1 + i);

                                    Vector128<sbyte> faceXNeg = Sse2.AndNot(gsXNeg, centerSigns);
                                    Vector128<sbyte> faceYNeg = Sse2.AndNot(gsYNeg, centerSigns);
                                    Vector128<sbyte> faceZNeg = Sse2.AndNot(gsZNeg, centerSigns);
                                    Vector128<sbyte> faceXPos = Sse2.AndNot(gsXPos, centerSigns);
                                    Vector128<sbyte> faceYPos = Sse2.AndNot(gsYPos, centerSigns);
                                    Vector128<sbyte> faceZPos = Sse2.AndNot(gsZPos, centerSigns);

                                    //From the non vectorized version one can infer what face directions must be true
                                    //in order for the voxel being used. 
                                    Vector128<sbyte> orXNegYNeg = Sse2.Or(faceXNeg, faceYNeg);
                                    Vector128<sbyte> orXNegYPos = Sse2.Or(faceXNeg, faceYPos);
                                    Vector128<sbyte> orXPosYNeg = Sse2.Or(faceXPos, faceYNeg);
                                    Vector128<sbyte> orXPosYPos = Sse2.Or(faceXPos, faceYPos);
                                    //Pseudo: IsUsingVoxel[x] |= faceA | faceB | faceC
                                    Sse2.Store(isUsingVoxelPtr + x0y0z0 + i, Sse2.Or(Sse2.LoadVector128(isUsingVoxelPtr + x0y0z0 + i), Sse2.Or(orXNegYNeg, faceZNeg)));
                                    Sse2.Store(isUsingVoxelPtr + x0y0z1 + i, Sse2.Or(Sse2.LoadVector128(isUsingVoxelPtr + x0y0z1 + i), Sse2.Or(orXNegYNeg, faceZPos)));
                                    Sse2.Store(isUsingVoxelPtr + x0y1z0 + i, Sse2.Or(Sse2.LoadVector128(isUsingVoxelPtr + x0y1z0 + i), Sse2.Or(orXNegYPos, faceZNeg)));
                                    Sse2.Store(isUsingVoxelPtr + x0y1z1 + i, Sse2.Or(Sse2.LoadVector128(isUsingVoxelPtr + x0y1z1 + i), Sse2.Or(orXNegYPos, faceZPos)));
                                    Sse2.Store(isUsingVoxelPtr + x1y0z0 + i, Sse2.Or(Sse2.LoadVector128(isUsingVoxelPtr + x1y0z0 + i), Sse2.Or(orXPosYNeg, faceZNeg)));
                                    Sse2.Store(isUsingVoxelPtr + x1y0z1 + i, Sse2.Or(Sse2.LoadVector128(isUsingVoxelPtr + x1y0z1 + i), Sse2.Or(orXPosYNeg, faceZPos)));
                                    Sse2.Store(isUsingVoxelPtr + x1y1z0 + i, Sse2.Or(Sse2.LoadVector128(isUsingVoxelPtr + x1y1z0 + i), Sse2.Or(orXPosYPos, faceZNeg)));
                                    Sse2.Store(isUsingVoxelPtr + x1y1z1 + i, Sse2.Or(Sse2.LoadVector128(isUsingVoxelPtr + x1y1z1 + i), Sse2.Or(orXPosYPos, faceZPos)));

                                    //Need to count the number of faces so the triangle count can be updated.
                                    //Each face is represented as a bit here. The idea is the shift the bits
                                    //in the 6 xmm registers so no bits overlap. Or the registers together
                                    //and use popcnt instruction in order to count the number of bits.
                                    Vector128<sbyte> faceXNegShift = Sse2.ShiftLeftLogical(faceXNeg.AsInt16(), 0).AsSByte();
                                    Vector128<sbyte> faceYNegShift = Sse2.ShiftLeftLogical(faceYNeg.AsInt16(), 1).AsSByte();
                                    Vector128<sbyte> faceZNegShift = Sse2.ShiftLeftLogical(faceZNeg.AsInt16(), 2).AsSByte();
                                    Vector128<sbyte> faceXPosShift = Sse2.ShiftLeftLogical(faceXPos.AsInt16(), 3).AsSByte();
                                    Vector128<sbyte> faceYPosShift = Sse2.ShiftLeftLogical(faceYPos.AsInt16(), 4).AsSByte();
                                    Vector128<sbyte> faceZPosShift = Sse2.ShiftLeftLogical(faceZPos.AsInt16(), 5).AsSByte();

                                    //Or the registers together
                                    Vector128<sbyte> bitPerFace = Sse2.Or(Sse2.Or(Sse2.Or(faceXNegShift, faceYNegShift), Sse2.Or(faceZNegShift, faceXPosShift)), Sse2.Or(faceYPosShift, faceZPosShift));

                                    //Use popcnt on the two ulongs to count the bits.
                                    //Each face consists of two triangles.
                                    int faces = (int)(Popcnt.X64.PopCount(bitPerFace.AsUInt64().GetElement(0)) + Popcnt.X64.PopCount(bitPerFace.AsUInt64().GetElement(1)));
                                    TriangleCount += faces * 2;
                                }
                            }

                            for (; i < GenData.GridSize - 2; i++)
                            {
                                bool centerSign = GridSign[gridIdxCenter + i];
                                if (!centerSign)
                                {
                                    continue;
                                }

                                if (centerSign && !GridSign[gridIdxxn1 + i])
                                {
                                    IsUsingVoxelPoint[x0y0z0 + i] = true;
                                    IsUsingVoxelPoint[x0y0z1 + i] = true;
                                    IsUsingVoxelPoint[x0y1z0 + i] = true;
                                    IsUsingVoxelPoint[x0y1z1 + i] = true;
                                    TriangleCount += 2;
                                }
                                if (centerSign && !GridSign[gridIdxyn1 + i])
                                {
                                    IsUsingVoxelPoint[x1y0z1 + i] = true;
                                    IsUsingVoxelPoint[x1y0z0 + i] = true;
                                    IsUsingVoxelPoint[x0y0z1 + i] = true;
                                    IsUsingVoxelPoint[x0y0z0 + i] = true;
                                    TriangleCount += 2;
                                }
                                if (centerSign && !GridSign[gridIdxzn1 + i])
                                {
                                    IsUsingVoxelPoint[x0y0z0 + i] = true;
                                    IsUsingVoxelPoint[x0y1z0 + i] = true;
                                    IsUsingVoxelPoint[x1y0z0 + i] = true;
                                    IsUsingVoxelPoint[x1y1z0 + i] = true;
                                    TriangleCount += 2;
                                }

                                if (centerSign && !GridSign[gridIdxxp1 + i])
                                {
                                    IsUsingVoxelPoint[x1y0z0 + i] = true;
                                    IsUsingVoxelPoint[x1y0z1 + i] = true;
                                    IsUsingVoxelPoint[x1y1z0 + i] = true;
                                    IsUsingVoxelPoint[x1y1z1 + i] = true;
                                    TriangleCount += 2;
                                }
                                if (centerSign && !GridSign[gridIdxyp1 + i])
                                {
                                    IsUsingVoxelPoint[x1y1z1 + i] = true;
                                    IsUsingVoxelPoint[x1y1z0 + i] = true;
                                    IsUsingVoxelPoint[x0y1z1 + i] = true;
                                    IsUsingVoxelPoint[x0y1z0 + i] = true;
                                    TriangleCount += 2;
                                }
                                if (centerSign && !GridSign[gridIdxzp1 + i])
                                {
                                    IsUsingVoxelPoint[x0y0z1 + i] = true;
                                    IsUsingVoxelPoint[x0y1z1 + i] = true;
                                    IsUsingVoxelPoint[x1y0z1 + i] = true;
                                    IsUsingVoxelPoint[x1y1z1 + i] = true;
                                    TriangleCount += 2;
                                }
                            }
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

        private static int CountTruesWithPopCnt(bool[] bools)
        {
            if (Popcnt.X64.IsSupported)
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
            else
            {
                int sum = 0;
                ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<bool, byte>(bools);
                for (int i = 0; i < bytes.Length; i++)
                {
                    sum += bytes[i];
                }

                return sum;
            }
        }

        private unsafe void FillWithFaceIndices(Span<uint> indices)
        {
            static int GridToVP(int sideLength, int x, int y, int z)
            {
                return (z - 1) * sideLength * sideLength + (y - 1) * sideLength + (x - 1);
            }

            static int PosToGridIndex(int sideLength, int x, int y, int z)
            {
                return z * sideLength * sideLength + y * sideLength + x;
            }

            static Vector256<uint> MakeFaceVectorIndices(uint a, uint b, uint c, uint d)
            {
                return Vector256.Create(c, a, b, b, d, c, 0, 0);
            }

            static uint* AddFaceIndices(uint* indices, Vector256<uint> faceVertices, Vector256<uint> storeMask)
            {
                Avx2.MaskStore(indices, storeMask, faceVertices);

                const int indicesPerFace = 6;
                return indices + indicesPerFace;
            }

            int gridSideLength = GenData.GridSize;
            int vpSideLength = gridSideLength - 1;
            bool[] gridSigns = GridSign;

            /*
            Each face consists of 6 indices which creates two triangles that
            together form a rectangle representing the face. Calculaing the
            6 different vertex indices for each face is slow. For a given face
            direction, the face indices are always a constant value different
            from a given indice. Therefore The 6 indices can be calculated 
            by only knowing the base vertex indice and the offsets to the
            other indices.
            The code below makes these offsets for each of the 6 indices
            and stores them in a vectors so calculating/storing the indices
            can be vectorized. The base indice that these offsets are
            calculated from is x0y0z0.
            */
            Vector256<uint> faceXNegVecIndice;
            Vector256<uint> faceYNegVecIndice;
            Vector256<uint> faceZNegVecIndice;
            Vector256<uint> faceXPosVecIndice;
            Vector256<uint> faceYPosVecIndice;
            Vector256<uint> faceZPosVecIndice;
            Vector256<uint> storeMask = Vector256.Create(int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue, 0u, 0u).AsUInt32();
            {
                uint x0y0z0 = (uint)GridToVP(vpSideLength, 1, 1, 1);
                uint x0y0z1 = (uint)GridToVP(vpSideLength, 1, 1, 2);
                uint x0y1z0 = (uint)GridToVP(vpSideLength, 1, 2, 1);
                uint x0y1z1 = (uint)GridToVP(vpSideLength, 1, 2, 2);
                uint x1y0z0 = (uint)GridToVP(vpSideLength, 2, 1, 1);
                uint x1y0z1 = (uint)GridToVP(vpSideLength, 2, 1, 2);
                uint x1y1z0 = (uint)GridToVP(vpSideLength, 2, 2, 1);
                uint x1y1z1 = (uint)GridToVP(vpSideLength, 2, 2, 2);

                faceXNegVecIndice = MakeFaceVectorIndices(x0y0z1, x0y0z0, x0y1z1, x0y1z0);
                faceYNegVecIndice = MakeFaceVectorIndices(x0y0z0, x0y0z1, x1y0z0, x1y0z1);
                faceZNegVecIndice = MakeFaceVectorIndices(x0y1z0, x0y0z0, x1y1z0, x1y0z0);
                faceXPosVecIndice = MakeFaceVectorIndices(x1y0z0, x1y0z1, x1y1z0, x1y1z1);
                faceYPosVecIndice = MakeFaceVectorIndices(x0y1z1, x0y1z0, x1y1z1, x1y1z0);
                faceZPosVecIndice = MakeFaceVectorIndices(x0y0z1, x0y1z1, x1y0z1, x1y1z1);
            }

            fixed (uint* indicesPtr = indices)
            {
                uint* indiceStore = indicesPtr;

                for (int z = 1; z < vpSideLength; z++)
                {
                    for (int y = 1; y < vpSideLength; y++)
                    {
                        int x = 1;

                        uint x0y0z0 = (uint)GridToVP(vpSideLength, x + 0, y + 0, z + 0);

                        int gridIdxCenter = PosToGridIndex(gridSideLength, x, y, z);
                        int gridIdxxn1 = PosToGridIndex(gridSideLength, x - 1, y, z);
                        int gridIdxyn1 = PosToGridIndex(gridSideLength, x, y - 1, z);
                        int gridIdxzn1 = PosToGridIndex(gridSideLength, x, y, z - 1);
                        int gridIdxxp1 = PosToGridIndex(gridSideLength, x + 1, y, z);
                        int gridIdxyp1 = PosToGridIndex(gridSideLength, x, y + 1, z);
                        int gridIdxzp1 = PosToGridIndex(gridSideLength, x, y, z + 1);

                        for (int i = 0; i < vpSideLength - 1; i++)
                        {
                            bool centerSign = gridSigns[gridIdxCenter + i];
                            if (!centerSign)
                            {
                                continue;
                            }

                            Vector256<uint> baseFaceIndex = Vector256.Create(x0y0z0 + (uint)i);

                            if (!gridSigns[gridIdxxn1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Avx2.Add(faceXNegVecIndice, baseFaceIndex), storeMask);
                            }
                            if (!gridSigns[gridIdxyn1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Avx2.Add(faceYNegVecIndice, baseFaceIndex), storeMask);
                            }
                            if (!gridSigns[gridIdxzn1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Avx2.Add(faceZNegVecIndice, baseFaceIndex), storeMask);
                            }

                            if (!gridSigns[gridIdxxp1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Avx2.Add(faceXPosVecIndice, baseFaceIndex), storeMask);
                            }
                            if (!gridSigns[gridIdxyp1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Avx2.Add(faceYPosVecIndice, baseFaceIndex), storeMask);
                            }
                            if (!gridSigns[gridIdxzp1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Avx2.Add(faceZPosVecIndice, baseFaceIndex), storeMask);
                            }
                        }
                    }
                }
            }
        }

        private void FillWithFaceVerticesAndRemoveDuplicateIndices(Span<uint> indices, Span<Vector3> vertices)
        {
            using (var indexConverterArr = new RentedArray<uint>(VoxelPoints.Length))
            {
                Span<uint> indexConverter = indexConverterArr.AsSpan();
                indexConverter.Fill(uint.MaxValue);

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
        }

        public GeometryData Triangulize()
        {
            int vertexCount = CountTruesWithPopCnt(IsUsingVoxelPoint);
            const int indicesPerTriangle = 3;
            int triangleIndiceCount = TriangleCount * indicesPerTriangle;
            GeometryData geoData = new GeometryData(vertexCount, triangleIndiceCount);

            FillWithFaceIndices(geoData.Indices);
            FillWithFaceVerticesAndRemoveDuplicateIndices(geoData.Indices, geoData.Vertices);
            Geometry.CalculateNormals(geoData.Vertices, geoData.Indices, geoData.Normals);

            return geoData;
        }
    }
}
