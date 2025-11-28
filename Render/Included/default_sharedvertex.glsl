struct ModelMatrix {
    mat4 model;
    mat4 modelInverse;
    mat4 normal;
    mat4 previousModel;
};

Buffer(ModelMatrices, ModelMatrix, 0)

#template Vertex
#ifndef Vertex
#pragma message "Vertex not defined"
#ifdef CustomFragmentData
#error "CustomFragmentData but not Vertex defined"
#endif
void VertexShader(in MaterialData material, inout Vert v) {
}
#endif
