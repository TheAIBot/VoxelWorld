using System;
using System.Numerics;

namespace VoxelWorld
{
    internal static class PlanetGen
    {
        internal static Func<Vector3, float> GetPlanetGen(int seed, float planetRadius, float noiseWeight, float noiseFrequency)
        {
            var seeds = XYZRandomGen.Initialize(seed, 30);

            return new Func<Vector3, float>(pos =>
            {
                float noise = XYZRandomGen.GetNoise(seeds, pos * noiseFrequency);
                float sphere = SphereGen.GetValue(pos, planetRadius);

                return noise * noiseWeight + sphere;
            });
        }
    }
}
