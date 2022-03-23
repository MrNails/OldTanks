#version 460 core
in vec3 iPos;
in vec2 iTexture;

out vec2 texture;

//uniform int useBillboardView;
uniform mat4 model;
uniform mat4 projection;
//uniform mat4 view;
//uniform vec4 targetPos;

void main() {
//    gl_Position = projection * vec4(iPos, 1.0);
    vec4 coord = vec4(iPos, 1.0);
    
//    if (useBillboardView == 0)
        gl_Position = projection * model * coord;
//    else
//        gl_Position = projection * createBillboardMatrix() * coord;
    
    texture = iTexture;
}

//mat4 createBillboardMatrix()
//{
//    mat4 invertedMat = inverse(view);
//    
//    mat4 result = mat4
//    (
//        view[0][0], view[0][1], view[0][2], 0,
//        view[1][0], view[1][1], view[1][2], 0,
//        invertedMat[3][0], invertedMat[3][1], invertedMat[3][2], 0,
//        targetPos.x, targetPos.y, targetPos.z, 1
//    );
//    
//    return result;
//}
