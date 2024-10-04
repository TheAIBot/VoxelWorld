using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System.Numerics;
using VoxelWorld.Render.VoxelGrid;

namespace VoxelWorld.Shaders
{
    internal static class SimpleShader
    {
        private static readonly string VertexShader = @"
#version 460 core

in vec3 vertex_pos;
in vec3 gridPosition;
in float gridSize;
in uint vertex_normal;
in vec3 size;
in uint baseVertexIndex;
uniform mat4 PVM;
uniform mat3 M;

out vec3 position;
out vec3 normal;

void main(void)
{
    float normalX = (vertex_normal &  1u) ==  1u ? 1.0f : (vertex_normal &  2u) ==  2u ? -1.0f : 0.0;
    float normalY = (vertex_normal &  4u) ==  4u ? 1.0f : (vertex_normal &  8u) ==  8u ? -1.0f : 0.0;
    float normalZ = (vertex_normal & 16u) == 16u ? 1.0f : (vertex_normal & 32u) == 32u ? -1.0f : 0.0;
    vec3 convertedNormal = -normalize(vec3(normalX, normalY, normalZ));

    gl_Position = PVM * vec4(vertex_pos, 1.0);
    position = M * vertex_pos;
    normal = M * convertedNormal;
}
";

        private static readonly string FragmentShader = @"
#version 460 core

in vec3 position;
in vec3 normal;

uniform vec4 light_pos;
uniform vec4 light_amb;
uniform vec4 light_diff;
uniform vec4 light_spec;

uniform vec4 mat_diff;
uniform vec4 mat_spec;
uniform float mat_spec_exp;

uniform vec3 viewPos;

layout(location = 0) out vec4 fragColor;

void main(void)
{
    vec3 norm = normalize(normal);

    // ambient part
    fragColor = mat_diff * light_amb;

    // diffuse part
    vec3 light_dir = normalize(light_pos.xyz - position);
    float cos_theta = max(dot(norm, light_dir), 0.0);
    fragColor += mat_diff * cos_theta * light_diff;
    
    // specular part
    vec3 viewDir = normalize(viewPos - position);
    vec3 refl_dir = reflect(-light_dir, norm);
    float r_dot_l = max(dot(viewDir, refl_dir), 0.0);
    fragColor += mat_spec * pow(r_dot_l, mat_spec_exp) * light_spec;
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
            Static_Shader["PVM"].SetValue(model * view * perspective);
            Static_Shader["M"].SetValue(new Matrix3X3<float>(
                model.M11, model.M12, model.M13,
                model.M21, model.M22, model.M23,
                model.M31, model.M32, model.M33));
        }

        internal static void SetLight(DirectionalLight light, Vector3 cameraPosition)
        {
            Static_Shader["light_pos"].SetValue(light.Position);
            Static_Shader["light_amb"].SetValue(light.Ambient);
            Static_Shader["light_diff"].SetValue(light.Diffuse);
            Static_Shader["light_spec"].SetValue(light.Specular);

            Static_Shader["viewPos"].SetValue(cameraPosition);
        }

        internal static void SetMaterial(Material material)
        {
            Static_Shader["mat_diff"].SetValue(material.Diffuse);
            Static_Shader["mat_spec"].SetValue(material.Specular);
            Static_Shader["mat_spec_exp"].SetValue(material.Shininess);
        }
    }
}
