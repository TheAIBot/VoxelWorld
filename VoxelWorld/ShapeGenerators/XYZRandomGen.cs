using System;
using System.Numerics;

namespace VoxelWorld
{
    internal static class XYZRandomGen
    {
        internal static Vector3[] Initialize(int seed, int seedLength)
        {
            Random rand = new Random(seed);

            Vector3[] randVecs = new Vector3[seedLength];
            for (int i = 0; i < randVecs.Length; i++)
            {
                while (true)
                {
                    Vector3 vec = new Vector3(((float)rand.NextDouble() * 2.0f) - 1.0f, ((float)rand.NextDouble() * 2.0f) - 1.0f, ((float)rand.NextDouble() * 2.0f) - 1.0f);
                    if (vec.Length() > 0.25f && vec.Length() < 1.0f)
                    {
                        randVecs[i] = vec;
                        break;
                    }
                }
            }

            return randVecs;
        }

        internal static float GetNoise(Vector3[] seeds, Vector3 pos)
        {
            float noise = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                noise += MathF.Sin(Vector3.Dot(pos, seeds[i]));
            }
            return noise / seeds.Length;
        }
    }
}
