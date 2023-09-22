using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using VoxelWorld.Shaders;
using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Hierarchy;
using static VoxelWorld.Render.VoxelGrid.VAO;

namespace VoxelWorld.Render.VoxelGrid
{

    internal sealed class IndirectDraw : IDisposable
    {
        private readonly List<CommandPair> TransferToBuffers = new List<CommandPair>();
        private readonly IndirectDrawCmdManager DrawCommands = new IndirectDrawCmdManager();
        private bool CommandsChangeSinceLastPrepareDraw = false;

        private readonly SlidingVBO<Vector3> VertexBuffer;
        private readonly SlidingVBO<Vector3> NormalBuffer;
        private readonly SlidingVBO<uint> IndiceBuffer;
        private readonly SlidingVBO<DrawElementsIndirectCommand> CommandBuffer;
        private readonly VAO Vao;

        public IndirectDraw(GL openGl, int vertexBufferSize, int indiceBufferSize, int commandBufferSize)
        {
            VertexBuffer = new SlidingVBO<Vector3>(openGl, new VBO<Vector3>(openGl, vertexBufferSize, BufferTargetARB.ArrayBuffer));
            NormalBuffer = new SlidingVBO<Vector3>(openGl, new VBO<Vector3>(openGl, vertexBufferSize, BufferTargetARB.ArrayBuffer));
            IndiceBuffer = new SlidingVBO<uint>(openGl, new VBO<uint>(openGl, indiceBufferSize, BufferTargetARB.ElementArrayBuffer));
            CommandBuffer = new SlidingVBO<DrawElementsIndirectCommand>(openGl, new VBO<DrawElementsIndirectCommand>(openGl, commandBufferSize, BufferTargetARB.DrawIndirectBuffer, BufferUsageARB.DynamicDraw));
            IGenericVBO[] vbos = new IGenericVBO[]
            {
                new GenericVBO<Vector3>(VertexBuffer.Buffer, "vertex_pos"),
                new GenericVBO<Vector3>(NormalBuffer.Buffer, "vertex_normal"),
                new GenericVBO<uint>(IndiceBuffer.Buffer),
                new GenericVBO<DrawElementsIndirectCommand>(CommandBuffer.Buffer),
            };
            Vao = new VAO(openGl, SimpleShader.GetShader(openGl), vbos);
            Vao.DisposeChildren = false;
            Vao.DisposeElementArray = false;
        }

        public bool TryAddGeometry(VoxelGridHierarchy grid, GeometryData geometry)
        {
            if (VertexBuffer.SpaceAvailable >= geometry.Vertices.Length &&
                IndiceBuffer.SpaceAvailable >= geometry.Indices.Length &&
                CommandBuffer.SpaceAvailable >= 1)
            {
                VertexBuffer.ReserveSpace(geometry.Vertices.Length);
                NormalBuffer.ReserveSpace(geometry.Normals.Length);
                IndiceBuffer.ReserveSpace(geometry.Indices.Length);
                CommandBuffer.ReserveSpace(1);
                TransferToBuffers.Add(new CommandPair(grid, geometry));
                return true;
            }
            return false;
        }

        public void RemoveGeometry(VoxelGridHierarchy grid)
        {
            if (!DrawCommands.Remove(grid))
            {
                int gridIndex = TransferToBuffers.FindIndex(x => x.Grid == grid);
                if (gridIndex == -1)
                {
                    throw new Exception("Failed to find grid and remove it.");
                }
                TransferToBuffers[gridIndex].Geometry.Reuse();
                TransferToBuffers.RemoveAt(gridIndex);
            }

            CommandBuffer.ReserveSpace(-1);
            CommandsChangeSinceLastPrepareDraw = true;
        }

        public void CopyToGPU()
        {
            if (TransferToBuffers.Count > 0)
            {
                using var vertexRange = VertexBuffer.MapReservedRange(MapBufferAccessMask.WriteBit | MapBufferAccessMask.InvalidateRangeBit);
                using var normalRange = NormalBuffer.MapReservedRange(MapBufferAccessMask.WriteBit | MapBufferAccessMask.InvalidateRangeBit);
                using var indiceRange = IndiceBuffer.MapReservedRange(MapBufferAccessMask.WriteBit | MapBufferAccessMask.InvalidateRangeBit);

                foreach (var transfer in TransferToBuffers)
                {
                    GeometryData geometry = transfer.Geometry;

                    DrawCommands.Add(transfer.Grid, geometry, IndiceBuffer.FirstAvailableIndex, VertexBuffer.FirstAvailableIndex);

                    vertexRange.AddRange(geometry.Vertices);
                    normalRange.AddRange(geometry.Normals);
                    indiceRange.AddRange(geometry.Indices);

                    geometry.Reuse();
                }

                TransferToBuffers.Clear();
                CommandsChangeSinceLastPrepareDraw = true;
            }
        }

