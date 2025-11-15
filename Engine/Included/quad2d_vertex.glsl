#include 2d_shared.glsl

layout(location = 0) in vec2 Position;
layout(location = 1) in vec2 UV;

layout(location = 0) out vec2 OutUV;

void main()
{
    gl_Position   = vec4(Position * 2.0, 0.0, 1.0);
    OutUV         = UV;
}