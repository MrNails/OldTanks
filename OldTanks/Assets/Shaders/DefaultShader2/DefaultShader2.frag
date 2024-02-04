#version 460 core
out vec4 fragColor;

in vec2 textureCoord;
in vec4 instanceColor;
in float hasTexture;

uniform sampler2D texture0;

void main() {
    fragColor = instanceColor;
    
    if (hasTexture == 1)
        fragColor *= texture(texture0, textureCoord);
}
