using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;

namespace VoxelWorld.Render.VoxelGrid
{
    public sealed class VAO : IDisposable
    {
        private static readonly Dictionary<VertexAttribPointerType, DrawElementsType> ValidElementTypes = new Dictionary<VertexAttribPointerType, DrawElementsType>()
        {
            [VertexAttribPointerType.UnsignedByte] = DrawElementsType.UnsignedByte,
            [VertexAttribPointerType.UnsignedShort] = DrawElementsType.UnsignedShort,
            [VertexAttribPointerType.UnsignedInt] = DrawElementsType.UnsignedInt
        };

        private readonly GL _openGl;

        private readonly IGenericVBO[] _vbos;

        public struct GenericVBO<T> : IGenericVBO
            where T : unmanaged
        {
            private readonly VBO<T> vbo;
            private readonly string name;

            public uint ID => vbo.ID;

            public string Name => name;

            public VertexAttribPointerType PointerType => vbo.PointerType;

            public int Length => vbo.Count;

            public BufferTargetARB BufferTarget => vbo.BufferTarget;

            public int Size => vbo.Size;

            public uint Divisor => vbo.Divisor;

            public bool Normalize => vbo.Normalize;

            public bool CastToFloat => vbo.CastToFloat;

            public bool IsIntegralType => vbo.IsIntegralType;

            public GenericVBO(VBO<T> vbo) : this(vbo, string.Empty)
            {
            }

            public GenericVBO(VBO<T> vbo, string name)
            {
                this.vbo = vbo;
                this.name = name;
            }

            /// <summary>
            /// Deletes the vertex array from the GPU and will also dispose of any child VBOs if (DisposeChildren == true).
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    vbo.Dispose();
                }
            }
        }

        public VAO(GL openGl, ShaderProgram program, IGenericVBO[] vbos)
        {
            _openGl = openGl;
            Program = program;
            _vbos = vbos;
            DrawMode = PrimitiveType.Triangles;

            ID = openGl.GenVertexArray();
            if (ID != 0)
            {
                openGl.BindVertexArray(ID);
                BindAttributes(Program);
            }
            openGl.BindVertexArray(0);
        }


        private bool disposeChildren = false;
        private DrawElementsType elementType;
        public DrawElementsType ElementType { get { return elementType; } }
        private bool allowIntAsElementType = true;
        private IntPtr offsetInBytes = IntPtr.Zero;
        public IntPtr OffsetInBytes { get { return offsetInBytes; } }


        /// <summary>
        /// The number of vertices that make up this VAO.
        /// </summary>
        public int VertexCount { get; set; }

        /// <summary>
        /// Specifies if the VAO should dispose of the child VBOs when Dispose() is called.
        /// </summary>
        public bool DisposeChildren
        {
            get { return disposeChildren; }
            set
            {
                disposeChildren = value;
                DisposeElementArray = value;    // TODO:  I think this is bad behaviour
            }
        }

        /// <summary>
        /// Specifies if the VAO should dispose of the element array when Dispose() is called.
        /// </summary>
        public bool DisposeElementArray { get; set; }

        /// <summary>
        /// The ShaderProgram associated with this VAO.
        /// </summary>
        public ShaderProgram Program { get; }

        /// <summary>
        /// The drawing mode to use when drawing the arrays.
        /// </summary>
        public PrimitiveType DrawMode { get; set; }

        /// <summary>
        /// The ID of this Vertex Array Object for use in calls to OpenGL.
        /// </summary>
        public uint ID { get; private set; }


        private int GetElementSizeInBytes()
        {
            switch (elementType)
            {
                case DrawElementsType.UnsignedByte:
                    return 1;
                case DrawElementsType.UnsignedShort:
                    return 2;
                case DrawElementsType.UnsignedInt:
                    return 4;
                default:
                    throw new Exception($"Unknown enum value. Expected an enum of type {nameof(DrawElementsType)}.");
            }
        }

        private unsafe void BindAttributes(ShaderProgram program)
        {
            IGenericVBO elementArray = null;

            for (int i = 0; i < _vbos.Length; i++)
            {
                if (_vbos[i].BufferTarget == BufferTargetARB.ElementArrayBuffer)
                {
                    elementArray = _vbos[i];

                    // Check if the element array can be used as an indice buffer.
                    if (!ValidElementTypes.ContainsKey(_vbos[i].PointerType))
                    {
                        throw new Exception($"The element buffer must be an unsigned integral type. See {nameof(DrawElementsType)} enum for valid types.");
                    }
                    elementType = ValidElementTypes[_vbos[i].PointerType];
                    continue;
                }
                else if (_vbos[i].BufferTarget == BufferTargetARB.DrawIndirectBuffer)
                {
                    continue;
                }

                // According to OGL spec then, if there is no location for an attribute, -1 is returned.
                // The same error representation is used here.
                int loc = program[_vbos[i].Name]?.Location ?? -1;
                if (loc == -1) throw new Exception(string.Format("Shader did not contain '{0}'.", _vbos[i].Name));

                _openGl.EnableVertexAttribArray((uint)loc);
                _openGl.BindBuffer(_vbos[i].BufferTarget, _vbos[i].ID);

                if (_vbos[i].CastToFloat)
                {
                    _openGl.VertexAttribPointer((uint)loc, _vbos[i].Size, _vbos[i].PointerType, _vbos[i].Normalize, 0u, null);
                }
                else if (_vbos[i].IsIntegralType)
                {
                    VertexAttribIType iType = _vbos[i].PointerType switch
                    {
                        VertexAttribPointerType.Byte => VertexAttribIType.Byte,
                        VertexAttribPointerType.UnsignedByte => VertexAttribIType.UnsignedByte,
                        VertexAttribPointerType.Short => VertexAttribIType.Short,
                        VertexAttribPointerType.UnsignedShort => VertexAttribIType.UnsignedShort,
                        VertexAttribPointerType.Int => VertexAttribIType.Int,
                        VertexAttribPointerType.UnsignedInt => VertexAttribIType.UnsignedInt,
                        _ => throw new Exception()
                    };

                    _openGl.VertexAttribIPointer((uint)loc, _vbos[i].Size, iType, 0u, null);
                }
                else if (_vbos[i].PointerType == VertexAttribPointerType.Double)
                {
                    _openGl.VertexAttribLPointer((uint)loc, _vbos[i].Size, VertexAttribLType.Double, 0u, null);
                }
                else
                {
                    throw new Exception("VBO shouldn't be cast to float, isn't an integral type and is not a float. No vertex attribute support this combination.");
                }

                // 0 is the divisors default value.
                // No need to set the divisor to its default value.
                if (_vbos[i].Divisor != 0)
                {
                    _openGl.VertexAttribDivisor((uint)loc, _vbos[i].Divisor);
                }
            }

            if (elementArray != null)
            {
                _openGl.BindBuffer(BufferTargetARB.ElementArrayBuffer, elementArray.ID);
                VertexCount = elementArray.Length;
            }
        }

        /// <summary>
        /// OGL3 method uses a vertex array object for quickly binding the VBOs to their attributes.
        /// </summary>
        public unsafe void Draw()
        {
            if (ID == 0 || VertexCount == 0) return;
            _openGl.BindVertexArray(ID);
            _openGl.DrawElements(DrawMode, (uint)VertexCount, elementType, null);
            _openGl.BindVertexArray(0);
        }

        /// <summary>
        /// OGL3 method uses a vertex array object for quickly binding the VBOs to their attributes.
        /// </summary>
        public unsafe void DrawInstanced(int count)
        {
            if (ID == 0 || VertexCount == 0 || count == 0) return;
            _openGl.BindVertexArray(ID);
            _openGl.DrawElementsInstanced(DrawMode, (uint)VertexCount, elementType, null, (uint)count);
            _openGl.BindVertexArray(0);
        }

        /// <summary>
        /// OGL4 method uses a vertex array object for quickly binding the VBOs to their attributes.
        /// </summary>
        public unsafe void MultiDrawElementsIndirect(VBO<DrawElementsIndirectCommand> cmdVBO, int cmdCount)
        {
            if (ID == 0) return;
            _openGl.BindVertexArray(ID);
            _openGl.BindBuffer(cmdVBO.BufferTarget, cmdVBO.ID);
            _openGl.MultiDrawElementsIndirect(DrawMode, elementType, null, (uint)cmdCount, 0);
            _openGl.BindVertexArray(0);
        }

        public unsafe void MultiDrawIndirect(VBO<DrawElementsIndirectCommand> cmdVBO, int cmdCount)
        {
            if (ID == 0) return;
            _openGl.BindVertexArray(ID);
            _openGl.BindBuffer(cmdVBO.BufferTarget, cmdVBO.ID);
            _openGl.MultiDrawArraysIndirect(DrawMode, null, (uint)cmdCount, 0);
            _openGl.BindVertexArray(0);
        }


        /// <summary>
        /// Deletes the vertex array from the GPU and will also dispose of any child VBOs if (DisposeChildren == true).
        /// </summary>
        public void Dispose()
        {
            // first try to dispose of the vertex array
            if (ID != 0)
            {
                _openGl.DeleteVertexArray(ID);

                ID = 0;
            }

            // children must be disposed of separately since OpenGL 2.1 will not have a vertex array
            if (DisposeChildren)
            {
                for (int i = 0; i < _vbos.Length; i++)
                {
                    if (_vbos[i].BufferTarget == BufferTargetARB.ElementArrayBuffer && !DisposeElementArray) continue;
                    _vbos[i].Dispose();
                }
            }
        }
    }
}
