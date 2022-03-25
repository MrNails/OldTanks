#version 460 core
in vec3 textureCoord;

uniform samplerCube texture;

void main() {
    gl_FragColor = texture(texture, textureCoord);
}
