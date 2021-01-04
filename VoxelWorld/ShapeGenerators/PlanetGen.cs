using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelWorld
{
    internal readonly struct PlanetGen
    {
        internal readonly SeedsInfo Seeds;
        private readonly float PlanetRadius;
        private readonly float NoiseWeight;
        private readonly float NoiseFrequency;
        private const int SEED_COUNT = 32;
        private const int TURBULENCE_COUNT = 16;

        public PlanetGen(int seed, float planetRadius, float noiseWeight, float noiseFrequency)
        {
            this.Seeds = new SeedsInfo(seed, SEED_COUNT);
            this.PlanetRadius = planetRadius;
            this.NoiseWeight = noiseWeight;
            this.NoiseFrequency = noiseFrequency;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe float GenerateWeight(Vector4 pos, float* seedsPtr, float* prods)
        {
            float sphere = SphereGen.GetValue(pos, PlanetRadius);
            float noise = Turbulence(pos * NoiseFrequency, seedsPtr, prods, sphere);

            return noise + sphere;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe float Turbulence(Vector4 pos, float* seedsPtr, float* prods, float sphereValue)
        {
            bool firstNoiseGen = true;
            float noiseSum = 0.0f;
            float scale = 2.0f * NoiseWeight;
            for (int q = 0; q < TURBULENCE_COUNT; q++)
            {
                if (MathF.Abs(sphereValue + noiseSum) > scale)
                {
                    break;
                }

                if (firstNoiseGen)
                {
                    firstNoiseGen = false;

                    Vector128<float> pos128 = pos.AsVector128();
                    Vector256<float> pos256 = Vector256.Create(pos128, pos128);

                    int index = 0;
                    for (int i = 0; i < Seeds.Seeds.Length; i += Vector256<float>.Count * 4)
                    {
                        //Load 8 seed vectors
                        Vector256<float> s12 = Avx.LoadVector256(seedsPtr + i + Vector256<float>.Count * 0);
                        Vector256<float> s34 = Avx.LoadVector256(seedsPtr + i + Vector256<float>.Count * 1);
                        Vector256<float> s56 = Avx.LoadVector256(seedsPtr + i + Vector256<float>.Count * 2);
                        Vector256<float> s78 = Avx.LoadVector256(seedsPtr + i + Vector256<float>.Count * 3);
                        
                        
                        Vector256<float> ps12 = Avx.DotProduct(pos256, s12, 0b0111_1000);//[2,_,_,_,1,_,_,_]
                        Vector256<float> ps34 = Avx.DotProduct(pos256, s34, 0b0111_0100);//[_,4,_,_,_,3,_,_]
                        Vector256<float> ps56 = Avx.DotProduct(pos256, s56, 0b0111_0010);//[_,_,6,_,_,_,5,_]
                        Vector256<float> ps78 = Avx.DotProduct(pos256, s78, 0b0111_0001);//[_,_,_,8,_,_,_,7]

                        Vector256<float> ps1234 = Avx.Or(ps12, ps34);//[2,4,_,_,1,3,_,_]
                        Vector256<float> ps5678 = Avx.Or(ps56, ps78);//[_,_,6,8,_,_,5,7]
                        Vector256<float> ps = Avx.Or(ps1234, ps5678);//[2,4,6,8,1,3,5,7]

                        Avx.Store(prods + index, ps);
                        index += Vector256<float>.Count;
                    }
                }
                else
                {
                    for (int i = 0; i < Seeds.Seeds.Length / 4; i += Vector256<float>.Count)
                    {
                        var fgre = Avx.LoadVector256(prods + i);
                        Avx.Store(prods + i, Avx.Add(fgre, fgre));
                    }
                }

                scale *= 0.5f;
                noiseSum += scale * XYZRandomGen.GetNoise(Seeds, prods);
                pos *= 2.0f;
            }

            return noiseSum;
        }
    }
}
