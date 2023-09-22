using Silk.NET.OpenGL;
using System;

namespace VoxelWorld.Render.VoxelGrid
{
    public sealed class Shader : IDisposable
    {
        private readonly GL _openGl;
        /// <summary>
        /// Specifies the OpenGL ShaderID.
        /// </summary>
        public uint ShaderID { get; private set; }

        /// <summary>
        /// Specifies the type of shader.
        /// </summary>
        public ShaderType ShaderType { get; private set; }

        /// <summary>
        /// Returns Gl.GetShaderInfoLog(ShaderID), which contains any compilation errors.
        /// </summary>
        public string ShaderLog
        {
            get { return _openGl.GetShaderInfoLog(ShaderID); }
        }


        /// <summary>
        /// Compiles a shader, which can be either vertex, fragment or geometry.
        /// </summary>
        /// <param name="source">Specifies the source code of the shader object.</param>
        /// <param name="type">Specifies the type of shader to create (either vertex, fragment or geometry).</param>
        public Shader(GL openGl, string source, ShaderType type)
        {
            _openGl = openGl;
            this.ShaderType = type;
            this.ShaderID = _openGl.CreateShader(type);

            _openGl.ShaderSource(ShaderID, source);
            _openGl.CompileShader(ShaderID);

            //Check whether the shader compiled successfully.
            //If not then throw an error with the compile error.
            string infoLog = _openGl.GetShaderInfoLog(ShaderID);
            if (!string.IsNullOrWhiteSpace(infoLog))
            {
                throw new Exception(ShaderLog);
            }
        }

        public void Dispose()
        {
            if (ShaderID != 0)
            {
                _openGl.DeleteShader(ShaderID);
                ShaderID = 0;
            }
        }
    }
}
