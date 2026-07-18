shader_type canvas_item;
render_mode blend_mix;

#include "res://sts2decktracker/texture/_util/polar_coordinates.gdshaderinc"

uniform float animation_speed = 1.0;
uniform sampler2D ring_base_alpha;
uniform sampler2D thin_ring_animation;

varying vec4 vertex_color;

void vertex() {
	vertex_color = COLOR;
}

void fragment() {
	float frac = fract(TIME * animation_speed);
	vec2 polar_uv = polar_coordinates(UV, vec2(0.5, 0.5), 0.8 ,1.0);
	
	vec4 thin_ring_anim_tex = texture(thin_ring_animation, vec2(frac, frac));
	
	vec4 main_tex = texture(TEXTURE, UV);
	vec4 main_tex_polar = texture(TEXTURE, polar_uv * 0.5 + vec2(thin_ring_anim_tex.r, 0.0));
	
	vec4 ring_base_alpha_tex = texture(ring_base_alpha, vec2(frac));
	
	float ring_r_alpha = main_tex.r * ring_base_alpha_tex.r;
	float ring_g_alpha = main_tex.g * ring_base_alpha_tex.g;
	float ring_b_alpha = main_tex.b * ring_base_alpha_tex.b;
	
	float final_alpha = max(ring_r_alpha, ring_g_alpha);
	final_alpha = max(final_alpha, ring_b_alpha);
	final_alpha = max(final_alpha, main_tex_polar.a * thin_ring_anim_tex.g);
	
	COLOR = vec4(vertex_color.rgb, final_alpha);
}