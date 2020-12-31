using System;
using System.Numerics;
using System.Runtime.CompilerServices;

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
        public unsafe float GenerateWeight(Vector4 pos, float* aa)
        {
            float sphere = SphereGen.GetValue(pos, PlanetRadius);
            float noise = Turbulence(pos, aa, sphere);

            return noise * NoiseWeight + sphere;
        }

        private unsafe float Turbulence(Vector4 pos, float* aa, float sphereValue)
        {
            sphereValue = MathF.Abs(sphereValue);
            float noiseSum = 0.0f;
            float scale = 1.0f;
            for (int i = 0; i < TURBULENCE_COUNT; i++)
            {
                if (sphereValue > (MathF.Abs(noiseSum) + scale * 2.0f) * NoiseWeight)
                {
                    break;
                }
                noiseSum += scale * XYZRandomGen.GetNoise(Seeds, aa, pos * NoiseFrequency);
                scale *= 0.5f;
                pos *= 2.0f;
            }

            return noiseSum;
        }
    }
}
