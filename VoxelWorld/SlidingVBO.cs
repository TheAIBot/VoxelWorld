﻿using OpenGL;
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

        public void AddCommandsGeom(List<CommandPair> commands, Func<CommandPair, Memory<T>> GeomSelector)
        {
            int bufferSize = 0;
            foreach (var cmd in commands)
            {
                bufferSize += GeomSelector(cmd).Length;
            }

            using var mappedRange = Buffer.MapBufferRange(FirstAvailableIndex, bufferSize, BufferAccessMask.MapWriteBit | BufferAccessMask.MapUnsynchronizedBit);
            Span<T> range = mappedRange.Range;

            for (int i = 0; i < commands.Count; i++)
            {
                Span<T> geomData = GeomSelector(commands[i]).Span;
                geomData.CopyTo(range);
                range = range.Slice(geomData.Length);
            }
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
    }
}
