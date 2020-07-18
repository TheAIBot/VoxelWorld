using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace VoxelWorld
{
    internal static class XYZRandomGen
    {
        internal static float[] Initialize(int seed, int seedLength)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static Vector256<float> CosApproximationVectorized(Vector256<float> x)
        {
            const float tp = 1.0f / (2.0f * MathF.PI);
            x = Avx.Multiply(x, Vector256.Create(tp));
            x = Avx.Subtract(x, Avx.Add(Vector256.Create(0.25f), Avx.Floor(Avx.Add(x, Vector256.Create(0.25f)))));
            x = Avx.Multiply(x, Avx.Multiply(Vector256.Create(16.0f), (Avx.Subtract(Avx.And(x, Vector256.Create(0x7fffffff).AsSingle()), Vector256.Create(0.5f)))));
            return x;
        }

        internal unsafe static float GetNoise(float[] seeds, Vector3 pos)
        {
            if (Avx.IsSupported)
            {
                fixed (float* aa = seeds)
                {
                    Vector128<float> pospos = Vector128.Create(pos.X, pos.Y, pos.Z, 0.0f);
                    Vector256<float> noise = Vector256<float>.Zero;
                    for (int i = 0; i < seeds.Length; i += 32)
                    {
                        Vector128<float> x0 = Avx.DotProduct(pospos, Avx.LoadVector128(aa + i + 0), 0b1111_1000);
                        Vector128<float> x1 = Avx.DotProduct(pospos, Avx.LoadVector128(aa + i + 4), 0b1111_0100);
                        Vector128<float> x2 = Avx.DotProduct(pospos, Avx.LoadVector128(aa + i + 8), 0b1111_0010);
                        Vector128<float> x3 = Avx.DotProduct(pospos, Avx.LoadVector128(aa + i + 12), 0b1111_0001);
                        Vector128<float> s1 = Avx.Add(Avx.Add(x0, x1), Avx.Add(x2, x3));

                        Vector128<float> x4 = Avx.DotProduct(pospos, Avx.LoadVector128(aa + i + 16), 0b1111_1000);
                        Vector128<float> x5 = Avx.DotProduct(pospos, Avx.LoadVector128(aa + i + 20), 0b1111_0100);
                        Vector128<float> x6 = Avx.DotProduct(pospos, Avx.LoadVector128(aa + i + 24), 0b1111_0010);
                        Vector128<float> x7 = Avx.DotProduct(pospos, Avx.LoadVector128(aa + i + 28), 0b1111_0001);
                        Vector128<float> s2 = Avx.Add(Avx.Add(x4, x5), Avx.Add(x6, x7));

                        noise = Avx.Add(noise, CosApproximationVectorized(Vector256.Create(s1, s2)));
                    }

                    noise = Avx.HorizontalAdd(noise, noise);
                    Vector128<float> lower = noise.GetLower();
                    Vector128<float> upper = noise.GetUpper();
                    lower = Avx.HorizontalAdd(lower, lower);
                    upper = Avx.HorizontalAdd(upper, upper);

                    return (Avx.Add(lower, upper).GetElement(0)) / (seeds.Length / 4);
                }
            }
            else
            {
                throw new Exception("I was too lazy so you need AVX in order to run this program.");
            }
        }
    }
}
