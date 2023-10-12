using System;
using System.Collections;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using VoxelWorld.ShapeGenerators;
using VoxelWorld.Voxel.System;

namespace VoxelWorld.Voxel.Grid
{
    internal readonly record struct UsedPointsBoxBoundary(VectorInt3 Min, VectorInt3 Max)
    {
        public const int BoxCornerCount = 8;

        public int XSideLength => Max.X - Min.X + 1;
        public int YSideLength => Max.Y - Min.Y + 1;
        public int ZSideLength => Max.Z - Min.Z + 1;

        public Span<VectorInt3> GetCorners(Span<VectorInt3> corners)
        {
            corners[0] = new VectorInt3(Min.X, Min.Y, Min.Z);
            corners[1] = new VectorInt3(Min.X, Min.Y, Max.Z);
            corners[2] = new VectorInt3(Min.X, Max.Y, Min.Z);
            corners[3] = new VectorInt3(Min.X, Max.Y, Max.Z);
            corners[4] = new VectorInt3(Max.X, Min.Y, Min.Z);
            corners[5] = new VectorInt3(Max.X, Min.Y, Max.Z);
            corners[6] = new VectorInt3(Max.X, Max.Y, Min.Z);
            corners[7] = new VectorInt3(Max.X, Max.Y, Max.Z);

            return corners;
        }

        public bool WithinBox(int x, int y, int z)
        {
            return x >= Min.X &&
                   y >= Min.Y &&
                   z >= Min.Z &&
                   x <= Max.X &&
                   y <= Max.Y &&
                   z <= Max.Z;
        }
    }

    internal readonly record struct VectorInt3(int X, int Y, int Z)
    {
        public Vector3 AsVector3() => new Vector3(X, Y, Z);
        public Vector4 AsVector4() => new Vector4(X, Y, Z, 0);
    }

    internal sealed class VoxelGrid
    {
        private Vector3 GridCenter;
        VoxelSystemData GenData;
        private readonly bool[] GridSign;
        private readonly bool[] IsUsingVoxelPoint;

        public VoxelGrid(Vector3 center, VoxelSystemData voxelSystemData)
        {
            GenData = voxelSystemData;
            GridCenter = center;

            GridSign = new bool[GenData.GridSize * GenData.GridSize * GenData.GridSize];
            IsUsingVoxelPoint = new bool[(GenData.GridSize - 1) * (GenData.GridSize - 1) * (GenData.GridSize - 1)];
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

        public UsedPointsBoxBoundary GetUsedPointsBox()
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            int maxZ = int.MinValue;

            int gridSideLength = GenData.GridSize;
            int vpSideLength = gridSideLength - 1;
            bool[] usedVoxelPoints = IsUsingVoxelPoint;
            int i = 0;
            for (int Z = 0; Z < vpSideLength; Z++)
            {
                for (int Y = 0; Y < vpSideLength; Y++)
                {
                    for (int X = 0; X < vpSideLength; X++)
                    {
                        if (!usedVoxelPoints[i++])
                        {
                            continue;
                        }

                        minX = Math.Min(minX, X);
                        minY = Math.Min(minY, Y);
                        minZ = Math.Min(minZ, Z);
                        maxX = Math.Max(maxX, X);
                        maxY = Math.Max(maxY, Y);
                        maxZ = Math.Max(maxZ, Z);
                    }
                }
            }

            return new UsedPointsBoxBoundary(new VectorInt3(minX, minY, minZ), new VectorInt3(maxX, maxY, maxZ));
        }

        public BoundingCircle GetBoundingCircle(UsedPointsBoxBoundary usedBoxPoints)
        {
            var topLeft = GetTopLeftCorner();
            Vector4 voxelSize = new Vector4(GenData.VoxelSize);
            Vector4 topLeftCorner = topLeft - voxelSize * 0.5f;
            Vector4 min = new Vector4(float.MaxValue);
            Vector4 max = new Vector4(float.MinValue);

            Span<VectorInt3> boxCorners = stackalloc VectorInt3[UsedPointsBoxBoundary.BoxCornerCount];
            foreach (VectorInt3 corner in usedBoxPoints.GetCorners(boxCorners))
            {
                Vector4 vp = topLeftCorner - corner.AsVector4() * voxelSize;
                min = Vector4.Min(min, vp);
                max = Vector4.Max(max, vp);
            }

            Vector4 center = (max + min) * 0.5f;
            float radius = (max - center).Length();
            return new BoundingCircle(new Vector3(center.X, center.Y, center.Z), radius);
        }

