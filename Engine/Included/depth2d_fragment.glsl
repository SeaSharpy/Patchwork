#include 2d_shared.glsl

layout(location = 0) in float FragDepth;
layout(location = 0) out float OutDepth;

void main()
{
    OutDepth    = FragDepth;
}