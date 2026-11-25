#version 460 core
in vec3 iPos;
in vec2 iTexture;

out vec2 textureCoord;

uniform mat4 model;
uniform mat4 projection;

void main() {
    gl_Position = projection * model * vec4(iPos, 1.0);

    textureCoord = iTexture;
}