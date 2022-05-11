#version 460 core
in vec2 textureCoord;

uniform bool hasTexture;
uniform vec4 color;
uniform sampler2D texture0;

void main() {
    if (hasTexture)
        gl_FragColor = texture(texture0, textureCoord) * color;
    else
        gl_FragColor = color;
}
