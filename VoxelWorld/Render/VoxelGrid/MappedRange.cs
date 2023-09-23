using Silk.NET.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace VoxelWorld.Render.VoxelGrid
{
    public readonly ref struct MappedRange<T> where T : unmanaged
    {
        private readonly GL _openGl;
        private readonly VBO<T> buffer;
        public Span<T> Range { get; }

        internal unsafe MappedRange(GL openGl, VBO<T> vbo, int offset, int length, MapBufferAccessMask mask)
        {
            _openGl = openGl;
            buffer = vbo;

            int byteOffset = offset * Marshal.SizeOf<T>();
            int byteLength = length * Marshal.SizeOf<T>();
            Range = new Span<T>(_openGl.MapBufferRange(vbo, byteOffset, byteLength, mask), length);
        }

        public void Dispose()
        {
            if (_openGl.IsExtensionDirectStateAccessEnabled())
            {
                _openGl.UnmapNamedBuffer(buffer.ID);
            }
            else
            {
                _openGl.BindBuffer(buffer.BufferTarget, buffer.ID);
                _openGl.UnmapBuffer(buffer.BufferTarget);
            }
        }
    }
}
