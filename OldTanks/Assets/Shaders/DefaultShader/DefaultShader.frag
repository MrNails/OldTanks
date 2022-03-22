#version 460 core
in vec2 textureCoord;

uniform sampler2D texture0;

void main() {
    gl_FragColor = texture(texture0, textureCoord);
}
