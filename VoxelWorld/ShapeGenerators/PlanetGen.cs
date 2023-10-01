using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelWorld.ShapeGenerators
{
    internal unsafe readonly struct CosApproxConsts
    {
        private readonly float* Seeds;
        private readonly float* BaseNoises;
        private readonly float* XNoiseDiffs;
        private readonly float* TurbulentNoises;
        private readonly int SeedsCount;
        private readonly Vector4 PosMultiplier;

        private readonly Vector256<float> const0_25;
        private const float consttp = 1.0f / (2.0f * MathF.PI);
        private const float const16 = 16.0f;
        private readonly Vector256<float> const0_5;
        private readonly Vector256<float> const_noSign;
        private readonly Vector128<float> seedsCountReci;

        internal CosApproxConsts(SeedsInfo seedsInfo, float noiseFrequency, float* seeds, float* stackSpace)
        {
            const0_25 = Vector256.Create(0.25f);
            const0_5 = Vector256.Create(0.5f);
            const_noSign = Vector256.Create(0x7fffffff).AsSingle();
            seedsCountReci = Vector128.Create(seedsInfo.Reci_SeedsCount * const16);
            PosMultiplier = new Vector4(noiseFrequency * consttp);

            Seeds = seeds;
            SeedsCount = seedsInfo.GetSeedsCount();
            BaseNoises = stackSpace + SeedsCount * 0;
            XNoiseDiffs = stackSpace + SeedsCount * 1;
            TurbulentNoises = stackSpace + SeedsCount * 2;

        }

        internal static int StackSpaceNeeded(SeedsInfo seedsInfo)
        {
            return seedsInfo.GetSeedsCount() * 3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector256<float> Cos(Vector256<float> x)
        {
            Vector256<float> cosApprox = Vector256.Subtract(Vector256.Subtract(x, const0_25), Vector256.Floor(Vector256.Add(x, const0_25)));
            return Avx.Multiply(cosApprox, Vector256.Subtract(Vector256.BitwiseAnd(cosApprox, const_noSign), const0_5));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float HorizontalSum(Vector256<float> x)
        {
            Vector128<float> lower = x.GetLower();
            Vector128<float> upper = x.GetUpper();
            Vector128<float> sum = Vector128.Add(lower, upper);

            return Sse41.DotProduct(sum, seedsCountReci, 0b1111_0001).GetElement(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MakeSeededBaseNoise(Vector4 dotWith)
        {
            CalculateSeedsDotProducts(dotWith, BaseNoises);
        }

        internal void BaseSeededXDiff(Vector4 dotWith)
        {
            CalculateSeedsDotProducts(dotWith, XNoiseDiffs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CalculateSeedsDotProducts(Vector4 dotWith, float* storeDots)
        {
            float* seedsPtr = Seeds;
            Vector128<float> dotWith128 = (dotWith * PosMultiplier).AsVector128();
            Vector256<float> dotWith256 = Vector256.Create(dotWith128, dotWith128);
            for (int i = 0; i < SeedsCount; i += Vector256<float>.Count)
            {
                //Load 8 seed vectors
                Vector256<float> s12 = Vector256.Load(seedsPtr + Vector256<float>.Count * 0);
                Vector256<float> s34 = Vector256.Load(seedsPtr + Vector256<float>.Count * 1);
                Vector256<float> s56 = Vector256.Load(seedsPtr + Vector256<float>.Count * 2);
                Vector256<float> s78 = Vector256.Load(seedsPtr + Vector256<float>.Count * 3);

                Vector256<float> ps12 = Avx.DotProduct(dotWith256, s12, 0b0111_1000);//[2,_,_,_,1,_,_,_]
                Vector256<float> ps34 = Avx.DotProduct(dotWith256, s34, 0b0111_0100);//[_,4,_,_,_,3,_,_]
                Vector256<float> ps56 = Avx.DotProduct(dotWith256, s56, 0b0111_0010);//[_,_,6,_,_,_,5,_]
                Vector256<float> ps78 = Avx.DotProduct(dotWith256, s78, 0b0111_0001);//[_,_,_,8,_,_,_,7]

                Vector256<float> ps1234 = Vector256.BitwiseOr(ps12, ps34);//[2,4,_,_,1,3,_,_]
                Vector256<float> ps5678 = Vector256.BitwiseOr(ps56, ps78);//[_,_,6,8,_,_,5,7]
                Vector256<float> ps = Vector256.BitwiseOr(ps1234, ps5678);//[2,4,6,8,1,3,5,7]

                Vector256.Store(ps, storeDots + i);
                seedsPtr += Vector256<float>.Count * 4;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdateSeededNoiseWithXPosChange()
        {
            float* baseNoises = BaseNoises;
            float* xDiff = XNoiseDiffs;
            for (int i = 0; i < SeedsCount; i += Vector256<float>.Count)
            {
                Vector256<float> baseNoise = Vector256.Load(baseNoises + i);
                Vector256<float> xDelta = Vector256.Load(xDiff + i);

                Vector256<float> correctedNoise = Vector256.Subtract(baseNoise, xDelta);

                Vector256.Store(correctedNoise, baseNoises + i);
            }
        }

        private const int TURBULENCE_COUNT = 16;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal float Turbulence(float sphereValue, float noiseWeight)
        {
            float* loadNoises = BaseNoises;
            float* storeNoise = TurbulentNoises;

            float noiseSum = sphereValue;
            float scale = 2.0f * noiseWeight;
            for (int q = 0; q < TURBULENCE_COUNT; q++)
            {
                if (MathF.Abs(noiseSum) > scale)
                {
                    break;
                }
                scale *= 0.5f;

                Vector256<float> noise = Vector256<float>.Zero;
                for (int i = 0; i < SeedsCount; i += Vector256<float>.Count)
                {
                    //Load noise values
                    Vector256<float> noises = Vector256.Load(loadNoises + i);

                    //Cos approximation
                    Vector256<float> cosNoise = Cos(noises);

                    //Sum cos approximations
                    noise = Vector256.Add(noise, cosNoise);

                    //Modify noise so the turbulence noise looks random
                    Vector256<float> turbulentNoise = Vector256.Add(noises, noises);
                    Vector256.Store(turbulentNoise, storeNoise + i);
                }

                noiseSum += scale * HorizontalSum(noise);
                loadNoises = TurbulentNoises;
            }

            return noiseSum;
        }
    }

    internal readonly struct PlanetGen
    {
        internal readonly SeedsInfo Seeds;
        private readonly float PlanetRadius;
        private readonly float NoiseWeight;
        internal readonly float NoiseFrequency;
        private const int SEED_COUNT = 32;


        public PlanetGen(int seed, float planetRadius, float noiseWeight, float noiseFrequency)
        {
            Seeds = new SeedsInfo(seed, SEED_COUNT);
            PlanetRadius = planetRadius;
            NoiseWeight = noiseWeight;
            NoiseFrequency = noiseFrequency;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe float GenerateWeight(Vector4 pos, in CosApproxConsts cosApprox)
        {
            float sphere = SphereGen.GetValue(pos, PlanetRadius);
            float noise = cosApprox.Turbulence(sphere, NoiseWeight);

            return noise;
        }


    }
}
