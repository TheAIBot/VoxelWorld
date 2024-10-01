using Silk.NET.OpenGL;
using VoxelWorld.Render.VoxelGrid;

namespace VoxelWorld.Shaders
{
    internal static class PointShader
    {
        private static readonly string VertexShader = @"
in vec3 vertex_pos;
uniform mat4 P;
uniform mat4 V;
uniform mat4 M;

void main(void)
{
    gl_Position = P * V * M * vec4(vertex_pos, 1.0);
}
";

        private static readonly string FragmentShader = @"
layout(location = 0) out vec4 fragColor;

void main(void)
{
    fragColor = vec4(1.0, 0.0, 0.0, 1.0);
}
";

        private static ShaderProgram Static_Shader;

        internal static ShaderProgram GetShader(GL openGl)
        {
            Static_Shader ??= new ShaderProgram(openGl, VertexShader, FragmentShader);
            return Static_Shader;
        }
    }
}
