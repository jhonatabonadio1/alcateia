extends SceneTree
## Fecha o mapa no muro da cidade: tudo o que fica do lado de fora dele vira
## parede invisível do chão ao teto (o corpo "BarreiraMuro", em Colisao).
##
## Por quê (Jhon, 25/11, no Distrito Neon): "tô conseguindo sair pelos muros
## da cidade com o lobo; o mapa é delimitado por este muro, não pelo que tem
## no lado de fora". As paredes de limite (Limite…) ficam na borda do mapa,
## 10 a 20 m além do muro: o lobo subia o muro (pulo, corrida e salto da
## parede) e andava lá fora, nas torres e no terreno de trás.
##
## Como mede, com a colisão de verdade da cena (numa grade de 12,5 cm):
##   1. onde cabe um corpo em pé (uma caixa de 60 cm na altura do corpo) e
##      se chega andando de onde se nasce: é a cidade (com a folga do corpo),
##      mais o que é baixo e encosta nela (carro, canteiro: pisa-se em cima);
##   2. o que se alcança da borda do mapa sem entrar na cidade — muro,
##      prédios do anel, o terreno de fora — é o lado de fora;
##   3. o lado de fora inteiro vira caixas (fileiras juntadas), do chão ao teto.
## Nada que se alcança andando fica tapado: portas, salas e pátios de dentro
## são cidade. O que só se tapa é o alto do muro e dos prédios do anel, e o
## que fica atrás deles.
##
## Uso, da raiz do projeto (rode de novo se o mapa mudar; refaz o corpo):
##   godot --headless --path . -s res://src/tools/mapas/muro_da_cidade.gd -- neon [--seco] [--imagem=/caminho.png]
## --seco: só mede e conta, sem gravar. --imagem: a grade em cores (verde a
## cidade, amarelo o baixo dela, vermelho o lado de fora, cinza o que é sólido
## dentro).

const CEL := 0.125
## "Barreira…": o lobo não corre nela nem no muro que ela tem logo atrás da
## face (Player.WallRun: IsInvisibleLimit, BackedByBarrier), e a bala não
## marca (BulletMarks) — a regra dos limites dos mapas de kit (Jhon, 20/11:
## "o lobo não pode andar nem escalar as limitações do jogo, ele deve cair").
const NOME := "BarreiraMuro"

