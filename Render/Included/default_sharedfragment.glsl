#template Fragment
#template FragmentCutout

#ifndef Fragment
#pragma message "Fragment not defined"
#ifdef CustomFragmentData
#error "CustomFragmentData but not Fragment defined"
#endif
vec3 FragmentShader(in MaterialData material, in Vert v, inout vec3 normal) {
    return v.color.rgb;
}
#endif

#ifndef FragmentCutout
bool FragmentDiscard(in MaterialData material, in vec2 uv) {
    return false;
}
#endif

