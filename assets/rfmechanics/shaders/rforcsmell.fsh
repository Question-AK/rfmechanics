#version 330 core
in vec2 uv;
in vec4 color;
out vec4 outColor;
void main() {
    int category = int(floor(uv.x / 2.0));
    vec2 p = vec2(uv.x - float(category)*2.0, uv.y)*2.0-1.0;
    float mask;
    if (category == 5) { // Fresh blood: narrow, pointed droplet.
        mask = 1.0-smoothstep(0.55, 1.0, abs(p.x)*2.0+abs(p.y+0.12));
    } else if (category == 2) { // Predator: paired sharp slashes.
        float slashes = min(abs(p.x-p.y*0.35-0.3), abs(p.x-p.y*0.35+0.3));
        mask = (1.0-smoothstep(0.06,0.22,slashes))*(1.0-smoothstep(0.5,0.95,abs(p.y)));
    } else if (category == 4) { // Player: open ring.
        mask = (1.0-smoothstep(0.10,0.25,abs(length(p)-0.55)));
    } else if (category == 3) { // Omnivore: two rounded lobes.
        float d = min(length(p-vec2(0.32,0.2)),length(p+vec2(0.32,0.2)));
        mask = 1.0-smoothstep(0.12,0.55,d);
    } else if (category == 1) { // Herbivore: broad soft oval.
        mask = 1.0-smoothstep(0.2,1.0,length(p*vec2(0.85,1.5)));
    } else {
        mask = 1.0-smoothstep(0.15,1.0,length(p));
    }
    float alpha = color.a*mask;
    if (alpha < 0.002) discard;
    outColor = vec4(color.rgb,alpha);
}
