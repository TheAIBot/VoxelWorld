using OpenGL;
using OpenGL.Constructs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using VoxelWorld.Shaders;
using static OpenGL.GenericVAO;

namespace VoxelWorld
{
    internal class IndirectDraw : IDisposable
    {
        private readonly List<CommandPair> TransferToBuffers = new List<CommandPair>();
        IndirectDrawCmdManager DrawCommands = new IndirectDrawCmdManager();
        private bool CommandsChangeSinceLastPrepareDraw = false;

        private readonly SlidingVBO<Vector3> VertexBuffer;
        private readonly SlidingVBO<Vector3> NormalBuffer;
        private readonly SlidingVBO<uint> IndiceBuffer;
        private readonly SlidingVBO<DrawElementsIndirectCommand> CommandBuffer;
        private readonly VAO Vao;

        public IndirectDraw(int vertexBufferSize, int indiceBufferSize, int commandBufferSize)
        {
            VertexBuffer = new SlidingVBO<Vector3>(new VBO<Vector3>(vertexBufferSize, BufferTarget.ArrayBuffer));
            NormalBuffer = new SlidingVBO<Vector3>(new VBO<Vector3>(vertexBufferSize, BufferTarget.ArrayBuffer));
            IndiceBuffer = new SlidingVBO<uint>(new VBO<uint>(indiceBufferSize, BufferTarget.ElementArrayBuffer));
            CommandBuffer = new SlidingVBO<DrawElementsIndirectCommand>(new VBO<DrawElementsIndirectCommand>(commandBufferSize, BufferTarget.DrawIndirectBuffer, BufferUsageHint.DynamicDraw));
            IGenericVBO[] vbos = new IGenericVBO[]
            {
                new GenericVBO<Vector3>(VertexBuffer.Buffer, "vertex_pos"),
                new GenericVBO<Vector3>(NormalBuffer.Buffer, "vertex_normal"),
                new GenericVBO<uint>(IndiceBuffer.Buffer),
                new GenericVBO<DrawElementsIndirectCommand>(CommandBuffer.Buffer),
            };
            Vao = new VAO(SimpleShader.GetShader(), vbos);
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
                if (gridIndex  == -1)
                {
                    throw new Exception("Failed to find grid and remove it.");
                }
                TransferToBuffers[gridIndex].Geom.Reuse();
                TransferToBuffers.RemoveAt(gridIndex);
            }

            CommandsChangeSinceLastPrepareDraw = true;
        }

        public void CopyToGPU()
        {
            if (TransferToBuffers.Count > 0)
            {
                using var vertexRange = VertexBuffer.MapReservedRange(BufferAccessMask.MapWriteBit | BufferAccessMask.MapInvalidateRangeBit);
                using var normalRange = NormalBuffer.MapReservedRange(BufferAccessMask.MapWriteBit | BufferAccessMask.MapInvalidateRangeBit);
                using var indiceRange = IndiceBuffer.MapReservedRange(BufferAccessMask.MapWriteBit | BufferAccessMask.MapInvalidateRangeBit);

                foreach (var transfer in TransferToBuffers)
                {
                    GeometryData geometry = transfer.Geom;

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

                using var commandRange = CommandBuffer.MapReservedRange(BufferAccessMask.MapWriteBit | BufferAccessMask.MapInvalidateBufferBit);
                foreach (var drawCmd in DrawCommands.GetCommands())
                {
                    commandRange.Add(drawCmd);
                }

                CommandsChangeSinceLastPrepareDraw = false;
            }
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
