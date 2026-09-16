#version 330 core
in vec2 uv;
in vec4 color;
out vec4 outColor;
void main() {
    vec2 p = uv * 2.0 - 1.0;
    // Two uneven, feathered fragments of a shallow arc. No radial/luminous core.
    float curve = 0.18 * sin(p.x * 3.4) + 0.12 * p.x;
    float streak = 1.0 - smoothstep(0.035, 0.16, abs(p.y - curve));
    float ends = 1.0 - smoothstep(0.42, 0.92, abs(p.x));
    float gap = smoothstep(0.025, 0.14, abs(p.x - 0.18));
    float irregular = 0.65 + 0.35 * sin(p.x * 11.0 + 1.0);
    float alpha = color.a * streak * ends * gap * irregular;
    if (alpha < 0.002) discard;
    outColor = vec4(color.rgb, alpha);
}
