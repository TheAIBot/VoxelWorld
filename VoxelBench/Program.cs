﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Collections;
using System.Numerics;
using VoxelWorld.ShapeGenerators;
using VoxelWorld.Voxel.Grid;
using VoxelWorld.Voxel.System;

namespace VoxelBench
{
    [DisassemblyDiagnoser(printSource: true, maxDepth: 5)]
    public class VoxelBencher
    {
        private readonly VoxelSystemData systemDataPlanet;
        private readonly VoxelGrid gridPlanet;
        private readonly BitArray compressedGrid;
        private readonly UsedPointsBoxBoundary UsedBoxPoints;
        private readonly int TriangleCount;

        public VoxelBencher()
        {
            this.systemDataPlanet = new VoxelSystemData(100, 0.03f, new PlanetGen(3, 1.0f, 3.0f, 1.0f));
            this.gridPlanet = new VoxelGrid(new Vector3(0, 0, 0), systemDataPlanet);
            gridPlanet.Randomize();
            TriangleCount = gridPlanet.PreCalculateGeometryData();
            UsedBoxPoints = gridPlanet.GetUsedPointsBox();
            this.compressedGrid = gridPlanet.GetCompressed();
        }

        //[Benchmark]
        //public void Randomize() => gridPlanet.Randomize();

        //[Benchmark]
        //public BitArray GetCompressed() => gridPlanet.GetCompressed();

        //[Benchmark]
        //public void Restore() => gridPlanet.Restore(compressedGrid);

        //[Benchmark]
        //public GridSidePointsUsed EdgePointsUsed() => gridPlanet.EdgePointsUsed();

        [Benchmark]
        public UsedPointsBoxBoundary GetUsedPointsBox() => gridPlanet.GetUsedPointsBox();

        //[Benchmark]
        //public void PreCalculateGeometryData() => gridPlanet.PreCalculateGeometryData();

        //[Benchmark]
        //public GeometryData Triangulize() => gridPlanet.Triangulize(UsedBoxPoints, TriangleCount);
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run(typeof(VoxelBencher).Assembly);
        }
    }
}
