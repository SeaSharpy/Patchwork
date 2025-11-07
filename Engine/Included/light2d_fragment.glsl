#include 2d_shared.glsl

layout(location   = 0) in vec2 UV;
layout(location   = 1) flat in int Instance;
layout(location   = 0) out float OutFlag;

void main()
{
    SpriteData s  = Sprites[Instance];
    sampler2D tex = sampler2D(s.Texture);
    vec4 color    = texture(tex, UV);
    if (color.a < 0.5)
        discard;
    OutFlag = 1.0;
}
