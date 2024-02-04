#version 460 core
layout(location = 0) in vec3 iPos;
layout(location = 1) in vec3 iNormal;
layout(location = 2) in vec2 iTextureCoord;
layout(location = 3) in mat4 iModel;
layout(location = 7) in vec4 iColor;
layout(location = 8) in vec4 iTextureTransform;
layout(location = 9) in float iHasTexture;

uniform mat4 projection;
uniform mat4 view;

out vec2 textureCoord;
out vec4 instanceColor;
out float hasTexture;

void main() {
    gl_Position = projection * view * iModel * vec4(iPos, 1.0f);
    textureCoord = mat2(iTextureTransform) * iTextureCoord;
    instanceColor = iColor;
    hasTexture = iHasTexture;
}
