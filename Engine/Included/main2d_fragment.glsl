#include 2d_shared.glsl

layout(location = 0) in vec2 UV;
layout(location = 1) flat in int Instance;
layout(location = 2) in vec3 WorldPos;
layout(location = 0) out vec4 Colour;

const float WhiteThresh = 0.99;
const float BlackThresh = 0.01;
const int MaxTotalSteps = 1024;
bool isWhite(vec2 cell, int layer, inout int mip) {
    if (mip < MaxLightMip)
        mip += 1;
    for (; ; ) {
        if (mip < 0) {
            mip = 0;
            return false;
        }
        float v = textureLod(LightTexArray, vec3(cell, float(layer)), mip).r;
        if (v >= WhiteThresh) return true;
        if (v <= BlackThresh) return false;
        mip -= 1;
    }
    return false;
}
float RayStepLength(vec2 dir, float move, bool fromX)
{
    // dir is normalized
    // move is the distance along one axis (positive)
    // fromX = true if 'move' is an X movement, false if Y
    if (fromX)
        return abs(move / max(abs(dir.x), 1e-8));
    else
        return abs(move / max(abs(dir.y), 1e-8));
}

vec3 ShadowTraceDDA(int layer, vec2 uvStart, vec2 uvEnd)
{
    const float SoftTexels = 20.0;
    const float WhiteThreshold = 0.5;

    vec2 dir = uvEnd - uvStart;
    float dirLen = length(dir);

    if (dirLen < 1e-6 || any(lessThan(uvStart, vec2(0.0))) || any(greaterThan(uvStart, vec2(1.0))))
        return vec3(1.0);

    vec2 rd = dir / dirLen;
    vec2 dimensions = vec2(float(LightTexSize));

    const float SoftUV = 0.1;

    vec2 step = sign(dir);
    float mip = MaxLightMip;
    float t = 1e-5;
    float lastT = 0.0;
    float totalWhiteUV = 0.0;
    bool inWhite;
    for (int i = 0; i < MaxTotalSteps; ++i)
    {
        vec2 point = uvStart + rd * t;
        if (t > dirLen || any(lessThan(point, vec2(0.0))) || any(greaterThan(point, vec2(1.0)))) break;
        inWhite = isWhite(clamp(point, 0.0, 1.0), layer, mip);
        vec2 mipDimensions = vec2(max(1.0, dimensions.x / exp2(float(mip))),
                max(1.0, dimensions.y / exp2(float(mip))));
        vec2 cellSize = 1.0 / mipDimensions;
        ivec2 cell = ivec2(floor(point / cellSize));
        vec2 nextBoundary = (cell + step) * cellSize;
        vec2 cellLocalUV = fract(point / cellSize);
        if (cellLocalUV.x < cellLocalUV.y) {
            return vec3(inWhite ? 1.0 : 0.0);
        }
        else {
            return vec3(inWhite ? 0.0 : 1.0);
        }
        float segDistUV = (t - lastT);
        lastT = t;

        if (inWhite)
        {
            totalWhiteUV += segDistUV;
            if (totalWhiteUV >= SoftUV)
                return vec3(0.0);
        }
    }

    return vec3(1.0 - smoothstep(0.0, SoftUV, totalWhiteUV));
}

void main()
{
    SpriteData s = Sprites[Instance];
    sampler2D tex = sampler2D(s.Texture);
    vec4 spriteColour = texture(tex, UV);
    if (spriteColour.a < 0.5) discard;

    float depth = texture(DepthTex, gl_FragCoord.xy / vec2(ViewportSize)).r;
    float blur = texture(DepthBlurTex, gl_FragCoord.xy / vec2(ViewportSize)).r;
    float AO = 1.0 - max(depth - blur, 0.0);

    vec3 ambient = vec3(0.1);
    vec3 color = ambient * AO * spriteColour.rgb;

    for (int i = 0; i < LightCount; ++i)
    {
        vec3 lp = vec3(Lights[i].Position, 0.0);
        float lightRadius = Lights[i].Radius;
        float dist = length(WorldPos - lp);
        if (dist > lightRadius) continue;

        vec2 uvWorld = (Lights[i].Matrix * vec4(WorldPos, 1.0)).xy * 0.5 + 0.5;
        vec2 uvLight = vec2(0.5, 0.5);

        vec3 visibility = ShadowTraceDDA(i, uvWorld, uvLight);
        Colour = vec4(visibility, spriteColour.a);
        return;

        vec3 lightColor = Lights[i].Color.rgb;
        float falloff = 1.0 - smoothstep(0.0, lightRadius, dist);

        color += lightColor * spriteColour.rgb * falloff * visibility;
    }

    Colour = vec4(color, spriteColour.a);
}
