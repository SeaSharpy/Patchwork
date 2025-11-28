#include uniforms.glsl
#disallow out
#disallow in
#disallow uniform
#disallow buffer
#disallow Buffer
#disallow VSOutBuiltin
#disallow VSInBuiltin
#disallow gl_Position

#template Material

#ifndef Material
#error "Material not defined"
#endif

Buffer(Materials, MaterialData, 1)

struct Vert {
    vec3 position;
    vec3 normal;
    vec4 tangent;
    vec2 uv;
    vec4 color;
};

