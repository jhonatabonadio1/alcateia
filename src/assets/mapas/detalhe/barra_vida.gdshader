// A barra de vida que paira sobre o robô de treino (pedido do Jhon, 26/11: "exibe a vida deles"). Um retângulo sempre
// de frente para a câmera: o fundo escuro, a vida (verde, amarela abaixo da metade, vermelha abaixo de um quarto), e o
// "rastro" claro do que acabou de sair (encolhe depois do golpe: lê-se o tamanho do dano). Sem luz e sem sombra.
// Quem manda os números é o src/scripts/debug/Dummy.cs.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, depth_draw_never, blend_mix;

// quanto da vida resta (0 a 1) e até onde vai o rastro do último golpe
uniform float vida : hint_range(0.0, 1.0) = 1.0;
uniform float rastro : hint_range(0.0, 1.0) = 1.0;
// some com a distância (o Dummy calcula)
uniform float mostra : hint_range(0.0, 1.0) = 1.0;
uniform vec4 cheia : source_color = vec4(0.30, 0.92, 0.42, 1.0);
uniform vec4 meia : source_color = vec4(1.0, 0.80, 0.22, 1.0);
uniform vec4 pouca : source_color = vec4(1.0, 0.28, 0.24, 1.0);
// largura ÷ altura do retângulo (para a moldura ter a mesma espessura dos dois lados)
uniform float proporcao = 9.0;

void vertex() {
	// de frente para a câmera, do tamanho que o nó tem
	vec3 escala = vec3(length(MODEL_MATRIX[0].xyz), length(MODEL_MATRIX[1].xyz), length(MODEL_MATRIX[2].xyz));
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * escala.x, INV_VIEW_MATRIX[1] * escala.y, INV_VIEW_MATRIX[2] * escala.z, MODEL_MATRIX[3]);
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
}

void fragment() {
	vec2 uv = UV;
	float borda = 0.16;
	bool moldura = uv.y < borda || uv.y > 1.0 - borda || uv.x < borda / proporcao || uv.x > 1.0 - borda / proporcao;
	float x = (uv.x - borda / proporcao) / (1.0 - 2.0 * borda / proporcao);
	vec3 tom = vida > 0.5 ? cheia.rgb : (vida > 0.25 ? meia.rgb : pouca.rgb);
	vec3 c = vec3(0.03, 0.04, 0.06);
	float a = 0.72;
	if (!moldura) {
		if (x <= vida) {
			// um brilho em cima, como vidro
			c = tom * (uv.y < 0.5 ? 1.25 : 0.95);
			a = 1.0;
		} else if (x <= rastro) {
			c = vec3(1.0, 0.96, 0.9);
			a = 0.95;
		} else {
			c = vec3(0.10, 0.12, 0.16);
			a = 0.6;
		}
		// os quartos, marcados
		float quarto = abs(fract(x * 4.0 + 0.5) - 0.5) / 4.0;
		if (quarto < 0.004 && x > 0.02 && x < 0.98) c *= 0.55;
	}
	ALBEDO = c;
	ALPHA = a * mostra;
}
