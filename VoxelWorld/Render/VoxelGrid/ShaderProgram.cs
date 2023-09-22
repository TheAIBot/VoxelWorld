using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace VoxelWorld.Render.VoxelGrid
{
    public sealed class ShaderProgram : IDisposable
    {
        private readonly GL _openGl;
        /// <summary>
        /// Specifies the OpenGL shader program ID.
        /// </summary>
        public uint ProgramID { get; private set; }

        /// <summary>
        /// Specifies the vertex shader used in this program.
        /// </summary>
        public Shader VertexShader { get; private set; }

        /// <summary>
        /// Specifies the fragment shader used in this program.
        /// </summary>
        public Shader FragmentShader { get; private set; }

        /// <summary>
        /// Specifies whether this program will dispose of the child 
        /// vertex/fragment programs when the IDisposable method is called.
        /// </summary>
        public bool DisposeChildren { get; set; }

        private Dictionary<string, ProgramParam> shaderParams;

        /// <summary>
        /// Queries the shader parameter hashtable to find a matching attribute/uniform.
        /// </summary>
        /// <param name="name">Specifies the case-sensitive name of the shader attribute/uniform.</param>
        /// <returns>The requested attribute/uniform, or null on a failure.</returns>
        public ProgramParam this[string name]
        {
            get { return shaderParams.ContainsKey(name) ? shaderParams[name] : null; }
        }

        /// <summary>
        /// Returns Gl.GetProgramInfoLog(ProgramID), which contains any linking errors.
        /// </summary>
        public string ProgramLog
        {
            get { return _openGl.GetProgramInfoLog(ProgramID); }
        }


        /// <summary>
        /// Links a vertex and fragment shader together to create a shader program.
        /// </summary>
        /// <param name="vertexShader">Specifies the vertex shader.</param>
        /// <param name="fragmentShader">Specifies the fragment shader.</param>
        public ShaderProgram(GL openGl, Shader vertexShader, Shader fragmentShader)
        {
            _openGl = openGl;
            this.VertexShader = vertexShader;
            this.FragmentShader = fragmentShader;
            this.ProgramID = _openGl.CreateProgram();
            this.DisposeChildren = false;

            _openGl.AttachShader(ProgramID, vertexShader.ShaderID);
            _openGl.AttachShader(ProgramID, fragmentShader.ShaderID);
            _openGl.LinkProgram(ProgramID);

            //Check whether the program linked successfully.
            //If not then throw an error with the linking error.
            string infoLog = _openGl.GetProgramInfoLog(ProgramID);
            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                throw new Exception(infoLog);
            }

            GetParams();
        }

        /// <summary>
        /// Creates two shaders and then links them together to create a shader program.
        /// </summary>
        /// <param name="vertexShaderSource">Specifies the source code of the vertex shader.</param>
        /// <param name="fragmentShaderSource">Specifies the source code of the fragment shader.</param>
        public ShaderProgram(GL openGl, string vertexShaderSource, string fragmentShaderSource)
            : this(openGl, new Shader(openGl, vertexShaderSource, ShaderType.VertexShader), new Shader(openGl, fragmentShaderSource, ShaderType.FragmentShader))
        {
            DisposeChildren = true;
        }


        /// <summary>
        /// Parses all of the parameters (attributes/uniforms) from the two attached shaders
        /// and then loads their location by passing this shader program into the parameter object.
        /// </summary>
        private void GetParams()
        {
            shaderParams = new Dictionary<string, ProgramParam>();

            int[] resources = new int[1];
            _openGl.GetProgram(ProgramID, ProgramPropertyARB.ActiveAttributes, resources);

            for (int i = 0; i < resources[0]; i++)
            {
                uint length;
                int size;
                Span<byte> name = new byte[256];
                AttributeType type;
                _openGl.GetActiveAttrib(ProgramID, (uint)i, out length, out size, out type, name);

                name = name.Slice(0, (int)length);
                if (!shaderParams.ContainsKey(Encoding.UTF8.GetString(name)))
                {
                    ProgramParam param = new ProgramParam(_openGl, TypeFromAttributeType(type), ParamType.Attribute, Encoding.UTF8.GetString(name));
                    shaderParams.Add(param.Name, param);
                    param.GetLocation(this);
                }
            }

            _openGl.GetProgram(ProgramID, ProgramPropertyARB.ActiveUniforms, resources);

            for (int i = 0; i < resources[0]; i++)
            {
                uint length;
                int size;
                Span<byte> name = new byte[256];
                UniformType type;
                _openGl.GetActiveUniform(ProgramID, (uint)i, out length, out size, out type, name);

                name = name.Slice(0, (int)length);
                if (!shaderParams.ContainsKey(Encoding.UTF8.GetString(name)))
                {
                    ProgramParam param = new ProgramParam(_openGl, TypeFromUniformType(type), ParamType.Uniform, Encoding.UTF8.GetString(name));
                    shaderParams.Add(param.Name, param);
                    param.GetLocation(this);
                }
            }
        }

        private Type TypeFromAttributeType(AttributeType type)
        {
            switch (type)
            {

                case AttributeType.Float: return typeof(float);
                case AttributeType.FloatMat2: return typeof(float[]);
                case AttributeType.FloatMat3: return typeof(Matrix3X3<float>);
                case AttributeType.FloatMat4: return typeof(Matrix4x4);
                case AttributeType.FloatVec2: return typeof(Vector2);
                case AttributeType.FloatVec3: return typeof(Vector3);
                case AttributeType.FloatVec4: return typeof(Vector4);
                default: return typeof(object);
            }
        }

        private Type TypeFromUniformType(UniformType type)
        {
            switch (type)
            {
                case UniformType.Int: return typeof(int);
                case UniformType.Float: return typeof(float);
                case UniformType.FloatVec2: return typeof(Vector2);
                case UniformType.FloatVec3: return typeof(Vector3);
                case UniformType.FloatVec4: return typeof(Vector4);
                case UniformType.IntVec2: return typeof(int[]);
                case UniformType.IntVec3: return typeof(int[]);
                case UniformType.IntVec4: return typeof(int[]);
                case UniformType.Bool: return typeof(bool);
                case UniformType.BoolVec2: return typeof(bool[]);
                case UniformType.BoolVec3: return typeof(bool[]);
                case UniformType.BoolVec4: return typeof(bool[]);
                case UniformType.FloatMat2: return typeof(float[]);
                case UniformType.FloatMat3: return typeof(Matrix3X3<float>);
                case UniformType.FloatMat4: return typeof(Matrix4x4);
                case UniformType.Sampler1D:
                case UniformType.Sampler2D:
                case UniformType.Sampler3D:
                case UniformType.SamplerCube:
                case UniformType.Sampler1DShadow:
                case UniformType.Sampler2DShadow:
                case UniformType.Sampler2DRect:
                case UniformType.Sampler2DRectShadow: return typeof(int);
                case UniformType.FloatMat2x3:
                case UniformType.FloatMat2x4:
                case UniformType.FloatMat3x2:
                case UniformType.FloatMat3x4:
                case UniformType.FloatMat4x2:
                case UniformType.FloatMat4x3: return typeof(float[]);
                case UniformType.Sampler1DArray:
                case UniformType.Sampler2DArray:
                case UniformType.SamplerBuffer:
                case UniformType.Sampler1DArrayShadow:
                case UniformType.Sampler2DArrayShadow:
                case UniformType.SamplerCubeShadow: return typeof(int);
                case UniformType.UnsignedIntVec2: return typeof(uint[]);
                case UniformType.UnsignedIntVec3: return typeof(uint[]);
                case UniformType.UnsignedIntVec4: return typeof(uint[]);
                case UniformType.IntSampler1D:
                case UniformType.IntSampler2D:
                case UniformType.IntSampler3D:
                case UniformType.IntSamplerCube:
                case UniformType.IntSampler2DRect:
                case UniformType.IntSampler1DArray:
                case UniformType.IntSampler2DArray:
                case UniformType.IntSamplerBuffer: return typeof(int);
                case UniformType.UnsignedIntSampler1D:
                case UniformType.UnsignedIntSampler2D:
                case UniformType.UnsignedIntSampler3D:
                case UniformType.UnsignedIntSamplerCube:
                case UniformType.UnsignedIntSampler2DRect:
                case UniformType.UnsignedIntSampler1DArray:
                case UniformType.UnsignedIntSampler2DArray:
                case UniformType.UnsignedIntSamplerBuffer: return typeof(uint);
                case UniformType.Sampler2DMultisample: return typeof(int);
                case UniformType.IntSampler2DMultisample: return typeof(int);
                case UniformType.UnsignedIntSampler2DMultisample: return typeof(uint);
                case UniformType.Sampler2DMultisampleArray: return typeof(int);
                case UniformType.IntSampler2DMultisampleArray: return typeof(int);
                case UniformType.UnsignedIntSampler2DMultisampleArray: return typeof(uint);
                default: return typeof(object);
            }
        }


        public void Use()
        {
            _openGl.UseProgram(this.ProgramID);
        }

        public int GetUniformLocation(string Name)
        {
            Use();
            return _openGl.GetUniformLocation(ProgramID, Name);
        }

        public int GetAttributeLocation(string Name)
        {
            Use();
            return _openGl.GetAttribLocation(ProgramID, Name);
        }


        public void Dispose()
        {
            if (ProgramID != 0)
            {
                // Make sure this program isn't being used
                _openGl.UseProgram(0);

                _openGl.DetachShader(ProgramID, VertexShader.ShaderID);
                _openGl.DetachShader(ProgramID, FragmentShader.ShaderID);
                _openGl.DeleteProgram(ProgramID);

                if (DisposeChildren)
                {
                    VertexShader.Dispose();
                    FragmentShader.Dispose();
                }

                ProgramID = 0;
            }
        }
    }
}
