using Silk.NET.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace VoxelWorld.Render.VoxelGrid
{
    internal sealed unsafe class SlidingVBO<T> : IDisposable where T : unmanaged
    {
        private readonly GL _openGl;
        private int _firstAvailableIndex;
        private int _spaceAvailable;

        public int SpaceAvailable
        {
            get
            {
                return _spaceAvailable;
            }
            private set
            {
                if (value < 0)
                {
                    throw new InvalidOperationException($"Attempted to set {nameof(SpaceAvailable)} to {value:N0} which is less than 0.");
                }

                _spaceAvailable = value;
            }
        }
        public int FirstAvailableIndex
        {
            get
            {
                return _firstAvailableIndex;
            }
            private set
            {
                if (value > Buffer.Count)
                {
                    throw new InvalidOperationException($"Attempted to set {nameof(FirstAvailableIndex)} to {value:N0} which is more than buffer size: {Buffer.Count:N0}");
                }

                _firstAvailableIndex = value;
            }
        }
        public readonly VBO<T> Buffer;
        private readonly void* BufferPointer;


        public SlidingVBO(GL openGl, VBO<T> buffer)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((long)buffer.Count * Marshal.SizeOf<T>(), int.MaxValue);

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
            if (FirstAvailableIndex + reserved > Buffer.Count)
            {
                throw new InvalidOperationException("Attempted to reserve range extended beyond the allocated range.");
            }

            return new PermanentFlushSlidingRange(this, new FlushPermanentMappedRange<T>(_openGl, Buffer, FirstAvailableIndex, reserved, BufferPointer));
        }

        public void CopyTo(SlidingVBO<T> dstBuffer, int srcOffset, int dstOffset, int length)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((long)srcOffset * Marshal.SizeOf<T>(), int.MaxValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((long)dstOffset * Marshal.SizeOf<T>(), int.MaxValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((long)length * Marshal.SizeOf<T>(), int.MaxValue);
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

                Sliding.FirstAvailableIndex++;
            }

            public void AddRange(Span<T> values)
            {
                int offset = Sliding.FirstAvailableIndex - StartIndex;
                Span<T> offsetRange = MappedRange.Range.Slice(offset);
                values.CopyTo(offsetRange);

                Sliding.FirstAvailableIndex += values.Length;
            }

            public void Dispose()
            {
                Sliding.SpaceAvailable = Sliding.Buffer.Count - Sliding.FirstAvailableIndex;
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

                Sliding.FirstAvailableIndex++;
                return sizeof(T);
            }

            public int AddRange(Span<T> values)
            {
                int offset = Sliding.FirstAvailableIndex - StartIndex;
                Span<T> offsetRange = MappedRange.Range.Slice(offset);
                values.CopyTo(offsetRange);

                Sliding.FirstAvailableIndex += values.Length;
                return values.Length * sizeof(T);
            }

            public void Dispose()
            {
                Sliding.SpaceAvailable = Sliding.Buffer.Count - Sliding.FirstAvailableIndex;
                MappedRange.Dispose();
            }
        }
    }
}
