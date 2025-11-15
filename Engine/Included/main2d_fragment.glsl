#include 2d_shared.glsl

uniform float ShadowSoftness;
uniform int MaxLightMip;

layout(location = 0) in vec2 UV;
layout(location = 1) flat in int Instance;
layout(location = 2) in vec3 WorldPos;
layout(location = 0) out vec4 Colour;

const float BlackThresh = 1 / 65535;
const float WhiteThresh = 1 - BlackThresh;
const int MaxTotalSteps = 256;
const float EPS = 1e-4;
float IsWhite(vec2 cell, int layer, inout int mip) {
    if (mip < MaxLightMip)
        mip += 1;
    for (; ;) {
        if (mip < 0) {
            mip = 0;
            return 10000.0;
        }
        float v = textureLod(LightTexArray, vec3(cell, float(layer)), mip).r;
        if (v >= WhiteThresh) return 1.0;
        if (v <= BlackThresh) return 0.0;
        mip -= 1;
    }
    return 10000.0;
}

vec2 Dimensions(int mip) {
    return textureSize(LightTexArray, mip).xy;
}
#include trace_2d.glsl

void main()
{
    SpriteData s = Sprites[Instance];
    sampler2D tex = sampler2D(s.Texture);
    vec4 spriteColour = texture(tex, UV);
    if (spriteColour.a < AlphaThresh) discard;

    vec3 color = vec3(0.0);
    for (int i = 0; i < LightCount; ++i)
    {
        vec3 lp = vec3(Lights[i].Position, 0.0);
        float lightRadius = Lights[i].Radius;
        vec3 localWorldPos = vec3(WorldPos.xy, WorldPos.z * lightRadius * LightingDepthStrength);
        float dist = length(localWorldPos - lp);
        if (dist > lightRadius) continue;

        vec2 uvWorld = (Lights[i].Matrix * vec4(localWorldPos, 1.0)).xy * 0.5 + 0.5;
        vec2 uvLight = vec2(0.5, 0.5);

        float visibility = ShadowTraceDDA(i, uvWorld, uvLight);

        vec3 lightColor = Lights[i].Color.rgb;
        float falloff = 1.0 - smoothstep(0.0, lightRadius, dist);

        color += lightColor * spriteColour.rgb * falloff * visibility;
    }

    Colour = vec4(color, spriteColour.a);
}
