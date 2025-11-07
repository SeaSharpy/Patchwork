uniform mat4 Projection;
uniform mat4 View;
uniform vec2 ViewportSize;
uniform int  SpriteCount;
uniform int  LightCount;
uniform sampler2DArray LightTexArray;
uniform sampler2D DepthTex;
uniform sampler2D DepthBlurTex;
struct SpriteData {
    mat4 Transform;
    uint64_t Texture;
    float Depth;
    float Extra;
};

struct LightData {
    vec4 Color;
    vec2 Position;
    float Radius;
    float Extra;
};

Buffer(Sprites, SpriteData, 0);
Buffer(Lights, LightData, 1);
