#version 330 core
uniform sampler2D entityTex;
uniform vec3 clipMin;
uniform vec3 clipMax;
uniform float glimpseAlpha;
in vec2 uv;
in vec3 relativeWorld;
in float vertexAlpha;
out vec4 outColor;
void main() {
    // Geometry outside the CPU-checked viewing volume can never contribute a pixel.
    if (any(lessThan(relativeWorld, clipMin)) || any(greaterThan(relativeWorld, clipMax))) discard;
    float textureAlpha = texture(entityTex, uv).a * vertexAlpha;
    if (textureAlpha < 0.1) discard;
    vec3 p = (relativeWorld - clipMin) / max(clipMax - clipMin, vec3(0.001));
    // Broad interrupted bands keep the actual animated profile recognisable but incomplete.
    float band = sin(p.y * 19.0 + p.x * 4.0 + p.z * 2.0);
    float broken = smoothstep(-0.3, 0.25, band);
    float edge = smoothstep(0.0, 0.08, p.y) * (1.0 - smoothstep(0.86, 1.0, p.y));
    float a = glimpseAlpha * textureAlpha * broken * edge;
    if (a < 0.004) discard;
    outColor = vec4(0.70, 0.86, 0.91, a);
}
