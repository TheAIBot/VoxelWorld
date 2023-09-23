using Silk.NET.OpenGL;
using System;
using System.Runtime.InteropServices;

namespace VoxelWorld.Render.VoxelGrid
{
    public static class GLExtensions
    {
        private static bool? _isDirectStateAccessEnabled;
        private static readonly object _lock = new();

        public static bool IsExtensionDirectStateAccessEnabled(this GL openGl)
        {
            if (_isDirectStateAccessEnabled.HasValue)
            {
                return _isDirectStateAccessEnabled.Value;
            }

            lock (_lock)
            {
                _isDirectStateAccessEnabled = openGl.IsExtensionPresent("GL_ARB_direct_state_access");
            }

            return _isDirectStateAccessEnabled.Value;
        }

        /// <summary>
        /// Creates a standard VBO of type T where the length of the VBO is less than or equal to the length of the data.
        /// </summary>
        /// <typeparam name="T">The type of the data being stored in the VBO (make sure it's byte aligned).</typeparam>
        /// <param name="target">The VBO BufferTarget (usually ArrayBuffer or ElementArrayBuffer).</param>
        /// <param name="data">The data to store in the VBO.</param>
        /// <param name="hint">The buffer usage hint (usually StaticDraw).</param>
        /// <param name="length">The length of the VBO (will take the first 'length' elements from data).</param>
        /// <returns>The buffer ID of the VBO on success, 0 on failure.</returns>
        public static uint CreateVBO<T>(this GL openGl, BufferTargetARB target, ReadOnlySpan<T> data, BufferUsageARB hint, int length)
            where T : unmanaged
        {
            uint vboHandle = openGl.CreateBuffer();
            if (vboHandle == 0) return 0;

            openGl.BindBuffer((GLEnum)target, vboHandle);
            openGl.BufferData(target, data, hint);
            openGl.BindBuffer((GLEnum)target, 0);
            return vboHandle;
        }

        public static uint CreateVBO<T>(this GL openGl, BufferTargetARB target, BufferUsageARB hint, int length)
    where T : unmanaged
        {
            uint vboHandle = openGl.CreateBuffer();
            if (vboHandle == 0) return 0;

            openGl.BindBuffer((GLEnum)target, vboHandle);
            openGl.BufferData<T>(target, (nuint)(length * Marshal.SizeOf<T>()), null, hint);
            openGl.BindBuffer((GLEnum)target, 0);
            return vboHandle;
        }

        /// <summary>
        /// Maps a range of the buffer object's data store to a Span<typeparamref name="T"/>>.
        /// </summary>
        /// <typeparam name="T">The type of data in the VBO.</typeparam>
        /// <param name="buffer">The VBO whose buffer will be mapped.</param>
        /// <param name="offset">The offset in bytes into the data store.</param>
        /// <param name="length">The size in bytes of the data store region being mapped.</param>
        /// <param name="mask">Specifies how the buffer should be mapped.</param>
        /// <returns></returns>
        public static unsafe void* MapBufferRange<T>(this GL openGl, VBO<T> buffer, int offset, int length, MapBufferAccessMask mask)
            where T : unmanaged
        {
            if (openGl.IsExtensionDirectStateAccessEnabled())
            {
                return openGl.MapNamedBufferRange(buffer.ID, offset, (nuint)length, mask);
            }
            else
            {
                openGl.BindBuffer(buffer.BufferTarget, buffer.ID);
                return openGl.MapBufferRange(buffer.BufferTarget, offset, (nuint)length, mask);
            }
        }
    }
}
