shader_type canvas_item;
render_mode blend_mix;

#include "res://sts2decktracker/texture/_util/rotate.gdshaderinc"
#include "res://sts2decktracker/texture/_util/polar_coordinates.gdshaderinc"
#include "res://sts2decktracker/texture/_util/tiling_and_offset.gdshaderinc"
#include "res://sts2decktracker/texture/_util/erosion_from_factors.gdshaderinc"

uniform vec4 blink_st;
uniform vec2 blink_smoothstep;

uniform sampler2D lut : repeat_disable;

uniform vec4 distortion_st;
uniform float distortion_offset;
uniform float distortion_intensity;

uniform vec2 smoothstep_factors;

varying vec4 vertex_color;

void vertex() {
	vertex_color = COLOR;
}

void fragment() {
	vec2 blink_uv = tiling_and_offset(UV, blink_st, vec2(0.0));
	
	vec2 polar_uv = polar_coordinates(UV, vec2(0.5, 0.5), 1.0, 1.0);
	polar_uv = tiling_and_offset(polar_uv, distortion_st, vec2(0.0));
	
	vec4 distortion_tex = texture(TEXTURE, polar_uv);
	vec2 distortion = UV + vec2((distortion_tex.b - 0.5) * 2.0 * distortion_offset) - vec2(0.5, 0.5);
	distortion *= distortion_tex.b;
	distortion *= -distortion_intensity;
	
	float blink = texture(TEXTURE, vec2(blink_uv.r, 0.0)).b;
	blink = erosion_from_factors(blink_smoothstep, blink);
	
	vec4 main_tex_distorted = texture(TEXTURE, UV + distortion);
	vec4 main_tex = texture(TEXTURE, UV);
	
	float lines = main_tex_distorted.r;
	
	float mask = erosion_from_factors(smoothstep_factors, main_tex.a);
	
	vec4 lut_tex = texture(lut, vec2(blink, 0.0));
	
	float glow = main_tex_distorted.g * lut_tex.a * blink;
	
	COLOR = vec4(lut_tex.rgb * vertex_color.rgb, vertex_color.a * max(lines, glow) * mask);
	//COLOR = vec4(vec3(blink), 1.0);
}