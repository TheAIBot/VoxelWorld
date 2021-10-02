﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace VoxelWorld
{
    internal static class MainThreadWork
    {
        private static ConcurrentQueue<GridRenderCommand> Commands = new ConcurrentQueue<GridRenderCommand>();

        private static readonly IndirectDrawFactory DrawFactory = new IndirectDrawFactory(20_000);
        private static readonly List<IndirectDraw> GridDrawBuffers = new List<IndirectDraw>();
        private static readonly Dictionary<VoxelGridHierarchy, IndirectDraw> GridsToBuffer = new Dictionary<VoxelGridHierarchy, IndirectDraw>();

        private static int GridsDrawing = 0;

        public static void MakeGridDrawable(VoxelGridHierarchy grid, GeometryData geometry)
        {
            Commands.Enqueue(new GridRenderCommand(GridRenderCommandType.Add, grid, geometry));
        }

        public static void RemoveDrawableGrid(VoxelGridHierarchy grid)
        {
            Commands.Enqueue(new GridRenderCommand(GridRenderCommandType.Remove, grid, null));
        }

        public static void DrawGrids()
        {
            int cmdCount = Commands.Count;

            int indexFirstBufferNotFull = 0;
            for (int cmdCounter = 0; cmdCounter < cmdCount; cmdCounter++)
            {
                GridRenderCommand cmd;
                if (!Commands.TryDequeue(out cmd))
                {
                    throw new Exception("Expected to dequeue a command but no command was found.");
                }

                if (cmd.CType == GridRenderCommandType.Add)
                {
                    AddGrid(cmd, ref indexFirstBufferNotFull);
                }
                else if (cmd.CType == GridRenderCommandType.Remove)
                {
                    RemoveGrid(cmd);
                }
                else
                {
                    throw new Exception($"Unknown enum value: {cmd.CType}");
                }
            }

            for (int i = 0; i < GridDrawBuffers.Count; i++)
            {
                GridDrawBuffers[i].CopyToGPU();
            }

            for (int i = 0; i < GridDrawBuffers.Count; i++)
            {
                GridDrawBuffers[i].SendCommandsToGPU();
            }

            for (int i = 0; i < GridDrawBuffers.Count; i++)
            {
                GridDrawBuffers[i].Draw();
            }

            for (int i = GridDrawBuffers.Count - 1; i >= 0; i--)
            {
                if (GridDrawBuffers[i].IsEmpty())
                {
                    if (DrawFactory.HasAcceptableBufferSizes(GridDrawBuffers[i]))
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

            //Console.WriteLine(GridDrawBuffers.Count);
            //Console.WriteLine(GetGPUBufferSizeInMB().ToString("N0") + "MB");
            //Console.WriteLine(GridsDrawing.ToString("N0"));
            //Console.WriteLine((GetBufferUtilization() * 100).ToString("N2"));
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
    }
}
