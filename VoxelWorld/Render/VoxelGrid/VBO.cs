using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VoxelWorld.Render.VoxelGrid
{
    public sealed class VBO<T> : IDisposable
        where T : unmanaged
    {
        private readonly GL _openGl;
        /// <summary>
        /// A collection of types and their respective number of components per generic vertex attribute.
        /// </summary>
        private static readonly Dictionary<Type, int> TypeComponentSize = new Dictionary<Type, int>()
        {
            [typeof(sbyte)] = 1,
            [typeof(byte)] = 1,
            [typeof(short)] = 1,
            [typeof(ushort)] = 1,
            [typeof(int)] = 1,
            [typeof(uint)] = 1,
            [typeof(float)] = 1,
            [typeof(double)] = 1,
            [typeof(Vector2)] = 2,
            [typeof(Vector3)] = 3,
            [typeof(Vector4)] = 4,
            [typeof(DrawElementsIndirectCommand)] = 1,
            //[typeof(int2101010)] = 4,
            //[typeof(uint2101010)] = 4,
            //[typeof(uint10f11f11f)] = 3,
        };

        /// <summary>
        /// A collection of conversions from numerical types to vertex attribute pointer types.
        /// </summary>
        private static readonly Dictionary<Type, VertexAttribPointerType> TypeAttribPointerType = new Dictionary<Type, VertexAttribPointerType>()
        {
            [typeof(sbyte)] = VertexAttribPointerType.Byte,
            [typeof(byte)] = VertexAttribPointerType.UnsignedByte,
            [typeof(short)] = VertexAttribPointerType.Short,
            [typeof(ushort)] = VertexAttribPointerType.UnsignedShort,
            [typeof(int)] = VertexAttribPointerType.Int,
            [typeof(uint)] = VertexAttribPointerType.UnsignedInt,
            [typeof(float)] = VertexAttribPointerType.Float,
            [typeof(double)] = VertexAttribPointerType.Double,
            [typeof(Vector2)] = VertexAttribPointerType.Float,
            [typeof(Vector3)] = VertexAttribPointerType.Float,
            [typeof(Vector4)] = VertexAttribPointerType.Float,
            [typeof(DrawElementsIndirectCommand)] = VertexAttribPointerType.Byte
            //[typeof(int2101010)] = VertexAttribPointerType.UnsignedInt2101010Reversed,
            //[typeof(uint2101010)] = VertexAttribPointerType.UnsignedUInt2101010Reversed,
            //[typeof(uint10f11f11f)] = VertexAttribPointerType.UnsignedUInt101111Reversed
        };

        /// <summary>
        /// Contains all known integral types.
        /// </summary>
        private static readonly HashSet<Type> IntegralTypes = new HashSet<Type>()
        {
            typeof(sbyte),
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
        };



        /// <summary>
        /// The ID of the vertex buffer object.
        /// </summary>
        public uint ID { get; private set; }

        /// <summary>
        /// The type of the buffer.
        /// </summary>
        public BufferTargetARB BufferTarget { get; private set; }

        /// <summary>
        /// The size (in floats) of the type of data in the buffer.  Size * 4 to get bytes.
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// The type of data that is stored in the buffer (either int or float).
        /// </summary>
        public VertexAttribPointerType PointerType { get; private set; }

        /// <summary>
        /// The length of data that is stored in the buffer.
        /// </summary>
        public int Count { get; private set; }

        /// <summary>
        /// Specifies the number of instances that will pass between updates of the generic attribute slot.
        /// Only used for instance drawing.
        /// </summary>
        public uint Divisor { get; set; } = 0;

        /// <summary>
        /// Specifies whether fixed-point data values should be normalized (true) or converted directly as fixed-point values (false) when they are accessed.
        /// If normalized is set to true, it indicates that values stored in an integer format are to be mapped to the range [-1,1] (for signed values) 
        /// or [0,1] (for unsigned values) when they are accessed and converted to floating point. 
        /// Otherwise, values will be converted to floats directly without normalization.
        /// </summary>
        public bool Normalize { get; set; } = true;

        /// <summary>
        /// Specifies whether types, other than float, should be cast to float.
        /// </summary>
        public bool CastToFloat { get; set; } = true;

        /// <summary>
        /// Specifies whether the VBO contains an integral type.
        /// </summary>
        public bool IsIntegralType { get; }


        /// <summary>
        /// Creates a buffer object of type T with a specified length.
        /// This allows the array T[] to be larger than the actual size necessary to buffer.
        /// Useful for reusing resources and avoiding unnecessary GC action.
        /// </summary>
        /// <param name="Data">An array of data of type T (which must be a struct) that will be buffered to the GPU.</param>
        /// <param name="Length">The length of the valid data in the data array.</param>
        /// <param name="Target">Specifies the target buffer object.</param>
        /// <param name="Hint">Specifies the expected usage of the data store.</param>
        public VBO(GL openGl, T[] Data, int Length, BufferTargetARB Target = BufferTargetARB.ArrayBuffer, BufferUsageARB Hint = BufferUsageARB.StaticDraw)
        {
            _openGl = openGl;
            Length = Math.Max(0, Math.Min(Length, Data.Length));

            ID = _openGl.CreateVBO<T>(Target, Data, Hint, Length);

            BufferTarget = Target;
            this.Size = GetTypeComponentSize();
            this.PointerType = GetAttribPointerType();
            this.Count = Length;
            this.IsIntegralType = IsTypeIntegral();
        }

        /// <summary>
        /// Creates a buffer object of type T with a specified length.
        /// This allows the array T[] to be larger than the actual size necessary to buffer.
        /// Useful for reusing resources and avoiding unnecessary GC action.
        /// </summary>
        /// <param name="Data">An array of data of type T (which must be a struct) that will be buffered to the GPU.</param>
        /// <param name="Position">An offset into the Data array from which to begin buffering.</param>
        /// <param name="Length">The length of the valid data in the data array.</param>
        /// <param name="Target">Specifies the target buffer object.</param>
        /// <param name="Hint">Specifies the expected usage of the data store.</param>
        public VBO(GL openGl, T[] Data, int Position, int Length, BufferTargetARB Target = BufferTargetARB.ArrayBuffer, BufferUsageARB Hint = BufferUsageARB.StaticDraw)
        {
            _openGl = openGl;
            Length = Math.Max(0, Math.Min(Length, Data.Length));

            ID = _openGl.CreateVBO<T>(Target, Data.AsSpan(Position), Hint, Length);

            BufferTarget = Target;
            this.Size = GetTypeComponentSize();
            this.PointerType = GetAttribPointerType();
            this.Count = Length;
            this.IsIntegralType = IsTypeIntegral();
        }

        /// <summary>
        /// Creates a buffer object of type T.
        /// </summary>
        /// <param name="Data">Specifies a pointer to data that will be copied into the data store for initialization.</param>
        /// <param name="Target">Specifies the target buffer object.</param>
        /// <param name="Hint">Specifies the expected usage of the data store.</param>
        public VBO(GL openGl, T[] Data, BufferTargetARB Target = BufferTargetARB.ArrayBuffer, BufferUsageARB Hint = BufferUsageARB.StaticDraw)
        {
            _openGl = openGl;
            ID = _openGl.CreateVBO<T>(Target, Data, Hint, Data.Length);

            BufferTarget = Target;
            this.Size = GetTypeComponentSize();
            this.PointerType = GetAttribPointerType();
            this.Count = Data.Length;
            this.IsIntegralType = IsTypeIntegral();
        }

        /// <summary>
        /// Creates a static-read array buffer of type T.
        /// </summary>
        /// <param name="Data">Specifies a pointer to data that will be copied into the data store for initialization.</param>
        public VBO(GL openGl, T[] Data)
            : this(openGl, Data, BufferTargetARB.ArrayBuffer, BufferUsageARB.StaticDraw)
        {
        }

        /// <summary>
        /// Creates a buffer object of type T with a specified length.
        /// </summary>
        /// <param name="Length">The length of the vertex buffer.</param>
        /// <param name="Target">Specifies the target buffer object.</param>
        /// <param name="Hint">Specifies the expected usage of the data store.</param>
        public VBO(GL openGl, int Length, BufferTargetARB Target = BufferTargetARB.ArrayBuffer, BufferUsageARB Hint = BufferUsageARB.StaticDraw)
        {
            _openGl = openGl;
            ID = _openGl.CreateVBO<T>(Target, Hint, Length);

            BufferTarget = Target;
            this.Size = GetTypeComponentSize();
            this.PointerType = GetAttribPointerType();
            this.Count = Length;
            this.IsIntegralType = IsTypeIntegral();
        }

        /// <summary>
        /// Get the component size of T.
        /// </summary>
        /// <returns>The component size of T.</returns>
        private int GetTypeComponentSize()
        {
            return TypeComponentSize[typeof(T)];
        }

        private VertexAttribPointerType GetAttribPointerType()
        {
            return TypeAttribPointerType[typeof(T)];
        }

        private bool IsTypeIntegral()
        {
            return IntegralTypes.Contains(typeof(T));
        }

        /// <summary>
        /// Updates a subset of the buffer object's data store.
        /// </summary>
        /// <param name="data">The new data that will be copied to the data store.</param>
        public unsafe void BufferSubData(ReadOnlySpan<T> data)
        {
            if (BufferTarget != BufferTargetARB.ArrayBuffer && BufferTarget != BufferTargetARB.ElementArrayBuffer &&
                BufferTarget != BufferTargetARB.PixelPackBuffer && BufferTarget != BufferTargetARB.PixelUnpackBuffer &&
                BufferTarget != BufferTargetARB.DrawIndirectBuffer)
                throw new InvalidOperationException(string.Format("BufferSubData cannot be called with a BufferTarget of type {0}", BufferTarget.ToString()));

            fixed (void* dataPointer = data)
            {
                if (_openGl.IsExtensionDirectStateAccessEnabled())
                {
                    _openGl.NamedBufferSubData(ID, 0, (nuint)(data.Length * Marshal.SizeOf<T>()), dataPointer);
                }
                else
                {
                    _openGl.BindBuffer(BufferTarget, ID);
                    _openGl.BufferSubData(BufferTarget, 0, (nuint)(data.Length * Marshal.SizeOf<T>()), dataPointer);
                }
            }
        }

        /// <summary>
        /// Maps a range of the buffer to a Span<typeparamref name="T"/>> that can be read/written to.
        /// </summary>
        /// <param name="offset">Element offset into the buffers range.</param>
        /// <param name="length">Element length of the mapped range.</param>
        /// <param name="mask"></param>
        /// <returns>A representation of the mapped range that also handles its lifetime. Remember to dispose if this when done.</returns>
        public MappedRange<T> MapBufferRange(GL openGl, int offset, int length, MapBufferAccessMask mask)
        {
            return new MappedRange<T>(openGl, this, offset, length, mask);
        }

        /// <summary>
        /// Deletes this buffer from GPU memory.
        /// </summary>
        public void Dispose()
        {
            if (ID != 0)
            {
                _openGl.DeleteBuffer(ID);
                ID = 0;
            }
        }
    }
}
