#version 460 core
in vec2 texture;

layout(location = 0) out vec4 fragColor;
uniform sampler2D texture0;

uniform vec4 color;
uniform int boldMultiplier;

void main() {
    int _bolMultipler = boldMultiplier;
    
    if (_bolMultipler == 0)
        _bolMultipler = 1;
    
    vec4 sampled = vec4(1.0f, 1.0f, 1.0f, texture(texture0, texture).r);
    fragColor = sampled * color * _bolMultipler;
}