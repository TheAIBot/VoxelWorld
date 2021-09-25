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
        private int CommandsAvailable = COMMAND_BUFFER_SIZE;
        private bool CommandsChangeSinceLastPrepareDraw = false;

        private readonly SlidingVBO<Vector3> VertexBuffer;
        private readonly SlidingVBO<Vector3> NormalBuffer;
        private readonly SlidingVBO<uint> IndiceBuffer;
        private readonly VBO<DrawElementsIndirectCommand> CommandBuffer;
        private readonly VAO Vao;

        public IndirectDraw()
        {
            VertexBuffer = new SlidingVBO<Vector3>(new VBO<Vector3>(VERTEX_BUFFER_SIZE, BufferTarget.ArrayBuffer));
            NormalBuffer = new SlidingVBO<Vector3>(new VBO<Vector3>(VERTEX_BUFFER_SIZE, BufferTarget.ArrayBuffer));
            IndiceBuffer = new SlidingVBO<uint>(new VBO<uint>(INDICE_BUFFER_SIZE, BufferTarget.ElementArrayBuffer));
            CommandBuffer = new VBO<DrawElementsIndirectCommand>(COMMAND_BUFFER_SIZE, BufferTarget.DrawIndirectBuffer, BufferUsageHint.DynamicDraw);
            IGenericVBO[] vbos = new IGenericVBO[]
            {
                new GenericVBO<Vector3>(VertexBuffer.Buffer, "vertex_pos"),
                new GenericVBO<Vector3>(NormalBuffer.Buffer, "vertex_normal"),
                new GenericVBO<uint>(IndiceBuffer.Buffer),
                new GenericVBO<DrawElementsIndirectCommand>(CommandBuffer),
            };
            Vao = new VAO(SimpleShader.GetShader(), vbos);
            Vao.DisposeChildren = false;
            Vao.DisposeElementArray = false;
        }

        public bool TryAddGeometry(VoxelGridHierarchy grid, GeometryData geometry)
        {
            if (VertexBuffer.SpaceAvailable >= geometry.Vertices.Length &&
                IndiceBuffer.SpaceAvailable >= geometry.Indices.Length &&
                CommandsAvailable >= 1)
            {
                VertexBuffer.ReserveSpace(geometry.Vertices.Length);
                NormalBuffer.ReserveSpace(geometry.Normals.Length);
                IndiceBuffer.ReserveSpace(geometry.Indices.Length);
                CommandsAvailable--;
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
                int vertices = 0;
                int indices = 0;
                for (int i = 0; i < TransferToBuffers.Count; i++)
                {
                    vertices += TransferToBuffers[i].Geom.Vertices.Length;
                    indices += TransferToBuffers[i].Geom.Indices.Length;
                }

                VertexBuffer.AddCommandsGeom(TransferToBuffers, static x => x.Geom.VerticesAsMemSpan);
                NormalBuffer.AddCommandsGeom(TransferToBuffers, static x => x.Geom.NormalsAsMemSpan);
                IndiceBuffer.AddCommandsGeom(TransferToBuffers, static x => x.Geom.IndicesAsMemSpan);

                for (int i = 0; i < TransferToBuffers.Count; i++)
                {
                    GeometryData geom = TransferToBuffers[i].Geom;
                    DrawCommands.Add(TransferToBuffers[i].Grid, new DrawElementsIndirectCommand(geom.Indices.Length, 1, IndiceBuffer.FirstAvailableIndex, VertexBuffer.FirstAvailableIndex, 0));
                    VertexBuffer.UseSpace(geom.Vertices.Length);
                    NormalBuffer.UseSpace(geom.Normals.Length);
                    IndiceBuffer.UseSpace(geom.Indices.Length);

                    TransferToBuffers[i].Geom.Reuse();
                }

                TransferToBuffers.Clear();
                CommandsChangeSinceLastPrepareDraw = true;
            }

            if (DrawCommands.Count > 0 && CommandsChangeSinceLastPrepareDraw)
            {
                using var commandsArr = new RentedArray<DrawElementsIndirectCommand>(DrawCommands.Count);
                DrawCommands.Values.CopyTo(commandsArr.Arr, 0);

                CommandBuffer.BufferSubData(commandsArr.Arr, commandsArr.Length * Marshal.SizeOf<DrawElementsIndirectCommand>());
                CommandsChangeSinceLastPrepareDraw = false;
            }
        }

        public void Draw()
        {
            if (DrawCommands.Count > 0)
            {
                Vao.MultiDrawElementsIndirect(CommandBuffer, DrawCommands.Count);
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
