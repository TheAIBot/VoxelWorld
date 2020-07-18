using OpenGL;
using OpenGL.Constructs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using VoxelWorld.Shaders;
using static OpenGL.GenericVAO;

namespace VoxelWorld
{
    internal class IndirectDraw : IDisposable
    {
        private readonly struct CommandPair
        {
            public readonly VoxelGridInfo Grid;
            public readonly GeometryData Geom;

            public CommandPair(VoxelGridInfo grid, GeometryData geometry)
            {
                this.Grid = grid;
                this.Geom = geometry;
            }
        }

        private readonly List<CommandPair> TransferToBuffers = new List<CommandPair>();
        private readonly Dictionary<VoxelGridInfo, DrawElementsIndirectCommand> DrawCommands = new Dictionary<VoxelGridInfo, DrawElementsIndirectCommand>();
        private const int VERTEX_BUFFER_SIZE = 20_000;
        private const int INDICE_BUFFER_SIZE = 100_000;
        private const int COMMAND_BUFFER_SIZE = 2_000;
        private int VerticesAvailable = VERTEX_BUFFER_SIZE;
        private int IndicesAvailable = INDICE_BUFFER_SIZE;
        private int CommandsAvailable = COMMAND_BUFFER_SIZE;
        private int FirstAvailableVertexIndex = 0;
        private int FirstAvailableIndiceIndex = 0;
        private readonly object Lock = new object();
        private bool CommandsChangeSinceLastPrepareDraw = false;

        private readonly VBO<Vector3> VertexBuffer;
        private readonly VBO<Vector3> NormalBuffer;
        private readonly VBO<uint> IndiceBuffer;
        private readonly VBO<DrawElementsIndirectCommand> CommandBuffer;
        private readonly VAO Vao;

        public IndirectDraw()
        {
            VertexBuffer = new VBO<Vector3>(VERTEX_BUFFER_SIZE, BufferTarget.ArrayBuffer);
            NormalBuffer = new VBO<Vector3>(VERTEX_BUFFER_SIZE, BufferTarget.ArrayBuffer);
            IndiceBuffer = new VBO<uint>(INDICE_BUFFER_SIZE, BufferTarget.ElementArrayBuffer);
            CommandBuffer = new VBO<DrawElementsIndirectCommand>(COMMAND_BUFFER_SIZE, BufferTarget.DrawIndirectBuffer, BufferUsageHint.DynamicDraw);
            IGenericVBO[] vbos = new IGenericVBO[]
            {
                new GenericVBO<Vector3>(VertexBuffer, "vertex_pos"),
                new GenericVBO<Vector3>(NormalBuffer, "vertex_normal"),
                new GenericVBO<uint>(IndiceBuffer),
                new GenericVBO<DrawElementsIndirectCommand>(CommandBuffer),
            };
            Vao = new VAO(SimpleShader.GetShader(), vbos);
            Vao.DisposeChildren = true;
            Vao.DisposeElementArray = true;
        }

        public bool TryAddGeometry(VoxelGridInfo grid, GeometryData geometry)
        {
            lock (Lock)
            {
                if (VerticesAvailable >= geometry.Vertices.Length &&
                    IndicesAvailable >= geometry.Indices.Length &&
                    CommandsAvailable >= 1)
                {
                    VerticesAvailable -= geometry.Vertices.Length;
                    IndicesAvailable -= geometry.Indices.Length;
                    CommandsAvailable--;
                    TransferToBuffers.Add(new CommandPair(grid, geometry));
                    return true;
                }
                return false;
            }
        }

        public bool RemoveGeometry(VoxelGridInfo grid)
        {
            lock (Lock)
            {
                if (!DrawCommands.Remove(grid))
                {
                    int removed = TransferToBuffers.RemoveAll(x => x.Grid == grid);
                    if (removed > 1)
                    {
                        throw new Exception();
                    }
                    return removed == 1;
                }
                else
                {
                    CommandsChangeSinceLastPrepareDraw = true;
                    return true;
                }
            }
        }

