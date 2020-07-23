using System;
using System.Buffers;

namespace VoxelWorld
{
    internal readonly ref struct RentedArray<T>
    {
        public readonly T[] Arr;
        public readonly int Length;

        public RentedArray(int length)
        {
            this.Arr = ArrayPool<T>.Shared.Rent(length);
            this.Length = length;
        }

        public Span<T> AsSpan()
        {
            return Arr.AsSpan(0, Length);
        }

        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(Arr);
        }
    }
}
