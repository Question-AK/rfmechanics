#version 330 core
layout(location = 0) in vec3 vertexPosition;
layout(location = 1) in vec2 uvIn;
layout(location = 2) in vec4 colorIn;
layout(location = 5) in int jointId;
layout(std140) uniform WatchAnimation { mat4 joints[230]; };
uniform mat4 projectionMatrix;
uniform mat4 viewMatrix;
uniform mat4 modelMatrix;
uniform int jointCount;
out vec2 uv;
out vec3 relativeWorld;
out float vertexAlpha;
void main() {
    int joint = clamp(jointId, 0, max(0, jointCount - 1));
    vec4 world = modelMatrix * joints[joint] * vec4(vertexPosition, 1.0);
    relativeWorld = world.xyz;
    uv = uvIn;
    vertexAlpha = colorIn.a;
    gl_Position = projectionMatrix * viewMatrix * world;
}
