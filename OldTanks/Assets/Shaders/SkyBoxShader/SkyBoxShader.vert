#version 460 core
in vec3 iPos;

out vec3 textureCoord;

uniform mat4 projection;
uniform mat3 view;
//uniform mat4 model;

mat4 transformedView;

void main() {
    transformedView = mat4(view);
    vec4 pos = projection * transformedView * vec4(iPos, 1.0f);
    gl_Position = pos.xyww;
    textureCoord = iPos;
}
