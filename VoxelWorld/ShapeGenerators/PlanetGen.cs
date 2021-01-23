using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelWorld
{
    internal readonly struct CosApproxConsts
    {
        internal readonly Vector256<float> const0_25;
        //internal readonly Vector256<float> consttp;
        internal const float consttp = 1.0f / (2.0f * MathF.PI);
        private const float const16 = 16.0f;
        internal readonly Vector256<float> const0_5;
        internal readonly Vector256<float> const_noSign;
        internal readonly Vector256<float> seedsCountReci;

        internal CosApproxConsts(SeedsInfo seeds)
        {
            const0_25 = Vector256.Create(0.25f);
            //consttp = Vector256.Create(1.0f / (2.0f * MathF.PI));
            //const16 = Vector256.Create(16.0f);
            const0_5 = Vector256.Create(0.5f);
            const_noSign = Vector256.Create(0x7fffffff).AsSingle();
            seedsCountReci = Vector256.Create(seeds.Reci_SeedsCount);
        }

        internal Vector256<float> Cos(Vector256<float> x)
        {
            Vector256<float> cosApprox = x;
            cosApprox = Avx.Subtract(cosApprox, Avx.Add(const0_25, Avx.Floor(Avx.Add(cosApprox, const0_25))));
            return Avx.Multiply(cosApprox, Avx.Subtract(Avx.And(cosApprox, const_noSign), const0_5));
        }

        internal float HorizontalSum(Vector256<float> x)
        {
            Vector256<float> sum = Avx.DotProduct(x, seedsCountReci, 0b1111_0001);
            Vector128<float> lower = sum.GetLower();
            Vector128<float> upper = sum.GetUpper();
            return Avx.Add(lower, upper).GetElement(0) * const16;
        }
    }

    internal readonly struct PlanetGen
    {
        internal readonly SeedsInfo Seeds;
        private readonly float PlanetRadius;
        private readonly float NoiseWeight;
        internal readonly float NoiseFrequency;
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
        public unsafe float GenerateWeight(Vector4 pos, in SeededNoiseStorage seedStorage, in CosApproxConsts cosApprox)
        {
            float sphere = SphereGen.GetValue(pos, PlanetRadius);
            float noise = Turbulence(sphere, seedStorage, cosApprox);

            return noise;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe float Turbulence(float sphereValue, SeededNoiseStorage seedStorage, in CosApproxConsts cosApprox)
        {
            float noiseSum = sphereValue;
            float scale = 2.0f * NoiseWeight;
            for (int q = 0; q < TURBULENCE_COUNT; q++)
            {
                if (MathF.Abs(noiseSum) > scale)
                {
                    break;
                }
                scale *= 0.5f;

                Vector256<float> noise = Vector256<float>.Zero;
                for (int i = 0; i < seedStorage.SeedsCount; i += Vector256<float>.Count)
                {
                    //Load noise values
                    Vector256<float> noises = Avx.LoadVector256(seedStorage.TurbulentNoises + i);

                    //Cos approximation
                    Vector256<float> cosNoise = cosApprox.Cos(noises);

                    //Sum cos approximations
                    noise = Avx.Add(noise, cosNoise);

                    //Modify noise so the turbulence noise looks random
                    Vector256<float> turbulentNoise = Avx.Add(noises, noises);
                    Avx.Store(seedStorage.TurbulentNoises + i, turbulentNoise);
                }

                noiseSum += scale * cosApprox.HorizontalSum(noise);
            }

            return noiseSum;
        }
    }
}
