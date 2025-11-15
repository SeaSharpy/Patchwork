#include 2d_shared.glsl

layout(location = 0) in vec2 UV;
layout(location = 1) flat in int Instance;
layout(location = 2) in float FragDepth;

layout(location = 0) out float OutDepth;
layout(location = 1) out vec4 OutColor;

void main()
{
    SpriteData s  = Sprites[Instance];
    sampler2D tex = sampler2D(s.Texture);
    vec4 color    = texture(tex, UV);
    if (color.a < AlphaThresh)
        discard;
    OutDepth    = FragDepth;
    OutColor    = vec4(color.rgb, 1.0);
}