func _init():
	await process_frame
	var args := OS.get_cmdline_user_args()
	var mapa: String = args[0] if args.size() > 0 and not args[0].begins_with("--") else "neon"
	var seco := "--seco" in args
	var imagem := ""
	for a in args:
		if a.begins_with("--imagem="):
			imagem = a.substr(9)
	var caminho := "res://src/scenes/maps/%s.tscn" % mapa

	# A cena na árvore, só para medir (os scripts dela rodam; o que eles
	# mudam não vai para o arquivo: grava-se outra cópia, fora da árvore).
	var medir: Node3D = load(caminho).instantiate()
	root.add_child(medir)
	for i in 3:
		await physics_frame
	var espaco := root.get_world_3d().direct_space_state

	# A caixa do mapa: o lado de dentro das paredes Limite…
	var x0 := -1000.0
	var x1 := 1000.0
	var z0 := -1000.0
	var z1 := 1000.0
	var ignorar: Array[RID] = []
	for corpo in medir.get_node("Colisao").get_children():
		var nome := String(corpo.name)
		if nome == NOME or nome == "Piso":
			ignorar.append(corpo.get_rid())
		if not nome.begins_with("Limite") or nome == "LimiteTeto":
			continue
		var p: Vector3 = corpo.global_position
		var forma: BoxShape3D = corpo.get_node("Colisao").shape
		var s := forma.size
		if s.x < s.z:
			if p.x < 0: x0 = maxf(x0, p.x + s.x / 2)
			else: x1 = minf(x1, p.x - s.x / 2)
		else:
			if p.z < 0: z0 = maxf(z0, p.z + s.z / 2)
			else: z1 = minf(z1, p.z - s.z / 2)
	var nx := int(round((x1 - x0) / CEL))
	var nz := int(round((z1 - z0) / CEL))
	print("MURO %s: caixa x %.1f..%.1f, z %.1f..%.1f, grade %d × %d" % [mapa, x0, x1, z0, z1, nx, nz])

	# 1. sólido (a célula tem colisão na altura do corpo) e corpo cabe
	var solido := PackedByteArray(); solido.resize(nx * nz)
	var cabe := PackedByteArray(); cabe.resize(nx * nz)
	var alto := PackedByteArray(); alto.resize(nx * nz)
	var q := PhysicsShapeQueryParameters3D.new()
	q.collision_mask = 1
	q.exclude = ignorar
	# o sólido é o meio da célula: a face da caixa fica a meia célula da do muro
	var fino := BoxShape3D.new(); fino.size = Vector3(0.04, 1.4, 0.04)
	var corpo_caixa := BoxShape3D.new(); corpo_caixa.size = Vector3(0.6, 1.3, 0.6)
	var fino_alto := BoxShape3D.new(); fino_alto.size = Vector3(0.04, 1.0, 0.04)
	for k in nz:
		for i in nx:
			var c := Vector3(x0 + (i + 0.5) * CEL, 0, z0 + (k + 0.5) * CEL)
			q.shape = fino
			q.transform = Transform3D(Basis.IDENTITY, c + Vector3(0, 1.05, 0))
			solido[k * nx + i] = 1 if espaco.intersect_shape(q, 1).size() > 0 else 0
			q.shape = corpo_caixa
			q.transform = Transform3D(Basis.IDENTITY, c + Vector3(0, 1.1, 0))
			cabe[k * nx + i] = 1 if espaco.intersect_shape(q, 1).size() == 0 else 0
			if solido[k * nx + i] == 1:
				q.shape = fino_alto
				q.transform = Transform3D(Basis.IDENTITY, c + Vector3(0, 3.0, 0))
				alto[k * nx + i] = 1 if espaco.intersect_shape(q, 1).size() > 0 else 0

	# 2. a cidade: andando de onde se nasce
	var sementes := []
	for grupo in ["SpawnA", "SpawnB", "NascerLivre"]:
		var no := medir.get_node_or_null(grupo)
		if no == null:
			continue
		for m in no.get_children():
			if m is Marker3D:
				var gp: Vector3 = m.global_position
				sementes.append(Vector2i(clampi(int((gp.x - x0) / CEL), 0, nx - 1), clampi(int((gp.z - z0) / CEL), 0, nz - 1)))
	var andar := _alaga(sementes, cabe, nx, nz)
	# com a folga do corpo (o centro anda em "andar"; a pele chega 30 cm além)
	var cidade := andar.duplicate()
	for passo in 3:
		var mais := cidade.duplicate()
		for k in nz:
			for i in nx:
				if cidade[k * nx + i] == 0 or false:
					continue
				for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
					var ii: int = i + d.x
					var kk: int = k + d.y
					if ii >= 0 and ii < nx and kk >= 0 and kk < nz and solido[kk * nx + ii] == 0:
						mais[kk * nx + ii] = 1
		cidade = mais
	var na_borda := 0
	for i in nx:
		na_borda += cidade[i] + cidade[(nz - 1) * nx + i]
	for k in nz:
		na_borda += cidade[k * nx] + cidade[k * nx + nx - 1]

	# O que é baixo e encosta na cidade (carro, canteiro, banco: sólido até
	# 1,8 m e livre a 2,5–3,5 m) é da cidade: em cima dele se pisa. Sem isto o
	# carro parado rente ao prédio do anel ficava dentro da barreira.
	var baixo_ok := PackedByteArray(); baixo_ok.resize(nx * nz)
	var bordas_cidade := []
	for k in nz:
		for i in nx:
			var j := k * nx + i
			baixo_ok[j] = 1 if solido[j] == 1 and alto[j] == 0 else 0
			if cidade[j] == 1:
				baixo_ok[j] = 1
				bordas_cidade.append(Vector2i(i, k))
	# até 12 m da cidade: a sala de piso alto atrás da porta, o carro; não o
	# que segue baixo até longe, lá fora
	var baixo := _alaga(bordas_cidade, baixo_ok, nx, nz, int(12.0 / CEL))

	# 3. o lado de fora: da borda, sem entrar na cidade nem no que é baixo dela
	var fora_ok := PackedByteArray(); fora_ok.resize(nx * nz)
	for j in nx * nz:
		fora_ok[j] = 1 - baixo[j]
	var bordas := []
	for i in nx:
		bordas.append(Vector2i(i, 0)); bordas.append(Vector2i(i, nz - 1))
	for k in nz:
		bordas.append(Vector2i(0, k)); bordas.append(Vector2i(nx - 1, k))
	var fora := _alaga(bordas, fora_ok, nx, nz)

	# A barreira de cada célula: -1 nenhuma; 0 do chão ao teto (o lado de
	# fora); q > 0 de q × 50 cm para cima — em cima do que é baixo e encosta na
	# cidade: 3 m acima do carro (pisa-se nele, não se pula dele para o muro) e
	# no telhado da sala de piso alto (dentro dela se anda; em cima, não).
	# Só o baixo que encosta no lado de fora (o carro rente ao prédio do anel,
	# a sala atrás da porta): o banco e o carro no meio da rua não têm tampa.
	var so_baixo := PackedByteArray(); so_baixo.resize(nx * nz)
	var no_muro := []
	for k in nz:
		for i in nx:
			var j := k * nx + i
			if baixo[j] == 0 or cidade[j] == 1:
				continue
			so_baixo[j] = 1
			for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var ii: int = i + d.x
				var kk: int = k + d.y
				if ii >= 0 and ii < nx and kk >= 0 and kk < nz and fora[kk * nx + ii] == 1:
					no_muro.append(Vector2i(i, k))
					break
	var tampar := _alaga(no_muro, so_baixo, nx, nz)
	var fundo := PackedInt32Array(); fundo.resize(nx * nz)
	var sem_muro: Array[RID] = ignorar.duplicate()
	# Uma altura por peça (o carro, a sala): coberta (a sala de piso alto), do
	# telhado mais baixo dela para cima, nunca abaixo de 2,5 m; descoberta (o
	# carro), 3 m acima do ponto mais alto dela.
	var n_tampa := 0
	var peca := PackedInt32Array(); peca.resize(nx * nz)
	for j in nx * nz:
		fundo[j] = 0 if fora[j] == 1 else -1
		peca[j] = -1
	var pecas := 0
	for k in nz:
		for i in nx:
			var j0 := k * nx + i
			if tampar[j0] == 0 or peca[j0] >= 0:
				continue
			var celulas := _alaga([Vector2i(i, k)], tampar, nx, nz)
			var coberta := INF
			var aberta := -INF
			var lista := []
			for kk in nz:
				for ii in nx:
					var j := kk * nx + ii
					if celulas[j] == 0 or peca[j] >= 0:
						continue
					peca[j] = pecas
					lista.append(j)
					var c := Vector3(x0 + (ii + 0.5) * CEL, 0, z0 + (kk + 0.5) * CEL)
					var desce := PhysicsRayQueryParameters3D.create(c + Vector3(0, 29.9, 0), c + Vector3(0, -1, 0))
					desce.exclude = sem_muro
					var topo: Dictionary = espaco.intersect_ray(desce)
					var y_topo: float = topo.position.y if topo.size() > 0 else 0.0
					var sobe := PhysicsRayQueryParameters3D.create(c + Vector3(0, 2.4, 0), c + Vector3(0, 29.9, 0))
					sobe.exclude = sem_muro
					if espaco.intersect_ray(sobe).size() > 0: coberta = minf(coberta, y_topo - 0.1)
					else: aberta = maxf(aberta, y_topo + 3.0)
			var y := maxf(coberta, 2.5) if coberta < INF else aberta
			for j in lista:
				fundo[j] = maxi(1, int(ceil(y / 0.5)))
			n_tampa += lista.size()
			pecas += 1

	var n_cidade := 0
	var n_fora := 0
	for j in nx * nz:
		n_cidade += cidade[j]
		n_fora += fora[j]
	print("MURO %s: cidade %d m², lado de fora %d m², tampa em cima do baixo %d m², células da cidade na borda do mapa: %d" % [mapa, int(n_cidade * CEL * CEL), int(n_fora * CEL * CEL), int(n_tampa * CEL * CEL), na_borda])

	if imagem != "":
		var img := Image.create(nx, nz, false, Image.FORMAT_RGB8)
		for k in nz:
			for i in nx:
				var j := k * nx + i
				var cor := Color(0.08, 0.08, 0.1)
				if fora[j] == 1: cor = Color(0.75, 0.15, 0.12) if solido[j] == 0 else Color(0.45, 0.08, 0.08)
				elif andar[j] == 1: cor = Color(0.2, 0.7, 0.3)
				elif cidade[j] == 1: cor = Color(0.15, 0.45, 0.2)
				elif tampar[j] == 1: cor = Color(0.85, 0.75, 0.2)
				elif baixo[j] == 1: cor = Color(0.55, 0.5, 0.25)
				elif solido[j] == 1: cor = Color(0.5, 0.5, 0.52)
				img.set_pixel(i, k, cor)
		for s in sementes:
			img.set_pixel(s.x, s.y, Color(1, 1, 0))
		img.save_png(imagem)
		print("MURO %s: imagem em %s" % [mapa, imagem])

	# as caixas: fileiras iguais (mesma altura de baixo) empilhadas
	var livre := PackedByteArray(); livre.resize(nx * nz)
	for j in nx * nz:
		livre[j] = 1 if fundo[j] == -1 else 0
	var feito := PackedByteArray(); feito.resize(nx * nz)
	var caixas := []
	for k in nz:
		var i := 0
		while i < nx:
			var j := k * nx + i
			var alt := fundo[j]
			if alt < 0 or feito[j] == 1:
				i += 1
				continue
			var i1 := i
			while i1 < nx and fundo[k * nx + i1] == alt and feito[k * nx + i1] == 0:
				i1 += 1
			var k1 := k + 1
			while k1 < nz:
				var inteira := true
				for ii in range(i, i1):
					if fundo[k1 * nx + ii] != alt or feito[k1 * nx + ii] == 1:
						inteira = false
						break
				if not inteira:
					break
				k1 += 1
			for kk in range(k, k1):
				for ii in range(i, i1):
					feito[kk * nx + ii] = 1
			# O lado que dá todo para onde não há barreira recua meia célula: a face
			# da caixa fica no meio da primeira célula sólida, atrás da face do muro
			# (até 12,5 cm), e não na frente dela. Lado que encosta em parte noutra
			# caixa não recua: abriria uma fresta entre as duas.
			var r := Rect2(x0 + i * CEL, z0 + k * CEL, (i1 - i) * CEL, (k1 - k) * CEL)
			var meia := CEL / 2
			if _toca(livre, nx, nz, i - 1, i - 1, k, k1 - 1): r = r.grow_individual(-meia, 0, 0, 0)
			if _toca(livre, nx, nz, i1, i1, k, k1 - 1): r = r.grow_individual(0, 0, -meia, 0)
			if _toca(livre, nx, nz, i, i1 - 1, k - 1, k - 1): r = r.grow_individual(0, -meia, 0, 0)
			if _toca(livre, nx, nz, i, i1 - 1, k1, k1): r = r.grow_individual(0, 0, 0, -meia)
			caixas.append([r, -1.0 if alt == 0 else alt * 0.5])
			i = i1
	print("MURO %s: %d caixas até o teto" % [mapa, caixas.size()])
	root.remove_child(medir)
	medir.free()

	if seco:
		quit()
		return

	# Lida de novo do disco, sem o cache: os scripts da cena medida mexeram
	# nos recursos dela (o ambiente ganha a qualidade das opções) e isso iria
	# para o arquivo.
	var cena: Node3D = ResourceLoader.load(caminho, "", ResourceLoader.CACHE_MODE_IGNORE_DEEP).instantiate()
	var colisao: Node3D = cena.get_node("Colisao")
	var velho := colisao.get_node_or_null(NOME)
	if velho != null:
		colisao.remove_child(velho)
		velho.free()
	var muro := StaticBody3D.new()
	muro.name = NOME
	muro.set_meta("mapa_fora", true)
	colisao.add_child(muro)
	muro.owner = cena
	var formas := {}
	var y_alto := 30.5
	var n := 0
	for caixa in caixas:
		var r: Rect2 = caixa[0]
		var y_baixo: float = caixa[1]
		var tam := Vector3(r.size.x, y_alto - y_baixo, r.size.y)
		if not formas.has(tam):
			var b := BoxShape3D.new(); b.size = tam
			formas[tam] = b
		var f := CollisionShape3D.new()
		n += 1
		f.name = "m%d" % n
		f.shape = formas[tam]
		f.position = Vector3(r.position.x + r.size.x / 2, (y_alto + y_baixo) / 2, r.position.y + r.size.y / 2)
		muro.add_child(f)
		f.owner = cena
	var pacote := PackedScene.new()
	var erro := pacote.pack(cena)
	if erro == OK:
		erro = ResourceSaver.save(pacote, caminho)
	print("GRAVADO " if erro == OK else "ERRO ao gravar: ", caminho, " ", erro, " (%d formas diferentes)" % formas.size())
	quit()


