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
                float* noiseValues = stackalloc float[GenData.WeightGen.Seeds.GetSeedsCount()];
                float* baseNoiseValues = stackalloc float[GenData.WeightGen.Seeds.GetSeedsCount()];
                float* xNoiseDeltas = stackalloc float[GenData.WeightGen.Seeds.GetSeedsCount()];

                fixed (float* seedsPtr = GenData.WeightGen.Seeds.Seeds)
                {                    
                    Vector256<float> voxelSize = Vector256.Create(GenData.VoxelSize * GenData.WeightGen.NoiseFrequency);
                    for (int i = 0; i < GenData.WeightGen.Seeds.GetSeedsCount(); i += Vector256<float>.Count)
                    {
                        //Load 8 seed vectors
                        Vector256<float> s12 = Avx.LoadVector256(seedsPtr + i * 4 + Vector256<float>.Count * 0);
                        Vector256<float> s34 = Avx.LoadVector256(seedsPtr + i * 4 + Vector256<float>.Count * 1);
                        Vector256<float> s56 = Avx.LoadVector256(seedsPtr + i * 4 + Vector256<float>.Count * 2);
                        Vector256<float> s78 = Avx.LoadVector256(seedsPtr + i * 4 + Vector256<float>.Count * 3);
                        
                        
                        Vector256<float> ps12 = Avx.DotProduct(voxelSize, s12, 0b0001_1000);//[2,_,_,_,1,_,_,_]
                        Vector256<float> ps34 = Avx.DotProduct(voxelSize, s34, 0b0001_0100);//[_,4,_,_,_,3,_,_]
                        Vector256<float> ps56 = Avx.DotProduct(voxelSize, s56, 0b0001_0010);//[_,_,6,_,_,_,5,_]
                        Vector256<float> ps78 = Avx.DotProduct(voxelSize, s78, 0b0001_0001);//[_,_,_,8,_,_,_,7]

                        Vector256<float> ps1234 = Avx.Or(ps12, ps34);//[2,4,_,_,1,3,_,_]
                        Vector256<float> ps5678 = Avx.Or(ps56, ps78);//[_,_,6,8,_,_,5,7]
                        Vector256<float> ps = Avx.Or(ps1234, ps5678);//[2,4,6,8,1,3,5,7]

                        Avx.Store(xNoiseDeltas + i, ps);
                    } 

                    CosApproxConsts cosApprox = new CosApproxConsts(GenData.WeightGen.Seeds);
                      

                    Vector4 topLeftCorner = GetTopLeftCorner();
                    int index = 0;
                    for (int z = 0; z < GenData.GridSize; z++)
                    {
                        for (int y = 0; y < GenData.GridSize; y++)
                        {
                            Vector4 posdwa = (topLeftCorner - ToFloat128(0, y, z, 0).AsVector4() * GenData.VoxelSize) * GenData.WeightGen.NoiseFrequency;
                            Vector128<float> pos128 = posdwa.AsVector128();
                            Vector256<float> pos256 = Vector256.Create(pos128, pos128);

                            for (int i = 0; i < GenData.WeightGen.Seeds.GetSeedsCount(); i += Vector256<float>.Count)
                            {
                                //Load 8 seed vectors
                                Vector256<float> s12 = Avx.LoadVector256(seedsPtr + i * 4 + Vector256<float>.Count * 0);
                                Vector256<float> s34 = Avx.LoadVector256(seedsPtr + i * 4 + Vector256<float>.Count * 1);
                                Vector256<float> s56 = Avx.LoadVector256(seedsPtr + i * 4 + Vector256<float>.Count * 2);
                                Vector256<float> s78 = Avx.LoadVector256(seedsPtr + i * 4 + Vector256<float>.Count * 3);
                                
                                
                                Vector256<float> ps12 = Avx.DotProduct(pos256, s12, 0b0111_1000);//[2,_,_,_,1,_,_,_]
                                Vector256<float> ps34 = Avx.DotProduct(pos256, s34, 0b0111_0100);//[_,4,_,_,_,3,_,_]
                                Vector256<float> ps56 = Avx.DotProduct(pos256, s56, 0b0111_0010);//[_,_,6,_,_,_,5,_]
                                Vector256<float> ps78 = Avx.DotProduct(pos256, s78, 0b0111_0001);//[_,_,_,8,_,_,_,7]

                                Vector256<float> ps1234 = Avx.Or(ps12, ps34);//[2,4,_,_,1,3,_,_]
                                Vector256<float> ps5678 = Avx.Or(ps56, ps78);//[_,_,6,8,_,_,5,7]
                                Vector256<float> ps = Avx.Or(ps1234, ps5678);//[2,4,6,8,1,3,5,7]

                                Avx.Store(noiseValues + i, ps);
                                Avx.Store(baseNoiseValues + i, ps);
                            }   

                            for (int x = 0; x < GenData.GridSize; x++)
                            {
                                Vector4 pos = topLeftCorner - ToFloat128(x, y, z, 0).AsVector4() * GenData.VoxelSize;

                                float noise = GenData.WeightGen.GenerateWeight(pos, noiseValues, cosApprox);
                                GridSign[index++] = noise > 0.0f;

                                for (int i = 0; i < GenData.WeightGen.Seeds.GetSeedsCount(); i += Vector256<float>.Count)
                                {
                                    Vector256<float> baseNoises = Avx.LoadVector256(baseNoiseValues + i);
                                    Vector256<float> xDeltas = Avx.LoadVector256(xNoiseDeltas + i);

                                    Vector256<float> correctedNoise = Avx.Subtract(baseNoises, xDeltas);
                                    
                                    Avx.Store(noiseValues + i, correctedNoise);
                                    Avx.Store(baseNoiseValues + i, correctedNoise);
                                }
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

            unsafe
            {
                void MakeFaceVectors(out Vector128<uint> face128, out Vector128<uint> face64, uint a, uint b, uint c, uint d)
                {
                    face128 = Vector128.Create(c, a, b, b);
                    face64 = Vector128.Create(d, c, 0, 0);
                }

                Vector128<uint> faceXNeg128;
                Vector128<uint> faceXNeg64;
                Vector128<uint> faceYNeg128;
                Vector128<uint> faceYNeg64;
                Vector128<uint> faceZNeg128;
                Vector128<uint> faceZNeg64;
                Vector128<uint> faceXPos128;
                Vector128<uint> faceXPos64;
                Vector128<uint> faceYPos128;
                Vector128<uint> faceYPos64;
                Vector128<uint> faceZPos128;
                Vector128<uint> faceZPos64;
                {
                    uint x0y0z0 = (uint)GridToVP(1, 1, 1);
                    uint x0y0z1 = (uint)GridToVP(1, 1, 2);
                    uint x0y1z0 = (uint)GridToVP(1, 2, 1);
                    uint x0y1z1 = (uint)GridToVP(1, 2, 2);
                    uint x1y0z0 = (uint)GridToVP(2, 1, 1);
                    uint x1y0z1 = (uint)GridToVP(2, 1, 2);
                    uint x1y1z0 = (uint)GridToVP(2, 2, 1);
                    uint x1y1z1 = (uint)GridToVP(2, 2, 2);

                    MakeFaceVectors(out faceXNeg128, out faceXNeg64, x0y0z1, x0y0z0, x0y1z1, x0y1z0);
                    MakeFaceVectors(out faceYNeg128, out faceYNeg64, x0y0z0, x0y0z1, x1y0z0, x1y0z1);
                    MakeFaceVectors(out faceZNeg128, out faceZNeg64, x0y1z0, x0y0z0, x1y1z0, x1y0z0);
                    MakeFaceVectors(out faceXPos128, out faceXPos64, x1y0z0, x1y0z1, x1y1z0, x1y1z1);
                    MakeFaceVectors(out faceYPos128, out faceYPos64, x0y1z1, x0y1z0, x1y1z1, x1y1z0);
                    MakeFaceVectors(out faceZPos128, out faceZPos64, x0y0z1, x0y1z1, x1y0z1, x1y1z1);
                }

                fixed(uint* indicesPtr = indices)
                {
                    void AddRectangleTriangles(uint* indices, Vector128<uint> firstFourFaceVertices, Vector128<uint> lastTwoFaceVertices)
                    {
                        Avx.Store(indices + indiceIndex, firstFourFaceVertices);
                        Avx.StoreLow((float*)(indices + indiceIndex + Vector128<uint>.Count), lastTwoFaceVertices.AsSingle());
                        indiceIndex += 6;
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
                                    AddRectangleTriangles(indicesPtr, Avx.Add(faceXNeg128, baseFaceIndex), Avx.Add(faceXNeg64, baseFaceIndex));
                                }
                                if (centerSign && !GridSign[gridIdxyn1 + i])
                                {
                                    AddRectangleTriangles(indicesPtr, Avx.Add(faceYNeg128, baseFaceIndex), Avx.Add(faceYNeg64, baseFaceIndex));
                                }
                                if (centerSign && !GridSign[gridIdxzn1 + i])
                                {
                                    AddRectangleTriangles(indicesPtr, Avx.Add(faceZNeg128, baseFaceIndex), Avx.Add(faceZNeg64, baseFaceIndex));
                                }

                                if (centerSign && !GridSign[gridIdxxp1 + i])
                                {
                                    AddRectangleTriangles(indicesPtr, Avx.Add(faceXPos128, baseFaceIndex), Avx.Add(faceXPos64, baseFaceIndex));
                                }
                                if (centerSign && !GridSign[gridIdxyp1 + i])
                                {
                                    AddRectangleTriangles(indicesPtr, Avx.Add(faceYPos128, baseFaceIndex), Avx.Add(faceYPos64, baseFaceIndex));
                                }
                                if (centerSign && !GridSign[gridIdxzp1 + i])
                                {
                                    AddRectangleTriangles(indicesPtr, Avx.Add(faceZPos128, baseFaceIndex), Avx.Add(faceZPos64, baseFaceIndex));
                                }
                            }
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
