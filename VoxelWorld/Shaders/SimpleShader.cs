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
uniform mat4 N;



out vec3 position;
out vec3 normal;

void main(void)
{
    gl_Position = P * V * M * vec4(vertex_pos, 1.0);
    position = (V * M * vec4(vertex_pos, 1.0)).xyz;
    normal = mat3(N) * vertex_normal;
}
";

        private static readonly string FragmentShader = @"
#version 330

in vec3 position;
in vec3 normal;

uniform vec4 light_pos;
uniform vec4 light_diff;
uniform vec4 light_spec;
uniform vec4 light_amb;

uniform vec4 mat_diff;
uniform vec4 mat_spec;
uniform float mat_spec_exp;

void main(void)
{
    //vec3 light_dir = normalize(light_pos.a > 0.0 ? light_pos.xyz - position : light_pos.xyz);
    vec3 light_dir = normalize(light_pos.xyz - position);
    float cos_theta = max(dot(normalize(normal), light_dir), 0.0);
    
    // ambient part
    gl_FragColor = mat_diff * light_amb;

    //diffuse part
    gl_FragColor += mat_diff * cos_theta * light_diff;
    
    // specular part
    vec3 refl_dir = reflect(normalize(position), normalize(normal));
    float r_dot_l = max(dot(refl_dir, light_dir), 0.0);
    gl_FragColor += mat_spec * pow(r_dot_l, max(mat_spec_exp, 1.0)) * light_spec;

    //gl_FragColor = gl_FragColor * 0.00001 + vec4(position, 1.0);
}
";

        private static ShaderProgram Static_Shader = new ShaderProgram(VertexShader, FragmentShader);

        internal static ShaderProgram  GetShader()
        {
            return Static_Shader;
        }
    }
}
