#version 460 core
in vec3 textureCoord;

layout(location = 0) out vec4 diffuseColor;

uniform samplerCube texture;

void main() {
    diffuseColor = texture(texture, textureCoord);
}