        public void PrepareDraw()
        {
            lock (Lock)
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

                    Vector3[] verticesTemp = new Vector3[vertices];
                    uint[] indicesTemp = new uint[indices];

                    {
                        Span<Vector3> availableSpace = verticesTemp.AsSpan();
                        for (int i = 0; i < TransferToBuffers.Count; i++)
                        {
                            TransferToBuffers[i].Geom.Vertices.CopyTo(availableSpace);
                            availableSpace = availableSpace.Slice(TransferToBuffers[i].Geom.Vertices.Length);
                        }
                        VertexBuffer.BufferSubData(verticesTemp, verticesTemp.Length * Marshal.SizeOf<Vector3>(), FirstAvailableVertexIndex * Marshal.SizeOf<Vector3>());
                    }
                    {
                        Span<Vector3> availableSpace = verticesTemp.AsSpan();
                        for (int i = 0; i < TransferToBuffers.Count; i++)
                        {
                            TransferToBuffers[i].Geom.Normals.CopyTo(availableSpace);
                            availableSpace = availableSpace.Slice(TransferToBuffers[i].Geom.Normals.Length);
                        }
                        NormalBuffer.BufferSubData(verticesTemp, verticesTemp.Length * Marshal.SizeOf<Vector3>(), FirstAvailableVertexIndex * Marshal.SizeOf<Vector3>());
                    }

                    {
                        Span<uint> availableSpace = indicesTemp.AsSpan();
                        for (int i = 0; i < TransferToBuffers.Count; i++)
                        {
                            TransferToBuffers[i].Geom.Indices.CopyTo(availableSpace);
                            availableSpace = availableSpace.Slice(TransferToBuffers[i].Geom.Indices.Length);
                        }
                        IndiceBuffer.BufferSubData(indicesTemp, indicesTemp.Length * Marshal.SizeOf<uint>(), FirstAvailableIndiceIndex * Marshal.SizeOf<uint>());
                    }

                    for (int i = 0; i < TransferToBuffers.Count; i++)
                    {
                        GeometryData geom = TransferToBuffers[i].Geom;
                        DrawCommands.Add(TransferToBuffers[i].Grid, new DrawElementsIndirectCommand(geom.Indices.Length, 1, FirstAvailableIndiceIndex, FirstAvailableVertexIndex, 0));
                        FirstAvailableVertexIndex += geom.Vertices.Length;
                        FirstAvailableIndiceIndex += geom.Indices.Length;
                    }

                    TransferToBuffers.Clear();
                    CommandsChangeSinceLastPrepareDraw = true;
                }

                if (DrawCommands.Count > 0 && CommandsChangeSinceLastPrepareDraw)
                {
                    DrawElementsIndirectCommand[] commands = DrawCommands.Values.ToArray();
                    CommandBuffer.BufferSubData(commands);
                    CommandsChangeSinceLastPrepareDraw = false;
                }
            }
        }

        public void Draw()
        {
            lock (Lock)
            {
                if (DrawCommands.Count > 0)
                {
                    Vao.MultiDrawElementsIndirect(CommandBuffer, DrawCommands.Count);
                }
            }
        }

        public int CommandCount()
        {
            return DrawCommands.Count;
        }

        public void Dispose()
        {
            Vao.Dispose();
        }
    }

    internal static class MainThreadWork
    {
        private static readonly List<IndirectDraw> GridDrawBuffers = new List<IndirectDraw>();
        private static readonly List<(VoxelGridInfo grid, GeometryData geometry)> UnallocatedGridGeometry = new List<(VoxelGridInfo grid, GeometryData geometry)>();


        public static void MakeGridDrawable(VoxelGridInfo grid, GeometryData geometry)
        {
            lock (GridDrawBuffers)
            {
                for (int i = 0; i < GridDrawBuffers.Count; i++)
                {
                    if (GridDrawBuffers[i].TryAddGeometry(grid, geometry))
                    {
                        return;
                    }
                }

                UnallocatedGridGeometry.Add((grid, geometry));
            }
        }

        public static void RemoveDrawableGrid(VoxelGridInfo grid)
        {
            lock (GridDrawBuffers)
            {
                int removed = UnallocatedGridGeometry.RemoveAll(x => x.grid == grid);
                if (removed > 1)
                {
                    throw new Exception();
                }
                else if (removed == 1)
                {
                    return;
                }

                for (int i = 0; i < GridDrawBuffers.Count; i++)
                {
                    if (GridDrawBuffers[i].RemoveGeometry(grid))
                    {
                        return;
                    }
                }

                throw new Exception("Failed to remove grid");
            }
        }

        public static void DrawGrids()
        {
            lock (GridDrawBuffers)
            {
                if (UnallocatedGridGeometry.Count > 0)
                {
                    int index = UnallocatedGridGeometry.Count - 1;
                    while (index >= 0)
                    {
                        IndirectDraw draw = new IndirectDraw();

                        while (draw.TryAddGeometry(UnallocatedGridGeometry[index].grid, UnallocatedGridGeometry[index].geometry))
                        {
                            index--;
                            if (index < 0)
                            {
                                break;
                            }
                        }

                        GridDrawBuffers.Add(draw);
                    }

                    UnallocatedGridGeometry.Clear();
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
                        GridDrawBuffers[i].Dispose();
                        GridDrawBuffers.RemoveAt(i);
                    }
                }

                //Console.WriteLine(GridDrawBuffers.Count);
            }
        }
    }
}
