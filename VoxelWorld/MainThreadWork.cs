using OpenGL;
using OpenGL.Constructs;
using System;
using System.Buffers;
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

        public void RemoveGeometry(VoxelGridInfo grid)
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

                Vector3[] verticesTempArr = ArrayPool<Vector3>.Shared.Rent(vertices);
                uint[] indicesTempArr = ArrayPool<uint>.Shared.Rent(indices);

                Span<Vector3> verticesTemp = verticesTempArr.AsSpan(0, vertices);
                Span<uint> indicesTemp = indicesTempArr.AsSpan(0, indices);

                {
                    Span<Vector3> availableSpace = verticesTemp;
                    for (int i = 0; i < TransferToBuffers.Count; i++)
                    {
                        TransferToBuffers[i].Geom.Vertices.CopyTo(availableSpace);
                        availableSpace = availableSpace.Slice(TransferToBuffers[i].Geom.Vertices.Length);
                    }
                    VertexBuffer.BufferSubData(verticesTempArr, verticesTemp.Length * Marshal.SizeOf<Vector3>(), FirstAvailableVertexIndex * Marshal.SizeOf<Vector3>());
                }
                {
                    Span<Vector3> availableSpace = verticesTemp;
                    for (int i = 0; i < TransferToBuffers.Count; i++)
                    {
                        TransferToBuffers[i].Geom.Normals.CopyTo(availableSpace);
                        availableSpace = availableSpace.Slice(TransferToBuffers[i].Geom.Normals.Length);
                    }
                    NormalBuffer.BufferSubData(verticesTempArr, verticesTemp.Length * Marshal.SizeOf<Vector3>(), FirstAvailableVertexIndex * Marshal.SizeOf<Vector3>());
                }

                {
                    Span<uint> availableSpace = indicesTemp;
                    for (int i = 0; i < TransferToBuffers.Count; i++)
                    {
                        TransferToBuffers[i].Geom.Indices.CopyTo(availableSpace);
                        availableSpace = availableSpace.Slice(TransferToBuffers[i].Geom.Indices.Length);
                    }
                    IndiceBuffer.BufferSubData(indicesTempArr, indicesTemp.Length * Marshal.SizeOf<uint>(), FirstAvailableIndiceIndex * Marshal.SizeOf<uint>());
                }

                ArrayPool<Vector3>.Shared.Return(verticesTempArr);
                ArrayPool<uint>.Shared.Return(indicesTempArr);

                for (int i = 0; i < TransferToBuffers.Count; i++)
                {
                    GeometryData geom = TransferToBuffers[i].Geom;
                    DrawCommands.Add(TransferToBuffers[i].Grid, new DrawElementsIndirectCommand(geom.Indices.Length, 1, FirstAvailableIndiceIndex, FirstAvailableVertexIndex, 0));
                    FirstAvailableVertexIndex += geom.Vertices.Length;
                    FirstAvailableIndiceIndex += geom.Indices.Length;

                    TransferToBuffers[i].Geom.Reuse();
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

            VerticesAvailable = VERTEX_BUFFER_SIZE;
            IndicesAvailable = INDICE_BUFFER_SIZE;
            CommandsAvailable = COMMAND_BUFFER_SIZE;
            FirstAvailableVertexIndex = 0;
            FirstAvailableIndiceIndex = 0;

            return true;
        }

        public void Dispose()
        {
            Vao.Dispose();
        }
    }

    internal static class MainThreadWork
    {
        private enum CmdType
        {
            Add,
            Remove
        }

        private readonly struct Command
        {
            public readonly VoxelGridInfo Grid;
            public readonly GeometryData GeoData;
            public readonly CmdType CType;

            public Command(CmdType cmd, VoxelGridInfo grid, GeometryData data)
            {
                this.Grid = grid;
                this.GeoData = data;
                this.CType = cmd;
            }
        }

        private static ConcurrentQueue<Command> Commands = new ConcurrentQueue<Command>();
        private static ConcurrentQueue<Command> CommandSwitchout = new ConcurrentQueue<Command>();

        private static readonly List<IndirectDraw> GridDrawBuffers = new List<IndirectDraw>();
        private static readonly Dictionary<VoxelGridInfo, IndirectDraw> GridsToBuffer = new Dictionary<VoxelGridInfo, IndirectDraw>();


        public static void MakeGridDrawable(VoxelGridInfo grid, GeometryData geometry)
        {
            Commands.Enqueue(new Command(CmdType.Add, grid, geometry));
        }

        public static void RemoveDrawableGrid(VoxelGridInfo grid)
        {
            Commands.Enqueue(new Command(CmdType.Remove, grid, null));
        }

        public static void DrawGrids()
        {
            //switch queues so items stops being added to the queue
            //we willwork on now
            var cmds = Interlocked.Exchange(ref Commands, CommandSwitchout);
            CommandSwitchout = cmds;

            //Console.WriteLine(cmds.Count);

            int indexFirstBufferNotFull = 0;
            while (!cmds.IsEmpty)
            {
                Command cmd;
                if (!cmds.TryDequeue(out cmd))
                {
                    throw new Exception("Expected to dequeue a command but no command was found.");
                }

                if (cmd.CType == CmdType.Add)
                {
                    bool allocatedGrid = false;
                    while (!allocatedGrid)
                    {
                        for (int i = indexFirstBufferNotFull; i < GridDrawBuffers.Count; i++)
                        {
                            if (!GridDrawBuffers[i].TryAddGeometry(cmd.Grid, cmd.GeoData))
                            {
                                if (i == indexFirstBufferNotFull)
                                {
                                    i++;
                                }
                            }
                            else
                            {
                                GridsToBuffer.Add(cmd.Grid, GridDrawBuffers[i]);
                                allocatedGrid = true;
                                break;
                            }
                        }

                        if (!allocatedGrid)
                        {
                            GridDrawBuffers.Add(new IndirectDraw());
                        }
                    }
                }
                else if (cmd.CType == CmdType.Remove)
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
    }
}
