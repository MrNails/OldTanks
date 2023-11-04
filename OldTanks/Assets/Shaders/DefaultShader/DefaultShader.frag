#version 460 core
out vec4 fragColor;
in vec2 textureCoord;

uniform bool hasTexture;
uniform vec4 color;
uniform sampler2D texture0;

void main() {
    if (hasTexture)
        fragColor = texture(texture0, textureCoord) * color;
    else
        fragColor = color;
}
