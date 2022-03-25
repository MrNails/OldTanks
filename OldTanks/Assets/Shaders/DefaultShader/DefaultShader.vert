#version 460 core
in vec3 iPos;
in vec2 iTextureCoord;

out vec2 textureCoord;

uniform mat4 projection;
uniform mat4 view;

uniform mat4 model[200];

void main() {
//    gl_Position = projection * view * model * vec4(iPos, 1.0f);
    gl_Position = projection * view * model[gl_InstanceID] * vec4(iPos, 1.0f);
    textureCoord = iTextureCoord;
}
