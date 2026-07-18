shader_type canvas_item;
render_mode blend_mix;

#include "res://sts2decktracker/texture/_util/rotate.gdshaderinc"

uniform vec2 pivot_offset = vec2(0.0, 0.0);
uniform vec2 rotation_offset = vec2(0.0, 0.0);

uniform vec4 rotation_st;

uniform sampler2D lut : repeat_disable;

uniform float rotation_range;

varying float animation_offset;
varying vec4 vertex_color;

void vertex() {
	vertex_color = COLOR;
	animation_offset = INSTANCE_CUSTOM.z;
	
	VERTEX += pivot_offset * (1.0/TEXTURE_PIXEL_SIZE);
}

void fragment() {
	vec4 rotation_tex = texture(TEXTURE, UV * rotation_st.xy + vec2(animation_offset) + (vec2(0.0, TIME * rotation_st.w)));
	float rotation_factor = (rotation_tex.b - 0.5) * 2.0;
	
	float rotation = rotation_range * rotation_factor;
	rotation *= PI * 0.5;
	
	vec2 rotation_uv = rotate(UV, rotation_offset, PI * 0.5 + rotation);
	
	vec4 main_tex = texture(TEXTURE, rotation_uv);
	
	vec4 lut_tex = texture(lut, vec2(main_tex.r));
	
	COLOR = vec4(vertex_color.rgb * lut_tex.rgb, vertex_color.a * main_tex.a);
}