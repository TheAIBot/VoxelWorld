﻿using Silk.NET.OpenGL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Render.VoxelGrid
{
    internal static class VoxelGridRenderManager
    {
        private static ConcurrentQueue<GridRenderCommand> Commands = new ConcurrentQueue<GridRenderCommand>();

        private const int MinTransferCount = 500;
        private static IndirectDrawFactory DrawFactory;
        private static readonly List<IndirectDraw> GridDrawBuffers = new List<IndirectDraw>();
        private static readonly Dictionary<VoxelGridHierarchy, IndirectDraw> GridsToBuffer = new Dictionary<VoxelGridHierarchy, IndirectDraw>();
        private static readonly Dictionary<VoxelGridHierarchy, int> GridsToTriangleCount = new();
        private static readonly TimeNumberAverage<int> AvgNewTriangles = new TimeNumberAverage<int>(TimeSpan.FromSeconds(2), x => x);

        private static int DrawCounter = 0;
        private static int GridsDrawing = 0;

        public static void SetOpenGl(GL openGl)
        {
            DrawFactory = new IndirectDrawFactory(openGl, 5_000);
        }

        public static void MakeGridDrawable(VoxelGridHierarchy grid, GeometryData geometry)
        {
            Commands.Enqueue(new GridRenderCommand(GridRenderCommandType.Add, grid, geometry));
        }

        public static void RemoveDrawableGrid(VoxelGridHierarchy grid)
        {
            Commands.Enqueue(new GridRenderCommand(GridRenderCommandType.Remove, grid, null));
        }

        public static void ProcessCommands()
        {
            //Add/Remove grids from drawers
            HandleIncommingGridCommands();

            //Move new grid data to the GPU
            for (int i = 0; i < GridDrawBuffers.Count; i++)
            {
                GridDrawBuffers[i].CopyToGPU();
            }

            //Transfer grids away from drawers that are almost empty
            //so they can be reset and filled with grids again. This
            //improves memory utilization by making these drawers
            //available faster.
            TransferFromAlmostEmptyDrawers();

            //Reset empty drawers so they can be filled again
            //or remove them if their buffer sizes aren't up to date
            HandleEmptyDrawers();
        }

        public static void DrawGrids()
        {

            //Send updated draw commands to the GPU
            for (int i = 0; i < GridDrawBuffers.Count; i++)
            {
                GridDrawBuffers[i].SendCommandsToGPU();
            }

            //Draw all the grids
            for (int i = 0; i < GridDrawBuffers.Count; i++)
            {
                GridDrawBuffers[i].Draw();
            }

            if (DrawCounter % 60 == 0)
            {
                //PrintDrawBufferUtilization();
            }

            Console.WriteLine(GridDrawBuffers.Count);
            //Console.WriteLine(GetGPUBufferSizeInMB().ToString("N0") + "MB");
            //Console.WriteLine(GridsDrawing.ToString("N0"));
            Console.WriteLine((GetBufferUtilization() * 100).ToString("N2"));
            Console.WriteLine(GridsToTriangleCount.Values.Sum().ToString("N0"));
            Console.WriteLine($"Generated triangles: {AvgNewTriangles.GetAveragePerTimeUnit(TimeSpan.FromSeconds(1)):N0}/s");

            DrawCounter++;
        }

        private static void HandleIncommingGridCommands()
        {
            int newTrianglesForFrame = 0;
            int cmdCount = Commands.Count;

            int indexFirstBufferNotFull = 0;
            for (int cmdCounter = 0; cmdCounter < cmdCount; cmdCounter++)
            {
                GridRenderCommand cmd;
                if (!Commands.TryDequeue(out cmd))
                {
                    throw new Exception("Expected to dequeue a command but no command was found.");
                }

                switch (cmd.CType)
                {
                    case GridRenderCommandType.Add:
                        AddGrid(cmd, ref indexFirstBufferNotFull);
                        GridsToTriangleCount.Add(cmd.Grid, cmd.GeoData.TriangleCount);
                        newTrianglesForFrame += cmd.GeoData.TriangleCount;
                        break;
                    case GridRenderCommandType.Remove:
                        RemoveGrid(cmd);
                        GridsToTriangleCount.Remove(cmd.Grid);
                        break;
                    default:
                        throw new Exception($"Unknown enum value: {cmd.CType}");
                }
            }

            AvgNewTriangles.AddSampleNow(newTrianglesForFrame);
        }

        private static void AddGrid(GridRenderCommand cmd, ref int indexFirstBufferNotFull)
        {
            GridsDrawing++;
            DrawFactory.AddGeometrySample(cmd.GeoData);

            while (true)
            {
                for (int i = indexFirstBufferNotFull; i < GridDrawBuffers.Count; i++)
                {
                    if (!GridDrawBuffers[i].TryAddGeometry(cmd.Grid, cmd.GeoData))
                    {
                        if (i == indexFirstBufferNotFull)
                        {
                            indexFirstBufferNotFull++;
                        }
                    }
                    else
                    {
                        GridsToBuffer.Add(cmd.Grid, GridDrawBuffers[i]);
                        return;
                    }
                }

                //No space in any buffer for the geometry so make a
                //new one and try ágain
                GridDrawBuffers.Add(DrawFactory.CreateIndirectDraw());
            }
        }

        private static void RemoveGrid(GridRenderCommand cmd)
        {
            GridsDrawing--;
            if (GridsToBuffer.TryGetValue(cmd.Grid, out var buffer) &&
                GridsToBuffer.Remove(cmd.Grid))
            {
                buffer.RemoveGeometry(cmd.Grid);
            }
            else
            {
                throw new Exception("Failed to find and remove a grid.");
            }
        }

        /// <summary>
        /// Improve GPU memory utilization by moving draw commands from
        /// almost empty drawers, to other drawers. This makes the almost
        /// empty drawers available again so they can accept new draw commands.
        /// </summary>
        private static void TransferFromAlmostEmptyDrawers()
        {
            int transferCount = 0;
            for (int i = 0; i < GridDrawBuffers.Count; i++)
            {
                IndirectDraw draw = GridDrawBuffers[i];
                if (draw.GetCommandCount() <= MinTransferCount)
                {
                    int vertexCount = draw.GetVertexCount();
                    int indiceCount = draw.GetIndiceCount();
                    int commandCount = draw.GetCommandCount();

                    for (int y = 0; y < GridDrawBuffers.Count; y++)
                    {
                        //Don't transfer to itself
                        if (y == i)
                        {
                            continue;
                        }

                        //Need to transfer to a drawer that isn't
                        //also almost empty
                        IndirectDraw copyTo = GridDrawBuffers[y];
                        if (copyTo.GetCommandCount() <= MinTransferCount)
                        {
                            continue;
                        }

                        if (!copyTo.HasSpaceFor(vertexCount, indiceCount, commandCount))
                        {
                            continue;
                        }

                        foreach (var grid in draw.GetGridsDrawing())
                        {
                            GridsToBuffer.Remove(grid);
                            GridsToBuffer.Add(grid, copyTo);
                        }

                        transferCount += draw.TransferDrawCommands(copyTo);
                    }
                }
            }

            Console.WriteLine($"Copy commands: {transferCount}");
        }

        private static void HandleEmptyDrawers()
        {
            for (int i = GridDrawBuffers.Count - 1; i >= 0; i--)
            {
                if (GridDrawBuffers[i].IsEmpty())
                {
                    if (GridDrawBuffers.Count(x => x.IsEmpty()) > 1)
                    {
                        GridDrawBuffers[i].Dispose();
                        GridDrawBuffers.RemoveAt(i);
                    }
                    else if (DrawFactory.HasAcceptableBufferSizes(GridDrawBuffers[i]))
                    {
                        GridDrawBuffers[i].Reset();
                    }
                    else
                    {
                        GridDrawBuffers[i].Dispose();
                        GridDrawBuffers.RemoveAt(i);
                    }
                }
            }
        }

        private static long GetGPUBufferSizeInMB()
        {
            const int bytesToMBRatio = 1_000_000;
            return GridDrawBuffers.Sum(x => x.GpuMemSize()) / bytesToMBRatio;
        }

        private static float GetBufferUtilization()
        {
            const int bytesToMBRatio = 1_000_000;
            long averageGridSize = DrawFactory.GetAverageGridMemUsage();
            long avgTotalGridMemUsage = averageGridSize * GridsDrawing;
            long avgTotalGridMemUsageInMB = avgTotalGridMemUsage / bytesToMBRatio;
            long gpuMemUsedInMB = GetGPUBufferSizeInMB();

            return (float)avgTotalGridMemUsageInMB / gpuMemUsedInMB;
        }

        private static void PrintDrawBufferUtilization()
        {
            PerfNumAverage<int> lol = new PerfNumAverage<int>(GridDrawBuffers.Count, x => x);
            for (int i = 0; i < GridDrawBuffers.Count; i++)
            {
                lol.AddSample(GridDrawBuffers[i].GetCommandCount());
            }
            lol.PrintHistogram();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
