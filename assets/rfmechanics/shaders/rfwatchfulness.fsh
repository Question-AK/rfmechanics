#version 330 core
in vec2 uv;
in vec4 color;
out vec4 outColor;
void main() {
    vec2 p = uv * 2.0 - 1.0;
    float a = 1.0 - smoothstep(0.2, 1.0, length((p - vec2(-0.22, 0.12)) / vec2(0.72, 0.26)));
    float b = 1.0 - smoothstep(0.1, 1.0, length((p - vec2(0.27, -0.17)) / vec2(0.50, 0.20)));
    float alpha = color.a * max(a, b * 0.8);
    if (alpha < 0.002) discard;
    outColor = vec4(color.rgb, alpha);
}
