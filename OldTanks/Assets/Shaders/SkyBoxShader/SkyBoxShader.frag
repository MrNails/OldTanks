#version 460 core
in vec3 textureCoord;

layout(location = 0) out vec4 fragColor;
uniform samplerCube texture0;

void main() {
    fragColor = texture(texture0, textureCoord);
}
