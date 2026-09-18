#version 330 core

layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aUV;
out vec2 uv;

void main() {
    uv = vec2(aUV.x, 1.0 - aUV.y);
    gl_Position = vec4(aPos, 0.0, 1.0);
}
