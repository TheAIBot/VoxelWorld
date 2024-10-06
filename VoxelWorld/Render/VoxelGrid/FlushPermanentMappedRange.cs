using Silk.NET.OpenGL;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelWorld.Render.VoxelGrid
{
    public readonly ref struct FlushPermanentMappedRange<T> where T : unmanaged
    {
        private readonly GL _openGl;
        private readonly VBO<T> _buffer;
        private readonly int _offset;
        public Span<T> Range { get; }

        public unsafe FlushPermanentMappedRange(GL openGl, VBO<T> buffer, int offset, int length, void* bufferRange)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((long)offset * Marshal.SizeOf<T>(), int.MaxValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((long)length * Marshal.SizeOf<T>(), int.MaxValue);

            _openGl = openGl;
            _buffer = buffer;
            _offset = offset;
            Range = new Span<T>(Unsafe.Add<T>(bufferRange, offset), length);
        }

        public void Dispose()
        {
            if (_openGl.IsExtensionDirectStateAccessEnabled())
            {
                _openGl.FlushMappedNamedBufferRange(_buffer.ID, _offset * Marshal.SizeOf<T>(), (nuint)(Range.Length * Marshal.SizeOf<T>()));
            }
            else
            {
                _openGl.BindBuffer(_buffer.BufferTarget, _buffer.ID);
                _openGl.FlushMappedBufferRange(_buffer.BufferTarget, _offset * Marshal.SizeOf<T>(), (nuint)(Range.Length * Marshal.SizeOf<T>()));
            }
        }
    }
}
