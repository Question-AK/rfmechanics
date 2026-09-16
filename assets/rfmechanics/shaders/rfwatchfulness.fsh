#version 330 core
in vec2 uv;
in vec4 color;
out vec4 outColor;
void main() {
    vec2 p = uv * 2.0 - 1.0;
    // Broad soft flicker, closer to the first prototype. Slightly uneven oval
    // rather than a star, sparkling point or thin subpixel streak at distance.
    p.y += 0.07 * sin(p.x * 4.0);
    float radius = length(p * vec2(1.0, 1.25));
    float flicker = 1.0 - smoothstep(0.12, 0.95, radius);
    float alpha = color.a * flicker;
    if (alpha < 0.002) discard;
    outColor = vec4(color.rgb, alpha);
}
