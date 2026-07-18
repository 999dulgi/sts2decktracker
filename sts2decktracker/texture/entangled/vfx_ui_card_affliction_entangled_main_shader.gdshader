shader_type canvas_item;
render_mode blend_mix;

uniform vec4 distortion_st;
uniform float distortion_intensity;

uniform sampler2D vine_lut : repeat_disable;

varying vec4 vertex_color;

void vertex() {
	vertex_color = COLOR;
}

// main_tex.g(흔들림 마스크)가 급격히 변하는 지점에서 가로 오프셋이 픽셀 단위로 툭 끊겨
// 리본이 계단처럼 잘려 보이는 걸 막기 위해, G채널을 가로로 살짝 블러링해서 사용한다.
float blurred_mask_g(sampler2D tex, vec2 uv, vec2 texel) {
	float g = 0.0;
	g += texture(tex, uv + texel * vec2(-1.5, 0.0)).g;
	g += texture(tex, uv + texel * vec2(-0.5, 0.0)).g;
	g += texture(tex, uv + texel * vec2( 0.5, 0.0)).g;
	g += texture(tex, uv + texel * vec2( 1.5, 0.0)).g;
	return g * 0.25;
}

// 원본 이미지를 스크롤해서 노이즈로 재활용하면(위 distortion_tex) 이미지 자체의 무늬가
// 노이즈에 섞여 들어와 "반투명한 텍스처가 스치는" 것처럼 보인다.
// 이미지 내용과 무관하게 항상 매끈한 절차적 노이즈로 대체한다.
float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float value_noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void fragment() {
	vec2 distortion_uv = UV * distortion_st.xy * 8.0 + vec2(distortion_st.z, TIME * distortion_st.w);
	float distortion = value_noise(distortion_uv) * distortion_intensity;
	
	vec4 main_tex = texture(TEXTURE, UV);
	vec2 texel = vec2(6.0) / vec2(textureSize(TEXTURE, 0));
	float mask_g = blurred_mask_g(TEXTURE, UV, texel);
	vec2 distorted_uv = clamp(UV + vec2(distortion * mask_g, 0.0), vec2(0.0), vec2(1.0));
	vec4 main_tex_distorted = texture(TEXTURE, distorted_uv);
	
	vec4 lut_tex = texture(vine_lut, vec2(main_tex_distorted.r));
	
	COLOR = vec4(vertex_color.rgb * lut_tex.rgb, vertex_color.a * main_tex_distorted.a);
}
