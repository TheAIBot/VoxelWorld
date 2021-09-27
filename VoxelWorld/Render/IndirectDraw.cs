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
        private readonly Dictionary<VoxelGridHierarchy, DrawElementsIndirectCommand> DrawCommands = new Dictionary<VoxelGridHierarchy, DrawElementsIndirectCommand>();
        private const int VERTEX_BUFFER_SIZE = 20_000;
        private const int INDICE_BUFFER_SIZE = 100_000;
        private const int COMMAND_BUFFER_SIZE = 2_000;
        private bool CommandsChangeSinceLastPrepareDraw = false;

        private readonly SlidingVBO<Vector3> VertexBuffer;
        private readonly SlidingVBO<Vector3> NormalBuffer;
        private readonly SlidingVBO<uint> IndiceBuffer;
        private readonly SlidingVBO<DrawElementsIndirectCommand> CommandBuffer;
        private readonly VAO Vao;

        public IndirectDraw()
        {
            VertexBuffer = new SlidingVBO<Vector3>(new VBO<Vector3>(VERTEX_BUFFER_SIZE, BufferTarget.ArrayBuffer));
            NormalBuffer = new SlidingVBO<Vector3>(new VBO<Vector3>(VERTEX_BUFFER_SIZE, BufferTarget.ArrayBuffer));
            IndiceBuffer = new SlidingVBO<uint>(new VBO<uint>(INDICE_BUFFER_SIZE, BufferTarget.ElementArrayBuffer));
            CommandBuffer = new SlidingVBO<DrawElementsIndirectCommand>(new VBO<DrawElementsIndirectCommand>(COMMAND_BUFFER_SIZE, BufferTarget.DrawIndirectBuffer, BufferUsageHint.DynamicDraw));
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

        public void PrepareDraw()
        {
            if (TransferToBuffers.Count > 0)
            {
                using var vertexRange = VertexBuffer.MapReservedRange();
                using var normalRange = NormalBuffer.MapReservedRange();
                using var indiceRange = IndiceBuffer.MapReservedRange();

                foreach (var transfer in TransferToBuffers)
                {
                    GeometryData geometry = transfer.Geom;

                    DrawCommands.Add(transfer.Grid, new DrawElementsIndirectCommand(geometry.Indices.Length, 1, IndiceBuffer.FirstAvailableIndex, VertexBuffer.FirstAvailableIndex, 0));

                    vertexRange.AddRange(geometry.Vertices);
                    normalRange.AddRange(geometry.Normals);
                    indiceRange.AddRange(geometry.Indices);

                    geometry.Reuse();
                }

                TransferToBuffers.Clear();
                CommandsChangeSinceLastPrepareDraw = true;
            }

            if (DrawCommands.Count > 0 && CommandsChangeSinceLastPrepareDraw)
            {
                CommandBuffer.Reset();
                CommandBuffer.ReserveSpace(DrawCommands.Count);

                using var commandRange = CommandBuffer.MapReservedRange();
                foreach (var drawCmd in DrawCommands.Values)
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

        public int CommandCount()
        {
            return DrawCommands.Count;
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

        public void Dispose()
        {
            Vao.Dispose();
        }
    }
}
