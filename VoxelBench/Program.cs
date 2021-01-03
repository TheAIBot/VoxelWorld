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
        private readonly VoxelSystemData systemData;
        private readonly VoxelGrid grid;

        public VoxelBencher()
        {
            this.seeds = new SeedsInfo(32, 32);
            this.systemData = new VoxelSystemData(100, 0.03f, new PlanetGen(3, 1.0f, 3.0f, 1.0f));

            this.grid = new VoxelGrid(new Vector3(0, 0, 0), systemData);
        }

        [Benchmark]
        public void MakeGrid() => grid.Randomize();
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run(typeof(VoxelBencher).Assembly);
        }
    }
}
