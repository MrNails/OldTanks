#version 460 core
in vec3 textureCoord;

layout(location = 0) out vec4 fragColor;
uniform samplerCube texture;

void main() {
    fragColor = texture(texture, textureCoord);
}
