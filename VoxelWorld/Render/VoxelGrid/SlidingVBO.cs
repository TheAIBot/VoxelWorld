using Silk.NET.OpenGL;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VoxelWorld.Render.VoxelGrid
{
    internal sealed unsafe class SlidingVBO<T> : IDisposable where T : unmanaged
    {
        private readonly GL _openGl;
        public int SpaceAvailable => Buffer.Count - FirstAvailableIndex - _reservedSpace;
        public int FirstAvailableIndex { get; private set; } = 0;
        private int _reservedSpace = 0;
        public readonly VBO<T> Buffer;
        private readonly void* BufferPointer;


        public SlidingVBO(GL openGl, VBO<T> buffer)
        {
            _openGl = openGl;
            Buffer = buffer;

            int byteLength = SpaceAvailable * Marshal.SizeOf<T>();
            BufferPointer = _openGl.MapBufferRange(Buffer, 0, byteLength, MapBufferAccessMask.WriteBit |
                                                                          MapBufferAccessMask.PersistentBit |
                                                                          MapBufferAccessMask.CoherentBit |
                                                                          MapBufferAccessMask.FlushExplicitBit);
        }

        public void ReserveSpace(int sizeToReserve)
        {
            _reservedSpace += sizeToReserve;
            if (FirstAvailableIndex + _reservedSpace > Buffer.Count)
            {
                throw new Exception();
            }
        }

        public void UseSpace(int sizeToUse)
        {
            FirstAvailableIndex += sizeToUse;
            if (FirstAvailableIndex + _reservedSpace > Buffer.Count)
            {
                throw new Exception();
            }
        }

        public SlidingRange MapReservedRange(MapBufferAccessMask mappingMask = MapBufferAccessMask.WriteBit)
        {
            return new SlidingRange(this, Buffer.MapBufferRange(_openGl, FirstAvailableIndex, _reservedSpace, mappingMask));
        }

        public PermanentFlushSlidingRange GetReservedRange()
        {
            int reserved = _reservedSpace;
            _reservedSpace = 0;
            return GetReservedRange(reserved);
        }

        public PermanentFlushSlidingRange GetReservedRange(int length)
        {
            int firstAvailableIndex = FirstAvailableIndex;
            UseSpace(length);
            return new PermanentFlushSlidingRange(_openGl, Buffer, firstAvailableIndex, length, BufferPointer);
        }

        public void CopyTo(SlidingVBO<T> dstBuffer, int srcOffset, int dstOffset, int length)
        {
            int srcOffsetInBytes = srcOffset * Marshal.SizeOf<T>();
            int dstOffsetInbytes = dstOffset * Marshal.SizeOf<T>();
            int lengthInBytes = length * Marshal.SizeOf<T>();

            _openGl.CopyNamedBufferSubData(Buffer.ID, dstBuffer.Buffer.ID, srcOffsetInBytes, dstOffsetInbytes, (nuint)lengthInBytes);
            dstBuffer.UseSpace(length);
        }

        public void Reset()
        {
            _reservedSpace = 0;
            FirstAvailableIndex = 0;
        }

        public long GpuMemSize()
        {
            return Marshal.SizeOf<T>() * Buffer.Count;
        }

        public void Dispose()
        {
            if (_openGl.IsExtensionDirectStateAccessEnabled())
            {
                _openGl.UnmapNamedBuffer(Buffer.ID);
            }
            else
            {
                _openGl.BindBuffer(Buffer.BufferTarget, Buffer.ID);
                _openGl.UnmapBuffer(Buffer.BufferTarget);
            }

            Buffer.Dispose();
        }

        internal readonly ref struct SlidingRange
        {
            private readonly SlidingVBO<T> Sliding;
            private readonly MappedRange<T> MappedRange;
            private readonly int StartIndex;

            internal SlidingRange(SlidingVBO<T> sliding, MappedRange<T> mapped)
            {
                Sliding = sliding;
                MappedRange = mapped;
                StartIndex = Sliding.FirstAvailableIndex;
            }

            public void Add(T value)
            {
                int offset = Sliding.FirstAvailableIndex - StartIndex;
                MappedRange.Range[offset] = value;

                Sliding.UseSpace(1);
            }

            public void AddRange(Span<T> values)
            {
                int offset = Sliding.FirstAvailableIndex - StartIndex;
                Span<T> offsetRange = MappedRange.Range.Slice(offset);
                values.CopyTo(offsetRange);

                Sliding.UseSpace(values.Length);
            }

            public void Dispose()
            {
                MappedRange.Dispose();
            }
        }

        internal ref struct PermanentFlushSlidingRange
        {
            private readonly GL _openGl;
            private readonly VBO<T> _buffer;
            private readonly int _offset;
            private readonly Span<T> _range;
            private int _firstAvailableIndex;

            public int BufferFirstAvailableIndex => _offset + _firstAvailableIndex;

            internal PermanentFlushSlidingRange(GL openGl, VBO<T> buffer, int offset, int length, void* bufferRange)
            {
                _openGl = openGl;
                _buffer = buffer;
                _offset = offset;
                _range = new Span<T>(Unsafe.Add<T>(bufferRange, offset), length);
            }

            public int Add(T value)
            {
                _range[_firstAvailableIndex] = value;
                _firstAvailableIndex++;

                return sizeof(T);
            }

            public int AddRange(Span<T> values)
            {
                if (_range.Length < values.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(values));
                }
                values.CopyTo(_range.Slice(_firstAvailableIndex));
                _firstAvailableIndex += values.Length;

                return values.Length * sizeof(T);
            }

            public void Dispose()
            {
                if (_openGl.IsExtensionDirectStateAccessEnabled())
                {
                    _openGl.FlushMappedNamedBufferRange(_buffer.ID, _offset * Marshal.SizeOf<T>(), (nuint)(_range.Length * Marshal.SizeOf<T>()));
                }
                else
                {
                    _openGl.BindBuffer(_buffer.BufferTarget, _buffer.ID);
                    _openGl.FlushMappedBufferRange(_buffer.BufferTarget, _offset * Marshal.SizeOf<T>(), (nuint)(_range.Length * Marshal.SizeOf<T>()));
                }
            }
        }
    }
}
