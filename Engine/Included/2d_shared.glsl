uniform mat4 Projection;
uniform vec2 ViewportSize;
uniform int  SpriteCount;
uniform int  LightCount;
uniform sampler2DArray LightTexArray;
uniform sampler2D DepthTex;
uniform sampler2D DepthBlurTex;
uniform int LightTexSize;
uniform int MaxLightMip;
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
