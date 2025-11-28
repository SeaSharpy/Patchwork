#include 2d_shared.glsl

layout(location = 0) in vec2 Position;
layout(location = 1) in vec2 UV;

layout(location = 0) out vec2 OutUV;
layout(location = 1) flat out int Instance;

void main()
{
    SpriteData s  = Sprites[gl_InstanceID];
    vec4 worldPos = s.Transform * vec4(Position, 0.0, 1.0);
    gl_Position   = vec4((Projection * worldPos).xy, clamp(s.Depth, 0.0, 1.0), 1.0);
    OutUV         = UV;
    Instance      = gl_InstanceID;
}