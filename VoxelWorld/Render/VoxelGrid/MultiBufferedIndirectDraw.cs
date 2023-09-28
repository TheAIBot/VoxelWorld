using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using VoxelWorld.Voxel;
using VoxelWorld.Voxel.Hierarchy;

namespace VoxelWorld.Render.VoxelGrid
{
    internal sealed class MultiBufferedIndirectDraw : IDisposable
    {
        private readonly Dictionary<GeometryData, int> _bufferCountGeometryResidesIn = new();
        private readonly IndirectDraw[] _bufferedDrawers;
        private int _updateBufferIndex;
        private int _drawBufferIndex;

        public MultiBufferedIndirectDraw(GL openGl, int bufferCount, int vertexBufferSize, int indiceBufferSize, int commandBufferSize)
        {
            _bufferedDrawers = new IndirectDraw[bufferCount];
            for (int i = 0; i < bufferCount; i++)
            {
                _bufferedDrawers[i] = new IndirectDraw(openGl, vertexBufferSize, indiceBufferSize, commandBufferSize);
            }

            _updateBufferIndex = 0;
            _drawBufferIndex = 1;
        }

        public bool TryAddGeometry(VoxelGridHierarchy grid, GeometryData geometry)
        {
            if (HasSpaceFor(geometry.Vertices.Length, geometry.Indices.Length, 1))
            {
                foreach (var indirectDrawer in _bufferedDrawers)
                {
                    indirectDrawer.TryAddGeometry(grid, geometry);
                }

                ref int buffersResideInCount = ref CollectionsMarshal.GetValueRefOrAddDefault(_bufferCountGeometryResidesIn, geometry, out var _);
                buffersResideInCount = _bufferedDrawers.Length;

                return true;
            }

            return false;
        }

        public void RemoveGeometry(VoxelGridHierarchy grid)
        {
            GeometryData geometryData = null;
            foreach (var indirectDrawer in _bufferedDrawers)
            {
                indirectDrawer.TryRemoveGeometry(grid, out geometryData);
            }

            geometryData?.Reuse();
        }

        public void CopyToGPU()
        {
            foreach (var geometry in _bufferedDrawers[_updateBufferIndex].CopyToGPU())
            {
                ref int buffersResideInCount = ref CollectionsMarshal.GetValueRefOrNullRef(_bufferCountGeometryResidesIn, geometry);
                buffersResideInCount--;
                if (buffersResideInCount == 0)
                {
                    geometry.Reuse();
                    _bufferCountGeometryResidesIn.Remove(geometry);
                }
            }
        }

        public void SendCommandsToGPU()
        {
            _bufferedDrawers[_updateBufferIndex].SendCommandsToGPU();
        }

        public bool HasSpaceFor(int vertexCount, int indiceCount, int cmdCount)
        {
            bool canAdd = true;
            foreach (var indirectDrawer in _bufferedDrawers)
            {
                canAdd &= indirectDrawer.HasSpaceFor(vertexCount, indiceCount, cmdCount);
            }

            return canAdd;
        }

        public bool IsTransferringToBuffers() => _bufferedDrawers.Any(x => x.IsTransferringToBuffers());

        public int GetVertexCount()
        {
            return _bufferedDrawers[0].GetVertexCount();
        }

        public int GetIndiceCount()
        {
            return _bufferedDrawers[0].GetIndiceCount();
        }

        public int GetCommandCount()
        {
            return _bufferedDrawers[0].GetCommandCount();
        }

        public IEnumerable<VoxelGridHierarchy> GetGridsDrawing()
        {
            return _bufferedDrawers[0].GetGridsDrawing();
        }

        public int TransferDrawCommands(MultiBufferedIndirectDraw multiBufferedDrawer)
        {
            int copyCommands = 0;
            for (int i = 0; i < _bufferedDrawers.Length; i++)
            {
                IndirectDraw copyFrom = _bufferedDrawers[i];
                IndirectDraw copyTo = multiBufferedDrawer._bufferedDrawers[i];

                copyCommands += copyFrom.TransferDrawCommands(copyTo);

            }

            return copyCommands;
        }

        public void Draw()
        {
            RotateBuffers();

            _bufferedDrawers[_drawBufferIndex].Draw();
        }

        public int VertexBufferSize()
        {
            return _bufferedDrawers[0].VertexBufferSize();
        }

        public int IndiceBufferSize()
        {
            return _bufferedDrawers[0].IndiceBufferSize();
        }

        public bool IsEmpty()
        {
            bool isEmpty = true;
            foreach (var indirectDrawer in _bufferedDrawers)
            {
                isEmpty &= indirectDrawer.IsEmpty();
            }

            return isEmpty;
        }

        public void Reset()
        {
            foreach (var indirectDrawer in _bufferedDrawers)
            {
                indirectDrawer.Reset();
            }
        }

        public long GpuMemSize()
        {
            return _bufferedDrawers.Sum(x => x.GpuMemSize());
        }

        public void Dispose()
        {
            foreach (var indirectDrawer in _bufferedDrawers)
            {
                indirectDrawer.Dispose();
            }
        }

        private void RotateBuffers()
        {
            _updateBufferIndex = (_updateBufferIndex + 1) % _bufferedDrawers.Length;
            _drawBufferIndex = (_drawBufferIndex + 1) % _bufferedDrawers.Length;
        }
    }
}
