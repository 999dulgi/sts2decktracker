shader_type canvas_item;
render_mode blend_mix;

#include "res://sts2decktracker/texture/_util/polar_coordinates.gdshaderinc"
#include "res://sts2decktracker/texture/_util/tiling_and_offset.gdshaderinc"
#include "res://sts2decktracker/texture/_util/erosion_from_factors.gdshaderinc"

uniform vec4 noise_st = vec4(1.0, 1.0, 0.0, 0.0);
uniform vec2 noise_smoothstep = vec2(0.0, 1.0);

// UV coordinate that represents the original (un-split) card's center.
// For a texture split into corner pieces, this should point at wherever the
// full-card center falls in *this piece's* local UV space (can be outside 0..1).
uniform vec2 polar_center = vec2(0.5, 0.5);

uniform vec4 main_color : source_color;
uniform vec4 bright_color : source_color;
uniform vec4 border_color : source_color;

varying vec4 vertex_color;

void vertex()
{	
	vertex_color = COLOR;
}

void fragment() 
{
	vec2 polar_uv = polar_coordinates(UV, polar_center, 1.0, 1.0);
	polar_uv = tiling_and_offset(polar_uv, noise_st, vec2(0.0));
	
	vec4 noise_tex = texture(TEXTURE, polar_uv);
	float noise = erosion_from_factors(noise_smoothstep, noise_tex.b);
	
	vec4 main_tex = texture(TEXTURE, UV);
	
	vec4 inner_color = mix(main_color, bright_color, noise);
	vec4 color_to_use = mix(border_color, inner_color, main_tex.r);
	
	COLOR = vec4(color_to_use.rgb * vertex_color.rgb, main_tex.a * vertex_color.a);
}