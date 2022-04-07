#version 460 core
in vec3 iPos;
in vec2 iTextureCoord;

out vec2 textureCoord;

uniform mat4 projection;
uniform mat4 view;
uniform mat4 model;
uniform vec3 textureScale;

mat3 scaleMatrix3x3(vec3 scale);

void main() {
    gl_Position = projection * view * model * vec4(iPos, 1.0f);
    textureCoord = (scaleMatrix3x3(textureScale) * vec3(iTextureCoord, 1)).xy;
}

mat3 scaleMatrix3x3(vec3 scale)
{
    return mat3
    (
        scale.x, 0, 0,
        0, scale.y, 0,
        0, 0, scale.z
    );
}
