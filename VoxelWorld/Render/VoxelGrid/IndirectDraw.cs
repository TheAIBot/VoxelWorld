using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private readonly GL _openGl;
        private nint? _syncFlag;

        public IndirectDraw(GL openGl, int vertexBufferSize, int indiceBufferSize, int commandBufferSize)
        {
            _openGl = openGl;
            VertexBuffer = new SlidingVBO<Vector3>(openGl, new VBO<Vector3>(openGl, vertexBufferSize, BufferStorageTarget.ArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit));
            NormalBuffer = new SlidingVBO<Vector3>(openGl, new VBO<Vector3>(openGl, vertexBufferSize, BufferStorageTarget.ArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit));
            IndiceBuffer = new SlidingVBO<uint>(openGl, new VBO<uint>(openGl, indiceBufferSize, BufferStorageTarget.ElementArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit));
            CommandBuffer = new SlidingVBO<DrawElementsIndirectCommand>(openGl, new VBO<DrawElementsIndirectCommand>(openGl, commandBufferSize, BufferStorageTarget.DrawIndirectBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit));
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
            if (HasSpaceFor(geometry.Vertices.Length, geometry.Indices.Length, 1))
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

        public bool TryRemoveGeometry(VoxelGridHierarchy grid, [NotNullWhen(true)] out GeometryData geometryData)
        {
            if (!DrawCommands.Remove(grid))
            {
                int gridIndex = TransferToBuffers.FindIndex(x => x.Grid == grid);
                if (gridIndex == -1)
                {
                    throw new Exception("Failed to find grid and remove it.");
                }
                geometryData = TransferToBuffers[gridIndex].Geometry;
                TransferToBuffers.RemoveAt(gridIndex);
                return true;
            }

            CommandBuffer.ReserveSpace(-1);
            CommandsChangeSinceLastPrepareDraw = true;
            geometryData = null;
            return false;
        }

        public GeometryData[] CopyToGPU()
        {
            if (TransferToBuffers.Count == 0)
            {
                return Array.Empty<GeometryData>();
            }

            while (_syncFlag.HasValue)
            {
                GLEnum lol = _openGl.ClientWaitSync(_syncFlag.Value, SyncObjectMask.Bit, 1);
                if (lol == GLEnum.AlreadySignaled || lol == GLEnum.ConditionSatisfied)
                {
                    break;
                }
            }

            GeometryData[] geometryTransfered = new GeometryData[TransferToBuffers.Count];
            var vertexRange = VertexBuffer.GetReservedRange();
            var normalRange = NormalBuffer.GetReservedRange();
            var indiceRange = IndiceBuffer.GetReservedRange();

            for (int i = 0; i < TransferToBuffers.Count; i++)
            {
                GeometryData geometry = TransferToBuffers[i].Geometry;
                geometryTransfered[i] = geometry;

                DrawCommands.Add(TransferToBuffers[i].Grid, geometry, IndiceBuffer.FirstAvailableIndex, VertexBuffer.FirstAvailableIndex);

                geometry.Vertices.CopyTo(vertexRange);
                vertexRange = vertexRange.Slice(geometry.Vertices.Length);

                geometry.Normals.CopyTo(normalRange);
                normalRange = normalRange.Slice(geometry.Normals.Length);

                geometry.Indices.CopyTo(indiceRange);
                indiceRange = indiceRange.Slice(geometry.Indices.Length);

                VertexBuffer.UseSpace(geometry.Vertices.Length);
                NormalBuffer.UseSpace(geometry.Normals.Length);
                IndiceBuffer.UseSpace(geometry.Indices.Length);
            }

            TransferToBuffers.Clear();
            CommandsChangeSinceLastPrepareDraw = true;

            return geometryTransfered;
        }

        public void SendCommandsToGPU()
        {
            if (DrawCommands.Count > 0 && CommandsChangeSinceLastPrepareDraw)
            {
                CommandBuffer.Reset();
                CommandBuffer.ReserveSpace(DrawCommands.Count);

                var commandRange = CommandBuffer.GetReservedRange();
                foreach (var drawCmd in DrawCommands.GetCommands())
                {
                    commandRange[0] = drawCmd;
                    commandRange = commandRange.Slice(1);
                    CommandBuffer.UseSpace(1);
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

                if (_syncFlag.HasValue)
                {
                    _openGl.DeleteSync(_syncFlag.Value);
                }
                _syncFlag = _openGl.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
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

        public void Reset()
        {
            TransferToBuffers.Clear();
            DrawCommands.Clear();

            VertexBuffer.Reset();
            NormalBuffer.Reset();
            IndiceBuffer.Reset();
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
