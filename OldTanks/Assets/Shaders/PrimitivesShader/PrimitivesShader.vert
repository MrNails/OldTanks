#version 460 core
in vec3 iPos;

uniform mat4 projection;
uniform mat4 view;

void main() {
    gl_Position = projection * view * vec4(iPos, 1.0f);
}
