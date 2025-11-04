#version 460 core
#extension GL_ARB_gpu_shader_int64 : enable
#extension GL_ARB_bindless_texture : enable
#extension GL_ARB_shader_draw_parameters : enable
#ifdef GL_NV_gpu_shader5
#extension GL_NV_gpu_shader5 : enable
#endif
#ifdef GL_EXT_nonuniform_qualifier
#extension GL_EXT_nonuniform_qualifier : enable
#endif
const float PI = 3.14159265358979323846264338327950288419716939937510;

#define Buffer(Name, Type, Binding) \
layout(std430, binding = Binding) readonly buffer Name##Buffer { \
    Type Name[]; \
};

#define BufferWriteable(Name, Type, Binding, Modifier) \
layout(std430, binding = Binding) Modifier buffer Name##Buffer { \
    Type Name[]; \
};
