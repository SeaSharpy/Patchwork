#include 2d_shared.glsl
const float Quantize = 1.0 / 128.0;
const float HalfQuantize = Quantize / 2.0;
layout(location = 0) in vec2 OutUV;
layout(location = 0) out vec4 Color;

float noise(vec2 coord)
{
    return fract(dot(floor(coord), vec2(0.06711056, 0.00583715)) * 52.9829189);
}

vec3 aces(vec3 x)
{
	return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0);
}

void main()
{
	vec4 s = texture(TonemapTex, OutUV);
	vec3 mapped = aces(s.rgb * Exposure);
	mapped += noise(gl_FragCoord.xy) * Quantize - HalfQuantize;
	Color = vec4(mapped, s.a);
}
