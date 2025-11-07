#include 2d_shared.glsl

layout(location   = 0) in vec2 UV;
layout(location   = 1) flat in int Instance;
layout(location   = 2) in vec3 WorldPos;
layout(location   = 0) out vec4 Colour;

void main()
{
    SpriteData s  = Sprites[Instance];
    sampler2D tex = sampler2D(s.Texture);
    vec4 color    = texture(tex, UV);
    if (color.a < 0.5)
        discard;
    float depth   = texture(DepthTex,     gl_FragCoord.xy / vec2(ViewportSize)).r;
    float blur    = texture(DepthBlurTex, gl_FragCoord.xy / vec2(ViewportSize)).r;
    float AO = 1.0 - max(depth - blur, 0.0);
    vec3 ambient = vec3(0.5);
    Colour += vec4(ambient * AO * color.rgb, 1.0);
    for (int i = 0; i < LightCount; ++i)
    {
        vec3 pos = vec3(Lights[i].Position, 0.0);
        vec3 lightColor = Lights[i].Color.rgb;
        float lightRadius = Lights[i].Radius;
        float dist = length(WorldPos - pos);
        if (dist > lightRadius)
            continue;
        float falloff = 1.0 - smoothstep(0.0, lightRadius, dist);
        Colour.rgb += falloff * lightColor * color.rgb;
    }
    Colour.a = color.a;
}
