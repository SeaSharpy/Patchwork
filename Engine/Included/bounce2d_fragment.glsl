#include 2d_shared.glsl

layout(location = 0) in vec2 UV;
layout(location = 0) out vec4 Color;

void main()
{
	vec4 center = texture(ShadedTex, UV);
	vec3 centerAlbedo = texture(DepthColorTex, UV).rgb;

    int mipLevel = max(0, ShadedTexMaxMip - GIMip);
    ivec2 sz = textureSize(ShadedTex, mipLevel);
    vec2 texel = 1.0 / vec2(sz);

    vec3 accum = vec3(0.0);
    int total = 0;

    for (int y = 0; y < sz.y; ++y)
    for (int x = 0; x < sz.x; ++x)
    {
        if (x == 0 && y == 0) continue;

        vec2 uv = clamp(vec2(x, y) * texel, 0.0, 1.0);
        float dist = length(vec2(x, y) - (UV * vec2(sz))) / length(vec2(sz));
        total++;
        float distanceWeight = 1.0 / (1.0 + dist * GIDistanceStrength);
        if (distanceWeight > 0.01)
            accum += (texelFetch(ShadedTex, ivec2(x, y), mipLevel).rgb * distanceWeight);
    }

    vec3 bounced = (total > 0) ? accum / float(total) : vec3(0.0);
    float ao = AO();

    vec3 outRgb = center.rgb + bounced * ao * centerAlbedo * GIStrength;
    Color = vec4(outRgb, center.a);
}