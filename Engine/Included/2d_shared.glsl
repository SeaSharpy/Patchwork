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
    vec2 Position;
    vec4 Color;
    float Radius;
};

Buffer(Sprites, SpriteData, 0);
Buffer(Lights, LightData, 1);
