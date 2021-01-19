using OpenGL;
using System;
using System.Buffers;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelWorld
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
            this.GenData = voxelSystemData;
            this.GridCenter = center;

            this.GridSign = new bool[GenData.GridSize * GenData.GridSize * GenData.GridSize];
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
            return new BitArray(GridSign);
        }

        public void Restore(BitArray compressedGrid)
        {
            compressedGrid.CopyTo(GridSign, 0);
        }

        private Vector4 GetTopLeftCorner()
        {
            float distanceFromCenter = (((float)GenData.GridSize - 1.0f) / 2.0f) * GenData.VoxelSize;
            return new Vector4(GridCenter + new Vector3(distanceFromCenter, distanceFromCenter, distanceFromCenter), 0.0f);
        }

        public unsafe void Randomize()
        {
            Vector128<float> ToFloat128(int x, int y, int z, int w)
            {
                return Avx.ConvertToVector128Single(Vector128.Create(x, y, z, w));
            }

            if (Avx.IsSupported)
            {
                float* baseNoiseValues = stackalloc float[GenData.WeightGen.Seeds.GetSeedsCount()];
                float* xNoiseDeltas = stackalloc float[GenData.WeightGen.Seeds.GetSeedsCount()];
                float* noiseValues = stackalloc float[GenData.WeightGen.Seeds.GetSeedsCount()];

                fixed (float* seedsPtr = GenData.WeightGen.Seeds.Seeds)
                {     
                    SeededNoiseStorage seedStorage = new SeededNoiseStorage(GenData.WeightGen.Seeds, seedsPtr, baseNoiseValues, xNoiseDeltas, noiseValues);

                    Vector256<float> voxelSize = Vector256.Create(GenData.VoxelSize * GenData.WeightGen.NoiseFrequency);
                    seedStorage.BaseSeededXDiff(voxelSize);

                    CosApproxConsts cosApprox = new CosApproxConsts(GenData.WeightGen.Seeds);
                      

                    Vector4 topLeftCorner = GetTopLeftCorner();
                    int index = 0;
                    for (int z = 0; z < GenData.GridSize; z++)
                    {
                        for (int y = 0; y < GenData.GridSize; y++)
                        {
                            Vector4 voxelPos = (topLeftCorner - ToFloat128(0, y, z, 0).AsVector4() * GenData.VoxelSize) * GenData.WeightGen.NoiseFrequency;
                            Vector128<float> voxelPos128 = voxelPos.AsVector128();
                            Vector256<float> voxelPos256 = Vector256.Create(voxelPos128, voxelPos128);
                            seedStorage.MakeSeededBaseNoise(voxelPos256);

                            for (int x = 0; x < GenData.GridSize; x++)
                            {
                                Vector4 pos = topLeftCorner - ToFloat128(x, y, z, 0).AsVector4() * GenData.VoxelSize;

                                float noise = GenData.WeightGen.GenerateWeight(pos, seedStorage, cosApprox);
                                GridSign[index++] = noise > 0.0f;

                                seedStorage.UpdateSeededNoiseWithXPosChange();
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

            fixed(bool* gridSifsgnPtr = GridSign)
            {
                sbyte* gridSignPtr = (sbyte*)gridSifsgnPtr;
                fixed(bool* isUsingVoxelBoolPtr = IsUsingVoxelPoint)
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
                                    Vector128<sbyte> centerSigns = Avx.LoadVector128(gridSignPtr + gridIdxCenter + i);

                                    Vector128<sbyte> gsXNeg = Avx.LoadVector128(gridSignPtr + gridIdxxn1 + i);
                                    Vector128<sbyte> gsYNeg = Avx.LoadVector128(gridSignPtr + gridIdxyn1 + i);
                                    Vector128<sbyte> gsZNeg = Avx.LoadVector128(gridSignPtr + gridIdxzn1 + i);
                                    Vector128<sbyte> gsXPos = Avx.LoadVector128(gridSignPtr + gridIdxxp1 + i);
                                    Vector128<sbyte> gsYPos = Avx.LoadVector128(gridSignPtr + gridIdxyp1 + i);
                                    Vector128<sbyte> gsZPos = Avx.LoadVector128(gridSignPtr + gridIdxzp1 + i);

                                    Vector128<sbyte> faceXNeg = Avx.AndNot(gsXNeg, centerSigns);
                                    Vector128<sbyte> faceYNeg = Avx.AndNot(gsYNeg, centerSigns);
                                    Vector128<sbyte> faceZNeg = Avx.AndNot(gsZNeg, centerSigns);
                                    Vector128<sbyte> faceXPos = Avx.AndNot(gsXPos, centerSigns);
                                    Vector128<sbyte> faceYPos = Avx.AndNot(gsYPos, centerSigns);
                                    Vector128<sbyte> faceZPos = Avx.AndNot(gsZPos, centerSigns);

                                    //From the non vectorized version one can infer what face directions must be true
                                    //in order for the voxel being used. 
                                    Vector128<sbyte> orXNegYNeg = Avx.Or(faceXNeg, faceYNeg);
                                    Vector128<sbyte> orXNegYPos = Avx.Or(faceXNeg, faceYPos);
                                    Vector128<sbyte> orXPosYNeg = Avx.Or(faceXPos, faceYNeg);
                                    Vector128<sbyte> orXPosYPos = Avx.Or(faceXPos, faceYPos);
                                    //Pseudo: IsUsingVoxel[x] |= faceA | faceB | faceC
                                    Avx.Store(isUsingVoxelPtr + x0y0z0 + i, Avx.Or(Avx.LoadVector128(isUsingVoxelPtr + x0y0z0 + i), Avx.Or(orXNegYNeg, faceZNeg)));
                                    Avx.Store(isUsingVoxelPtr + x0y0z1 + i, Avx.Or(Avx.LoadVector128(isUsingVoxelPtr + x0y0z1 + i), Avx.Or(orXNegYNeg, faceZPos)));
                                    Avx.Store(isUsingVoxelPtr + x0y1z0 + i, Avx.Or(Avx.LoadVector128(isUsingVoxelPtr + x0y1z0 + i), Avx.Or(orXNegYPos, faceZNeg)));
                                    Avx.Store(isUsingVoxelPtr + x0y1z1 + i, Avx.Or(Avx.LoadVector128(isUsingVoxelPtr + x0y1z1 + i), Avx.Or(orXNegYPos, faceZPos)));
                                    Avx.Store(isUsingVoxelPtr + x1y0z0 + i, Avx.Or(Avx.LoadVector128(isUsingVoxelPtr + x1y0z0 + i), Avx.Or(orXPosYNeg, faceZNeg)));
                                    Avx.Store(isUsingVoxelPtr + x1y0z1 + i, Avx.Or(Avx.LoadVector128(isUsingVoxelPtr + x1y0z1 + i), Avx.Or(orXPosYNeg, faceZPos)));
                                    Avx.Store(isUsingVoxelPtr + x1y1z0 + i, Avx.Or(Avx.LoadVector128(isUsingVoxelPtr + x1y1z0 + i), Avx.Or(orXPosYPos, faceZNeg)));
                                    Avx.Store(isUsingVoxelPtr + x1y1z1 + i, Avx.Or(Avx.LoadVector128(isUsingVoxelPtr + x1y1z1 + i), Avx.Or(orXPosYPos, faceZPos)));

                                    //Need to count the number of faces so the triangle count can be updated.
                                    //Each face is represented as a bit here. The idea is the shift the bits
                                    //in the 6 xmm registers so no bits overlap. Or the registers together
                                    //and use popcnt instruction in order to count the number of bits.
                                    Vector128<sbyte> faceXNegShift = Avx.ShiftLeftLogical(faceXNeg.AsInt16(), 0).AsSByte();
                                    Vector128<sbyte> faceYNegShift = Avx.ShiftLeftLogical(faceYNeg.AsInt16(), 1).AsSByte();
                                    Vector128<sbyte> faceZNegShift = Avx.ShiftLeftLogical(faceZNeg.AsInt16(), 2).AsSByte();
                                    Vector128<sbyte> faceXPosShift = Avx.ShiftLeftLogical(faceXPos.AsInt16(), 3).AsSByte();
                                    Vector128<sbyte> faceYPosShift = Avx.ShiftLeftLogical(faceYPos.AsInt16(), 4).AsSByte();
                                    Vector128<sbyte> faceZPosShift = Avx.ShiftLeftLogical(faceZPos.AsInt16(), 5).AsSByte();

                                    //Or the registers together
                                    Vector128<sbyte> bitPerFace = Avx.Or(Avx.Or(Avx.Or(faceXNegShift, faceYNegShift), Avx.Or(faceZNegShift, faceXPosShift)), Avx.Or(faceYPosShift, faceZPosShift));

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

        private void FillWithFaceIndices(Span<uint> indices)
        {
            int GridToVP(int x, int y, int z)
            {
                return (z - 1) * (GenData.GridSize - 1) * (GenData.GridSize - 1) + (y - 1) * (GenData.GridSize - 1) + (x - 1);
            }

            int PosToGridIndex(int x, int y, int z)
            {
                return z * GenData.GridSize * GenData.GridSize + y * GenData.GridSize + x;
            }

            void MakeFaceVectorIndices(out Vector128<uint> first4FaceIndices, out Vector128<uint> last2FaceIndices, uint a, uint b, uint c, uint d)
            {
                first4FaceIndices = Vector128.Create(c, a, b, b);
                last2FaceIndices = Vector128.Create(d, c, 0, 0);
            }


            /*
            Each face consists of 6 indices which creates two triangles that
            together form a rectangle representing the face. Calculaing the
            6 different vertex indices for each face is slow. For a given face
            direction, the face indices are always a constant value different
            from a given indice. Therefore The 6 indices can be calculated 
            by only knowing the base vertex indice and the offsets to the
            other indices.
            The code below makes these offsets for each of the 6 indices
            and stores them in two vectors so calculating/storing the indices
            can be vectorized. The base indice that these offsets are
            calculated from is x0y0z0.
            */
            Vector128<uint> faceXNegVecIndice1234;
            Vector128<uint> faceXNegVecIndice56;
            Vector128<uint> faceYNegVecIndice1234;
            Vector128<uint> faceYNegVecIndice56;
            Vector128<uint> faceZNegVecIndice1234;
            Vector128<uint> faceZNegVecIndice56;
            Vector128<uint> faceXPosVecIndice1234;
            Vector128<uint> faceXPosVecIndice56;
            Vector128<uint> faceYPosVecIndice1234;
            Vector128<uint> faceYPosVecIndice56;
            Vector128<uint> faceZPosVecIndice1234;
            Vector128<uint> faceZPosVecIndice56;
            {
                uint x0y0z0 = (uint)GridToVP(1, 1, 1);
                uint x0y0z1 = (uint)GridToVP(1, 1, 2);
                uint x0y1z0 = (uint)GridToVP(1, 2, 1);
                uint x0y1z1 = (uint)GridToVP(1, 2, 2);
                uint x1y0z0 = (uint)GridToVP(2, 1, 1);
                uint x1y0z1 = (uint)GridToVP(2, 1, 2);
                uint x1y1z0 = (uint)GridToVP(2, 2, 1);
                uint x1y1z1 = (uint)GridToVP(2, 2, 2);

                MakeFaceVectorIndices(out faceXNegVecIndice1234, out faceXNegVecIndice56, x0y0z1, x0y0z0, x0y1z1, x0y1z0);
                MakeFaceVectorIndices(out faceYNegVecIndice1234, out faceYNegVecIndice56, x0y0z0, x0y0z1, x1y0z0, x1y0z1);
                MakeFaceVectorIndices(out faceZNegVecIndice1234, out faceZNegVecIndice56, x0y1z0, x0y0z0, x1y1z0, x1y0z0);
                MakeFaceVectorIndices(out faceXPosVecIndice1234, out faceXPosVecIndice56, x1y0z0, x1y0z1, x1y1z0, x1y1z1);
                MakeFaceVectorIndices(out faceYPosVecIndice1234, out faceYPosVecIndice56, x0y1z1, x0y1z0, x1y1z1, x1y1z0);
                MakeFaceVectorIndices(out faceZPosVecIndice1234, out faceZPosVecIndice56, x0y0z1, x0y1z1, x1y0z1, x1y1z1);
            }

            int indiceIndex = 0;
            unsafe
            {
                fixed(uint* indicesPtr = indices)
                {
                    void AddFaceIndices(uint* indices, Vector128<uint> firstFourFaceVertices, Vector128<uint> lastTwoFaceVertices)
                    {
                        Avx.Store(indices + indiceIndex, firstFourFaceVertices);
                        Avx.StoreLow((float*)(indices + indiceIndex + Vector128<uint>.Count), lastTwoFaceVertices.AsSingle());

                        const int indicesPerFace = 6;
                        indiceIndex += indicesPerFace;
                    }

                    for (int z = 1; z < GenData.GridSize - 1; z++)
                    {
                        for (int y = 1; y < GenData.GridSize - 1; y++)
                        {
                            int x = 1;

                            uint x0y0z0 = (uint)GridToVP(x + 0, y + 0, z + 0);

                            int gridIdxCenter = PosToGridIndex(x, y, z);
                            int gridIdxxn1 = PosToGridIndex(x - 1, y, z);
                            int gridIdxyn1 = PosToGridIndex(x, y - 1, z);
                            int gridIdxzn1 = PosToGridIndex(x, y, z - 1);
                            int gridIdxxp1 = PosToGridIndex(x + 1, y, z);
                            int gridIdxyp1 = PosToGridIndex(x, y + 1, z);
                            int gridIdxzp1 = PosToGridIndex(x, y, z + 1);

                            for (int i = 0; i < GenData.GridSize - 2; i++)
                            {
                                bool centerSign = GridSign[gridIdxCenter + i];
                                if (!centerSign)
                                {
                                    continue;
                                }

                                Vector128<uint> baseFaceIndex = Vector128.Create(x0y0z0 + (uint)i);

                                if (centerSign && !GridSign[gridIdxxn1 + i])
                                {
                                    AddFaceIndices(indicesPtr, Avx.Add(faceXNegVecIndice1234, baseFaceIndex), Avx.Add(faceXNegVecIndice56, baseFaceIndex));
                                }
                                if (centerSign && !GridSign[gridIdxyn1 + i])
                                {
                                    AddFaceIndices(indicesPtr, Avx.Add(faceYNegVecIndice1234, baseFaceIndex), Avx.Add(faceYNegVecIndice56, baseFaceIndex));
                                }
                                if (centerSign && !GridSign[gridIdxzn1 + i])
                                {
                                    AddFaceIndices(indicesPtr, Avx.Add(faceZNegVecIndice1234, baseFaceIndex), Avx.Add(faceZNegVecIndice56, baseFaceIndex));
                                }

                                if (centerSign && !GridSign[gridIdxxp1 + i])
                                {
                                    AddFaceIndices(indicesPtr, Avx.Add(faceXPosVecIndice1234, baseFaceIndex), Avx.Add(faceXPosVecIndice56, baseFaceIndex));
                                }
                                if (centerSign && !GridSign[gridIdxyp1 + i])
                                {
                                    AddFaceIndices(indicesPtr, Avx.Add(faceYPosVecIndice1234, baseFaceIndex), Avx.Add(faceYPosVecIndice56, baseFaceIndex));
                                }
                                if (centerSign && !GridSign[gridIdxzp1 + i])
                                {
                                    AddFaceIndices(indicesPtr, Avx.Add(faceZPosVecIndice1234, baseFaceIndex), Avx.Add(faceZPosVecIndice56, baseFaceIndex));
                                }
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
