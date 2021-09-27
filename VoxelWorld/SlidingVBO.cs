using OpenGL;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VoxelWorld
{
    internal class SlidingVBO<T> : IDisposable where T : struct
    {
        public int SpaceAvailable { get; private set; }
        public int FirstAvailableIndex { get; private set; }
        public readonly VBO<T> Buffer;

        public SlidingVBO(VBO<T> buffer)
        {
            Buffer = buffer;
            SpaceAvailable = Buffer.Count;
            FirstAvailableIndex = 0;
        }

        public void ReserveSpace(int sizeToReserve)
        {
            SpaceAvailable -= sizeToReserve;
        }

        public void UseSpace(int sizeToUse)
        {
            FirstAvailableIndex += sizeToUse;
        }

        public SlidingRange MapReservedRange(BufferAccessMask mappingMask = BufferAccessMask.MapWriteBit | BufferAccessMask.MapUnsynchronizedBit)
        {
            int reserved = Buffer.Count - FirstAvailableIndex - SpaceAvailable;
            return new SlidingRange(this, Buffer.MapBufferRange(FirstAvailableIndex, reserved, mappingMask));
        }

        public void Reset()
        {
            SpaceAvailable = Buffer.Count;
            FirstAvailableIndex = 0;
        }

        public void Dispose()
        {
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
    }
}
