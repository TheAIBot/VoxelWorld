using Silk.NET.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace VoxelWorld.Render.VoxelGrid
{
    internal sealed unsafe class SlidingVBO<T> : IDisposable where T : unmanaged
    {
        private readonly GL _openGl;
        public int SpaceAvailable { get; private set; }
        public int FirstAvailableIndex { get; private set; }
        public readonly VBO<T> Buffer;
        private readonly void* BufferPointer;


        public SlidingVBO(GL openGl, VBO<T> buffer)
        {
            _openGl = openGl;
            Buffer = buffer;
            SpaceAvailable = Buffer.Count;
            FirstAvailableIndex = 0;

            int byteLength = SpaceAvailable * Marshal.SizeOf<T>();
            BufferPointer = _openGl.MapBufferRange(Buffer, 0, byteLength, MapBufferAccessMask.WriteBit |
                                                                          MapBufferAccessMask.PersistentBit |
                                                                          MapBufferAccessMask.CoherentBit |
                                                                          MapBufferAccessMask.FlushExplicitBit);
        }

        public void ReserveSpace(int sizeToReserve)
        {
            SpaceAvailable -= sizeToReserve;
        }

        public void UseSpace(int sizeToUse)
        {
            FirstAvailableIndex += sizeToUse;
            SpaceAvailable -= sizeToUse;
        }

        public SlidingRange MapReservedRange(MapBufferAccessMask mappingMask = MapBufferAccessMask.WriteBit)
        {
            int reserved = Buffer.Count - FirstAvailableIndex - SpaceAvailable;
            return new SlidingRange(this, Buffer.MapBufferRange(_openGl, FirstAvailableIndex, reserved, mappingMask));
        }

        public PermanentFlushSlidingRange GetReservedRange()
        {
            int reserved = Buffer.Count - FirstAvailableIndex - SpaceAvailable;
            return new PermanentFlushSlidingRange(this, new FlushPermanentMappedRange<T>(_openGl, Buffer, FirstAvailableIndex, reserved, BufferPointer));
        }

        public void CopyTo(SlidingVBO<T> dstBuffer, int srcOffset, int dstOffset, int length)
        {
            int srcOffsetInBytes = srcOffset * Marshal.SizeOf<T>();
            int dstOffsetInbytes = dstOffset * Marshal.SizeOf<T>();
            int lengthInBytes = length * Marshal.SizeOf<T>();

            _openGl.CopyNamedBufferSubData(Buffer.ID, dstBuffer.Buffer.ID, (IntPtr)srcOffsetInBytes, (IntPtr)dstOffsetInbytes, (nuint)lengthInBytes);
            dstBuffer.UseSpace(length);
        }

        public void Reset()
        {
            SpaceAvailable = Buffer.Count;
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

        internal readonly ref struct PermanentFlushSlidingRange
        {
            private readonly SlidingVBO<T> Sliding;
            private readonly FlushPermanentMappedRange<T> MappedRange;
            private readonly int StartIndex;

            internal PermanentFlushSlidingRange(SlidingVBO<T> sliding, FlushPermanentMappedRange<T> mapped)
            {
                Sliding = sliding;
                MappedRange = mapped;
                StartIndex = Sliding.FirstAvailableIndex;
            }

            public int Add(T value)
            {
                int offset = Sliding.FirstAvailableIndex - StartIndex;
                MappedRange.Range[offset] = value;

                Sliding.UseSpace(1);
                return sizeof(T);
            }

            public int AddRange(Span<T> values)
            {
                int offset = Sliding.FirstAvailableIndex - StartIndex;
                Span<T> offsetRange = MappedRange.Range.Slice(offset);
                values.CopyTo(offsetRange);

                Sliding.UseSpace(values.Length);
                return values.Length * sizeof(T);
            }

            public void Dispose()
            {
                MappedRange.Dispose();
            }
        }
    }
}
