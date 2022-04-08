#version 460 core
in vec2 textureCoord;

uniform bool useColor;
uniform vec4 color;
uniform sampler2D texture0;

void main() {
    if (useColor)
        gl_FragColor = color;
    else
        gl_FragColor = texture(texture0, textureCoord) * color;
}
