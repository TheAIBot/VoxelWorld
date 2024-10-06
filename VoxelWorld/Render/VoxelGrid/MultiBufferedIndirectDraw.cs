using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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
            for (int i = 0; i < _bufferedDrawers.Length; i++)
            {
                _bufferedDrawers[i] = new IndirectDraw(openGl, vertexBufferSize, indiceBufferSize, commandBufferSize);
            }

            _updateBufferIndex = 0;
            _drawBufferIndex = (_updateBufferIndex + 1) % _bufferedDrawers.Length;
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

        public bool TryRemoveGeometry(VoxelGridHierarchy grid)
        {
            GeometryData geometryData = null;
            int removedGeometryCount = 0;
            foreach (var indirectDrawer in _bufferedDrawers)
            {
                if (indirectDrawer.TryRemoveGeometry(grid, out GeometryData drawerGeometryData))
                {
                    removedGeometryCount++;
                }

                if (drawerGeometryData != null)
                {
                    geometryData = drawerGeometryData;
                }
            }

            if (geometryData != null)
            {
                DecrementBufferCounter(geometryData, removedGeometryCount);
            }

            return removedGeometryCount != 0;
        }

        public long CopyToGPU()
        {
            (GeometryData[] geometriesCopied, long copiedBytes) = _bufferedDrawers[_updateBufferIndex].CopyToGPU();
            foreach (var geometry in geometriesCopied)
            {
                DecrementBufferCounter(geometry, 1);
            }

            return copiedBytes;
        }

        public void SendCommandsToGPU()
        {
            _bufferedDrawers[_updateBufferIndex].SendCommandsToGPU();
        }

        public bool HasSpaceFor(int vertexCount, int indiceCount, int cmdCount)
        {
            // Copying is done over multiple steps to ensure copying
            // is not done to a buffer that is drawing. Removing grids
            // is still done between steps which means the same number 
            // of grids are not necessarily being copied for each buffer.
            // Each buffer will therefore not contain the same grids
            // which is why all of them must be queried.
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
            // Copying is done over multiple steps to ensure copying
            // is not done to a buffer that is drawing. Removing grids
            // is still done between steps which means the same number 
            // of grids are not necessarily being copied for each buffer.
            // Meaning the buffers size will no longer be in sync which
            // is why the max needs to be taken.
            return _bufferedDrawers.Max(x => x.GetVertexCount());
        }

        public int GetIndiceCount()
        {
            // Copying is done over multiple steps to ensure copying
            // is not done to a buffer that is drawing. Removing grids
            // is still done between steps which means the same number 
            // of grids are not necessarily being copied for each buffer.
            // Meaning the buffers size will no longer be in sync which
            // is why the max needs to be taken.
            return _bufferedDrawers.Max(x => x.GetIndiceCount());
        }

        public int GetCommandCount()
        {
            // Copying is done over multiple steps to ensure copying
            // is not done to a buffer that is drawing. Removing grids
            // is still done between steps which means the same number 
            // of grids are not necessarily being copied for each buffer.
            // Meaning the buffers size will no longer be in sync which
            // is why the max needs to be taken.
            return _bufferedDrawers.Max(x => x.GetCommandCount());
        }

        public IEnumerable<VoxelGridHierarchy> GetGridsDrawing()
        {
            // Copying is done over multiple steps to ensure copying
            // is not done to a buffer that is drawing. Removing grids
            // is still done between steps which means the same number 
            // of grids are not necessarily being copied for each buffer.
            // Each buffer will therefore not contain the same grids
            // which is why all of them must be queried.
            return _bufferedDrawers.SelectMany(x => x.GetGridsDrawing())
                                   .Distinct();
        }

        public int TransferDrawCommandsFromSingleBuffer(MultiBufferedIndirectDraw multiBufferedDrawer)
        {
            int copyCommands = 0;
            IndirectDraw copyFrom = _bufferedDrawers[_updateBufferIndex];
            IndirectDraw copyTo = multiBufferedDrawer._bufferedDrawers[multiBufferedDrawer._updateBufferIndex];

            copyCommands += copyFrom.TransferDrawCommands(copyTo);

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

        private void DecrementBufferCounter(GeometryData geometryData, int decrementCount)
        {
            ref int buffersResideInCount = ref CollectionsMarshal.GetValueRefOrNullRef(_bufferCountGeometryResidesIn, geometryData);
            if (Unsafe.IsNullRef(ref buffersResideInCount))
            {
                throw new InvalidOperationException("Expected geometry to be in buffer counter dictionary but it was not.");
            }

            Debug.Assert(buffersResideInCount > 0);

            buffersResideInCount -= decrementCount;
            if (buffersResideInCount == 0)
            {
                geometryData.Reuse();
                _bufferCountGeometryResidesIn.Remove(geometryData);
            }
        }
    }
}
