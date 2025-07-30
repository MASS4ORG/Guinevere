# Vulkan Shaders for Guinevere

This directory contains the GLSL shader sources for the Vulkan integration of Guinevere.

> NOTE: This would require distributing the compiled `.spv` files with your application.

## Shader Files

- `shader.vert` - Vertex shader that passes through position and texture coordinates
- `shader.frag` - Fragment shader that samples from a texture

## Compiling Shaders

To use these shaders with Vulkan, they need to be compiled to SPIR-V bytecode.

You can compile them using the Vulkan SDK's `glslc` compiler:

```bash
# Compile vertex shader
glslc shader.vert -o vert.spv

# Compile fragment shader
glslc shader.frag -o frag.spv
```

Alternatively, you can use `glslangValidator`:

```bash
# Compile vertex shader
glslangValidator -V shader.vert -o vert.spv

# Compile fragment shader
glslangValidator -V shader.frag -o frag.spv
```