        public void SendCommandsToGPU()
        {
            if (DrawCommands.Count > 0 && CommandsChangeSinceLastPrepareDraw)
            {
                CommandBuffer.Reset();
                CommandBuffer.ReserveSpace(DrawCommands.Count);

                using var commandRange = CommandBuffer.MapReservedRange(MapBufferAccessMask.WriteBit | MapBufferAccessMask.InvalidateBufferBit);
                foreach (var drawCmd in DrawCommands.GetCommands())
                {
                    commandRange.Add(drawCmd);
                }

                CommandsChangeSinceLastPrepareDraw = false;
            }
        }

        public bool HasSpaceFor(int vertexCount, int indiceCount, int cmdCount)
        {
            return VertexBuffer.SpaceAvailable >= vertexCount &&
                NormalBuffer.SpaceAvailable >= vertexCount &&
                IndiceBuffer.SpaceAvailable >= indiceCount &&
                CommandBuffer.SpaceAvailable >= cmdCount;
        }

        public int GetVertexCount()
        {
            int vertexCount = 0;
            foreach (var drawInfo in DrawCommands.GetCommandsInfo())
            {
                vertexCount += drawInfo.VertexCount;
            }

            return vertexCount;
        }

        public int GetIndiceCount()
        {
            int indiceCount = 0;
            foreach (var drawInfo in DrawCommands.GetCommandsInfo())
            {
                indiceCount += drawInfo.IndiceCount;
            }

            return indiceCount;
        }

        public int GetCommandCount()
        {
            return DrawCommands.Count;
        }

        public IEnumerable<VoxelGridHierarchy> GetGridsDrawing()
        {
            return DrawCommands.GetGrids();
        }

        public int TransferDrawCommands(IndirectDraw dstDrawer)
        {
            int copyCommands = 0;
            foreach (var gridDrawInfo in DrawCommands.GetGridCommandsInfo())
            {
                VoxelGridHierarchy grid = gridDrawInfo.Key;
                DrawCommandInfo drawInfo = gridDrawInfo.Value;

                int dstVertexOffset = dstDrawer.VertexBuffer.FirstAvailableIndex;
                int dstIndiceOffset = dstDrawer.IndiceBuffer.FirstAvailableIndex;

                VertexBuffer.CopyTo(dstDrawer.VertexBuffer, drawInfo.VertexOffset, dstVertexOffset, drawInfo.VertexCount);
                NormalBuffer.CopyTo(dstDrawer.NormalBuffer, drawInfo.VertexOffset, dstVertexOffset, drawInfo.VertexCount);
                IndiceBuffer.CopyTo(dstDrawer.IndiceBuffer, drawInfo.IndiceOffset, dstIndiceOffset, drawInfo.IndiceCount);
                dstDrawer.CommandBuffer.ReserveSpace(1);
                dstDrawer.DrawCommands.Add(grid, dstIndiceOffset, drawInfo.IndiceCount, dstVertexOffset, drawInfo.VertexCount);

                copyCommands++;
            }

            DrawCommands.Clear();
            dstDrawer.CommandsChangeSinceLastPrepareDraw = true;

            return copyCommands;
        }

        public void Draw()
        {
            if (DrawCommands.Count > 0)
            {
                Vao.MultiDrawElementsIndirect(CommandBuffer.Buffer, DrawCommands.Count);
            }
        }

        public int VertexBufferSize()
        {
            return VertexBuffer.Buffer.Count;
        }

        public int IndiceBufferSize()
        {
            return IndiceBuffer.Buffer.Count;
        }

        public bool IsEmpty()
        {
            return TransferToBuffers.Count == 0 && DrawCommands.Count == 0;
        }

        public bool Reset()
        {
            if (TransferToBuffers.Count > 0 || DrawCommands.Count > 0)
            {
                return false;
            }

            TransferToBuffers.Clear();
            DrawCommands.Clear();

            VertexBuffer.Reset();
            NormalBuffer.Reset();
            IndiceBuffer.Reset();

            return true;
        }

        public long GpuMemSize()
        {
            return VertexBuffer.GpuMemSize() +
                NormalBuffer.GpuMemSize() +
                IndiceBuffer.GpuMemSize() +
                CommandBuffer.GpuMemSize();
        }

        public void Dispose()
        {
            Vao.Dispose();
            VertexBuffer.Dispose();
            NormalBuffer.Dispose();
            IndiceBuffer.Dispose();
            CommandBuffer.Dispose();
        }
    }
}