        public unsafe int PreCalculateGeometryData()
        {
            int GridToVP(int x, int y, int z)
            {
                return (z - 1) * (GenData.GridSize - 1) * (GenData.GridSize - 1) + (y - 1) * (GenData.GridSize - 1) + (x - 1);
            }

            int PosToGridIndex(int x, int y, int z)
            {
                return z * GenData.GridSize * GenData.GridSize + y * GenData.GridSize + x;
            }

            int triangleCount = 0;

            fixed (bool* gridSifsgnPtr = GridSign)
            {
                sbyte* gridSignPtr = (sbyte*)gridSifsgnPtr;
                fixed (bool* isUsingVoxelBoolPtr = IsUsingVoxelPoint)
                {
                    sbyte* isUsingVoxelPtr = (sbyte*)isUsingVoxelBoolPtr;
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
                            //Does the same as the non vectorized version but does it 16 points at a time
                            for (; i + Vector128<sbyte>.Count <= GenData.GridSize - 2; i += Vector128<sbyte>.Count)
                            {
                                Vector128<sbyte> centerSigns = Vector128.Load(gridSignPtr + gridIdxCenter + i);

                                Vector128<sbyte> gsXNeg = Vector128.Load(gridSignPtr + gridIdxxn1 + i);
                                Vector128<sbyte> gsYNeg = Vector128.Load(gridSignPtr + gridIdxyn1 + i);
                                Vector128<sbyte> gsZNeg = Vector128.Load(gridSignPtr + gridIdxzn1 + i);
                                Vector128<sbyte> gsXPos = Vector128.Load(gridSignPtr + gridIdxxp1 + i);
                                Vector128<sbyte> gsYPos = Vector128.Load(gridSignPtr + gridIdxyp1 + i);
                                Vector128<sbyte> gsZPos = Vector128.Load(gridSignPtr + gridIdxzp1 + i);

                                Vector128<sbyte> faceXNeg = Vector128.AndNot(centerSigns, gsXNeg);
                                Vector128<sbyte> faceYNeg = Vector128.AndNot(centerSigns, gsYNeg);
                                Vector128<sbyte> faceZNeg = Vector128.AndNot(centerSigns, gsZNeg);
                                Vector128<sbyte> faceXPos = Vector128.AndNot(centerSigns, gsXPos);
                                Vector128<sbyte> faceYPos = Vector128.AndNot(centerSigns, gsYPos);
                                Vector128<sbyte> faceZPos = Vector128.AndNot(centerSigns, gsZPos);

                                //From the non vectorized version one can infer what face directions must be true
                                //in order for the voxel being used. 
                                Vector128<sbyte> orXNegYNeg = Vector128.BitwiseOr(faceXNeg, faceYNeg);
                                Vector128<sbyte> orXNegYPos = Vector128.BitwiseOr(faceXNeg, faceYPos);
                                Vector128<sbyte> orXPosYNeg = Vector128.BitwiseOr(faceXPos, faceYNeg);
                                Vector128<sbyte> orXPosYPos = Vector128.BitwiseOr(faceXPos, faceYPos);
                                //Pseudo: IsUsingVoxel[x] |= faceA | faceB | faceC
                                Vector128.Store(Vector128.BitwiseOr(Vector128.Load(isUsingVoxelPtr + x0y0z0 + i), Vector128.BitwiseOr(orXNegYNeg, faceZNeg)), isUsingVoxelPtr + x0y0z0 + i);
                                Vector128.Store(Vector128.BitwiseOr(Vector128.Load(isUsingVoxelPtr + x0y0z1 + i), Vector128.BitwiseOr(orXNegYNeg, faceZPos)), isUsingVoxelPtr + x0y0z1 + i);
                                Vector128.Store(Vector128.BitwiseOr(Vector128.Load(isUsingVoxelPtr + x0y1z0 + i), Vector128.BitwiseOr(orXNegYPos, faceZNeg)), isUsingVoxelPtr + x0y1z0 + i);
                                Vector128.Store(Vector128.BitwiseOr(Vector128.Load(isUsingVoxelPtr + x0y1z1 + i), Vector128.BitwiseOr(orXNegYPos, faceZPos)), isUsingVoxelPtr + x0y1z1 + i);
                                Vector128.Store(Vector128.BitwiseOr(Vector128.Load(isUsingVoxelPtr + x1y0z0 + i), Vector128.BitwiseOr(orXPosYNeg, faceZNeg)), isUsingVoxelPtr + x1y0z0 + i);
                                Vector128.Store(Vector128.BitwiseOr(Vector128.Load(isUsingVoxelPtr + x1y0z1 + i), Vector128.BitwiseOr(orXPosYNeg, faceZPos)), isUsingVoxelPtr + x1y0z1 + i);
                                Vector128.Store(Vector128.BitwiseOr(Vector128.Load(isUsingVoxelPtr + x1y1z0 + i), Vector128.BitwiseOr(orXPosYPos, faceZNeg)), isUsingVoxelPtr + x1y1z0 + i);
                                Vector128.Store(Vector128.BitwiseOr(Vector128.Load(isUsingVoxelPtr + x1y1z1 + i), Vector128.BitwiseOr(orXPosYPos, faceZPos)), isUsingVoxelPtr + x1y1z1 + i);

                                //Need to count the number of faces so the triangle count can be updated.
                                //Each face is represented as a bit here. The idea is the shift the bits
                                //in the 6 xmm registers so no bits overlap. Or the registers together
                                //and use popcnt instruction in order to count the number of bits.
                                Vector128<sbyte> faceXNegShift = Vector128.ShiftLeft(faceXNeg.AsInt16(), 0).AsSByte();
                                Vector128<sbyte> faceYNegShift = Vector128.ShiftLeft(faceYNeg.AsInt16(), 1).AsSByte();
                                Vector128<sbyte> faceZNegShift = Vector128.ShiftLeft(faceZNeg.AsInt16(), 2).AsSByte();
                                Vector128<sbyte> faceXPosShift = Vector128.ShiftLeft(faceXPos.AsInt16(), 3).AsSByte();
                                Vector128<sbyte> faceYPosShift = Vector128.ShiftLeft(faceYPos.AsInt16(), 4).AsSByte();
                                Vector128<sbyte> faceZPosShift = Vector128.ShiftLeft(faceZPos.AsInt16(), 5).AsSByte();

                                //Or the registers together
                                Vector128<sbyte> bitPerFace = Vector128.BitwiseOr(Vector128.BitwiseOr(Vector128.BitwiseOr(faceXNegShift, faceYNegShift),
                                                                                                      Vector128.BitwiseOr(faceZNegShift, faceXPosShift)),
                                                                                  Vector128.BitwiseOr(faceYPosShift, faceZPosShift));

                                //Use popcnt on the two ulongs to count the bits.
                                //Each face consists of two triangles.
                                int faces = BitOperations.PopCount(bitPerFace.AsUInt64().GetElement(0)) + BitOperations.PopCount(bitPerFace.AsUInt64().GetElement(1));
                                triangleCount += faces * 2;
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
                                    triangleCount += 2;
                                }
                                if (centerSign && !GridSign[gridIdxyn1 + i])
                                {
                                    IsUsingVoxelPoint[x1y0z1 + i] = true;
                                    IsUsingVoxelPoint[x1y0z0 + i] = true;
                                    IsUsingVoxelPoint[x0y0z1 + i] = true;
                                    IsUsingVoxelPoint[x0y0z0 + i] = true;
                                    triangleCount += 2;
                                }
                                if (centerSign && !GridSign[gridIdxzn1 + i])
                                {
                                    IsUsingVoxelPoint[x0y0z0 + i] = true;
                                    IsUsingVoxelPoint[x0y1z0 + i] = true;
                                    IsUsingVoxelPoint[x1y0z0 + i] = true;
                                    IsUsingVoxelPoint[x1y1z0 + i] = true;
                                    triangleCount += 2;
                                }

                                if (centerSign && !GridSign[gridIdxxp1 + i])
                                {
                                    IsUsingVoxelPoint[x1y0z0 + i] = true;
                                    IsUsingVoxelPoint[x1y0z1 + i] = true;
                                    IsUsingVoxelPoint[x1y1z0 + i] = true;
                                    IsUsingVoxelPoint[x1y1z1 + i] = true;
                                    triangleCount += 2;
                                }
                                if (centerSign && !GridSign[gridIdxyp1 + i])
                                {
                                    IsUsingVoxelPoint[x1y1z1 + i] = true;
                                    IsUsingVoxelPoint[x1y1z0 + i] = true;
                                    IsUsingVoxelPoint[x0y1z1 + i] = true;
                                    IsUsingVoxelPoint[x0y1z0 + i] = true;
                                    triangleCount += 2;
                                }
                                if (centerSign && !GridSign[gridIdxzp1 + i])
                                {
                                    IsUsingVoxelPoint[x0y0z1 + i] = true;
                                    IsUsingVoxelPoint[x0y1z1 + i] = true;
                                    IsUsingVoxelPoint[x1y0z1 + i] = true;
                                    IsUsingVoxelPoint[x1y1z1 + i] = true;
                                    triangleCount += 2;
                                }
                            }
                        }
                    }
                }
            }

            return triangleCount;
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
                                indiceStore = AddFaceIndices(indiceStore, Vector256.Add(faceXNegVecIndice, baseFaceIndex), storeMask);
                            }
                            if (!gridSigns[gridIdxyn1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Vector256.Add(faceYNegVecIndice, baseFaceIndex), storeMask);
                            }
                            if (!gridSigns[gridIdxzn1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Vector256.Add(faceZNegVecIndice, baseFaceIndex), storeMask);
                            }

                            if (!gridSigns[gridIdxxp1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Vector256.Add(faceXPosVecIndice, baseFaceIndex), storeMask);
                            }
                            if (!gridSigns[gridIdxyp1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Vector256.Add(faceYPosVecIndice, baseFaceIndex), storeMask);
                            }
                            if (!gridSigns[gridIdxzp1 + i])
                            {
                                indiceStore = AddFaceIndices(indiceStore, Vector256.Add(faceZPosVecIndice, baseFaceIndex), storeMask);
                            }
                        }
                    }
                }
            }
        }

        private unsafe void FillWithNormals(Span<byte> normals)
        {
            static int GridToVP(int sideLength, int x, int y, int z)
            {
                return (z - 1) * sideLength * sideLength + (y - 1) * sideLength + (x - 1);
            }

            static int PosToGridIndex(int sideLength, int x, int y, int z)
            {
                return z * sideLength * sideLength + y * sideLength + x;
            }

            int gridSideLength = GenData.GridSize;
            int vpSideLength = gridSideLength - 1;
            bool[] gridSigns = GridSign;

            normals.Clear();

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
            int x0y0z0 = GridToVP(vpSideLength, 1, 1, 1);
            int x0y0z1 = GridToVP(vpSideLength, 1, 1, 2);
            int x0y1z0 = GridToVP(vpSideLength, 1, 2, 1);
            int x0y1z1 = GridToVP(vpSideLength, 1, 2, 2);
            int x1y0z0 = GridToVP(vpSideLength, 2, 1, 1);
            int x1y0z1 = GridToVP(vpSideLength, 2, 1, 2);
            int x1y1z0 = GridToVP(vpSideLength, 2, 2, 1);
            int x1y1z1 = GridToVP(vpSideLength, 2, 2, 2);

            fixed (bool* readonlyGridSigns = gridSigns)
            {
                fixed (byte* readonlyNormals = normals)
                {
                    bool* gridSignsPtr = readonlyGridSigns;
                    byte* gridSignsBytePtr = (byte*)readonlyGridSigns;
                    byte* normalsPtr = readonlyNormals;
                    for (int z = 1; z < vpSideLength; z++)
                    {
                        for (int y = 1; y < vpSideLength; y++)
                        {
                            int x = 1;

                            int basex0y0z0 = GridToVP(vpSideLength, x + 0, y + 0, z + 0);

                            int gridIdxCenter = PosToGridIndex(gridSideLength, x, y, z);
                            int gridIdxxn1 = PosToGridIndex(gridSideLength, x - 1, y, z);
                            int gridIdxyn1 = PosToGridIndex(gridSideLength, x, y - 1, z);
                            int gridIdxzn1 = PosToGridIndex(gridSideLength, x, y, z - 1);
                            int gridIdxxp1 = PosToGridIndex(gridSideLength, x + 1, y, z);
                            int gridIdxyp1 = PosToGridIndex(gridSideLength, x, y + 1, z);
                            int gridIdxzp1 = PosToGridIndex(gridSideLength, x, y, z + 1);

                            byte* baseNormalsPtr = normalsPtr + basex0y0z0;
                            byte* basegridSignsBytePtr = gridSignsBytePtr;

                            int i = 0;
                            for (; i + Vector128<byte>.Count < vpSideLength - 1; i += (Vector128<byte>.Count - 2))
                            {
                                Vector128<byte> centerSigns = Vector128.Load(gridSignsBytePtr + gridIdxCenter + i);

                                Vector128<byte> signIdxxp1 = Vector128.Load(basegridSignsBytePtr + gridIdxxp1);
                                Vector128<byte> signIdxxn1 = Vector128.Load(basegridSignsBytePtr + gridIdxxn1);
                                Vector128<byte> signIdxyp1 = Vector128.Load(basegridSignsBytePtr + gridIdxyp1);
                                Vector128<byte> signIdxyn1 = Vector128.Load(basegridSignsBytePtr + gridIdxyn1);
                                Vector128<byte> signIdxzp1 = Vector128.Load(basegridSignsBytePtr + gridIdxzp1);
                                Vector128<byte> signIdxzn1 = Vector128.Load(basegridSignsBytePtr + gridIdxzn1);

                                signIdxxp1 = Vector128.BitwiseAnd(Vector128.LessThan(signIdxxp1.AsSByte(), centerSigns.AsSByte()).AsByte(), Vector128.Create((byte)0b00_00_00_01));
                                signIdxxn1 = Vector128.BitwiseAnd(Vector128.LessThan(signIdxxn1.AsSByte(), centerSigns.AsSByte()).AsByte(), Vector128.Create((byte)0b00_00_00_10));
                                signIdxyp1 = Vector128.BitwiseAnd(Vector128.LessThan(signIdxyp1.AsSByte(), centerSigns.AsSByte()).AsByte(), Vector128.Create((byte)0b00_00_01_00));
                                signIdxyn1 = Vector128.BitwiseAnd(Vector128.LessThan(signIdxyn1.AsSByte(), centerSigns.AsSByte()).AsByte(), Vector128.Create((byte)0b00_00_10_00));
                                signIdxzp1 = Vector128.BitwiseAnd(Vector128.LessThan(signIdxzp1.AsSByte(), centerSigns.AsSByte()).AsByte(), Vector128.Create((byte)0b00_01_00_00));
                                signIdxzn1 = Vector128.BitwiseAnd(Vector128.LessThan(signIdxzn1.AsSByte(), centerSigns.AsSByte()).AsByte(), Vector128.Create((byte)0b00_10_00_00));

                                Vector128<byte> signIdxxn1xp1 = Vector128.BitwiseOr(signIdxxn1, signIdxxp1);
                                Vector128<byte> signIdxxn1xp1yn1 = Vector128.BitwiseOr(signIdxxn1xp1, signIdxyn1);
                                Vector128<byte> signIdxxn1xp1yp1 = Vector128.BitwiseOr(signIdxxn1xp1, signIdxyp1);
                                Vector128<byte> signIdxxn1xp1yn1zn1 = Vector128.BitwiseOr(signIdxxn1xp1yn1, signIdxzn1);
                                Vector128<byte> signIdxxn1xp1yn1zp1 = Vector128.BitwiseOr(signIdxxn1xp1yn1, signIdxzp1);
                                Vector128<byte> signIdxxn1xp1yp1zn1 = Vector128.BitwiseOr(signIdxxn1xp1yp1, signIdxzn1);
                                Vector128<byte> signIdxxn1xp1yp1zp1 = Vector128.BitwiseOr(signIdxxn1xp1yp1, signIdxzp1);

                                Vector128<byte> resultx0y0z0 = Vector128.Load(baseNormalsPtr + x0y0z0);
                                resultx0y0z0 = Vector128.BitwiseOr(resultx0y0z0, Sse2.ShiftLeftLogical128BitLane(resultx0y0z0, 1));
                                resultx0y0z0 = Vector128.BitwiseOr(resultx0y0z0, signIdxxn1xp1yn1zn1);
                                Vector128.Store(resultx0y0z0, baseNormalsPtr + x0y0z0);

                                Vector128<byte> resultx0y0z1 = Vector128.Load(baseNormalsPtr + x0y0z1);
                                resultx0y0z1 = Vector128.BitwiseOr(resultx0y0z1, Sse2.ShiftLeftLogical128BitLane(resultx0y0z1, 1));
                                resultx0y0z1 = Vector128.BitwiseOr(resultx0y0z1, signIdxxn1xp1yn1zp1);
                                Vector128.Store(resultx0y0z1, baseNormalsPtr + x0y0z1);

                                Vector128<byte> resultx0y1z0 = Vector128.Load(baseNormalsPtr + x0y1z0);
                                resultx0y1z0 = Vector128.BitwiseOr(resultx0y1z0, Sse2.ShiftLeftLogical128BitLane(resultx0y1z0, 1));
                                resultx0y1z0 = Vector128.BitwiseOr(resultx0y1z0, signIdxxn1xp1yp1zn1);
                                Vector128.Store(resultx0y1z0, baseNormalsPtr + x0y1z0);

                                Vector128<byte> resultx0y1z1 = Vector128.Load(baseNormalsPtr + x0y1z1);
                                resultx0y1z1 = Vector128.BitwiseOr(resultx0y1z1, Sse2.ShiftLeftLogical128BitLane(resultx0y1z1, 1));
                                resultx0y1z1 = Vector128.BitwiseOr(resultx0y1z1, signIdxxn1xp1yp1zp1);
                                Vector128.Store(resultx0y1z1, baseNormalsPtr + x0y1z1);

                                basegridSignsBytePtr += (Vector128<byte>.Count - 2);
                                baseNormalsPtr += (Vector128<byte>.Count - 2);
                            }

                            for (; i < vpSideLength - 1; i++)
                            {
                                bool centerSign = gridSignsPtr[gridIdxCenter + i];
                                if (!centerSign)
                                {
                                    continue;
                                }

                                if (!gridSigns[gridIdxxn1 + i])
                                {
                                    normals[basex0y0z0 + i + x0y0z1] |= 0b00_00_00_10;
                                    normals[basex0y0z0 + i + x0y0z0] |= 0b00_00_00_10;
                                    normals[basex0y0z0 + i + x0y1z1] |= 0b00_00_00_10;
                                    normals[basex0y0z0 + i + x0y1z0] |= 0b00_00_00_10;
                                }
                                if (!gridSigns[gridIdxyn1 + i])
                                {
                                    normals[basex0y0z0 + i + x0y0z0] |= 0b00_00_10_00;
                                    normals[basex0y0z0 + i + x0y0z1] |= 0b00_00_10_00;
                                    normals[basex0y0z0 + i + x1y0z0] |= 0b00_00_10_00;
                                    normals[basex0y0z0 + i + x1y0z1] |= 0b00_00_10_00;
                                }
                                if (!gridSigns[gridIdxzn1 + i])
                                {
                                    normals[basex0y0z0 + i + x0y1z0] |= 0b00_10_00_00;
                                    normals[basex0y0z0 + i + x0y0z0] |= 0b00_10_00_00;
                                    normals[basex0y0z0 + i + x1y1z0] |= 0b00_10_00_00;
                                    normals[basex0y0z0 + i + x1y0z0] |= 0b00_10_00_00;
                                }

                                if (!gridSigns[gridIdxxp1 + i])
                                {
                                    normals[basex0y0z0 + i + x1y0z0] |= 0b00_00_00_01;
                                    normals[basex0y0z0 + i + x1y0z1] |= 0b00_00_00_01;
                                    normals[basex0y0z0 + i + x1y1z0] |= 0b00_00_00_01;
                                    normals[basex0y0z0 + i + x1y1z1] |= 0b00_00_00_01;
                                }
                                if (!gridSigns[gridIdxyp1 + i])
                                {
                                    normals[basex0y0z0 + i + x0y1z1] |= 0b00_00_01_00;
                                    normals[basex0y0z0 + i + x0y1z0] |= 0b00_00_01_00;
                                    normals[basex0y0z0 + i + x1y1z1] |= 0b00_00_01_00;
                                    normals[basex0y0z0 + i + x1y1z0] |= 0b00_00_01_00;
                                }
                                if (!gridSigns[gridIdxzp1 + i])
                                {
                                    normals[basex0y0z0 + i + x0y0z1] |= 0b00_01_00_00;
                                    normals[basex0y0z0 + i + x0y1z1] |= 0b00_01_00_00;
                                    normals[basex0y0z0 + i + x1y0z1] |= 0b00_01_00_00;
                                    normals[basex0y0z0 + i + x1y1z1] |= 0b00_01_00_00;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void UpdateIndicesToMatchBox(Span<uint> indices, UsedPointsBoxBoundary usedBoxPoints)
        {
            uint xSideLength = (uint)(usedBoxPoints.Max.X - usedBoxPoints.Min.X) + 1;
            uint ySideLength = (uint)(usedBoxPoints.Max.Y - usedBoxPoints.Min.Y) + 1;

            int gridSideLength = GenData.GridSize;
            uint vpSideLength = (uint)gridSideLength - 1;
            for (int i = 0; i < indices.Length; i++)
            {
                uint x = indices[i] % vpSideLength;
                uint y = (indices[i] / vpSideLength) % vpSideLength;
                uint z = indices[i] / (vpSideLength * vpSideLength);

                indices[i] = (x - (uint)usedBoxPoints.Min.X)
                           + (y - (uint)usedBoxPoints.Min.Y) * xSideLength
                           + (z - (uint)usedBoxPoints.Min.Z) * xSideLength * ySideLength;
            }
        }

        private void CopyNormalsToBoxSize(Span<byte> allNormals, Span<byte> boxNormals, UsedPointsBoxBoundary usedBoxPoints)
        {
            int gridSideLength = GenData.GridSize;
            int vpSideLength = gridSideLength - 1;
            int allNormalsIndex = 0;
            int boxNormalsIndex = 0;
            for (int z = 0; z < vpSideLength; z++)
            {
                for (int y = 0; y < vpSideLength; y++)
                {
                    for (int x = 0; x < vpSideLength; x++)
                    {
                        if (!usedBoxPoints.WithinBox(x, y, z))
                        {
                            allNormalsIndex++;
                            continue;
                        }

                        boxNormals[boxNormalsIndex] = allNormals[allNormalsIndex];

                        allNormalsIndex++;
                        boxNormalsIndex++;
                    }
                }
            }
        }

        public GeometryData Triangulize(UsedPointsBoxBoundary usedBoxPoints, int triangleCount)
        {
            var topLeft = GetTopLeftCorner();
            Vector3 topLeftCorner = new Vector3(topLeft.X, topLeft.Y, topLeft.Z) - new Vector3(GenData.VoxelSize) * 0.5f;

            const int indicesPerTriangle = 3;
            int triangleIndiceCount = triangleCount * indicesPerTriangle;
            GeometryData geoData = new GeometryData(topLeftCorner, GenData.VoxelSize, new Vector3(GenData.GridSize - 1), usedBoxPoints.XSideLength * usedBoxPoints.YSideLength * usedBoxPoints.ZSideLength, triangleIndiceCount);

            FillWithFaceIndices(geoData.Indices);
            UpdateIndicesToMatchBox(geoData.Indices, usedBoxPoints);

            using (var allNormals = new RentedArray<byte>(IsUsingVoxelPoint.Length))
            {
                FillWithNormals(allNormals.AsSpan());
                CopyNormalsToBoxSize(allNormals.AsSpan(), geoData.Normals, usedBoxPoints);
            }

            geoData.GridTopLeftPosition = topLeftCorner - usedBoxPoints.Min.AsVector3() * new Vector3(GenData.VoxelSize);
            geoData.Size = usedBoxPoints.Max.AsVector3() - usedBoxPoints.Min.AsVector3() + Vector3.One;

            return geoData;
        }
    }
}
