shader_type canvas_item;
render_mode blend_mix;

#include "res://sts2decktracker/texture/_util/tiling_and_offset.gdshaderinc"
#include "res://sts2decktracker/texture/_util/erosion_from_factors.gdshaderinc"
#include "res://sts2decktracker/texture/_util/rotate.gdshaderinc"

uniform vec4 main_st;
uniform vec4 secondary_st;

uniform float secondary_smoke_influence : hint_range(0.0, 1.0, 0.1);
uniform vec2 erosion_factors;

uniform sampler2D lut : repeat_disable;

uniform sampler2D mask : repeat_disable;

varying vec4 vertex_color;

void vertex() {
	vertex_color = COLOR;
}

void fragment() {
	vec2 rotated_uv = rotate(UV, vec2(0.5, 0.5), PI);
	vec2 main_uv = tiling_and_offset(rotated_uv, vec4(main_st.xyz, 0.0), vec2(0.0, main_st.w));
	vec2 secondary_uv = tiling_and_offset(rotated_uv, vec4(secondary_st.xyz, 0.0), vec2(0.0, secondary_st.w));
	
	vec4 main_tex = texture(TEXTURE, main_uv);
	vec4 secondary_tex = texture(TEXTURE, secondary_uv);
	
	vec4 mask_tex = texture(mask, UV);
	
	float smoke_alpha = mix(main_tex.r, secondary_tex.r, secondary_smoke_influence) * mask_tex.r;
	smoke_alpha = erosion_from_factors(erosion_factors, smoke_alpha);
	
	vec4 lut_tex = texture(lut, vec2(smoke_alpha));
	
	COLOR = vec4(vertex_color.rgb * lut_tex.rgb, smoke_alpha * vertex_color.a + mask_tex.g);
}