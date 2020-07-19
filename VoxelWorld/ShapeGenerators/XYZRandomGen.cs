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
        private static readonly Vector256<float> const0_25 = Vector256.Create(0.25f);
        private static readonly Vector256<float> consttp = Vector256.Create(1.0f / (2.0f * MathF.PI));
        private static readonly Vector256<float> const16 = Vector256.Create(16.0f);
        private static readonly Vector256<float> const0_5 = Vector256.Create(0.5f);
        private static readonly Vector256<float> const_noSign = Vector256.Create(0x7fffffff).AsSingle();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static Vector256<float> CosApproximationVectorized(Vector256<float> x)
        {
            x = Avx.Multiply(x, consttp);
            x = Avx.Subtract(x, Avx.Add(const0_25, Avx.Floor(Avx.Add(x, const0_25))));
            x = Avx.Multiply(Avx.Multiply(const16, x), Avx.Subtract(Avx.And(x, const_noSign), const0_5));
            return x;
        }

        internal unsafe static float GetNoise(SeedsInfo seeds, Vector3 pos)
        {
            if (Avx.IsSupported)
            {
                fixed (float* aa = seeds.Seeds)
                {
                    Vector128<float> pospos = Vector128.Create(pos.X, pos.Y, pos.Z, 0.0f);
                    Vector256<float> pospospos = Vector256.Create(pospos, pospos);
                    Vector256<float> noise = Vector256<float>.Zero;
                    for (int i = 0; i < seeds.Seeds.Length; i += 32)
                    {
                        //dotnet core 3.1 makes shit code generation so this temp variable is needed to
                        //avoid that.
                        //The problem does not persist in dotnet 5.0
                        float* core3_1fix = aa + i;
                        Vector256<float> x0 = Avx.DotProduct(pospospos, Avx.LoadVector256(core3_1fix + 0), 0b1111_1000);
                        Vector256<float> x2 = Avx.DotProduct(pospospos, Avx.LoadVector256(core3_1fix + 8), 0b1111_0100);
                        Vector256<float> s1 = Avx.Add(x0, x2);

                        Vector256<float> x4 = Avx.DotProduct(pospospos, Avx.LoadVector256(core3_1fix + 16), 0b1111_0010);
                        Vector256<float> x6 = Avx.DotProduct(pospospos, Avx.LoadVector256(core3_1fix + 24), 0b1111_0001);
                        Vector256<float> s2 = Avx.Add(x4, x6);

                        noise = Avx.Add(noise, CosApproximationVectorized(Avx.Add(s1, s2)));
                    }

                    noise = Avx.HorizontalAdd(noise, noise);
                    Vector128<float> lower = noise.GetLower();
                    Vector128<float> upper = noise.GetUpper();
                    Vector128<float> dd = Avx.Add(lower, upper);
                    return Avx.Multiply(Avx.HorizontalAdd(dd, dd), Vector128.Create(seeds.Reci_SeedsCount)).GetElement(0);
                }
            }
            else
            {
                throw new Exception("I was too lazy so you need AVX in order to run this program.");
            }
        }
    }
}
