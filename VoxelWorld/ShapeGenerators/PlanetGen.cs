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
            float noise = Turbulence(pos * NoiseFrequency, aa, sphere);

            return noise * NoiseWeight + sphere;
        }

        private unsafe float Turbulence(Vector4 pos, float* aa, float sphereValue)
        {
            sphereValue = sphereValue / NoiseWeight;
            float noiseSum = 0.0f;
            float scale = 2.0f;
            for (int i = 0; i < TURBULENCE_COUNT; i++)
            {
                if (MathF.Abs(sphereValue + noiseSum) > scale)
                {
                    break;
                }
                scale *= 0.5f;
                noiseSum += scale * XYZRandomGen.GetNoise(Seeds, aa, pos);
                pos *= 2.0f;
            }

            return noiseSum;
        }
    }
}
