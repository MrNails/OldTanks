#version 460 core
in vec3 iPos;
in vec2 iTextureCoord;

out vec2 textureCoord;

uniform mat4 projection;
uniform mat4 view;
uniform mat4 model;
uniform mat2 textureTransform;

mat3 scaleMatrix3x3(vec3 scale);

void main() {
    gl_Position = projection * view * model * vec4(iPos, 1.0f);
    textureCoord = textureTransform * iTextureCoord;
}
