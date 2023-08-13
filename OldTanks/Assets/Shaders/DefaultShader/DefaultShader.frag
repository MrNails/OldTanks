#version 460 core
in vec2 textureCoord;
layout(location = 0) out vec4 fragColor;

uniform bool hasTexture;
uniform vec4 color;
uniform sampler2D texture0;

void main() {
    if (hasTexture)
        fragColor = texture(texture0, textureCoord) * color;
    else
        fragColor = color;
}
