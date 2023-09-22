﻿using Silk.NET.OpenGL;
using System.Numerics;
using VoxelWorld.Render.VoxelGrid;

namespace VoxelWorld.Shaders
{
    internal static class BoxShader
    {
        private static readonly string VertexShader = @"
attribute vec3 vertex_pos;
attribute vec3 vertex_offset;
attribute float vertex_scale;
uniform mat4 P;
uniform mat4 V;
uniform mat4 M;

void main(void)
{
    gl_Position = P * V * M * vec4((vertex_pos * vertex_scale) + vertex_offset, 1.0);
}";

        private static readonly string FragmentShader = @"
void main(void)
{
    gl_FragColor = vec4(1.0, 0.0, 0.0, 0.5);
}
";

        private static ShaderProgram Static_Shader;

        internal static ShaderProgram GetShader(GL openGl)
        {
            Static_Shader ??= new ShaderProgram(openGl, VertexShader, FragmentShader);
            return Static_Shader;
        }

        internal static void SetPVM(Matrix4x4 perspective, Matrix4x4 view, Matrix4x4 model)
        {
            Static_Shader["P"].SetValue(perspective);
            Static_Shader["V"].SetValue(view);
            Static_Shader["M"].SetValue(model);
        }
    }
}
