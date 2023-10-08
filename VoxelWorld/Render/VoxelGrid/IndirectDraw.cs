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
        private readonly Dictionary<VoxelGridHierarchy, DrawInformation> DrawInformations = new Dictionary<VoxelGridHierarchy, DrawInformation>();
        private bool CommandsChangeSinceLastPrepareDraw = false;

        private readonly SlidingVBO<Vector3> GridPositionBuffer;
        private readonly SlidingVBO<float> GridSizeBuffer;
        private readonly SlidingVBO<byte> NormalBuffer;
        private readonly SlidingVBO<uint> IndiceBuffer;
        private readonly SlidingVBO<uint> SizeBuffer;
        private readonly SlidingVBO<uint> BaseVertexIndexBuffer;
        private readonly SlidingVBO<DrawElementsIndirectCommand> CommandBuffer;
        private readonly VAO Vao;
        private readonly GL _openGl;
        private nint? _syncFlag;

        public IndirectDraw(GL openGl, int vertexBufferSize, int indiceBufferSize, int commandBufferSize)
        {
            _openGl = openGl;
            GridPositionBuffer = new SlidingVBO<Vector3>(openGl, new VBO<Vector3>(openGl, commandBufferSize, BufferStorageTarget.ArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit)
            {
                Divisor = 1,
            });
            GridSizeBuffer = new SlidingVBO<float>(openGl, new VBO<float>(openGl, commandBufferSize, BufferStorageTarget.ArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit)
            {
                Divisor = 1,
            });
            NormalBuffer = new SlidingVBO<byte>(openGl, new VBO<byte>(openGl, vertexBufferSize, BufferStorageTarget.ArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit)
            {
                CastToFloat = false,
            });
            IndiceBuffer = new SlidingVBO<uint>(openGl, new VBO<uint>(openGl, indiceBufferSize, BufferStorageTarget.ElementArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit)
            {
                CastToFloat = false
            });
            SizeBuffer = new SlidingVBO<uint>(openGl, new VBO<uint>(openGl, commandBufferSize, BufferStorageTarget.ArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit)
            {
                CastToFloat = false,
                Divisor = 1,
            });
            BaseVertexIndexBuffer = new SlidingVBO<uint>(openGl, new VBO<uint>(openGl, commandBufferSize, BufferStorageTarget.ArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit)
            {
                CastToFloat = false,
                Divisor = 1,
            });
            CommandBuffer = new SlidingVBO<DrawElementsIndirectCommand>(openGl, new VBO<DrawElementsIndirectCommand>(openGl, commandBufferSize, BufferStorageTarget.DrawIndirectBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit | BufferStorageMask.MapCoherentBit));
            IGenericVBO[] vbos = new IGenericVBO[]
            {
                new GenericVBO<Vector3>(GridPositionBuffer.Buffer, "gridPosition"),
                new GenericVBO<float>(GridSizeBuffer.Buffer, "gridSize"),
                new GenericVBO<byte>(NormalBuffer.Buffer, "vertex_normal"),
                new GenericVBO<uint>(IndiceBuffer.Buffer),
                new GenericVBO<uint>(SizeBuffer.Buffer, "size"),
                new GenericVBO<uint>(BaseVertexIndexBuffer.Buffer, "baseVertexIndex"),
                new GenericVBO<DrawElementsIndirectCommand>(CommandBuffer.Buffer),
            };
            Vao = new VAO(openGl, SimpleShader.GetShader(openGl), vbos);
            Vao.DisposeChildren = false;
            Vao.DisposeElementArray = false;
        }

        private void Add(VoxelGridHierarchy grid, GeometryData geometry, int gridPositionOffset, int firstIndice, int firstVertex)
        {
            Add(grid, gridPositionOffset, firstIndice, geometry.Indices.Length, firstVertex, geometry.Normals.Length);
        }

        private void Add(VoxelGridHierarchy grid, int gridPositionOffset, int firstIndice, int indiceCount, int firstVertex, int normalCount)
        {
            DrawInformations.Add(grid, new DrawInformation(new DrawElementsIndirectCommand((uint)indiceCount, 1u, (uint)firstIndice, (uint)firstVertex, (uint)gridPositionOffset),
                                                           new DrawCommandInfo(gridPositionOffset, firstIndice, indiceCount, firstVertex, normalCount)));
        }

        public bool TryAddGeometry(VoxelGridHierarchy grid, GeometryData geometry)
        {
            if (HasSpaceFor(geometry.Normals.Length, geometry.Indices.Length, 1))
            {
                GridPositionBuffer.ReserveSpace(1);
                GridSizeBuffer.ReserveSpace(1);
                NormalBuffer.ReserveSpace(geometry.Normals.Length);
                IndiceBuffer.ReserveSpace(geometry.Indices.Length);
                SizeBuffer.ReserveSpace(1);
                BaseVertexIndexBuffer.ReserveSpace(1);
                CommandBuffer.ReserveSpace(1);
                TransferToBuffers.Add(new CommandPair(grid, geometry));
                return true;
            }
            return false;
        }

        public bool TryRemoveGeometry(VoxelGridHierarchy grid, [NotNullWhen(true)] out GeometryData geometryData)
        {
            if (!DrawInformations.Remove(grid))
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

        public bool IsTransferringToBuffers() => TransferToBuffers.Count > 0;

        public (GeometryData[], long) CopyToGPU()
        {
            if (TransferToBuffers.Count == 0)
            {
                return (Array.Empty<GeometryData>(), 0);
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
            using var gridPositionRange = GridPositionBuffer.GetReservedRange();
            using var gridSizeRange = GridSizeBuffer.GetReservedRange();
            using var normalRange = NormalBuffer.GetReservedRange();
            using var indiceRange = IndiceBuffer.GetReservedRange();
            using var sizeRange = SizeBuffer.GetReservedRange();
            using var baseVertexIndexRange = BaseVertexIndexBuffer.GetReservedRange();
            long copiedBytes = 0;

            for (int i = 0; i < TransferToBuffers.Count; i++)
            {
                GeometryData geometry = TransferToBuffers[i].Geometry;
                geometryTransfered[i] = geometry;

                int baseVertexIndex = NormalBuffer.FirstAvailableIndex;
                Add(TransferToBuffers[i].Grid, geometry, GridPositionBuffer.FirstAvailableIndex, IndiceBuffer.FirstAvailableIndex, baseVertexIndex);

                copiedBytes += gridPositionRange.Add(geometry.GridTopLeftPosition);
                copiedBytes += gridSizeRange.Add(geometry.GridSize);
                copiedBytes += normalRange.AddRange(geometry.Normals);
                copiedBytes += indiceRange.AddRange(geometry.Indices);
                copiedBytes += sizeRange.Add((uint)geometry.Size);
                copiedBytes += baseVertexIndexRange.Add((uint)baseVertexIndex);
            }

            TransferToBuffers.Clear();
            CommandsChangeSinceLastPrepareDraw = true;

            return (geometryTransfered, copiedBytes);
        }

        public void SendCommandsToGPU()
        {
            if (DrawInformations.Count > 0 && CommandsChangeSinceLastPrepareDraw)
            {
                CommandBuffer.Reset();
                CommandBuffer.ReserveSpace(DrawInformations.Count);

                using var commandRange = CommandBuffer.GetReservedRange();
                foreach (var drawCmd in DrawInformations.Values)
                {
                    commandRange.Add(drawCmd.Command);
                }


                CommandsChangeSinceLastPrepareDraw = false;
            }
        }

        public bool HasSpaceFor(int normalCount, int indiceCount, int cmdCount)
        {
            return NormalBuffer.SpaceAvailable >= normalCount &&
                   IndiceBuffer.SpaceAvailable >= indiceCount &&
                   CommandBuffer.SpaceAvailable >= cmdCount;
        }

        public int GetVertexCount()
        {
            int vertexCount = 0;
            foreach (var drawInfo in DrawInformations.Values)
            {
                vertexCount += drawInfo.Information.VertexCount;
            }

            return vertexCount;
        }

        public int GetIndiceCount()
        {
            int indiceCount = 0;
            foreach (var drawInfo in DrawInformations.Values)
            {
                indiceCount += drawInfo.Information.IndiceCount;
            }

            return indiceCount;
        }

        public int GetCommandCount()
        {
            return DrawInformations.Count;
        }

        public IEnumerable<VoxelGridHierarchy> GetGridsDrawing()
        {
            return DrawInformations.Keys;
        }

        public int TransferDrawCommands(IndirectDraw dstDrawer)
        {
            dstDrawer.BaseVertexIndexBuffer.ReserveSpace(DrawInformations.Count);
            using var baseVertexIndexRange = dstDrawer.BaseVertexIndexBuffer.GetReservedRange();

            int copyCommands = 0;
            foreach (var gridDrawInfo in DrawInformations)
            {
                VoxelGridHierarchy grid = gridDrawInfo.Key;
                DrawCommandInfo drawInfo = gridDrawInfo.Value.Information;

                int dstGridPositionOffset = dstDrawer.GridPositionBuffer.FirstAvailableIndex;
                int dstNormalOffset = dstDrawer.NormalBuffer.FirstAvailableIndex;
                int dstIndiceOffset = dstDrawer.IndiceBuffer.FirstAvailableIndex;

                GridPositionBuffer.CopyTo(dstDrawer.GridPositionBuffer, drawInfo.GridPositionOffset, dstGridPositionOffset, 1);
                GridSizeBuffer.CopyTo(dstDrawer.GridSizeBuffer, drawInfo.GridPositionOffset, dstGridPositionOffset, 1);
                NormalBuffer.CopyTo(dstDrawer.NormalBuffer, drawInfo.VertexOffset, dstNormalOffset, drawInfo.VertexCount);
                IndiceBuffer.CopyTo(dstDrawer.IndiceBuffer, drawInfo.IndiceOffset, dstIndiceOffset, drawInfo.IndiceCount);
                SizeBuffer.CopyTo(dstDrawer.SizeBuffer, drawInfo.GridPositionOffset, dstGridPositionOffset, 1);
                baseVertexIndexRange.Add((uint)dstNormalOffset);
                dstDrawer.CommandBuffer.ReserveSpace(1);
                dstDrawer.Add(grid, dstGridPositionOffset, dstIndiceOffset, drawInfo.IndiceCount, dstNormalOffset, drawInfo.VertexCount);

                copyCommands++;
            }

            DrawInformations.Clear();
            dstDrawer.CommandsChangeSinceLastPrepareDraw = true;

            return copyCommands;
        }

        public void Draw()
        {
            if (DrawInformations.Count > 0)
            {
                Vao.MultiDrawElementsIndirect(CommandBuffer.Buffer, DrawInformations.Count);

                if (_syncFlag.HasValue)
                {
                    _openGl.DeleteSync(_syncFlag.Value);
                }
                _syncFlag = _openGl.FenceSync(SyncCondition.SyncGpuCommandsComplete, SyncBehaviorFlags.None);
            }
        }

        public int NormalBufferSize()
        {
            return NormalBuffer.Buffer.Count;
        }

        public int IndiceBufferSize()
        {
            return IndiceBuffer.Buffer.Count;
        }

        public bool IsEmpty()
        {
            return TransferToBuffers.Count == 0 && DrawInformations.Count == 0;
        }

        public void Reset()
        {
            TransferToBuffers.Clear();
            DrawInformations.Clear();

            GridPositionBuffer.Reset();
            GridSizeBuffer.Reset();
            NormalBuffer.Reset();
            NormalBuffer.Reset();
            IndiceBuffer.Reset();
            SizeBuffer.Reset();
            BaseVertexIndexBuffer.Reset();
        }

        public long GpuMemSize()
        {
            return GridPositionBuffer.GpuMemSize() +
                GridSizeBuffer.GpuMemSize() +
                NormalBuffer.GpuMemSize() +
                IndiceBuffer.GpuMemSize() +
                SizeBuffer.GpuMemSize() +
                BaseVertexIndexBuffer.GpuMemSize() +
                CommandBuffer.GpuMemSize();
        }

        public void Dispose()
        {
            Vao.Dispose();
            GridPositionBuffer.Dispose();
            GridSizeBuffer.Dispose();
            NormalBuffer.Dispose();
            IndiceBuffer.Dispose();
            SizeBuffer.Dispose();
            BaseVertexIndexBuffer.Dispose();
            CommandBuffer.Dispose();
        }

        private sealed record DrawInformation(DrawElementsIndirectCommand Command, DrawCommandInfo Information);
    }
}
