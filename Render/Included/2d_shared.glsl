uniform mat4 Projection;
uniform mat4 InverseProjection;
uniform vec2 ViewportSize;
uniform int  SpriteCount;
uniform int  LightCount;
uniform sampler2DArray LightTexArray;
uniform sampler2D DepthTex;
uniform sampler2D DepthBlurTex;
uniform sampler2D DepthColorTex; 
uniform sampler2D TonemapTex;
uniform sampler2D ScreenLightTex;
uniform sampler2D ShadedTex;
uniform int ShadedTexMaxMip;
uniform float Exposure;
uniform float LightingDepthStrength;
uniform float AOStrength;
uniform float GIStrength;
uniform float GIDistanceStrength;
uniform float LightingFalloffStrength;
uniform int GIMip;
struct SpriteData {
    mat4 Transform;
    uint64_t Texture;
    float Depth;
    float Extra;
};

struct LightData {
    mat4 Matrix;
    vec4 Color;
    vec2 Position;
    float Radius;
    float Extra;
};

Buffer(Sprites, SpriteData, 0);
Buffer(Lights, LightData, 1);

const float AlphaThresh = 1 / 255;

#ifdef FRAGMENT
float AO() {
    float depth = texture(DepthTex, gl_FragCoord.xy / vec2(ViewportSize)).r;
    float blur = texture(DepthBlurTex, gl_FragCoord.xy / vec2(ViewportSize)).r;
    float AO = pow(1.0 - max(depth - blur, 0.0), AOStrength);
    return AO;
}
#endif