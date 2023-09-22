using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VoxelWorld.Render.VoxelGrid
{
    public sealed class ProgramParam
    {
        private readonly GL _openGl;
        private Type type;
        private int location;
        private uint programid;
        private ParamType ptype;
        private string name;

        /// <summary>
        /// Specifies the C# equivalent of the GLSL data type.
        /// </summary>
        public Type Type { get { return type; } }

        /// <summary>
        /// Specifies the location of the parameter in the OpenGL program.
        /// </summary>
        public int Location { get { return location; } }

        /// <summary>
        /// Specifies the OpenGL program ID.
        /// </summary>
        public uint Program { get { return programid; } }

        /// <summary>
        /// Specifies the parameter type (either attribute or uniform).
        /// </summary>
        public ParamType ParamType { get { return ptype; } }

        /// <summary>
        /// Specifies the case-sensitive name of the parameter.
        /// </summary>
        public string Name { get { return name; } }

        /// <summary>
        /// Creates a program parameter with a given type and name.
        /// The location must be found after the program is compiled
        /// by using the GetLocation(ShaderProgram Program) method.
        /// </summary>
        /// <param name="Type">Specifies the C# equivalent of the GLSL data type.</param>
        /// <param name="ParamType">Specifies the parameter type (either attribute or uniform).</param>
        /// <param name="Name">Specifies the case-sensitive name of the parameter.</param>
        public ProgramParam(GL openGl, Type Type, ParamType ParamType, string Name)
        {
            _openGl = openGl;
            type = Type;
            ptype = ParamType;
            name = Name;
        }

        /// <summary>
        /// Creates a program parameter with a type, name, program and location.
        /// </summary>
        /// <param name="Type">Specifies the C# equivalent of the GLSL data type.</param>
        /// <param name="ParamType">Specifies the parameter type (either attribute or uniform).</param>
        /// <param name="Name">Specifies the case-sensitive name of the parameter.</param>
        /// <param name="Program">Specifies the OpenGL program ID.</param>
        /// <param name="Location">Specifies the location of the parameter.</param>
        public ProgramParam(GL openGl, Type Type, ParamType ParamType, string Name, uint Program, int Location)
            : this(openGl, Type, ParamType, Name)
        {
            programid = Program;
            location = Location;
        }


        /// <summary>
        /// Gets the location of the parameter in a compiled OpenGL program.
        /// </summary>
        /// <param name="Program">Specifies the shader program that contains this parameter.</param>
        public void GetLocation(ShaderProgram Program)
        {
            Program.Use();
            if (programid == 0)
            {
                programid = Program.ProgramID;
                location = (ptype == ParamType.Uniform ? Program.GetUniformLocation(name) : Program.GetAttributeLocation(name));
            }
        }


        public void SetValue(bool param)
        {
            if (Type != typeof(bool)) throw new Exception(string.Format("SetValue({0}) was given a bool.", Type));
            _openGl.Uniform1(location, (param) ? 1 : 0);
        }

        public void SetValue(int param)
        {
            if (Type != typeof(int) && Type != typeof(Texture)) throw new Exception(string.Format("SetValue({0}) was given a int.", Type));
            _openGl.Uniform1(location, param);
        }

        public void SetValue(float param)
        {
            if (Type != typeof(float)) throw new Exception(string.Format("SetValue({0}) was given a float.", Type));
            _openGl.Uniform1(location, param);
        }

        public void SetValue(Vector2 param)
        {
            if (Type != typeof(Vector2)) throw new Exception(string.Format("SetValue({0}) was given a Vector2.", Type));
            _openGl.Uniform2(location, param.X, param.Y);
        }

        public void SetValue(Vector3 param)
        {
            if (Type != typeof(Vector3)) throw new Exception(string.Format("SetValue({0}) was given a Vector3.", Type));
            _openGl.Uniform3(location, param.X, param.Y, param.Z);
        }

        public void SetValue(Vector4 param)
        {
            if (Type != typeof(Vector4)) throw new Exception(string.Format("SetValue({0}) was given a Vector4.", Type));
            _openGl.Uniform4(location, param.X, param.Y, param.Z, param.W);
        }

        public void SetValue(Matrix3X3<float> param)
        {
            if (Type != typeof(Matrix3X3<float>)) throw new Exception(string.Format("SetValue({0}) was given a Matrix3.", Type));

            _openGl.UniformMatrix3(location, false, MemoryMarshal.CreateSpan(ref param.Row1.X, 9));
        }

        public void SetValue(Matrix4x4 param)
        {
            if (Type != typeof(Matrix4x4)) throw new Exception(string.Format("SetValue({0}) was given a Matrix4.", Type));

            _openGl.UniformMatrix4(location, false, MemoryMarshal.CreateSpan(ref param.M11, 16));
        }

        public void SetValue(float[] param)
        {
            if (param.Length == 16)
            {
                if (Type != typeof(Matrix4x4)) throw new Exception(string.Format("SetValue({0}) was given a Matrix4.", Type));
                _openGl.UniformMatrix4(location, 1, false, param);
            }
            else if (param.Length == 9)
            {
                if (Type != typeof(Matrix3X3<float>)) throw new Exception(string.Format("SetValue({0}) was given a Matrix3.", Type));
                _openGl.UniformMatrix3(location, 1, false, param);
            }
            else if (param.Length == 4)
            {
                if (Type != typeof(Vector4)) throw new Exception(string.Format("SetValue({0}) was given a Vector4.", Type));
                _openGl.Uniform4(location, param[0], param[1], param[2], param[3]);
            }
            else if (param.Length == 3)
            {
                if (Type != typeof(Vector3)) throw new Exception(string.Format("SetValue({0}) was given a Vector3.", Type));
                _openGl.Uniform3(location, param[0], param[1], param[2]);
            }
            else if (param.Length == 2)
            {
                if (Type != typeof(Vector2)) throw new Exception(string.Format("SetValue({0}) was given a Vector2.", Type));
                _openGl.Uniform2(location, param[0], param[1]);
            }
            else if (param.Length == 1)
            {
                if (Type != typeof(float)) throw new Exception(string.Format("SetValue({0}) was given a float.", Type));
                _openGl.Uniform1(location, param[0]);
            }
            else
            {
                throw new ArgumentException("param was an unexpected length.", "param");
            }
        }

        /*public void SetValue(Texture param)
        {
            if (Type != typeof(Texture)) throw new Exception(string.Format("SetValue({0}) was given a Texture.", Type));
            Gl.Uniform1i(location, param.Binding);
        }*/



    }
}
