#include 2d_shared.glsl

layout(location = 0) in vec2 UV;
layout(location = 1) flat in int Instance;
layout(location = 2) in vec3 WorldPos;
layout(location = 0) out vec4 Colour;

const float BlackThresh = 1 / 65535;
const float WhiteThresh = 1 - BlackThresh;
const int MaxTotalSteps = 64;
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
    return false;
}


float ShadowTraceDDA(int layer, vec2 uvStart, vec2 uvEnd)
{
    vec2 dir = uvEnd - uvStart;
    if (dir.x == 0.0) dir.x = EPS;
    if (dir.y == 0.0) dir.y = EPS;
    float dirLen = length(dir);

    if (dirLen < EPS || any(lessThan(uvStart, vec2(0.0))) || any(greaterThan(uvStart, vec2(1.0))))
        return 1.0;

    vec2 rd = dir / dirLen;

    vec2 stepA = sign(dir);
    vec2 stepB = step(0.0, dir);
    int mip = MaxLightMip;
    float t = EPS;
    float lastT = 0.0;
    float totalWhiteUV = 0.0;
    bool inWhite;
    int i;
    for (i = 0; i < MaxTotalSteps; ++i)
    {
        vec2 point = uvStart + rd * t;
        if (t > dirLen || any(lessThan(point, vec2(0.0))) || any(greaterThan(point, vec2(1.0)))) break;
        inWhite = IsWhite(clamp(point, 0.0, 1.0), layer, mip);
        vec2 mipDimensions = textureSize(LightTexArray, mip).xy;
        vec2 cellSize = 1.0 / mipDimensions;
        ivec2 cell = ivec2(floor(point / cellSize));
        vec2 nextBoundary = (vec2(cell) + stepB) * cellSize;
        vec2 offset = nextBoundary - point;
        vec2 cellLocalUV = fract(point / cellSize);
        float moveX = offset.x / rd.x;
        float moveY = offset.y / rd.y;
        if (abs(moveX) < abs(moveY)) {
            t += max(moveX, EPS);
        } else {
            t += max(moveY, EPS);
        }
        float segDistUV = (t - lastT);
        lastT = t;

        if (inWhite)
        {
            totalWhiteUV += segDistUV;
            if (totalWhiteUV >= ShadowSoftness)
                break;
        }
    }
    return 1.0 - clamp(smoothstep(0.0, ShadowSoftness, totalWhiteUV), 0.0, 1.0);
}

void main()
{
    SpriteData s = Sprites[Instance];
    sampler2D tex = sampler2D(s.Texture);
    vec4 spriteColour = texture(tex, UV);
    if (spriteColour.a < AlphaThresh) discard;

    float depth = texture(DepthTex, gl_FragCoord.xy / vec2(ViewportSize)).r;
    float blur = texture(DepthBlurTex, gl_FragCoord.xy / vec2(ViewportSize)).r;
    float AO = 1.0 - max(depth - blur, 0.0);

    vec3 color = AOColor * pow(AO, AOStrength) * spriteColour.rgb;
    vec3 worldPos = vec3(WorldPos.xy, WorldPos.z * LightingDepthStrength);

    for (int i = 0; i < LightCount; ++i)
    {
        vec3 lp = vec3(Lights[i].Position, 0.0);
        float lightRadius = Lights[i].Radius;
        float dist = length(worldPos - lp);
        if (dist > lightRadius) continue;

        vec2 uvWorld = (Lights[i].Matrix * vec4(worldPos, 1.0)).xy * 0.5 + 0.5;
        vec2 uvLight = vec2(0.5, 0.5);

        float visibility = ShadowTraceDDA(i, uvWorld, uvLight);

        vec3 lightColor = Lights[i].Color.rgb;
        float falloff = 1.0 - smoothstep(0.0, lightRadius, dist);

        color += lightColor * spriteColour.rgb * falloff * visibility;
    }

    Colour = vec4(color, spriteColour.a);
}
