#version 460 core
in vec3 iPos;

out vec3 textureCoord;

uniform mat4 projection;
uniform mat3 view;

mat4 transformedView;

void main() {
    transformedView = mat4(view);
    vec4 pos = projection * transformedView * vec4(iPos, 1.0f);
    // Reversed Z: depth 0 is the far plane, so the skybox sits behind everything.
    gl_Position = vec4(pos.xy, 0.0, pos.w);
    textureCoord = iPos;
}
