#version 460 core
in vec2 textureCoord;

uniform bool useOnlyColor;
uniform vec4 color;
uniform sampler2D texture0;

void main() {
    if (useOnlyColor)
        gl_FragColor = color;
    else
        gl_FragColor = texture(texture0, textureCoord) * color;
}
