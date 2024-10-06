using Silk.NET.OpenGL;
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

        private static readonly List<MultiBufferedIndirectDraw> BuffersWithSpace = new List<MultiBufferedIndirectDraw>();
        private static readonly List<MultiBufferedIndirectDraw> FullBuffers = new List<MultiBufferedIndirectDraw>();
        private static readonly List<CopyPair> CopyingBuffers = new List<CopyPair>();

        private readonly record struct CopyPair(MultiBufferedIndirectDraw From, MultiBufferedIndirectDraw To, List<VoxelGridHierarchy> CopiedGrids);

        // Due to copying it is possible for the grid to be in to places at once.
        private readonly record struct GridBufferLocation(MultiBufferedIndirectDraw Primary, MultiBufferedIndirectDraw? Secondary);


        private static readonly Dictionary<VoxelGridHierarchy, GridBufferLocation> GridsToBuffer = new Dictionary<VoxelGridHierarchy, GridBufferLocation>();
        private static readonly ConcurrentDictionary<VoxelGridHierarchy, int> GridsToTriangleCount = new();
        public static readonly TimeNumberAverage<int> AvgNewTriangles = new TimeNumberAverage<int>(TimeSpan.FromSeconds(5));
        public static readonly TimeNumberAverage<int> AvgNewGrids = new TimeNumberAverage<int>(TimeSpan.FromSeconds(5));
        public static readonly TimeNumberAverage<int> AvgTransferedGridsFromAlmostEmptyBuffers = new TimeNumberAverage<int>(TimeSpan.FromSeconds(5));
        public static readonly TimeNumberAverage<long> AvgTransferedBytes = new TimeNumberAverage<long>(TimeSpan.FromSeconds(5));
        public static readonly PerfNumAverage<long> AvgGridSize = new PerfNumAverage<long>(10_000, x => x);

        private static int DrawCounter = 0;

        public static int DrawBuffers => BuffersWithSpace.Count + FullBuffers.Count + CopyingBuffers.Count * 2;
        public static int TrianglesDrawing => GridsToTriangleCount.Values.Sum();
        public static int GridsDrawing { get; private set; } = 0;

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
            long copiedBytes = 0;
            for (int i = 0; i < BuffersWithSpace.Count; i++)
            {
                copiedBytes += BuffersWithSpace[i].CopyToGPU();
            }
            for (int i = 0; i < FullBuffers.Count; i++)
            {
                copiedBytes += FullBuffers[i].CopyToGPU();
            }
            for (int i = 0; i < CopyingBuffers.Count; i++)
            {
                copiedBytes += CopyingBuffers[i].From.CopyToGPU();
                copiedBytes += CopyingBuffers[i].To.CopyToGPU();
            }
            AvgTransferedBytes.AddSampleNow(copiedBytes);

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
            for (int i = 0; i < BuffersWithSpace.Count; i++)
            {
                BuffersWithSpace[i].SendCommandsToGPU();
            }
            for (int i = 0; i < FullBuffers.Count; i++)
            {
                FullBuffers[i].SendCommandsToGPU();
            }
            for (int i = 0; i < CopyingBuffers.Count; i++)
            {
                CopyingBuffers[i].From.SendCommandsToGPU();
                CopyingBuffers[i].To.SendCommandsToGPU();
            }

            //Draw all the grids
            for (int i = 0; i < BuffersWithSpace.Count; i++)
            {
                BuffersWithSpace[i].Draw();
            }
            for (int i = 0; i < FullBuffers.Count; i++)
            {
                FullBuffers[i].Draw();
            }
            for (int i = 0; i < CopyingBuffers.Count; i++)
            {
                CopyingBuffers[i].From.Draw();
                CopyingBuffers[i].To.Draw();
            }

            DrawCounter++;
        }

        private static void HandleIncommingGridCommands()
        {
            int newTrianglesForFrame = 0;
            int newGridsForFrame = 0;
            int cmdCount = Commands.Count;// Math.Min(1000, Commands.Count);// (int)(Commands.Count * 0.2f);

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
                        AddGrid(cmd);
                        GridsToTriangleCount.TryAdd(cmd.Grid, cmd.GeoData.TriangleCount);
                        newTrianglesForFrame += cmd.GeoData.TriangleCount;
                        newGridsForFrame++;
                        break;
                    case GridRenderCommandType.Remove:
                        RemoveGrid(cmd);
                        GridsToTriangleCount.TryRemove(cmd.Grid, out var _);
                        break;
                    default:
                        throw new Exception($"Unknown enum value: {cmd.CType}");
                }
            }

            AvgNewTriangles.AddSampleNow(newTrianglesForFrame);
            AvgNewGrids.AddSampleNow(newGridsForFrame);
        }

        private static void AddGrid(GridRenderCommand cmd)
        {
            GridsDrawing++;
            DrawFactory.AddGeometrySample(cmd.GeoData);
            AvgGridSize.AddSample(cmd.GeoData.GetSizeInBytes());

            while (true)
            {
                for (int i = BuffersWithSpace.Count - 1; i >= 0; i--)
                {
                    if (!BuffersWithSpace[i].TryAddGeometry(cmd.Grid, cmd.GeoData))
                    {
                        var fullBuffer = BuffersWithSpace[i];
                        BuffersWithSpace.RemoveAt(i);
                        FullBuffers.Add(fullBuffer);
                    }
                    else
                    {
                        GridsToBuffer.Add(cmd.Grid, new GridBufferLocation(BuffersWithSpace[i], null));
                        return;
                    }
                }

                //No space in any buffer for the geometry so make a
                //new one and try again
                BuffersWithSpace.Add(DrawFactory.CreateIndirectDraw());
            }
        }

        private static void RemoveGrid(GridRenderCommand cmd)
        {
            GridsDrawing--;
            if (GridsToBuffer.Remove(cmd.Grid, out var gridBufferLocation))
            {
                bool removedGrid = false;
                removedGrid |= gridBufferLocation.Primary.TryRemoveGeometry(cmd.Grid);
                if (gridBufferLocation.Secondary != null)
                {
                    removedGrid |= gridBufferLocation.Secondary.TryRemoveGeometry(cmd.Grid);
                }

                if (!removedGrid)
                {
                    throw new Exception("Failed to find and remove a grid.");
                }
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
            for (int i = FullBuffers.Count - 1; i >= 0; i--)
            {
                MultiBufferedIndirectDraw draw = FullBuffers[i];
                if (draw.GetCommandCount() <= MinTransferCount && !draw.IsTransferringToBuffers())
                {
                    int vertexCount = draw.GetVertexCount();
                    int indiceCount = draw.GetIndiceCount();
                    int commandCount = draw.GetCommandCount();

                    for (int y = 0; y < BuffersWithSpace.Count; y++)
                    {
                        MultiBufferedIndirectDraw copyTo = BuffersWithSpace[y];

                        if (!copyTo.HasSpaceFor(vertexCount, indiceCount, commandCount))
                        {
                            continue;
                        }

                        foreach (var grid in draw.GetGridsDrawing())
                        {
                            GridsToBuffer.Remove(grid);
                            GridsToBuffer.Add(grid, new GridBufferLocation(draw, copyTo));
                        }

                        FullBuffers.RemoveAt(i);
                        BuffersWithSpace.RemoveAt(y);
                        List<VoxelGridHierarchy> copiedGrids = draw.GetGridsDrawing().ToList();
                        CopyingBuffers.Add(new CopyPair(draw, copyTo, copiedGrids));
                        break;
                    }
                }
            }

            for (int i = CopyingBuffers.Count - 1; i >= 0; i--)
            {
                CopyPair copyPair = CopyingBuffers[i];
                copyPair.From.TransferDrawCommandsFromSingleBuffer(copyPair.To);

                if (!copyPair.From.IsEmpty())
                {
                    continue;
                }

                foreach (var grid in copyPair.CopiedGrids)
                {
                    // Do not add grid again as it was removed while copying
                    if (!GridsToBuffer.TryGetValue(grid, out GridBufferLocation gridLocation))
                    {
                        continue;
                    }

                    // If grid was removed and then added while copying then
                    // the location should not be overidden as its copied 
                    // location is no longer its location
                    if (gridLocation.Primary != copyPair.From)
                    {
                        continue;
                    }

                    GridsToBuffer[grid] = new GridBufferLocation(copyPair.To, null);
                }

                copyPair.From.Reset();

                CopyingBuffers.RemoveAt(i);
                BuffersWithSpace.Add(copyPair.From);
                BuffersWithSpace.Add(copyPair.To);
            }
        }

        private static void HandleEmptyDrawers()
        {
            HandleEmptyDrawers(BuffersWithSpace, BuffersWithSpace);
            HandleEmptyDrawers(FullBuffers, BuffersWithSpace);
        }

        private static void HandleEmptyDrawers(List<MultiBufferedIndirectDraw> drawers, List<MultiBufferedIndirectDraw> forEmptyDrawers)
        {
            for (int i = drawers.Count - 1; i >= 0; i--)
            {
                if (drawers[i].IsEmpty())
                {
                    if (drawers.Count(x => x.IsEmpty()) > 1)
                    {
                        drawers[i].Dispose();
                        drawers.RemoveAt(i);
                    }
                    else if (DrawFactory.HasAcceptableBufferSizes(drawers[i]))
                    {
                        drawers[i].Reset();

                        var drawer = drawers[i];
                        drawers.RemoveAt(i);
                        forEmptyDrawers.Add(drawer);
                    }
                    else
                    {
                        drawers[i].Dispose();
                        drawers.RemoveAt(i);
                    }
                }
            }
        }

        private static long GetGPUBufferSizeInMB()
        {
            long bufferSizeInBytes = BuffersWithSpace.Sum(x => x.GpuMemSize());
            bufferSizeInBytes += FullBuffers.Count > 0 ? FullBuffers.Sum(x => x.GpuMemSize()) : 0;
            bufferSizeInBytes += CopyingBuffers.Count > 0 ? CopyingBuffers.Sum(x => x.From.GpuMemSize() + x.To.GpuMemSize()) : 0;

            const int bytesToMBRatio = 1_000_000;
            return bufferSizeInBytes / bytesToMBRatio;
        }

        public static float GetBufferUtilization()
        {
            const int bytesToMBRatio = 1_000_000;
            long averageGridSize = DrawFactory.GetAverageGridMemUsage();
            long avgTotalGridMemUsage = averageGridSize * GridsDrawing;
            long avgTotalGridMemUsageInMB = avgTotalGridMemUsage / bytesToMBRatio;
            long gpuMemUsedInMB = GetGPUBufferSizeInMB();

            return (float)avgTotalGridMemUsageInMB / gpuMemUsedInMB;
        }

        public static void PrintDrawBufferUtilization()
        {
            PerfNumAverage<int> lol = new PerfNumAverage<int>(DrawBuffers, x => x);
            for (int i = 0; i < BuffersWithSpace.Count; i++)
            {
                lol.AddSample(BuffersWithSpace[i].GetCommandCount());
            }
            for (int i = 0; i < FullBuffers.Count; i++)
            {
                lol.AddSample(FullBuffers[i].GetCommandCount());
            }
            for (int i = 0; i < CopyingBuffers.Count; i++)
            {
                lol.AddSample(CopyingBuffers[i].From.GetCommandCount());
                lol.AddSample(CopyingBuffers[i].To.GetCommandCount());
            }
            lol.PrintHistogram();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}
