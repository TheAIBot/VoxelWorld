using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VoxelWorld
{
    internal readonly struct PlanetGen
    {
        private readonly SeedsInfo Seeds;
        private readonly float PlanetRadius;
        private readonly float NoiseWeight;
        private readonly float NoiseFrequency;
        private const int SEED_COUNT = 32;
        private const int TURBULENCE_COUNT = 5;

        public PlanetGen(int seed, float planetRadius, float noiseWeight, float noiseFrequency)
        {
            this.Seeds = new SeedsInfo(seed, SEED_COUNT);
            this.PlanetRadius = planetRadius;
            this.NoiseWeight = noiseWeight;
            this.NoiseFrequency = noiseFrequency;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GenerateWeight(Vector3 pos)
        {
            float noise = Turbulence(pos);
            float sphere = SphereGen.GetValue(pos, PlanetRadius);

            return noise * NoiseWeight + sphere;
        }

        private float Turbulence(Vector3 pos)
        {
            float noiseSum = 0.0f;
            float scale = 1.0f;
            for (int i = 0; i < TURBULENCE_COUNT; i++)
            {
                noiseSum += scale * XYZRandomGen.GetNoise(Seeds, pos * NoiseFrequency);
                scale *= 0.5f;
                pos *= 2.0f;
            }

            return noiseSum;
        }
    }
}
