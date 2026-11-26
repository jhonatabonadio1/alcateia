extends SceneTree
## Troca a caixa de colisão das peças finas ou recortadas de um mapa de kit
## pela forma exata da peça (a malha do desenho), e grava a cena.
##
## Por quê (Jhon, 24/11, no Neon): "postes, placas e coisas pequenas estão
## com áreas de colisão muito grandes: não consegui andar em regiões que pode
## andar, não consegui atirar em oponentes nessas regiões". O gerador punha
## uma caixa do tamanho total da peça (a lista CAIXA do neon.py): no poste com
## braço, uma parede invisível de 1,5 × 4 m; na mesa, um bloco até o chão.
## Caixa continua só no que é bloco de verdade (caixote, máquina, carro, laje)
## e nas paredes invisíveis da borda.
##
## Faz também (24/11, nas fotos do Jhon: "essas placas estão sem colisão,
## então estou passando por elas, e nesses locais não consigo entrar"):
## - as peças que o gerador deixou sem colisão nenhuma e que são sólidas
##   (SOLIDAS_SEM_COLISAO: os painéis de anúncio) ganham colisão na forma delas;
## - com --sem-barreiras, tira as paredes invisíveis "Barreira…" de dentro do
##   mapa. O gerador as punha onde o ladrilho da rua acaba, e elas tapavam
##   portas, salas e pátios; os prédios já fecham o mapa (medido: sem elas o
##   andável cresce 544 m², tudo cômodo ou pátio fechado, nada que chegue na
##   borda) e as paredes de limite ("Limite…") ficam.
##
## Uso, da raiz do projeto (o mapa é a cena: rode de novo se a cena voltar a
## ter caixa nessas peças):
##   godot --headless --path . -s res://src/tools/mapas/colisao_exata.gd -- neon [--seco] [--sem-barreiras]
## --seco: só conta o que trocaria, sem gravar.

## As peças que trocam (pelo começo do nome, sem o "pcN_"). Os itens do
## mercado (SM_Props_Market) ficam com caixa: a deles já é justa.
const FINAS := ["SM_Floor_Light", "SM_Props_0", "SM_Props_BarProps", "SM_Props_Furniture", "SM_Props_CyberIce",
	"SM_Props_Screen", "SM_Props_Cafe", "SM_Props_Bench", "SM_Nature_Flowerbed", "SM_Nature_Rock",
	"SM_Nature_Log",
	"CementC"]

## Destas, a forma é o casco convexo da peça, não a malha inteira: as pilhas de
## sacos de cimento da Obra (26/11). A caixa cobria os cantos vazios da pilha e o
## tiro de cima dos andaimes parava no ar ali (TiroDeCimaObra, com a câmera nos
## olhos do corpo desde 26/11); a malha exata são 4 mil triângulos por pilha (132
## pilhas), e o casco fica rente aos sacos com poucas faces.
const CONVEXAS := ["CementC"]

## Peças sem colisão nenhuma no gerador que são sólidas: ganham a forma delas.
const SOLIDAS_SEM_COLISAO := ["SM_Props_Ads"]

