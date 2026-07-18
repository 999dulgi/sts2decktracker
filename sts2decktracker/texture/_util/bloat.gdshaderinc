vec2 bloat(vec2 uv, vec2 center, float intensity) {
	vec2 delta = uv - center;
	float delta_dot = dot(delta.xy, delta.xy);
	float delta_dot_sqr = delta_dot * delta_dot;
	float delta_offset = delta_dot_sqr * intensity;
	
	return uv + delta * delta_offset;
}