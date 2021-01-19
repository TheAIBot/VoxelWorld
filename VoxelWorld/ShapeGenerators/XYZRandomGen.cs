using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelWorld
{
    internal unsafe readonly ref struct SeededNoiseStorage
    {
        private readonly float* Seeds;
        private readonly float* BaseNoises;
        private readonly float* XNoiseDiffs;
        internal readonly float* TurbulentNoises;
        internal readonly int SeedsCount;

        internal SeededNoiseStorage(SeedsInfo seedsInfo, float* seeds, float* baseNoises, float* xNoiseDiffs, float* turbulentNoises)
        {
            this.Seeds = seeds;
            this.BaseNoises = baseNoises;
            this.XNoiseDiffs = xNoiseDiffs;
            this.TurbulentNoises = turbulentNoises;
            this.SeedsCount = seedsInfo.GetSeedsCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void MakeSeededBaseNoise(Vector256<float> dotWith)
        {
            for (int i = 0; i < SeedsCount; i += Vector256<float>.Count)
            {
                //Load 8 seed vectors
                Vector256<float> s12 = Avx.LoadVector256(Seeds + i * 4 + Vector256<float>.Count * 0);
                Vector256<float> s34 = Avx.LoadVector256(Seeds + i * 4 + Vector256<float>.Count * 1);
                Vector256<float> s56 = Avx.LoadVector256(Seeds + i * 4 + Vector256<float>.Count * 2);
                Vector256<float> s78 = Avx.LoadVector256(Seeds + i * 4 + Vector256<float>.Count * 3);
                
                
                Vector256<float> ps12 = Avx.DotProduct(dotWith, s12, 0b0111_1000);//[2,_,_,_,1,_,_,_]
                Vector256<float> ps34 = Avx.DotProduct(dotWith, s34, 0b0111_0100);//[_,4,_,_,_,3,_,_]
                Vector256<float> ps56 = Avx.DotProduct(dotWith, s56, 0b0111_0010);//[_,_,6,_,_,_,5,_]
                Vector256<float> ps78 = Avx.DotProduct(dotWith, s78, 0b0111_0001);//[_,_,_,8,_,_,_,7]

                Vector256<float> ps1234 = Avx.Or(ps12, ps34);//[2,4,_,_,1,3,_,_]
                Vector256<float> ps5678 = Avx.Or(ps56, ps78);//[_,_,6,8,_,_,5,7]
                Vector256<float> ps = Avx.Or(ps1234, ps5678);//[2,4,6,8,1,3,5,7]

                Avx.Store(BaseNoises + i, ps);
                Avx.Store(TurbulentNoises + i, ps);
            } 
        }

        internal unsafe void BaseSeededXDiff(Vector256<float> dotWith)
        {
            for (int i = 0; i < SeedsCount; i += Vector256<float>.Count)
            {
                //Load 8 seed vectors
                Vector256<float> s12 = Avx.LoadVector256(Seeds + i * 4 + Vector256<float>.Count * 0);
                Vector256<float> s34 = Avx.LoadVector256(Seeds + i * 4 + Vector256<float>.Count * 1);
                Vector256<float> s56 = Avx.LoadVector256(Seeds + i * 4 + Vector256<float>.Count * 2);
                Vector256<float> s78 = Avx.LoadVector256(Seeds + i * 4 + Vector256<float>.Count * 3);
                
                
                Vector256<float> ps12 = Avx.DotProduct(dotWith, s12, 0b0001_1000);//[2,_,_,_,1,_,_,_]
                Vector256<float> ps34 = Avx.DotProduct(dotWith, s34, 0b0001_0100);//[_,4,_,_,_,3,_,_]
                Vector256<float> ps56 = Avx.DotProduct(dotWith, s56, 0b0001_0010);//[_,_,6,_,_,_,5,_]
                Vector256<float> ps78 = Avx.DotProduct(dotWith, s78, 0b0001_0001);//[_,_,_,8,_,_,_,7]

                Vector256<float> ps1234 = Avx.Or(ps12, ps34);//[2,4,_,_,1,3,_,_]
                Vector256<float> ps5678 = Avx.Or(ps56, ps78);//[_,_,6,8,_,_,5,7]
                Vector256<float> ps = Avx.Or(ps1234, ps5678);//[2,4,6,8,1,3,5,7]

                Avx.Store(XNoiseDiffs + i, ps);
            } 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void UpdateSeededNoiseWithXPosChange()
        {
            for (int i = 0; i < SeedsCount; i += Vector256<float>.Count)
            {
                Vector256<float> baseNoises = Avx.LoadVector256(BaseNoises + i);
                Vector256<float> xDeltas = Avx.LoadVector256(XNoiseDiffs + i);

                Vector256<float> correctedNoise = Avx.Subtract(baseNoises, xDeltas);
                
                Avx.Store(TurbulentNoises + i, correctedNoise);
                Avx.Store(BaseNoises + i, correctedNoise);
            }
        }
    }

    internal readonly struct SeedsInfo
    {
        public readonly float[] Seeds;
        public readonly float Reci_SeedsCount;

        public SeedsInfo(int seed, int seedCount)
        {
            this.Seeds = Initialize(seed, seedCount);
            this.Reci_SeedsCount = 1.0f / (seedCount);
        }

        internal int GetSeedsCount()
        {
            return Seeds.Length / 4;
        }

        private static float[] Initialize(int seed, int seedLength)
        {
            Random rand = new Random(seed);

            float[] randVecs = new float[seedLength * 4];
            for (int i = 0; i < randVecs.Length; i += 4)
            {
                while (true)
                {
                    Vector4 vec = new Vector4(((float)rand.NextDouble() * 2.0f) - 1.0f, ((float)rand.NextDouble() * 2.0f) - 1.0f, ((float)rand.NextDouble() * 2.0f) - 1.0f, 0.0f);
                    if (vec.Length() > 0.25f && vec.Length() < 1.0f)
                    {
                        randVecs[i + 0] = vec.X;
                        randVecs[i + 1] = vec.Y;
                        randVecs[i + 2] = vec.Z;
                        randVecs[i + 3] = vec.W;
                        break;
                    }
                }
            }

            return randVecs;
        }
    }

    internal static class XYZRandomGen
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static float GetNoise(SeedsInfo seeds, float* seedsPtr)
        {
            //Cos approximation constants
            Vector256<float> const0_25 = Vector256.Create(0.25f);
            Vector256<float> consttp = Vector256.Create(1.0f / (2.0f * MathF.PI));
            Vector256<float> const16 = Vector256.Create(16.0f);
            Vector256<float> const0_5 = Vector256.Create(0.5f);
            Vector256<float> const_noSign = Vector256.Create(0x7fffffff).AsSingle();

            Vector256<float> noise = Vector256<float>.Zero;
            for (int i = 0; i < seeds.GetSeedsCount(); i += Vector256<float>.Count)
            {
                Vector256<float> ps = Avx.LoadVector256(seedsPtr + i);

                //Cos approximation
                Vector256<float> cosApprox = Avx.Multiply(ps, consttp);
                cosApprox = Avx.Subtract(cosApprox, Avx.Add(const0_25, Avx.Floor(Avx.Add(cosApprox, const0_25))));
                cosApprox = Avx.Multiply(Avx.Multiply(const16, cosApprox), Avx.Subtract(Avx.And(cosApprox, const_noSign), const0_5));

                noise = Avx.Add(noise, cosApprox);
            }

            noise = Avx.DotProduct(noise, Vector256.Create(seeds.Reci_SeedsCount), 0b1111_0001);
            Vector128<float> lower = noise.GetLower();
            Vector128<float> upper = noise.GetUpper();
            return Avx.Add(lower, upper).GetElement(0);
        }
    }
}
