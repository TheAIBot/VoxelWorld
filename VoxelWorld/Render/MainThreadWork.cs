﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace VoxelWorld
{

    internal static class MainThreadWork
    {
        private static ConcurrentQueue<GridRenderCommand> Commands = new ConcurrentQueue<GridRenderCommand>();

        private static readonly List<IndirectDraw> GridDrawBuffers = new List<IndirectDraw>();
        private static readonly Dictionary<VoxelGridHierarchy, IndirectDraw> GridsToBuffer = new Dictionary<VoxelGridHierarchy, IndirectDraw>();


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
                GridDrawBuffers[i].PrepareDraw();
            }

            for (int i = 0; i < GridDrawBuffers.Count; i++)
            {
                GridDrawBuffers[i].Draw();
            }

            for (int i = GridDrawBuffers.Count - 1; i >= 0; i--)
            {
                if (GridDrawBuffers[i].CommandCount() == 0)
                {
                    GridDrawBuffers[i].Reset();
                }
            }

            //Console.WriteLine(GridDrawBuffers.Count);
        }

        private static void AddGrid(GridRenderCommand cmd, ref int indexFirstBufferNotFull)
        {
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
                GridDrawBuffers.Add(new IndirectDraw());
            }
        }

        private static void RemoveGrid(GridRenderCommand cmd)
        {
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
    }
}