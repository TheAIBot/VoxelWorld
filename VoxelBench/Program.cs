using System;
using VoxelWorld;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace VoxelBench
{
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    public class VoxelBencher
    {
        private readonly SeedsInfo seeds;
        private readonly VoxelSystemData systemDataPlanet;
        private readonly VoxelSystemData systemDataRandom;
        private readonly VoxelGrid gridPlanet;
        private readonly VoxelGrid gridRandom;

        public VoxelBencher()
        {
            this.seeds = new SeedsInfo(32, 32);
            this.systemDataPlanet = new VoxelSystemData(100, 0.03f, new PlanetGen(3, 1.0f, 3.0f, 1.0f));
            this.systemDataRandom = new VoxelSystemData(100, 0.03f, new PlanetGen(3, 1.0f, 30.0f, 1.0f));

            this.gridPlanet = new VoxelGrid(new Vector3(0, 0, 0), systemDataPlanet);
            this.gridRandom = new VoxelGrid(new Vector3(0, 0, 0), systemDataRandom);
        }

        [Benchmark]
        public void MakeGridPlanet() => gridPlanet.Randomize();

        [Benchmark]
        public void MakeGridRandom() => gridRandom.Randomize();
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run(typeof(VoxelBencher).Assembly);
        }
    }
}