## Todas as células do retângulo (i0..i1, k0..k1) estão marcadas? (Fora da
## grade não conta: lá é a parede Limite.)
func _toca(cidade: PackedByteArray, nx: int, nz: int, i0: int, i1: int, k0: int, k1: int) -> bool:
	var alguma := false
	for k in range(maxi(k0, 0), mini(k1, nz - 1) + 1):
		for i in range(maxi(i0, 0), mini(i1, nx - 1) + 1):
			if cidade[k * nx + i] == 0:
				return false
			alguma = true
	return alguma


## Tudo de "onde" (1 = pode) que se alcança das sementes, de célula em
## célula (para os quatro lados).
func _alaga(sementes: Array, onde: PackedByteArray, nx: int, nz: int, passos: int = 1 << 30) -> PackedByteArray:
	var v := PackedByteArray(); v.resize(nx * nz)
	var dist := PackedInt32Array(); dist.resize(nx * nz)
	var fila := []
	for s in sementes:
		var j: int = s.y * nx + s.x
		if onde[j] == 1 and v[j] == 0:
			v[j] = 1
			fila.append(s)
	var cabeca := 0
	while cabeca < fila.size():
		var c: Vector2i = fila[cabeca]
		cabeca += 1
		var dc := dist[c.y * nx + c.x]
		if dc >= passos:
			continue
		for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
			var p: Vector2i = c + d
			if p.x < 0 or p.x >= nx or p.y < 0 or p.y >= nz:
				continue
			var j := p.y * nx + p.x
			if onde[j] == 1 and v[j] == 0:
				v[j] = 1
				dist[j] = dc + 1
				fila.append(p)
	return v
