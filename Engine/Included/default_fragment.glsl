layout(location = 0) in VertO {
    vec3 position;
    vec3 geoNormal;
    vec4 tangent;
    vec2 uv;
    vec4 color;
} VSInBuiltin;

layout(location = 5) flat in int InstanceIndex;

layout(location = 6) in vec4 vCurrClip;
layout(location = 7) in vec4 vPrevClip;
#disallow vCurrClip
#disallow vPrevClip

layout(location = 0) out vec4 OutColor;
layout(location = 1) out vec4 OutNormal;
layout(location = 2) out vec4 OutGeoNormal;
layout(location = 3) out uint OutMaterialId;
layout(location = 4) out vec2 MotionVectors;
#disallow OutColor
#disallow OutNormal
#disallow OutGeoNormal
#disallow OutMaterialId

#include default_shared.glsl
#include default_sharednoray.glsl
#include default_sharedfragment.glsl

void main()
{
    MaterialData material = Materials[InstanceIndex];
    if (FragmentDiscard(material, VSInBuiltin.uv))
        discard;
    if (DepthOnly == 1)
        return;
    Vert VSInBuiltinVert = Vert(
        VSInBuiltin.position,
        VSInBuiltin.geoNormal,
        VSInBuiltin.tangent,
        VSInBuiltin.uv,
        VSInBuiltin.color
    );
    vec2 currNDC = vCurrClip.xy / vCurrClip.w;
    vec2 prevNDC = vPrevClip.xy / vPrevClip.w;
    vec2 currUV = currNDC * 0.5 + 0.5;
    vec2 prevUV = prevNDC * 0.5 + 0.5;
    vec2 velUV = currUV - prevUV;
    MotionVectors = velUV;
    vec3 normal = VSInBuiltinVert.normal;
    vec3 shadedColor = FragmentShader(material, VSInBuiltinVert, normal);
    OutColor = vec4(shadedColor, 1.0);
    OutNormal = vec4(normalize(normal), 0.0);
    OutGeoNormal = vec4(normalize(VSInBuiltinVert.normal), 0.0);
    OutMaterialId = uint(InstanceIndex);
}
