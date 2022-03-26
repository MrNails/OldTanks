#version 460 core
in vec3 iPos;
in vec2 iTexture;

out vec2 texture;

uniform mat4 projection;
uniform mat4 model;

void main() {
        texture = iTexture;
        gl_Position = projection * model * vec4(iPos, 1.0);
}