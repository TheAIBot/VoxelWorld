using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private readonly SlidingVBO<Vector3> VertexBuffer;
        private readonly SlidingVBO<byte> NormalBuffer;
        private readonly SlidingVBO<uint> IndiceBuffer;
        private readonly SlidingVBO<DrawElementsIndirectCommand> CommandBuffer;
        private readonly VAO Vao;
        private GpuCommandSync _gpuSync;

        public IndirectDraw(GL openGl, int vertexBufferSize, int indiceBufferSize, int commandBufferSize)
        {
            _gpuSync = new GpuCommandSync(openGl);
            VertexBuffer = new SlidingVBO<Vector3>(openGl, new VBO<Vector3>(openGl, vertexBufferSize, BufferStorageTarget.ArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit));
            NormalBuffer = new SlidingVBO<byte>(openGl, new VBO<byte>(openGl, vertexBufferSize, BufferStorageTarget.ArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit)
            {
                CastToFloat = false
            });
            IndiceBuffer = new SlidingVBO<uint>(openGl, new VBO<uint>(openGl, indiceBufferSize, BufferStorageTarget.ElementArrayBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit));
            CommandBuffer = new SlidingVBO<DrawElementsIndirectCommand>(openGl, new VBO<DrawElementsIndirectCommand>(openGl, commandBufferSize, BufferStorageTarget.DrawIndirectBuffer, BufferStorageMask.MapPersistentBit | BufferStorageMask.MapWriteBit));
            IGenericVBO[] vbos = new IGenericVBO[]
            {
                new GenericVBO<Vector3>(VertexBuffer.Buffer, "vertex_pos"),
                new GenericVBO<byte>(NormalBuffer.Buffer, "vertex_normal"),
                new GenericVBO<uint>(IndiceBuffer.Buffer),
                new GenericVBO<DrawElementsIndirectCommand>(CommandBuffer.Buffer),
            };
            Vao = new VAO(openGl, SimpleShader.GetShader(openGl), vbos);
            Vao.DisposeChildren = false;
            Vao.DisposeElementArray = false;
        }

        private void Add(VoxelGridHierarchy grid, GeometryData geometry, int firstIndice, int firstVertex)
        {
            Add(grid, firstIndice, geometry.Indices.Length, firstVertex, geometry.Vertices.Length);
        }

        private void Add(VoxelGridHierarchy grid, int firstIndice, int indiceCount, int firstVertex, int vertexCount)
        {
            DrawInformations.Add(grid, new DrawInformation(new DrawElementsIndirectCommand((uint)indiceCount, 1u, (uint)firstIndice, (uint)firstVertex, 0u),
                                                           new DrawCommandInfo(firstIndice, indiceCount, firstVertex, vertexCount)));
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

        public bool TryRemoveGeometry(VoxelGridHierarchy grid, out GeometryData geometryData)
        {
            if (!DrawInformations.Remove(grid))
            {
                int gridIndex = TransferToBuffers.FindIndex(x => x.Grid == grid);
                if (gridIndex == -1)
                {
                    geometryData = null;
                    return false;
                }
                geometryData = TransferToBuffers[gridIndex].Geometry;
                TransferToBuffers.RemoveAt(gridIndex);
                return true;
            }

            CommandsChangeSinceLastPrepareDraw = true;
            geometryData = null;
            return true;
        }

        public bool IsTransferringToBuffers() => TransferToBuffers.Count > 0;

        public (GeometryData[], long) CopyToGPU()
        {
            if (TransferToBuffers.Count == 0)
            {
                return (Array.Empty<GeometryData>(), 0);
            }

            _gpuSync.Wait();

            GeometryData[] geometryTransfered = new GeometryData[TransferToBuffers.Count];
            long copiedBytes = 0;

            using (var vertexRange = VertexBuffer.GetReservedRange())
            using (var normalRange = NormalBuffer.GetReservedRange())
            using (var indiceRange = IndiceBuffer.GetReservedRange())
            {

                for (int i = 0; i < TransferToBuffers.Count; i++)
                {
                    GeometryData geometry = TransferToBuffers[i].Geometry;
                    geometryTransfered[i] = geometry;

                    Add(TransferToBuffers[i].Grid, geometry, IndiceBuffer.FirstAvailableIndex, VertexBuffer.FirstAvailableIndex);

                    copiedBytes += vertexRange.AddRange(geometry.Vertices);
                    copiedBytes += normalRange.AddRange(geometry.Normals);
                    copiedBytes += indiceRange.AddRange(geometry.Indices);
                }
            }

            TransferToBuffers.Clear();
            CommandsChangeSinceLastPrepareDraw = true;

            _gpuSync.CreateFence();
            return (geometryTransfered, copiedBytes);
        }

        public void SendCommandsToGPU()
        {
            if (DrawInformations.Count > 0 && CommandsChangeSinceLastPrepareDraw)
            {
                CommandBuffer.Reset();
                CommandBuffer.ReserveSpace(DrawInformations.Count);

                _gpuSync.Wait(); // Should be different sync from others

                using (var commandRange = CommandBuffer.GetReservedRange())
                {
                    foreach (var drawCmd in DrawInformations.Values)
                    {
                        commandRange.Add(drawCmd.Command);
                    }
                }

                _gpuSync.CreateFence();


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
            Debug.Assert(TransferToBuffers.Count == 0);

            _gpuSync.Wait();
            dstDrawer._gpuSync.Wait();

            foreach (var gridDrawInfo in DrawInformations)
            {
                VoxelGridHierarchy grid = gridDrawInfo.Key;
                DrawCommandInfo drawInfo = gridDrawInfo.Value.Information;

                int dstVertexOffset = dstDrawer.VertexBuffer.FirstAvailableIndex;
                int dstIndiceOffset = dstDrawer.IndiceBuffer.FirstAvailableIndex;

                VertexBuffer.CopyTo(dstDrawer.VertexBuffer, drawInfo.VertexOffset, dstVertexOffset, drawInfo.VertexCount);
                NormalBuffer.CopyTo(dstDrawer.NormalBuffer, drawInfo.VertexOffset, dstVertexOffset, drawInfo.VertexCount);
                IndiceBuffer.CopyTo(dstDrawer.IndiceBuffer, drawInfo.IndiceOffset, dstIndiceOffset, drawInfo.IndiceCount);
                dstDrawer.CommandBuffer.ReserveSpace(1);
                dstDrawer.Add(grid, dstIndiceOffset, drawInfo.IndiceCount, dstVertexOffset, drawInfo.VertexCount);

            }

            _gpuSync.CreateFence();
            dstDrawer._gpuSync.CreateFence();

            DrawInformations.Clear();
            dstDrawer.CommandsChangeSinceLastPrepareDraw = true;

            return 0;
        }

        public void Draw()
        {
            if (DrawInformations.Count > 0)
            {
                _gpuSync.Wait();

                Vao.MultiDrawElementsIndirect(CommandBuffer.Buffer, DrawInformations.Count);

                _gpuSync.CreateFence();
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
            return TransferToBuffers.Count == 0 && DrawInformations.Count == 0;
        }

        public void Reset()
        {
            TransferToBuffers.Clear();
            DrawInformations.Clear();

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
            _gpuSync.Wait();

            Vao.Dispose();
            VertexBuffer.Dispose();
            NormalBuffer.Dispose();
            IndiceBuffer.Dispose();
            CommandBuffer.Dispose();
        }

        private sealed record DrawInformation(DrawElementsIndirectCommand Command, DrawCommandInfo Information);
    }
}
