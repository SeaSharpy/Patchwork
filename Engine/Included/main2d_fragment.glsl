#include 2d_shared.glsl

uniform float ShadowSoftness;
uniform int ShadowMode;
uniform int MaxLightMip;

layout(location = 0) in vec2 UV;
layout(location = 1) flat in int Instance;
layout(location = 2) in vec3 WorldPos;
layout(location = 0) out vec4 Colour;

const float BlackThresh = 1 / 65535;
const float WhiteThresh = 1 - BlackThresh;
const int MaxTotalSteps = 256;
const float EPS = 1e-4;
bool IsWhite(vec2 cell, int layer, inout int mip) {
    if (mip < MaxLightMip)
        mip += 1;
    for (; ; ) {
        if (mip < 0) {
            mip = 0;
            return true;
        }
        float v = textureLod(LightTexArray, vec3(cell, float(layer)), mip).r;
        if (v >= WhiteThresh) return true;
        if (v <= BlackThresh) return false;
        mip -= 1;
    }
    return true;
}

vec2 Dimensions(int mip) {
    return textureSize(LightTexArray, mip).xy;
}
float ShadowTraceDDA(int layer, vec2 uvStart, vec2 uvEnd)
{
    vec2 dir = uvEnd - uvStart;
    float dirLen = length(dir);

    if (dirLen < EPS || any(lessThan(uvStart, vec2(0.0))) || any(greaterThan(uvStart, vec2(1.0))))
        return 1.0;

    vec2 rd = dir / dirLen;

    vec2 stepA = sign(dir);
    vec2 stepB = step(0.0, stepA);
    int mip = MaxLightMip;
    float t = 0.0;
    float totalWhiteUV = 0.0;
    bool inWhite;
    bool outside = false;
    int i;
    for (i = 0; i < MaxTotalSteps; ++i)
    {
        vec2 point = uvStart + rd * t;
        if (t > dirLen || any(lessThan(point, vec2(0.0))) || any(greaterThan(point, vec2(1.0)))) break;
        inWhite = IsWhite(point, layer, mip);
        if (!inWhite) outside = true;
        else if (inWhite && ShadowMode == 1 && outside) return 0.0;
        vec2 mipDimensions = Dimensions(mip);
        vec2 cellSize = 1.0 / mipDimensions;
        ivec2 cell = ivec2(floor(point / cellSize));
        vec2 nextBoundary = (vec2(cell) + stepB) * cellSize;
        vec2 offset = nextBoundary - point;
        vec2 cellLocalUV = fract(point / cellSize);
        float moveX = offset.x / rd.x;
        float moveY = offset.y / rd.y;
        float newT = t;
        if (abs(moveX) < abs(moveY)) {
            newT += moveX + EPS;
        } else {
            newT += moveY + EPS;
        }
        if (ShadowMode == 0)
        {
            float segDistUV = max(newT - t, EPS);
            t = newT;
            totalWhiteUV += segDistUV * (inWhite ? 1.0 : 0.0);
            if (totalWhiteUV >= ShadowSoftness)
                break;
        }
        else t = newT;
    }
    return ShadowMode == 0 ? 1.0 - clamp(smoothstep(0.0, ShadowSoftness, totalWhiteUV), 0.0, 1.0) : 1.0;
}
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

        color += lightColor * spriteColour.rgb * pow(falloff, LightingFalloffStrength) * visibility;
    }

    Colour = vec4(color, spriteColour.a);
}
