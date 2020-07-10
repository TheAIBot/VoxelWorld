using OpenGL;
using System;
using System.Collections.Generic;
using System.Text;

namespace VoxelWorld.Shaders
{
    internal static class PointShader
    {
        private static readonly string VertexShader = @"
attribute vec3 vertex_pos;
uniform mat4 P;
uniform mat4 V;
uniform mat4 M;

void main(void)
{
    gl_Position = P * V * M * vec4(vertex_pos, 1.0);
}
";

        private static readonly string FragmentShader = @"
void main(void)
{
    gl_FragColor = vec4(1.0, 0.0, 0.0, 1.0);
}
";

        private static ShaderProgram Static_Shader = new ShaderProgram(VertexShader, FragmentShader);

        internal static ShaderProgram GetShader()
        {
            return Static_Shader;
        }
    }
}
