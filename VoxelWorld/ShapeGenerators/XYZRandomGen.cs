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
            for (int i = 0; i < seeds.Seeds.Length / 4; i += Vector256<float>.Count)
            {
                Vector256<float> ps = Avx.LoadVector256(seedsPtr + i);

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