func _init():
	await process_frame
	var args := OS.get_cmdline_user_args()
	var mapa: String = args[0] if args.size() > 0 else "neon"
	var seco := "--seco" in args
	var caminho := "res://src/scenes/maps/%s.tscn" % mapa
	# A cena não entra na árvore: os scripts dela (o NavBaker, os pontos de
	# nascer) rodariam e o que eles mudam iria junto para o arquivo.
	var cena: Node3D = load(caminho).instantiate()

	# o desenho de cada peça: "<peça>_<k>" no .glb do mapa
	var desenhos := {}
	for n in cena.find_children("*", "MeshInstance3D", true, false):
		desenhos[String(n.name)] = n
	var formas := {}        # uma forma por malha: as peças iguais dividem
	var trocadas := 0
	var triangulos := 0
	var sem_desenho := []
	var esticadas := []
	var por_tipo := {}
	for corpo in cena.get_node("Colisao").get_children():
		if not corpo is StaticBody3D:
			continue
		var nome := String(corpo.name)
		var partes := nome.split("_", false, 1)
		if partes.size() < 2 or not partes[0].begins_with("pc"):
			continue
		var peca := partes[1]
		var fina := false
		for f in FINAS:
			if peca.begins_with(f):
				fina = true
		if not fina:
			continue
		var forma_no: CollisionShape3D = corpo.get_node_or_null("Colisao")
		var desenho: MeshInstance3D = desenhos.get("%s_%s" % [peca, partes[0].substr(2)])
		if forma_no == null or desenho == null or desenho.mesh == null:
			sem_desenho.append(nome)
			continue
		var convexa := false
		for c in CONVEXAS:
			if peca.begins_with(c):
				convexa = true
		if not formas.has(desenho.mesh):
			formas[desenho.mesh] = desenho.mesh.create_convex_shape(true, false) if convexa else desenho.mesh.create_trimesh_shape()
		var forma: Shape3D = formas[desenho.mesh]
		var antes: Vector3 = forma_no.shape.size if forma_no.shape is BoxShape3D else Vector3.ZERO
		forma_no.shape = forma
		# a forma no lugar do desenho, no espaço do corpo
		forma_no.transform = _no_mundo(corpo, cena).affine_inverse() * _no_mundo(desenho, cena)
		var escala := forma_no.transform.basis.get_scale()
		if absf(escala.x - escala.y) > 0.001 or absf(escala.y - escala.z) > 0.001:
			esticadas.append("%s %s" % [nome, escala])
		trocadas += 1
		var faces: int = forma.get_faces().size() / 3 if forma is ConcavePolygonShape3D else (forma as ConvexPolygonShape3D).points.size()
		triangulos += faces
		var tipo := peca.rstrip("0123456789").trim_suffix("_")
		if not por_tipo.has(tipo):
			por_tipo[tipo] = [0, 0, antes]
		por_tipo[tipo][0] += 1
		por_tipo[tipo][1] += faces

	# As sólidas que ficaram sem colisão: um corpo novo com a forma da peça,
	# no lugar do desenho (o nome segue o das outras: pc<k>_<peça>).
	var colisao: Node3D = cena.get_node("Colisao")
	var novas := 0
	for nome_desenho in desenhos:
		var solida := false
		for p in SOLIDAS_SEM_COLISAO:
			if String(nome_desenho).begins_with(p):
				solida = true
		if not solida:
			continue
		var desenho: MeshInstance3D = desenhos[nome_desenho]
		var corte := String(nome_desenho).rfind("_")
		var nome_corpo := "pc%s_%s" % [String(nome_desenho).substr(corte + 1), String(nome_desenho).substr(0, corte)]
		if colisao.has_node(nome_corpo) or desenho.mesh == null:
			continue
		if not formas.has(desenho.mesh):
			formas[desenho.mesh] = desenho.mesh.create_trimesh_shape()
		var corpo_novo := StaticBody3D.new()
		corpo_novo.name = nome_corpo
		var forma_nova := CollisionShape3D.new()
		forma_nova.name = "Colisao"
		forma_nova.shape = formas[desenho.mesh]
		corpo_novo.add_child(forma_nova)
		colisao.add_child(corpo_novo)
		corpo_novo.owner = cena
		forma_nova.owner = cena
		# o corpo na origem do mapa; a forma no lugar do desenho
		forma_nova.transform = _no_mundo(colisao, cena).affine_inverse() * _no_mundo(desenho, cena)
		novas += 1
	print("COLISAO %s: %d peças sólidas sem colisão ganharam a forma delas" % [mapa, novas])

	var tiradas := 0
	if "--sem-barreiras" in args:
		for corpo in colisao.get_children():
			# só as de dentro (Barreira1, 2…): a BarreiraMuro é o lado de fora do
			# muro da cidade (muro_da_cidade.gd) e fica
			if String(corpo.name).begins_with("Barreira") and String(corpo.name).substr(8).is_valid_int():
				colisao.remove_child(corpo)
				corpo.free()
				tiradas += 1
		print("COLISAO %s: %d barreiras invisíveis tiradas" % [mapa, tiradas])

	print("COLISAO %s: %d peças trocadas para a forma exata (%d formas diferentes, %d triângulos no total)" % [mapa, trocadas, formas.size(), triangulos])
	for t in por_tipo:
		print("  %s: %d peças, %d triângulos (caixa de antes %s)" % [t, por_tipo[t][0], por_tipo[t][1], por_tipo[t][2]])
	if esticadas.size() > 0:
		print("  esticadas (escala diferente por eixo): ", esticadas)
	if sem_desenho.size() > 0:
		print("  sem desenho achado (ficam com caixa): ", sem_desenho)
	if not seco and (trocadas > 0 or novas > 0 or tiradas > 0):
		var pacote := PackedScene.new()
		var erro := pacote.pack(cena)
		if erro == OK:
			erro = ResourceSaver.save(pacote, caminho)
		print("GRAVADO " if erro == OK else "ERRO ao gravar: ", caminho, " ", erro)
	quit()


## O lugar no mundo de um nó fora da árvore: os transforms até a raiz da cena.
func _no_mundo(no: Node3D, raiz: Node) -> Transform3D:
	var t := no.transform
	var pai := no.get_parent()
	while pai != null and pai != raiz:
		if pai is Node3D:
			t = pai.transform * t
		pai = pai.get_parent()
	return (raiz as Node3D).transform * t if raiz is Node3D else t
