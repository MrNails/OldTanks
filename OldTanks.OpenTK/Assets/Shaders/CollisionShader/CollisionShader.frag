#version 460 core

layout(location = 0) out vec4 diffuseColor;
uniform vec4 color;

void main()
{
    diffuseColor = color;
}