using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelWorld
{
    internal readonly struct SeedsInfo
    {
        public readonly float[] Seeds;
        public readonly float Reci_SeedsCount;

        public SeedsInfo(int seed, int seedCount)
        {
            this.Seeds = Initialize(seed, seedCount);
            this.Reci_SeedsCount = 1.0f / (seedCount);
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
        internal unsafe static float GetNoise(SeedsInfo seeds, float* seedsPtr, Vector4 pos)
        {
            //Cos approximation constants
            Vector256<float> const0_25 = Vector256.Create(0.25f);
            Vector256<float> consttp = Vector256.Create(1.0f / (2.0f * MathF.PI));
            Vector256<float> const16 = Vector256.Create(16.0f);
            Vector256<float> const0_5 = Vector256.Create(0.5f);
            Vector256<float> const_noSign = Vector256.Create(0x7fffffff).AsSingle();

            Vector128<float> pos128 = pos.AsVector128();
            Vector256<float> pos256 = Vector256.Create(pos128, pos128);
            Vector256<float> noise = Vector256<float>.Zero;
            for (int i = 0; i < seeds.Seeds.Length; i += Vector256<float>.Count * 4)
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

                Vector256<float> ps1234 = Avx.Add(ps12, ps34);//[2,4,_,_,1,3,_,_]
                Vector256<float> ps5678 = Avx.Add(ps56, ps78);//[_,_,6,8,_,_,5,7]
                Vector256<float> ps = Avx.Add(ps1234, ps5678);//[2,4,6,8,1,3,5,7]

                //Cos approximation
                Vector256<float> cosApprox = Avx.Multiply(ps, consttp);
                cosApprox = Avx.Subtract(cosApprox, Avx.Add(const0_25, Avx.Floor(Avx.Add(cosApprox, const0_25))));
                cosApprox = Avx.Multiply(Avx.Multiply(const16, cosApprox), Avx.Subtract(Avx.And(cosApprox, const_noSign), const0_5));

                noise = Avx.Add(noise, cosApprox);
            }

            noise = Avx.HorizontalAdd(noise, noise);
            Vector128<float> lower = noise.GetLower();
            Vector128<float> upper = noise.GetUpper();
            Vector128<float> dd = Avx.Add(lower, upper);
            return Avx.HorizontalAdd(dd, dd).GetElement(0) * seeds.Reci_SeedsCount;
        }
    }
}
