#ifndef AAAA_BINDLESS_INCLUDED
#define AAAA_BINDLESS_INCLUDED

// Forces Shader Model 6.6
#pragma require Int64BufferAtomics

Texture2D GetBindlessTexture2D(const uint index)
{
    Texture2D texture = ResourceDescriptorHeap[index];
    return texture;
}

#endif // AAAA_BINDLESS_INCLUDED