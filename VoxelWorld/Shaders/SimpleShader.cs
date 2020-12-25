using OpenGL;
using System;
using System.Collections.Generic;
using System.Text;

namespace VoxelWorld.Shaders
{
    internal static class SimpleShader
    {
        private static readonly string VertexShader = @"
#version 330

attribute vec3 vertex_pos;
attribute vec3 vertex_normal;
uniform mat4 P;
uniform mat4 V;
uniform mat4 M;

out vec3 position;
out vec3 normal;

void main(void)
{
    gl_Position = P * V * M * vec4(vertex_pos, 1.0);
    position = (M * vec4(vertex_pos, 1.0)).xyz;
    normal = mat3(M) * vertex_normal;
}
";

        private static readonly string FragmentShader = @"
#version 330

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

void main(void)
{
    vec3 norm = normalize(normal);

    // ambient part
    gl_FragColor = mat_diff * light_amb;

    // diffuse part
    vec3 light_dir = normalize(light_pos.xyz - position);
    float cos_theta = max(dot(norm, light_dir), 0.0);
    gl_FragColor += mat_diff * cos_theta * light_diff;
    
    // specular part
    vec3 viewDir = normalize(viewPos - position);
    vec3 refl_dir = reflect(-light_dir, norm);
    float r_dot_l = max(dot(viewDir, refl_dir), 0.0);
    gl_FragColor += mat_spec * pow(r_dot_l, mat_spec_exp) * light_spec;
}
";

        private static ShaderProgram Static_Shader = new ShaderProgram(VertexShader, FragmentShader);

        internal static ShaderProgram  GetShader()
        {
            return Static_Shader;
        }
    }
}
