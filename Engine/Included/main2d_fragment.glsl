#include 2d_shared.glsl

layout(location = 0) in vec2 UV;
layout(location = 1) flat in int Instance;
layout(location = 2) in vec3 WorldPos;
layout(location = 0) out vec4 Colour;

const int MaxTotalSteps = 1024;
float ShadowTraceDDA(int layer, vec2 uvStart, vec2 uvEnd)
{
    const float WhiteThresh     = 0.999;
    const float SoftTexels      = 20;

    float sizeF = float(LightTexSize);
    ivec2 dims  = ivec2(LightTexSize);

    vec2 origin = uvStart * sizeF;
    vec2 endPos = uvEnd * sizeF;
    vec2 dir    = endPos - origin;
    float dirLen = length(dir);

    // Degenerate ray: just test the starting texel
    if (all(lessThan(abs(dir), vec2(1e-6)))) {
        ivec2 c0 = clamp(ivec2(floor(origin)), ivec2(0), dims - 1);
        float r0 = texelFetch(LightTexArray, ivec3(c0, layer), 0).r;
        float totalWhite = (r0 >= WhiteThresh) ? SoftTexels : 0.0;
        return 1.0 - smoothstep(0.0, SoftTexels, totalWhite);
    }

    ivec2 cell = ivec2(floor(origin));
    if (any(lessThan(cell, ivec2(0))) || any(greaterThanEqual(cell, dims))) {
        // Outside the light mask counts as not occluded
        return 1.0;
    }

    vec2 step = vec2(sign(dir));

    vec2 nextBoundary = vec2(
        (step.x > 0.0) ? float(cell.x + 1) : float(cell.x),
        (step.y > 0.0) ? float(cell.y + 1) : float(cell.y)
    );

    vec2 tMax = vec2(
        (dir.x != 0.0) ? (nextBoundary.x - origin.x) / dir.x : 1e30,
        (dir.y != 0.0) ? (nextBoundary.y - origin.y) / dir.y : 1e30
    );

    vec2 tDelta = vec2(
        (dir.x != 0.0) ? step.x / dir.x : 1e30,
        (dir.y != 0.0) ? step.y / dir.y : 1e30
    );

    bool  inWhite        = texelFetch(LightTexArray, ivec3(cell, layer), 0).r >= WhiteThresh;
    float t              = 0.0;
    float lastT          = 0.0;
    float totalWhiteDist = 0.0;

    // March along the ray, summing distances spent inside white cells
    for (int i = 0; i < MaxTotalSteps; ++i) {
        bool stepX = tMax.x < tMax.y;
        if (stepX) {
            t = tMax.x;
            tMax.x += tDelta.x;
            cell.x += int(step.x);
        } else {
            t = tMax.y;
            tMax.y += tDelta.y;
            cell.y += int(step.y);
        }

        float segDist = (t - lastT) * dirLen;
        lastT = t;

        if (inWhite) {
            totalWhiteDist += segDist;
            // Early out once we exceed the soft threshold
            if (totalWhiteDist >= SoftTexels) {
                return 0.0;
            }
        }

        // Termination: reached end of segment or left the mask
        if (t > 1.0 || any(lessThan(cell, ivec2(0))) || any(greaterThanEqual(cell, dims))) {
            float shade = 1.0 - smoothstep(0.0, SoftTexels, totalWhiteDist);
            return shade;
        }

        // Fetch next cell state and continue
        inWhite = texelFetch(LightTexArray, ivec3(cell, layer), 0).r >= WhiteThresh;
    }

    // Safety exit for very long rays
    return 1.0 - smoothstep(0.0, SoftTexels, totalWhiteDist);
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

        float visibility = ShadowTraceDDA(i, uvWorld, uvLight);

        vec3 lightColor = Lights[i].Color.rgb;
        float falloff = 1.0 - smoothstep(0.0, lightRadius, dist);

        color += lightColor * spriteColour.rgb * falloff * visibility;
    }

    Colour = vec4(color, spriteColour.a);
}
