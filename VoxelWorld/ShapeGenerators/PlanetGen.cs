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

        public PlanetGen(int seed, float planetRadius, float noiseWeight, float noiseFrequency)
        {
            this.Seeds = new SeedsInfo(seed, 32);
            this.PlanetRadius = planetRadius;
            this.NoiseWeight = noiseWeight;
            this.NoiseFrequency = noiseFrequency;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GenerateWeight(Vector3 pos)
        {
            float noise = XYZRandomGen.GetNoise(Seeds, pos * NoiseFrequency);
            float sphere = SphereGen.GetValue(pos, PlanetRadius);

            return noise * NoiseWeight + sphere;
        }
    }
}
