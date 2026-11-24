#!/usr/bin/env python3
"""
A animação de terceira pessoa de uma arma, com a mão do personagem (ossos do
Manny, `mao_manny_tpose.py`) copiando a da primeira pessoa osso por osso
(Jhon, 23/11: "aprovado", "sem trabalhar com gerador, trabalho limpo").

    python3 tools/personagens/blender/mao_manny_anim.py eve ar15

Sem procura de encaixe, sem alívio, sem mínimos quadrados: cada osso de dedo e
de metacarpo recebe, a cada quadro, a rotação local do osso de mesmo nome da
primeira pessoa (os dois têm os mesmos eixos de osso: o `mao_manny_tpose.py`
dá aos ossos daqui o roll dos de lá). O que não é mão — tronco, cabeça,
ombros, braços por IK até o pulso — é o do `animacoes_armas.py` (a postura
de cada arma) e do `mao_manny.py` (o braço), como antes.

Precisa do `humano.glb` já com os ossos do Manny (`mao_manny_tpose.py eve
--aplicar`). Grava `assets/personagens/<p>/animacoes/<arma>.json` com os
nomes novos dos ossos.
"""
import base64
import json
import math
import os
import struct
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)

import bpy  # noqa: E402
from mathutils import Matrix, Quaternion, Vector  # noqa: E402

import animacoes_armas as aa  # noqa: E402
import mao_manny as mm  # noqa: E402
import mao_manny_tpose as mt  # noqa: E402
import poses_armas as pa  # noqa: E402
from poses_armas import EIXOS, RAIZ, orient  # noqa: E402

LADOS = (('r', 'Right'), ('l', 'Left'))
# Quanto o braço de cima gira em volta do próprio eixo (graus).
ROLO_BRACO = float(os.environ.get('ROLO_BRACO', 0.0))
# Quanto custa (por grau) o braço de cima girar em volta de si em relação à pose T (Braco.peso_torcao).
PESO_TORCAO = float(os.environ.get('PESO_TORCAO', 6.0))
# A torção que não custa (graus) e o cotovelo para baixo (Jhon, 23/11: "os braços precisam ter o
# cotovelo apontando para baixo, não para os lados"): na pistola e nos fuzis os dois cotovelos
# preferem apontar para baixo (COTOVELO_BAIXO: para fora, para cima, para trás) com este peso por grau.
TORCAO_LIVRE = float(os.environ.get('TORCAO_LIVRE', 50.0))
PESO_COTOVELO = float(os.environ.get('PESO_COTOVELO', 1.5))
# Na pistola, até quantos graus o punho pode ficar do de lá (a mão e a arma giram no pulso).
# Na granada, o braço esquerdo fica na pose de armada durante o lance.
# O saque de trás (Copia.sacar_de_tras): a arma começa pendurada na mão direita, com o braço solto.
# Direções no corpo: x direita, y frente, z cima.
SACAR_DE_TRAS = os.environ.get('SACAR_DE_TRAS', '1') == '1'
def _vetor(nome, padrao):
    return tuple(float(x) for x in os.environ.get(nome, padrao).split(','))
# a direção do ombro ao punho no começo: para baixo, afastado da coxa (colado nela, a coronha
# entrava no quadril) e um pouco para trás; e para onde aponta o cano
SACAR_BRACO_FUZIL = _vetor('SACAR_BRACO_FUZIL', '0.4,-0.12,-1.0')
SACAR_CANO_FUZIL = _vetor('SACAR_CANO_FUZIL', '0.35,0.4,-1.0')
SACAR_BRACO_PISTOLA = _vetor('SACAR_BRACO_PISTOLA', '0.3,-0.1,-1.0')
SACAR_CANO_PISTOLA = _vetor('SACAR_CANO_PISTOLA', '0.25,0.45,-1.0')
# para onde vão os dedos e a palma da direita no começo (o cano corrige depois)
SACAR_EIXOS_FUZIL = ((0.0, 0.25, -0.97), (-1.0, 0.0, 0.0))
SACAR_ESTICA = float(os.environ.get('SACAR_ESTICA', 0.995))   # do alcance do braço
SACAR_OMBRO_DESCE = float(os.environ.get('SACAR_OMBRO_DESCE', 8.0))   # graus da clavícula
# a esquerda no saque: pendurada (direção do ombro, dedos e palma; a palma uns cm além do punho),
# afastada da coxa (a -0,08 a mão entrava 2,4 cm na calça)
SACAR_BRACO_E = tuple(float(x) for x in os.environ.get('SACAR_BRACO_E', '-0.28,-0.02,-1.0').split(','))
SACAR_EIXOS_E = ((0.0, 0.15, -1.0), (1.0, 0.0, 0.0))
SACAR_PALMA_E = 0.06
# o braço solto: o cotovelo prefere apontar para trás (fora, cima, trás) e a torção que não custa
SACAR_COTOVELO = (0.15, 0.0, 1.0)
SACAR_TORCAO_LIVRE = float(os.environ.get('SACAR_TORCAO_LIVRE', 5.0))
# a cabeça no saque: a fração do caminho até a mão que ela olha, e o máximo (graus)
OLHAR_SACAR = float(os.environ.get('OLHAR_SACAR', 0.4))
OLHAR_SACAR_MAX = float(os.environ.get('OLHAR_SACAR_MAX', 20.0))
# O lance da granada (Copia.lance; Jhon, 23/11: "refaça, não está natural… só precisa fazer o mesmo
# movimento de puxar o braço para trás e lançar"). O braço que lança segue a referência do Mixamo
# (lance_referencia.json: só o braço direito, no referencial do peito); o tronco, o braço que
# puxa o pino e o fim são daqui, presos no tempo desse braço.
LANCE_REF = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'lance_referencia.json')
# os quadros da referência: o braço sai da frente do peito, engatilha atrás da cabeça, solta,
# acompanha
REF_PINO, REF_SAI, REF_ARMADO, REF_SOLTA, REF_SEGUE = 1.0, 43.0, 57.0, 62.0, 74.0
# no armar: até PINO s a esquerda puxa o pino (a primeira pessoa); de PINO ao fim o braço engatilha
LANCE_PINO = float(os.environ.get('LANCE_PINO', 0.22))
# A esquerda fica no pino até ele sair (na primeira pessoa sai aos 0,26 s; tools/armas/conjuntos.py), com a ponta do
# indicador na de lá, dentro da argola (Jhon, 23/11: "deixe a personagem grudar mais um pouco a mão no gatilho da
# granada"; ela soltava aos 0,22 s, a 12 a 16 cm da argola). Com o pino na mão ela se solta da granada e, LANCE_LARGA
# depois, abre e larga o pino, que cai no chão (HeldWeapon.Pin; "o gatilho da granada deve cair no chão também").
LANCE_PINO_SAI = float(os.environ.get('LANCE_PINO_SAI', 0.27))
LANCE_LARGA = float(os.environ.get('LANCE_LARGA', 0.12))
# no arremesso: a soltura (s; a granada sai da mão no jogo em aa.LANCE[0]), o fim do
# acompanhamento e o começo da volta para os braços soltos
LANCE_SEGUE = float(os.environ.get('LANCE_SEGUE', 0.36))
LANCE_SOLTOS = float(os.environ.get('LANCE_SOLTOS', 0.38))
# o tronco (graus; giro + para a esquerda dela, inclina + para a frente): engatilhado, o ombro
# direito vai para trás; no lance o tronco puxa na frente do braço, passa do alvo e volta
TRONCO_ARMADO = (float(os.environ.get('TRONCO_ARMADO_GIRO', -38.0)), -6.0)
TRONCO_LANCE = (float(os.environ.get('TRONCO_LANCE_GIRO', 30.0)), float(os.environ.get('TRONCO_LANCE_INCLINA', 22.0)))
TRONCO_PUXA = 0.15   # s: quando o tronco chega no giro do lance (a mão solta depois, chicoteando)
# o tronco também inclina de lado (graus; + para a direita dela): engatilhado, o ombro direito
# baixa um pouco; no lance, para a esquerda — sem isso o braço passava em pé acima da cabeça
TRONCO_LADO = (float(os.environ.get('TRONCO_LADO_ARMADO', 6.0)), float(os.environ.get('TRONCO_LADO_LANCE', -16.0)))
# na soltura a mão e o cotovelo passam por fora (comprimentos de braço, para a direita): 0 desde
# que o Jhon pediu a mão para dentro (era 0,25, para o braço de cima não atravessar a gola)
LANCE_FORA = float(os.environ.get('LANCE_FORA', 0.0))
# depois de soltar, a mão acompanha para dentro e para baixo, cruzando o corpo (comprimentos de
# braço; Jhon, 23/11: "no lançamento da granada, a mão gira para dentro, não para fora")
LANCE_DENTRO = tuple(float(x) for x in os.environ.get('LANCE_DENTRO', '-0.3,0.5,-0.15').split(','))
LANCE_DENTRO_PICO = float(os.environ.get('LANCE_DENTRO_PICO', 0.27))
# a esquerda: o arco para a frente entre o pino e o alvo (m) e o quanto ela abre ao descer no lance
LANCE_ARCO_E = float(os.environ.get('LANCE_ARCO_E', 0.25))
LANCE_ABRE_E = float(os.environ.get('LANCE_ABRE_E', 0.7))
# o braço direito solto no fim (a pose em que o saque da arma começa)
SOLTO_D = (0.35, -0.1, -1.0)
SOLTO_EIXOS_D = ((0.0, 0.15, -1.0), (-1.0, 0.0, 0.0))
PESO_COTOVELO_LANCE = float(os.environ.get('PESO_COTOVELO_LANCE', 2.0))
# a torção do braço de cima que não custa no lance (graus): o braço segue a referência, mas o
# modelo não tem osso de torção e a pele do braço torcia em espiral engatilhado (130°; Jhon:
# "braço da granada torcendo aqui")
TORCAO_LANCE = float(os.environ.get('TORCAO_LANCE', 50.0))
# quanto a mão engatilhada vem para a frente (e para cima) da da referência, em comprimentos de braço
LANCE_ARMADO_ADIANTA = tuple(float(x) for x in os.environ.get('LANCE_ARMADO_ADIANTA', '0.15,1.0,-0.2').split(','))
# e a mão engatilhada: para onde vão os dedos e a palma (no peito: x direita, y frente, z cima).
# Dedos para cima, palma para o rosto (para dentro e um pouco para trás), a granada entre a mão e a
# cabeça, o dorso para fora (Jhon, 23/11: "a palma da mão tem que estar na direção do rosto"; a
# palma para a frente mostrava a granada de fora). Com o antebraço de pé e o cotovelo para fora, é a
# rotação neutra do antebraço: menos torção que a palma para a frente.
def _par(nome, padrao):
    v = [float(x) for x in os.environ.get(nome, padrao).split(',')]
    return (tuple(v[:3]), tuple(v[3:6]))


LANCE_ARMADO_MAO = _par('LANCE_ARMADO_MAO', '0.0,0.1,1.0,-1.0,-0.35,0.0')
# A faca (Copia.faca; Jhon, 23/11: "corrija esses pontos"). A posição da mão, o tempo, o tronco e a
# esquerda são daqui; a orientação da mão nos extremos de cada golpe é a da primeira pessoa (a
# pegada é a dela). Pontos no peito: x direita, y frente, z cima, a partir do meio dos ombros.
# A guarda: a mão direita na frente do peito, a lâmina de pé e um pouco para a frente
# (FACA_GUARDA_LAMINA); a de lá atravessava para a esquerda. A mão só gira em volta do antebraço
# (a lâmina sai de lado dos dedos nessa pegada): girá-la em volta da vertical punha os dedos para a
# direita e o antebraço, para segui-los, vinha de dentro do corpo — o cotovelo 10 cm dentro do
# colete (Jhon: "o cotovelo da faca está para dentro"). A esquerda aberta, mais alta e um pouco à
# frente, protegendo o rosto.
FACA_GUARDA = (0.15, 0.33, -0.12)
FACA_GUARDA_LAMINA = (-0.1, 0.3, 0.95)
FACA_GUARDA_E = (-0.12, 0.30, -0.04)
# na guarda o cotovelo fica para baixo e um pouco para fora, longe do tronco (Jhon: "o cotovelo da
# faca está para dentro": entrava na lateral do corpo): (fora, cima, trás)
FACA_COTOVELO = tuple(float(x) for x in os.environ.get('FACA_COTOVELO', '0.35,-1.0,0.0').split(','))
FACA_EIXOS_E = ((0.15, 0.6, 0.78), (0.6, 0.6, -0.5))
# Cada golpe: (tempo s, palma direita, orientação: 'guarda' ou o quadro k da primeira pessoa,
# giro do tronco (+ para a esquerda dela), inclinação (+ para a frente), palma esquerda ou 'guarda').
# Os tempos são os da primeira pessoa: a lâmina no extremo no quadro 4 (0,133 s) e de volta à
# guarda no quadro 11 (0,367 s); o dano sai no clique e o clipe dura 0,40 s.
FACA_GOLPES = {
    # estocada: recolhe a mão para o lado da cintura com a ponta para a frente e empurra reto, o
    # tronco gira o ombro direito para a frente e o peso vai junto; a esquerda recua para o peito
    'golpe': [(0.00, 'guarda', 'guarda', 0.0, 0.0, 'guarda'),
              (0.07, (0.20, 0.16, -0.27), 4, -12.0, -2.0, (-0.10, 0.38, -0.02)),
              (0.133, (0.04, 0.62, -0.20), 4, 18.0, 10.0, (-0.14, 0.14, -0.10)),
              (0.18, (0.05, 0.59, -0.20), 4, 16.0, 9.0, (-0.14, 0.15, -0.10)),
              (0.367, 'guarda', 'guarda', 0.0, 0.0, 'guarda')],
    # estocada alta: a mesma, na altura do peito do outro
    'golpe4': [(0.00, 'guarda', 'guarda', 0.0, 0.0, 'guarda'),
               (0.07, (0.24, 0.14, -0.12), 4, -12.0, -2.0, (-0.10, 0.38, 0.00)),
               (0.133, (0.06, 0.60, 0.00), 4, 15.0, 5.0, (-0.14, 0.14, -0.08)),
               (0.18, (0.07, 0.57, -0.01), 4, 13.0, 4.0, (-0.14, 0.15, -0.08)),
               (0.367, 'guarda', 'guarda', 0.0, 0.0, 'guarda')],
    # corte (de revés, do alto à esquerda para baixo à direita): a mão recua para a frente do peito,
    # no meio, com o cotovelo dobrado e a lâmina já para fora (a orientação de lá do quadro 3; a do
    # quadro 2 apontava a lâmina para o pescoço; mais para a esquerda o braço de cima afundava 6 cm
    # no peito), o tronco gira para a esquerda, corta atravessando o corpo
    # e termina embaixo à direita (o tronco gira para a direita e inclina); a esquerda sai do
    # caminho, para o lado da costela, e volta para a guarda
    'golpe2': [(0.00, 'guarda', 'guarda', 0.0, 0.0, 'guarda'),
               (0.07, tuple(float(x) for x in os.environ.get('FACA_RECUO2', '0.0,0.50,0.0').split(',')), 3, 18.0, 2.0, (-0.24, 0.14, -0.26)),
               (0.133, (0.08, 0.56, -0.14), 4, 0.0, 6.0, (-0.22, 0.16, -0.22)),
               (0.18, (0.30, 0.42, -0.30), 4, -18.0, 9.0, (-0.14, 0.24, -0.12)),
               (0.367, 'guarda', 'guarda', 0.0, 0.0, 'guarda')],
    # corte deitado (de revés, na altura do peito, da esquerda para a direita)
    'golpe3': [(0.00, 'guarda', 'guarda', 0.0, 0.0, 'guarda'),
               (0.07, tuple(float(x) for x in os.environ.get('FACA_RECUO3', '0.0,0.50,-0.10').split(',')), 3, 18.0, 1.0, (-0.24, 0.14, -0.26)),
               (0.133, (0.05, 0.56, -0.13), 5, 0.0, 3.0, (-0.22, 0.16, -0.22)),
               (0.18, (0.32, 0.40, -0.14), 5, -18.0, 4.0, (-0.14, 0.24, -0.12)),
               (0.367, 'guarda', 'guarda', 0.0, 0.0, 'guarda')],
}
# O saque da faca: a mão vai à bainha no quadril direito, puxa a faca para cima com a lâmina para
# baixo (FACA_BAINHA, até FACA_PUXA s) e a gira no caminho até a guarda; a esquerda sobe da pose
# solta para a guarda; a cabeça olha de leve para a mão (olhar_a_arma).
FACA_BAINHA = tuple(float(x) for x in os.environ.get('FACA_BAINHA', '0.33,0.08,-0.54').split(','))
FACA_BAINHA_SAI = (0.26, 0.12, -0.42)
FACA_PUXA = 0.12
# A troca de pente na recarga (Copia.trocar_pente). No peito: x direita, y frente, z cima.
RECARGA_TROCA = os.environ.get('RECARGA_TROCA', '1') == '1'
# onde fica o meio do pente novo quando a mão o pega no bolso (o lado esquerdo do quadril)
RECARGA_BOLSO = tuple(float(x) for x in os.environ.get('RECARGA_BOLSO', '-0.25,0.08,-0.43').split(','))
# O saque da granada vem do bolso (Jhon, 23/11: "no terceira pessoa, a Eve deve tirar a granada do bolso também, igual
# às outras armas"): a palma no bolso da frente da direita (no peito: x direita, y frente, z cima), os dedos para baixo
# e a palma para a coxa, segurando a granada; sobe até a pegada (Copia.sacar_do_bolso).
GRANADA_BOLSO = tuple(float(x) for x in os.environ.get('GRANADA_BOLSO', '0.22,0.10,-0.45').split(','))
GRANADA_BOLSO_EIXOS = ((0.0, 0.25, -1.0), (-1.0, 0.3, 0.0))
# quanto o pente já saiu do encaixe quando a mão o solta (m)
RECARGA_SOLTA = float(os.environ.get('RECARGA_SOLTA', 0.22))
# quanto a mão puxa o pente para cima antes de levá-lo à arma (m)
RECARGA_PUXA = 0.07
# o cotovelo indo ao bolso: para trás (fora, cima, trás), e não aberto de mão no quadril
RECARGA_COTOVELO = (0.25, -0.2, 1.0)
# quantos quadros a mão empurra o pente para dentro, e de quão longe do encaixe (m, ao longo do
# pente; lá ele para uns 7 cm abaixo e entra em dois quadros)
RECARGA_EMPURRA = 3
RECARGA_ENTRA = 0.12
# Quanto a mão que segura a granada abre (0 = a pegada de lá; 1 = a mão em repouso): a granada de
# fora é 1,4 vez maior (Jhon, 23/11: "aumente o tamanho da granada")
GRANADA_MAO_ABRE = float(os.environ.get('GRANADA_MAO_ABRE', 0.3))
# Quanto a granada da mão cresce no jogo em volta do meio dela (HeldWeapon.GrenadeHeldGrow — os dois têm de ser iguais):
# o que encosta nela (o dedo na argola) é posto onde ela fica crescida.
GRANADA_CRESCE = float(os.environ.get('GRANADA_CRESCE', 1.4))
# Quanto do giro do braço de cima em volta de si o osso de torção desfaz perto do ombro (1 = todo:
# a pele afinava na braçadeira, metade num osso e metade no outro a 130°; 0,6 fica liso)
TORCAO_PARTE = float(os.environ.get('TORCAO_PARTE', 0.6))
# Até quanto o punho dobra quando a mão vai na linha do antebraço (graus).
PUNHO_ALINHADO = float(os.environ.get('PUNHO_ALINHADO', 6.0))
SEGUE_GRANADA = (-0.06, 0.42, -0.36)
MAO_SEGUE_GRANADA = ((-0.15, 0.35, -0.92), (-0.95, -0.15, 0.1))
BRACO_E_FIXO_NO_LANCE = os.environ.get('BRACO_E_FIXO_NO_LANCE', '1') == '1'
PUNHO_PISTOLA = float(os.environ.get('PUNHO_PISTOLA', 8.0))
# Quanto a pistola pode girar na mira (graus; o cano sai da linha do olho este tanto).
OMBRO_MIRA_PISTOLA = float(os.environ.get('OMBRO_MIRA_PISTOLA', 26.0))
LIMITE_GIRO_MIRA = float(os.environ.get('LIMITE_GIRO_MIRA', 3.0))
# até quanto a pistola pode descer (m) para o punho ficar reto
DESCE_PISTOLA = float(os.environ.get('DESCE_PISTOLA', 0.14))
# A pistola reta (Jhon, 23/11: "pulso e arma precisam estar retas no 3 pessoa"): o cano na
# horizontal e os dois punhos retos (não com a dobra da primeira pessoa, que na pegada da P9 é de
# ~50°, o antebraço vindo de baixo). A arma não gira: anda (frente, lado, altura) até os dois
# punhos ficarem até PUNHO_RETO graus do reto.
PISTOLA_RETA = os.environ.get('PISTOLA_RETA', '1') == '1'
PUNHO_RETO = float(os.environ.get('PUNHO_RETO', 8.0))
COTOVELO_BAIXO = tuple(float(x) for x in os.environ.get('COTOVELO_BAIXO', '0.25,-1.0,0.0').split(','))


def nomes_de_mao(s):
    """Os ossos que copiam a primeira pessoa: os metacarpos e os dedos."""
    out = []
    for d in mt.DEDOS4:
        out += [f'{d}_metacarpal_{s}', f'{d}_01_{s}', f'{d}_02_{s}', f'{d}_03_{s}']
    out += [f'thumb_01_{s}', f'thumb_02_{s}', f'thumb_03_{s}']
    return out


def dados_do_glb_novo(glb):
    """De cada osso da mão no .glb novo: o pai, a matriz do osso no Blender e
    a que leva o osso do Blender ao nó do glTF (como o Corpo faz)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    antes = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=glb)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o not in antes)
    repouso = pa.gltf_repouso(glb)
    out = {}
    nomes = set(sum((nomes_de_mao(s) for s, _ in LADOS), []))
    for n, (mundo, pai) in repouso.items():
        osso = arm.data.bones.get(n)
        if osso is None:
            continue
        out[n] = dict(pai=pai, ml=osso.matrix_local.copy(), acerto=osso.matrix_local.inverted() @ EIXOS.inverted() @ mundo,
                      lr=(repouso[pai][0].inverted() @ mundo) if pai in repouso else mundo)
    # a mão de cada lado é a do Mixamo: o pai dos metacarpos
    return out, nomes


def qe_do_original(corpo, manny, s):
    """O mesmo alinhamento do mao_manny_tpose (pulso ancorado), com os nomes do Mixamo."""
    lado = 'Right' if s == 'r' else 'Left'
    c = {f'hand_{s}': corpo.arm.data.bones[corpo.n(lado + 'Hand')].head_local.copy(),
         f'middle_01_{s}': corpo.arm.data.bones[corpo.n(f'{lado}HandMiddle1')].head_local.copy(),
         f'index_01_{s}': corpo.arm.data.bones[corpo.n(f'{lado}HandIndex1')].head_local.copy(),
         f'pinky_01_{s}': corpo.arm.data.bones[corpo.n(f'{lado}HandPinky1')].head_local.copy()}

    def quadro_mao(cc):
        ao_longo = cc[f'middle_01_{s}'] - cc[f'hand_{s}']
        travessa = cc[f'index_01_{s}'] - cc[f'pinky_01_{s}']
        dentro = travessa.cross(ao_longo) if s == 'r' else ao_longo.cross(travessa)
        return orient(ao_longo, dentro)
    Qa, Qm = quadro_mao(c), quadro_mao(manny['cab'])
    return Matrix.Translation(c[f'hand_{s}']) @ (Qa @ Qm.inverted()).to_4x4() @ Matrix.Translation(-manny['cab'][f'hand_{s}'])


class Copia:
    def __init__(self, personagem, arma):
        self.personagem, self.arma = personagem, arma
        self.glb = os.environ.get('HUMANO_GLB') or os.path.join(RAIZ, 'assets/personagens', personagem, 'humano.glb')
        self.original = os.path.join(mm.COPIAS, f'humano_{personagem}_antes_manny.glb')

    def preparar(self):
        self.novo, self.nomes = dados_do_glb_novo(self.glb)
        # confere: a rotação de repouso que a conta dá é a do nó
        pior = 0.0
        for n in self.nomes:
            d = self.novo[n]
            pai = d['pai']
            desvio = self.novo[pai]['ml'].inverted() @ d['ml']
            lr = self.novo[pai]['acerto'].inverted() @ desvio @ d['acerto']
            ref = d['lr']
            pior = max(pior, math.degrees(lr.to_quaternion().rotation_difference(ref.to_quaternion()).angle))
        print(f'COPIA a conta de repouso bate com o nó do glTF: pior {pior:.4f}°', flush=True)
        manny = mt.importar_manny()
        self.manny = manny
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.render.fps = aa.FPS
        self.corpo = aa.Corpo(self.original)
        self.rig = aa.Conjunto(self.arma, 1.0)
        self.rig.colocar(Matrix.Identity(4))
        # o alinhamento da mão de lá (a do conjunto, no mundo dele) com a daqui
        cab_fp = {b.name: self.rig.arm.matrix_world @ b.head_local for b in self.rig.arm.data.bones}
        self.Qe = {}
        for s, lado in LADOS:
            self.Qe[lado] = qe_do_original(self.corpo, dict(cab=cab_fp), s)
        if aa.ARMAS[self.arma] == 'pistola' and OMBRO_MIRA_PISTOLA is not None:
            # na mira os ombros sobem (os braços vêm do ombro mais alto, menos inclinados até
            # a arma na altura do olho; o punho fica reto)
            d_ = dict(aa.POSTURA['pistola']['mira'])
            d_['ombro_d'] = (OMBRO_MIRA_PISTOLA, d_['ombro_d'][1])
            d_['ombro_e'] = (OMBRO_MIRA_PISTOLA, d_['ombro_e'][1])
            aa.POSTURA['pistola']['mira'] = d_
        self.plano = aa.Plano(self.corpo, self.rig, self.arma)
        self.ger = mm.Gerador(self.corpo, self.rig, self.plano, self.Qe, {}, None)
        for br in self.ger.braco.values():
            br.peso_torcao = PESO_TORCAO
            br.torcao_livre = TORCAO_LIVRE
            br.peso_cotovelo = PESO_COTOVELO
        # os eixos dos ossos daqui e de lá: iguais, a menos da rotação global do alinhamento
        # (cada osso daqui = uma rotação só (a mesma em todos) vezes o de lá)
        fp = {b.name: (self.rig.arm.matrix_world @ b.matrix_local).to_3x3().normalized() for b in self.rig.arm.data.bones}
        for s, lado in LADOS:
            rs = []
            for n in self.nomes:
                if n.endswith('_' + s):
                    rs.append((self.novo[n]['ml'].to_3x3().normalized() @ fp[n].inverted()).to_quaternion())
            media = rs[0]
            pior = max(math.degrees(media.rotation_difference(q).angle) for q in rs)
            print(f'COPIA {lado}: os eixos dos {len(rs)} ossos da mão daqui contra os de lá: a rotação entre eles varia até {pior:.2f}°', flush=True)

    def torcao_do_braco(self, locais, lado, nt):
        """A rotação local do osso de torção: o giro do braço de cima em volta do próprio eixo
        (Y do osso), desfeito. A rotação local do braço é a do Blender (o mesmo osso nos dois glb)."""
        corpo = self.corpo
        nb = corpo.n(lado + 'Arm')
        q = locais.get(nb, Quaternion())
        giro = 2.0 * math.atan2(q.y, q.w)
        giro = (giro + math.pi) % (2 * math.pi) - math.pi
        desfaz = Quaternion((0.0, 1.0, 0.0), -giro * TORCAO_PARTE).to_matrix()
        # do eixo do braço para o do osso de torção (no repouso, os dois têm os mesmos eixos)
        A = self.novo[nb]['ml'].to_3x3().normalized().inverted() @ self.novo[nt]['ml'].to_3x3().normalized()
        return (A.inverted() @ desfaz @ A).to_quaternion()

    def posicao_do_jogo(self, nome, delta, t):
        """A posição local (no espaço do nó do glTF) de um osso da mão: a de repouso
        mais o deslize que a primeira pessoa dá a ele."""
        d = self.novo[nome]
        pai = d['pai']
        desvio = self.novo[pai]['ml'].inverted() @ d['ml']
        m = self.novo[pai]['acerto'].inverted() @ desvio @ Matrix.Translation(t) @ delta.to_matrix().to_4x4() @ d['acerto']
        return m.translation.copy()

    def quatérnio_do_jogo(self, nome, delta):
        """A rotação local (no espaço do nó do glTF) de um osso da mão, dada a
        rotação local do osso do Blender (o delta em relação ao repouso)."""
        d = self.novo[nome]
        pai = d['pai']
        desvio = self.novo[pai]['ml'].inverted() @ d['ml']
        m = self.novo[pai]['acerto'].inverted() @ desvio @ delta.to_matrix().to_4x4() @ d['acerto']
        q = m.to_quaternion().normalized()
        return -q if q.w < 0 else q

    def rolar_braco(self, M, locais, br, lado):
        """O braço de cima gira em volta do próprio eixo (ROLO_BRACO graus; o esquerdo ao
        contrário) sem tirar o cotovelo nem o pulso do lugar: o antebraço e a mão ficam
        com a orientação que tinham (Jhon, 23/11: "o braço deve girar um pouco, veja que está
        torcido no início")."""
        graus = ROLO_BRACO if lado == 'Right' else -ROLO_BRACO
        if abs(graus) < 1e-6:
            return
        corpo = self.corpo
        lo = M[br.lo].to_3x3().normalized()
        ha = M[br.ha].to_3x3().normalized()
        up = M[br.up].to_3x3().normalized()
        eixo = (M[br.lo].translation - M[br.up].translation).normalized()
        corpo.esq.por(M, locais, br.up, Quaternion(eixo, math.radians(graus)).to_matrix() @ up)
        corpo.esq.por(M, locais, br.lo, lo)
        corpo.esq.por(M, locais, br.ha, ha)

    def _quadro_fp(self, clipe, k, col):
        """A palma direita da primeira pessoa no quadro k do clipe (matriz no mundo)."""
        self.rig.tocar(clipe, k)
        self.rig.colocar(col)
        fp = self.rig.mao('Right')
        return Matrix.Translation(fp['palma']) @ orient(fp['ao_longo'], fp['dentro']).to_4x4()

    def _guarda(self, L):
        """A palma direita na guarda (matriz no mundo), a partir da guarda de lá: girada em volta
        do antebraço até a lâmina ficar o mais perto de FACA_GUARDA_LAMINA."""
        corpo = self.corpo
        F0 = self._fp_guarda
        if not hasattr(self, '_giro_guarda'):
            quer = (aa.quadro_mira(corpo) @ Vector(FACA_GUARDA_LAMINA)).normalized()
            eixo = self._dedos_guarda.normalized()
            melhor = None
            for g in range(-180, 181):
                r = Quaternion(eixo, math.radians(g)).to_matrix()
                d = (r @ self._lamina_guarda).angle(quer)
                if melhor is None or d < melhor[0]:
                    melhor = (d, r)
            self._giro_guarda = melhor[1]
        return Matrix.Translation(aa.no_peito(corpo, L, FACA_GUARDA)) @ (self._giro_guarda @ F0.to_3x3()).to_4x4()

    def _esquerda_guarda(self, L, ponto=None):
        p, r = aa.quadro_mao(self.corpo, L, ponto or FACA_GUARDA_E, FACA_EIXOS_E)
        return p, r

    def faca(self, nome, quadros, n):
        """A faca vista de fora (Jhon, 23/11: "corrija esses pontos"): a guarda, os quatro golpes e
        o saque. A mão vai para onde a chave do golpe manda (curva suave pelos pontos), com a
        orientação de lá no extremo; o conjunto vai junto (a faca fica presa na pegada de lá). O
        tronco e a esquerda acompanham."""
        corpo, rig = self.corpo, self.rig
        col_parado = self.plano.colocacoes['quadril']
        if not hasattr(self, '_fp_guarda'):
            self._fp_guarda = self._quadro_fp('parado', 0, col_parado)
            self._lamina_guarda = rig.eixos_arma()[1].copy()
            self._dedos_guarda = rig.mao('Right')['ao_longo'].copy()
        chaves = FACA_GOLPES.get(nome)
        ori_fp = {}
        if chaves:
            for c in chaves:
                if not isinstance(c[2], str):
                    ori_fp[c[2]] = self._quadro_fp(nome, c[2], quadros[min(c[2], n)]['col']).to_3x3()
        for k, q in enumerate(quadros):
            t = k / aa.FPS
            L = q['locais']
            q['forma_d'], q['forma_e'] = 'punho', 'aberta'
            # o tronco primeiro (ele leva o meio dos ombros, de onde os pontos são medidos)
            if chaves:
                _, g, i, _ = self._chave_faca(chaves, t, L, ori_fp)
                aa.girar_tronco(corpo, L, g, i)
            F_fp = self._quadro_fp(nome, k, q['col'])
            if chaves:
                F_t, g, i, pe = self._chave_faca(chaves, t, L, ori_fp)
            elif nome == 'sacar':
                F_t, g, i, pe = self._saque_faca(k, n, L, F_fp)
            else:
                # parado e andar: a guarda, com o balanço de lá por cima
                F_t = self._guarda(L) @ self._fp_guarda.inverted() @ F_fp
                g, i, pe = 0.0, 0.0, None
            q['col'] = F_t @ F_fp.inverted() @ q['col']
            if nome == 'sacar':
                self.olhar_a_arma(q, aa.suave(k / max(1, n)))
            pe_, re_ = self._esquerda_guarda(L, pe if isinstance(pe, tuple) else None)
            peso_e = 1.0
            if nome == 'sacar':
                peso_e = aa.suave((k / max(1, n) - 0.2) / 0.8)
                M = corpo.esq.fk(L)
                Se = M[corpo.n('LeftArm')].translation
                _, rs_ = aa.quadro_mao(corpo, L, (0.0, 0.0, 0.0), SACAR_EIXOS_E)
                ps_ = Se + (aa.quadro_mira(corpo) @ Vector(SACAR_BRACO_E)).normalized() * (self.ger.braco['Left'].alcance() * SACAR_ESTICA + SACAR_PALMA_E)
                pe_ = ps_.lerp(pe_, peso_e)
                re_ = rs_.to_quaternion().slerp(re_.to_quaternion(), peso_e).to_matrix()
                q['solto_e'] = 1.0 - peso_e
                q['solto_d'] = 1.0 - aa.suave(k / max(1, n) / 0.5)
            q['esquerda'] = ('quadro', pe_, 1.0, re_)
            q['relaxa_e'] = 1.0
            q['guarda_e'] = peso_e
            if chaves:
                # longe da guarda (no golpe) o cotovelo vai onde o movimento pede
                q['guarda_d'] = self._perto_da_guarda(chaves, t)
            elif nome == 'sacar':
                q['guarda_d'] = aa.suave((k / max(1, n) - 0.5) / 0.5)
            else:
                q['guarda_d'] = 1.0

    def _perto_da_guarda(self, chaves, t):
        """1 na guarda, 0 a partir da chave seguinte (o golpe), misturado no caminho."""
        primeira, ultima = chaves[1][0], chaves[-2][0]
        if t <= primeira:
            return 1.0 - aa.suave(t / primeira)
        if t >= ultima:
            return aa.suave((t - ultima) / max(1e-6, chaves[-1][0] - ultima))
        return 0.0

    def _chave_faca(self, chaves, t, L, ori_fp):
        """A palma, o tronco e a esquerda no tempo t de um golpe: a curva passa pelos pontos
        (Catmull-Rom), a orientação gira de uma chave para a outra."""
        corpo = self.corpo
        G = self._guarda(L)

        def ponto(c):
            return G.translation if c[1] == 'guarda' else aa.no_peito(corpo, L, c[1])

        def ori(c):
            return G.to_3x3() if c[2] == 'guarda' else ori_fp[c[2]]

        def esq(c):
            return FACA_GUARDA_E if c[5] == 'guarda' else c[5]
        j = 0
        while j < len(chaves) - 2 and t > chaves[j + 1][0]:
            j += 1
        a, b = chaves[j], chaves[j + 1]
        u = max(0.0, min(1.0, (t - a[0]) / max(1e-6, b[0] - a[0])))
        us = aa.suave(u)
        p0 = ponto(chaves[max(0, j - 1)])
        p1, p2 = ponto(a), ponto(b)
        p3 = ponto(chaves[min(len(chaves) - 1, j + 2)])
        # Catmull-Rom no parâmetro suavizado (sai e chega devagar em cada chave)
        x = us
        pos = 0.5 * ((2 * p1) + (-p0 + p2) * x + (2 * p0 - 5 * p1 + 4 * p2 - p3) * x * x + (-p0 + 3 * p1 - 3 * p2 + p3) * x ** 3)
        rot = ori(a).to_quaternion().slerp(ori(b).to_quaternion(), us).to_matrix()
        g = a[3] + (b[3] - a[3]) * us
        i = a[4] + (b[4] - a[4]) * us
        ea, eb = Vector(esq(a)), Vector(esq(b))
        pe = tuple(ea.lerp(eb, us))
        return Matrix.Translation(pos) @ rot.to_4x4(), g, i, pe

    def _saque_faca(self, k, n, L, F_fp):
        """O saque: da bainha no quadril direito, a lâmina para baixo, até a guarda."""
        corpo = self.corpo
        t = k / aa.FPS
        G = self._guarda(L)
        # a lâmina para baixo na bainha: a guarda virada até a ponta apontar para o chão
        lamina_g = G.to_3x3() @ (self._fp_guarda.to_3x3().inverted() @ self._lamina_guarda)
        para_baixo = lamina_g.rotation_difference(-pa.CIMA).to_matrix() @ G.to_3x3()
        if t <= FACA_PUXA:
            u = aa.suave(t / FACA_PUXA)
            pos = aa.no_peito(corpo, L, FACA_BAINHA).lerp(aa.no_peito(corpo, L, FACA_BAINHA_SAI), u)
            rot = para_baixo
        else:
            u = aa.suave((t - FACA_PUXA) / max(1e-6, n / aa.FPS - FACA_PUXA))
            a_ = aa.no_peito(corpo, L, FACA_BAINHA_SAI)
            meio = aa.no_peito(corpo, L, (0.30, 0.26, -0.24))
            b_ = G.translation
            pos = a_ * (1 - u) ** 2 + meio * (2 * u * (1 - u)) + b_ * u * u
            rot = para_baixo.to_quaternion().slerp(G.to_3x3().to_quaternion(), u).to_matrix()
        return Matrix.Translation(pos) @ rot.to_4x4(), 0.0, 0.0, None

    def fases_do_pente(self, nome, n):
        """A recarga de lá, medida (em metros: as matrizes do conjunto têm a escala 0,01 dele, e
        sem o mm.quadro as distâncias saíam em centímetros): quando o pente começa a sair, se a
        mão de lá o segura nessa hora, quando encaixa de novo, e desde quando ele fica rígido na
        mão de lá (a pegada). Lá a mão tira o pente, desce com ele até fora da câmera (uns 40 cm)
        e volta com o mesmo; na recarga de pente vazio da pistola a mão sai antes e o pente é
        ejetado e cai sozinho."""
        rig = self.rig
        rig.colocar(Matrix.Identity(4))
        rig.tocar(nome, 0)
        g0 = mm.quadro(rig.m('ik_hand_gun')).inverted() @ mm.quadro(rig.m('Magazine'))
        desloc, na_mao = [], []
        for k in range(n + 1):
            rig.tocar(nome, k)
            rig.colocar(Matrix.Identity(4))
            rel = mm.quadro(rig.m('ik_hand_gun')).inverted() @ mm.quadro(rig.m('Magazine'))
            desloc.append((rel.translation - g0.translation).length)
            na_mao.append(mm.quadro(rig.m('hand_l')).inverted() @ mm.quadro(rig.m('Magazine')))
        fora = [k for k, d in enumerate(desloc) if d > 0.01]
        if not fora:
            return None
        k_agarra, k_encaixa = fora[0], fora[-1] + 1
        # a pegada: logo antes do encaixe a mão de lá com certeza segura o pente novo; para trás,
        # desde quando ele está rígido nela
        k_ref = k_encaixa - 1

        def rigido(k):
            d = na_mao[k].inverted() @ na_mao[k_ref]
            return d.translation.length < 0.03 and math.degrees(d.to_quaternion().angle) < 20.0
        k_rigido = k_ref
        while k_rigido > k_agarra and rigido(k_rigido - 1):
            k_rigido -= 1
        # a mão de lá está no pente quando ele sai? (perto dele como na pegada; na recarga de
        # pente vazio da pistola ela já foi buscar o outro, e o pente é ejetado e cai sozinho)
        perto = na_mao[k_ref].translation.length
        segura = abs(na_mao[k_agarra].translation.length - perto) < 0.08
        if segura:
            # solta quando já o puxou RECARGA_SOLTA m para fora do encaixe (nas recargas curtas,
            # metade do que a mão de lá o tira)
            limite = min(RECARGA_SOLTA, 0.5 * max(desloc))
            k_solta = next((k for k in fora if desloc[k] > limite), k_agarra + 3)
            k_sai = k_solta
        else:
            k_solta = k_agarra
            # a mão saiu da arma antes: o último quadro em que ainda estava no lugar
            k_sai = 0
            while k_sai < k_agarra and abs(na_mao[k_sai + 1].translation.length - na_mao[0].translation.length) < 0.03:
                k_sai += 1
        return dict(agarra=k_agarra, solta=k_solta, sai=k_sai, encaixa=k_encaixa, rigido=k_rigido, ref=k_ref, segura=segura)

    def punho_no_bolso(self, nome, quadros, k_pega):
        """Onde o punho esquerdo fica (matriz, no peito) pegando o pente novo no bolso. Sai da
        pegada de lá: o pente fica de pé ao lado do quadril (o meio dele em RECARGA_BOLSO), a mão
        o segura como a de lá o segura nesse quadro, e de todos os jeitos de pôr o pente de pé
        (boca para cima ou para baixo, virado em volta da vertical) fica o que deixa o braço
        mais à vontade (o punho menos dobrado, o braço de cima menos girado)."""
        corpo, rig, ger = self.corpo, self.rig, self.ger
        Q = aa.quadro_mira(corpo)
        mag = next(pc for pc in rig.pecas if 'mag' in pc['osso'].lower())
        # a boca do pente (o lado que entra na arma) e o meio dele, no osso: encaixado, os pontos
        # mais altos no "cima" da arma
        self.tocar(nome, 0)
        rig.colocar(quadros[0]['col'])
        cima = rig.eixos_arma()[3]
        m0 = mm.quadro(rig.m(mag['osso']))
        pts = [rig.m(mag['osso']) @ p_ for p_ in mag['amostra']]
        alturas = sorted(p_.dot(cima) for p_ in pts)
        corte = alturas[int(len(alturas) * 0.85)]
        boca = [p_ for p_ in pts if p_.dot(cima) >= corte]
        centro_l = m0.inverted() @ (sum(pts, Vector()) / len(pts))
        eixo_l = (m0.inverted().to_3x3() @ ((sum(boca, Vector()) / len(boca)) - (sum(pts, Vector()) / len(pts)))).normalized()
        # a pegada de lá logo antes do encaixe (a mão com certeza segura o pente): o pente em
        # relação ao punho daqui
        k_ref = k_pega
        q = quadros[k_ref]
        self.tocar(nome, k_ref)
        rig.colocar(q['col'])
        punho_fp = mm.quadro(rig.m('hand_l')) @ ger.K[('Left', 'Hand')]
        pente_na_mao = punho_fp.inverted() @ mm.quadro(rig.m(mag['osso']))
        L = quadros[k_ref]['locais']
        M = corpo.esq.fk(L)
        centro = (M[corpo.n('RightArm')].translation + M[corpo.n('LeftArm')].translation) * 0.5
        alvo_centro = aa.no_peito(corpo, L, RECARGA_BOLSO)
        br = ger.braco['Left']
        melhor = None
        for sentido in (1.0, -1.0):
            # de pé, ou inclinado para a frente, para trás ou para fora (como sai de um bolso)
            for inclina in ((0.0, 0.0), (0.45, 0.0), (-0.45, 0.0), (0.0, 0.45), (0.45, 0.45), (-0.45, 0.45), (0.8, 0.0), (0.8, 0.45)):
                quer = (Q @ Vector((-inclina[1], inclina[0], sentido))).normalized()
                base = (m0.to_3x3() @ eixo_l).rotation_difference(quer).to_matrix() @ m0.to_3x3()
                tombo = math.degrees(quer.angle(pa.CIMA * sentido))
                for graus in range(0, 360, 10):
                    Rm = Quaternion(quer, math.radians(graus)).to_matrix() @ base
                    Mm = Matrix.Translation(alvo_centro - Rm @ centro_l) @ Rm.to_4x4()
                    W = Mm @ pente_na_mao.inverted()
                    r = br.avaliar(M, W.translation, W.to_3x3().normalized(), RECARGA_COTOVELO, corpo.mao['Left'], 0.03, 0.0, alvo=(0.0, 0.0))
                    # a mão fica do lado de fora do quadril (não entre o pente e o corpo, nem dentro dele)
                    fora = (W.translation - alvo_centro).dot(Q @ Vector((-1.0, 0.3, 0.0)))
                    custo = r['custo'] + max(0.0, 0.02 - fora) * 4000 + tombo * 0.6
                    if melhor is None or custo < melhor[0]:
                        melhor = (custo, W, r, sentido, graus, tombo)
        custo, W, r, sentido, graus, tombo = melhor
        print(f'PENTE {nome}: no bolso a boca fica para {"cima" if sentido > 0 else "baixo"}, inclinado {tombo:.0f}°, virado {graus}°; '
              f'punho dobra {r["dobra"]:.0f}° e desvia {r["lado"]:.0f}°', flush=True)
        if os.environ.get('DEPURAR_PENTE'):
            print(f'PENTE {nome}: punho no bolso (peito) {tuple(round(x, 2) for x in (Q.inverted() @ (W.translation - centro)))}; '
                  f'meio do pente {tuple(round(x, 2) for x in (Q.inverted() @ (alvo_centro - centro)))}; '
                  f'pente na mão (m) {tuple(round(x, 2) for x in pente_na_mao.translation)}; meio no osso {tuple(round(x, 3) for x in centro_l)}; custo {custo:.0f}', flush=True)
        return Q.inverted() @ (W.translation - centro), Q.inverted() @ W.to_3x3().normalized()

    def trocar_pente(self, nome, quadros, n):
        """A recarga vista de fora (Jhon, 23/11: "ela tira o carregador vazio e coloca o mesmo";
        "a Eve tira o pente, ele cai no chão (velocidade da gravidade), tira outro pente do
        bolso; o pente precisa estar nas mãos, orientado pela pegada da mão, veja a referência
        na recarga em primeira pessoa"). Da primeira pessoa vêm a saída do pente (a mão o tira
        como lá), a pegada (como a mão de lá o segura, rígida do quadro em que o agarra até o
        encaixe) e a pose do encaixe; o meio é daqui (lá a mão some para longe, veja
        fases_do_pente): a esquerda solta o pente quando ele já saiu do encaixe (ele cai), desce
        vazia e aberta até o bolso no lado esquerdo do quadril, pega o novo — de pé ao lado do
        quadril, seguro pela pegada de lá (punho_no_bolso) —, sobe até ficar embaixo do encaixe,
        já alinhada com ele, e o empurra para dentro nos últimos quadros, chegando na pose de lá.
        Os tempos (agarra, solta, pega, encaixa) vão no .json; o jogo prende o pente na mão do
        corpo pela pegada de lá, quadro a quadro."""
        corpo, rig, ger = self.corpo, self.rig, self.ger
        f = self.fases_do_pente(nome, n)
        if f is None:
            return
        k_solta, k_sai, k_encaixa = f['solta'], f['sai'], f['encaixa']
        # o tempo entre a mão sair e começar a empurrar o pente novo: 40% descendo ao bolso,
        # 15% nele, o resto subindo (a mão só pega o novo quando a pegada de lá já vale)
        k_pre = k_encaixa - RECARGA_EMPURRA
        total = k_pre - k_sai
        k_bolsa = k_sai + max(3, round(total * 0.40))
        k_pega = max(k_bolsa + max(2, round(total * 0.15)), f['rigido'])
        k_pega = min(k_pega, k_pre - 3)
        k_bolsa = min(k_bolsa, k_pega - 2)
        if not hasattr(self, '_tempos_pente'):
            self._tempos_pente = {}
        self._tempos_pente[nome] = [round(k / aa.FPS, 4) for k in (f['agarra'], k_solta, k_pega, k_encaixa)]
        print(f'PENTE {nome}: {"a mão tira o pente e o solta" if f["segura"] else "o pente é ejetado"} no quadro {k_solta}; '
              f'a mão sai no {k_sai}, chega ao bolso no {k_bolsa}, pega o novo no {k_pega}, encaixa no {k_encaixa} '
              f'(a pegada de lá é rígida desde o {f["rigido"]})', flush=True)
        Q = aa.quadro_mira(corpo)
        bolso = self.punho_no_bolso(nome, quadros, f['ref'])
        sobe = Q @ Vector((0.0, 0.0, RECARGA_PUXA))
        # o encaixe em relação à arma (ela mexe um pouco na recarga): o punho de lá no quadro do
        # encaixe e por onde o pente entra (a boca dele: de baixo para dentro do encaixe)
        mag = next(pc for pc in rig.pecas if 'mag' in pc['osso'].lower())
        self.tocar(nome, k_encaixa)
        rig.colocar(quadros[k_encaixa]['col'])
        arma_e = mm.quadro(rig.m('ik_hand_gun'))
        punho_na_arma = arma_e.inverted() @ (mm.quadro(rig.m('hand_l')) @ ger.K[('Left', 'Hand')])
        me = rig.m(mag['osso'])
        pts = [me @ p_ for p_ in mag['amostra']]
        cima = rig.eixos_arma()[3]
        alturas = sorted(p_.dot(cima) for p_ in pts)
        corte = alturas[int(len(alturas) * 0.85)]
        boca = [p_ for p_ in pts if p_.dot(cima) >= corte]
        entra_na_arma = arma_e.to_3x3().inverted() @ ((sum(boca, Vector()) / len(boca)) - (sum(pts, Vector()) / len(pts))).normalized()
        sai = None
        for k, q in enumerate(quadros):
            L = q['locais']
            M = corpo.esq.fk(L)
            centro = (M[corpo.n('RightArm')].translation + M[corpo.n('LeftArm')].translation) * 0.5
            self.tocar(nome, k)
            rig.colocar(q['col'])
            alvo_fp = mm.quadro(rig.m('hand_l')) @ ger.K[('Left', 'Hand')]
            if k <= k_sai:
                sai = (Q.inverted() @ (alvo_fp.translation - centro), Q.inverted() @ alvo_fp.to_3x3().normalized())
                continue
            if k >= k_encaixa:
                continue
            punho_bolso = centro + Q @ bolso[0]
            rot_bolso = Q @ bolso[1]
            arma_k = mm.quadro(rig.m('ik_hand_gun'))
            encaixe = arma_k @ punho_na_arma
            entra = (arma_k.to_3x3() @ entra_na_arma).normalized()
            if k <= k_bolsa:
                # a mão desce vazia e aberta até o bolso (um arco por fora)
                u = aa.suave((k - k_sai) / max(1, k_bolsa - k_sai))
                arco = (Q @ Vector((-1.0, 0.3, 0.0))) * (0.05 * math.sin(math.pi * u))
                pos = (centro + Q @ sai[0]).lerp(punho_bolso, u) + arco
                rot = (Q @ sai[1]).to_quaternion().slerp(rot_bolso.to_quaternion(), u).to_matrix()
                q['relaxa_e'] = math.sin(math.pi * min(1.0, (k - k_sai) / max(1, k_bolsa - k_sai))) * 0.85
                q['cotovelo_e'] = RECARGA_COTOVELO
            elif k <= k_pega:
                # fecha no pente novo e começa a puxá-lo
                u = aa.suave((k - k_bolsa) / max(1, k_pega - k_bolsa))
                pos = punho_bolso + sobe * (0.3 * u)
                rot = rot_bolso
                q['cotovelo_e'] = RECARGA_COTOVELO
            elif k <= k_pre:
                # puxa para cima e leva até embaixo do encaixe, já alinhado com ele
                u = aa.suave((k - k_pega) / max(1, k_pre - k_pega))
                de = punho_bolso + sobe * 0.3
                ate = encaixe.translation - entra * RECARGA_ENTRA
                pos = de * (1 - u) ** 2 + (punho_bolso + sobe) * (2 * u * (1 - u)) + ate * (u * u)
                rot = rot_bolso.to_quaternion().slerp(encaixe.to_3x3().normalized().to_quaternion(), aa.suave(min(1.0, u * 1.25))).to_matrix()
            else:
                # empurra para dentro: chega na pose de lá no quadro do encaixe
                u = aa.suave((k - k_pre) / max(1, k_encaixa - k_pre))
                pos = encaixe.translation - entra * (RECARGA_ENTRA * (1.0 - u))
                rot = encaixe.to_3x3().normalized()
            q['esquerda'] = ('punho', pos, 1.0, rot)

    def referencia(self, f):
        """O braço da referência no quadro f (fracionário): as direções misturadas e normalizadas."""
        if not hasattr(self, '_ref'):
            with open(LANCE_REF) as arq:
                dados = json.load(arq)
            self._ref = {r['q']: r for r in dados['quadros']}
            self._ref_fps = dados['fps']
        a = int(math.floor(f))
        u = f - a
        ra, rb = self._ref[max(min(self._ref), min(max(self._ref), a))], self._ref[max(min(self._ref), min(max(self._ref), a + 1))]
        out = {}
        for c in ('clavicula', 'cotovelo', 'punho', 'dedos', 'palma'):
            v = Vector(ra[c]).lerp(Vector(rb[c]), u)
            out[c] = v.normalized() if c in ('clavicula', 'dedos', 'palma') else v
        return out

    def tempo_do_lance(self, nome, t):
        """Onde a referência está (quadro, ou um par de quadros e a mistura entre eles) e quanto
        ela manda no braço direito (0 = a primeira pessoa, 1 = a referência), e quanto o braço já
        foi para a pose solta do fim."""
        if nome == 'armar':
            # a granada vem para a frente do peito (onde a esquerda puxa o pino) e, puxado o pino,
            # o braço desce, vai para trás e engatilha atrás da cabeça
            if t <= LANCE_PINO:
                return (REF_PINO, REF_PINO, 0.0), aa.suave(t / 0.12), 0.0
            u = min(1.0, (t - LANCE_PINO) / max(1e-6, self._fim_armar - LANCE_PINO))
            return (REF_PINO, REF_SAI + (REF_ARMADO - REF_SAI) * aa.suave(u), aa.suave(u / 0.3)), 1.0, 0.0
        if nome == 'armado':
            return REF_ARMADO, 1.0, 0.0
        solta = aa.LANCE[0]
        if t <= solta:
            f = REF_ARMADO + (REF_SOLTA - REF_ARMADO) * (t / solta) ** 1.3
        else:
            f = REF_SOLTA + (REF_SEGUE - REF_SOLTA) * (1 - (1 - min(1.0, (t - solta) / (LANCE_SEGUE - solta))) ** 1.6)
        return f, 1.0, aa.suave((t - LANCE_SOLTOS) / max(1e-6, self._fim_arremesso - LANCE_SOLTOS))

    def perto_do_armado(self, nome, t):
        """1 com a granada engatilhada (o fim do armar, o armado, o começo do lance), indo a 0
        enquanto o braço desce no armar e enquanto ele passa por cima no lance."""
        if nome == 'armado':
            return 1.0
        if nome == 'armar':
            u = (t - LANCE_PINO) / max(1e-6, self._fim_armar - LANCE_PINO)
            return aa.suave((u - 0.45) / 0.55)
        if nome == 'arremesso':
            return 1.0 - aa.suave(t / aa.LANCE[0])
        return 0.0

    def tronco_do_lance(self, nome, t):
        """O giro, a inclinação e o caimento de lado do tronco: ele começa antes do braço e
        termina depois."""
        ga, ia = TRONCO_ARMADO
        gl, il = TRONCO_LANCE
        la, ll = TRONCO_LADO
        if nome == 'armar':
            w = aa.suave((t - LANCE_PINO + 0.04) / max(1e-6, self._fim_armar - LANCE_PINO))
            return ga * w, ia * w, la * w
        if nome == 'armado':
            return ga, ia, la
        if t <= TRONCO_PUXA:
            w = aa.suave(t / TRONCO_PUXA)
            g, i, c = ga + (gl - ga) * w, ia + (il - ia) * w, la + (ll - la) * w
        else:
            w = aa.suave((t - TRONCO_PUXA) / max(1e-6, self._fim_arremesso - TRONCO_PUXA))
            g, i, c = gl * (1 - w), il * (1 - w), ll * (1 - w)
        return g, i, c

    def cair_de_lado(self, L, graus):
        """Inclina o tronco de lado (graus, + para a direita dela), repartido pelas três vértebras;
        a cabeça desfaz metade (continua olhando o alvo)."""
        if abs(graus) < 1e-3:
            return
        corpo = self.corpo
        if not hasattr(self, '_sinal_lado'):
            M = corpo.esq.fk(L)
            teste = dict(L)
            n_ = corpo.n('Spine2')
            corpo.esq.por(M, teste, n_, Quaternion(pa.FRENTE, math.radians(10)).to_matrix() @ M[n_].to_3x3())
            pescoco = corpo.esq.fk(teste)[corpo.n('Neck')].translation - M[corpo.n('Neck')].translation
            self._sinal_lado = 1.0 if pescoco.dot(pa.DIREITA) > 0 else -1.0
        M = corpo.esq.fk(L)
        q = Quaternion(pa.FRENTE, math.radians(self._sinal_lado * graus / 3))
        for nome in ('Spine', 'Spine1', 'Spine2'):
            n_ = corpo.n(nome)
            corpo.esq.por(M, L, n_, q.to_matrix() @ M[n_].to_3x3())
        volta = Quaternion(pa.FRENTE, math.radians(-self._sinal_lado * graus / 2))
        for nome in ('Neck', 'Head'):
            n_ = corpo.n(nome)
            corpo.esq.por(M, L, n_, volta.to_matrix() @ M[n_].to_3x3())

    def lance(self, nome, quadros, n, info):
        """A granada armada e lançada (Jhon, 23/11). O braço direito (clavícula, braço, cotovelo,
        punho e mão) segue a referência; os dedos são os da primeira pessoa segurando a granada
        e relaxam depois da soltura. O tronco gira o ombro direito para trás enquanto o braço
        engatilha e, no lance, gira para a frente antes do braço (o tronco puxa, o braço chicoteia),
        passa do alvo inclinado e volta. A esquerda puxa o pino (a primeira pessoa), aponta o alvo
        enquanto o braço engatilha e desce esticada junto com o lance. No fim os dois braços ficam
        soltos, na pose em que o saque da arma começa."""
        corpo, rig, ger = self.corpo, self.rig, self.ger
        G = aa.GRANADA
        self._fim_armar = rig.quadros('armar') / aa.FPS if 'armar' in rig.clipes() else 0.64
        self._fim_arremesso = rig.quadros('arremesso') / aa.FPS if 'arremesso' in rig.clipes() else 0.53
        Q = aa.quadro_mira(corpo)
        cla = corpo.n('RightShoulder')
        cla0 = self.referencia(1)['clavicula']
        if hasattr(self, '_pino_e'):
            del self._pino_e
        for k, q in enumerate(quadros):
            t = k / aa.FPS
            L = q['locais']
            q['forma_d'], q['forma_e'] = 'punho', None
            M0 = corpo.esq.fk(L)
            peito0 = M0[corpo.n('Spine2')].to_3x3().normalized()
            g, i, c = self.tronco_do_lance(nome, t)
            aa.girar_tronco(corpo, L, g, i, cabeca=0.85, cabeca_inclina=0.6)
            self.cair_de_lado(L, c)
            M = corpo.esq.fk(L)
            C = (M[corpo.n('Spine2')].to_3x3().normalized() @ peito0.inverted() @ Q).normalized()
            f, peso, solto = self.tempo_do_lance(nome, t)
            if isinstance(f, tuple):
                ra_, rb_ = self.referencia(f[0]), self.referencia(f[1])
                ref = {c: (ra_[c].lerp(rb_[c], f[2]).normalized() if c in ('clavicula', 'dedos', 'palma')
                           else ra_[c].lerp(rb_[c], f[2])) for c in ra_}
            else:
                ref = self.referencia(f)
            # a clavícula: o que a da referência anda desde o começo (de pé), no peito daqui
            if peso > 0.0:
                giro_c = Quaternion().slerp(cla0.rotation_difference(ref['clavicula']), peso)
                giro_c = (C @ giro_c.to_matrix() @ C.inverted()).to_quaternion()
                corpo.esq.por(M, L, cla, giro_c.to_matrix() @ M[cla].to_3x3())
                M = corpo.esq.fk(L)
            S = M[corpo.n('RightArm')].translation
            alcance = ger.braco['Right'].alcance()
            rig.tocar(nome, k)
            rig.colocar(q['col'])
            fp = rig.mao('Right')
            F_fp = Matrix.Translation(fp['palma']) @ orient(fp['ao_longo'], fp['dentro']).to_4x4()
            punho_fp = (mm.quadro(rig.m('hand_r')) @ ger.K[('Right', 'Hand')]).translation
            punho_na_palma = F_fp.inverted() @ punho_fp
            # o punho e a mão da referência, no tamanho do braço daqui
            W = S + C @ ref['punho'] * alcance
            # engatilhado, a mão vem para a frente, ao lado da orelha: atrás da cabeça o braço de
            # cima girava 130° em volta de si e a pele torcia (Jhon: "braço da granada torcendo")
            perto = self.perto_do_armado(nome, t)
            if perto > 0.0:
                W = W + C @ Vector(LANCE_ARMADO_ADIANTA) * (alcance * perto)
            fora = dentro = 0.0
            if nome == 'arremesso':
                fora = max(0.0, 1.0 - ((t - 0.13) / 0.11) ** 2)
                W = W + C @ Vector((LANCE_FORA * fora, 0.0, 0.0)) * alcance
                # o acompanhamento: sobe da soltura até o pico e volta (o fim é o braço solto)
                if t > aa.LANCE[0]:
                    u = (t - aa.LANCE[0]) / max(1e-6, LANCE_DENTRO_PICO - aa.LANCE[0])
                    dentro = aa.suave(u) if u <= 1.0 else 1.0 - aa.suave((t - LANCE_DENTRO_PICO) / max(1e-6, self._fim_arremesso - LANCE_DENTRO_PICO))
                    W = W + C @ Vector(LANCE_DENTRO) * (alcance * dentro)
            rot = orient(C @ ref['dedos'], C @ ref['palma'])
            if perto > 0.0:
                # engatilhada na frente, a mão vira: dedos para cima segurando a granada, a palma para
                # o rosto (LANCE_ARMADO_MAO)
                rot_p = orient(C @ Vector(LANCE_ARMADO_MAO[0]), C @ Vector(LANCE_ARMADO_MAO[1]))
                rot = rot.to_quaternion().slerp(rot_p.to_quaternion(), perto).to_matrix()
            if solto > 0.0:
                # e no fim, o braço solto (o mesmo começo do saque)
                W_s = S + (Q @ Vector(SOLTO_D)).normalized() * alcance * SACAR_ESTICA
                rot_s = orient(Q @ Vector(SOLTO_EIXOS_D[0]), Q @ Vector(SOLTO_EIXOS_D[1]))
                W = W.lerp(W_s, solto)
                rot = rot.to_quaternion().slerp(rot_s.to_quaternion(), solto).to_matrix()
            p_alvo = W - rot @ punho_na_palma
            pos = fp['palma'].lerp(p_alvo, peso)
            rot_b = F_fp.to_3x3().to_quaternion().slerp(rot.to_quaternion(), peso).to_matrix()
            q['col'] = (Matrix.Translation(pos) @ rot_b.to_4x4()) @ F_fp.inverted() @ q['col']
            # o cotovelo onde o da referência está (fora, cima, trás)
            if peso > 0.5:
                e = C @ ref['cotovelo']
                pref = (e.dot(pa.DIREITA) + 0.6 * fora + 0.4 * dentro, e.dot(pa.CIMA), -e.dot(pa.FRENTE))
                if solto > 0.0:
                    pref = tuple(a + (b - a) * solto for a, b in zip(pref, SACAR_COTOVELO))
                q['cotovelo_d'] = pref
            q['ref_d'] = peso
            q['solto_d'] = solto
            # a esquerda
            Se = M[corpo.n('LeftArm')].translation
            aponta = aa.quadro_mao(corpo, L, G['aponta'], G['mao_aponta_e'])
            _, rs_ = aa.quadro_mao(corpo, L, (0.0, 0.0, 0.0), SACAR_EIXOS_E)
            ps_ = Se + (Q @ Vector(SACAR_BRACO_E)).normalized() * (ger.braco['Left'].alcance() * SACAR_ESTICA + SACAR_PALMA_E)
            if nome == 'armar':
                # puxa o pino (a mão de lá, junto da granada) e vai apontar o alvo enquanto o braço
                # direito engatilha
                # Até o pino sair, a esquerda é a de lá (presa na granada). Depois ela se solta do
                # conjunto — que vai junto com a mão direita engatilhando e a arrastava para dentro
                # do corpo (8 cm no colete) — e vai, em arco pela frente, de onde estava (no peito)
                # até apontar o alvo.
                centro = (M[corpo.n('RightArm')].translation + M[corpo.n('LeftArm')].translation) * 0.5
                if t <= LANCE_PINO_SAI or not hasattr(self, '_pino_e'):
                    rig.colocar(q['col'])
                    alvo_e = mm.quadro(rig.m('hand_l')) @ ger.K[('Left', 'Hand')]
                    self._pino_e = (C.inverted() @ (alvo_e.translation - centro), C.inverted() @ alvo_e.to_3x3().normalized())
                    q['esquerda'] = None
                    # a ponta do indicador na de lá (resolvido junto com o braço)
                    q['pino_e'] = True
                    wa = 0.0
                else:
                    wa = aa.suave((t - LANCE_PINO_SAI) / 0.30)
                    mi = corpo.mao['Left']
                    quer = aponta[1] @ orient(mi['dedos'], mi['palma']).inverted()
                    punho_aponta = aponta[0] - quer @ (mi['nos'] * aa.ALINHA)
                    punho_pino = centro + C @ self._pino_e[0]
                    rot_pino = C @ self._pino_e[1]
                    arco = (Q @ Vector((0.0, 1.0, 0.0))) * (LANCE_ARCO_E * math.sin(math.pi * wa))
                    q['esquerda'] = ('punho', punho_pino.lerp(punho_aponta, wa) + arco, 1.0,
                                     rot_pino.to_quaternion().slerp(quer.to_quaternion(), wa).to_matrix())
                q['forma_e'] = 'aberta' if wa > 0.5 else None
                # solto o pino, a mão abre (em concha) para apontar
                q['relaxa_e'] = wa
                # e no começo vem de onde a pose parada a deixa (solta, em concha)
                wp = 1.0 - aa.suave((t - 0.02) / 0.10)
                if wp > 0.0:
                    solta_e = aa.quadro_mao(corpo, L, G['solta_e'], G['mao_solta_e'])
                    q['esquerda'] = ('quadro', solta_e[0], wp, solta_e[1])
                    q['relaxa_e'] = wp
                    q['pino_e'] = False
            elif nome == 'armado':
                q['esquerda'] = ('quadro', aponta[0], 1.0, aponta[1])
                q['forma_e'] = 'aberta'
                q['relaxa_e'] = 1.0
            else:
                # desce esticada junto com o lance, girando no ombro, até solta ao lado da coxa
                w_ = aa.suave(min(1.0, t / LANCE_SEGUE))
                da_, ds_ = aponta[0] - Se, ps_ - Se
                direcao = da_.normalized().slerp(ds_.normalized(), w_) if da_.angle(ds_) > 1e-4 else da_.normalized()
                # e desce um pouco aberta (rente ao tronco o braço de cima raspava o colete: 4 cm)
                direcao = (direcao + (Q @ Vector((-1.0, 0.0, 0.0))) * (LANCE_ABRE_E * math.sin(math.pi * w_))).normalized()
                comp = da_.length + (ds_.length - da_.length) * w_
                q['esquerda'] = ('quadro', Se + direcao * comp, 1.0,
                                 aponta[1].to_quaternion().slerp(rs_.to_quaternion(), w_).to_matrix())
                q['relaxa_e'] = w_
                q['solto_e'] = aa.suave((w_ - 0.5) / 0.5)
                # a mão que lançou solta a granada e os dedos vão para o repouso
                q['relaxa_d'] = aa.suave(max(0.0, min(1.0, (t - aa.LANCE[0]) / 0.12)))

    def baixar_ombro(self, M, locais, graus):
        """Desce a clavícula direita (gira em volta da frente) e devolve o esqueleto refeito."""
        corpo = self.corpo
        cla = corpo.n('RightShoulder')
        z0 = M[corpo.n('RightArm')].translation.z
        rot0 = M[cla].to_3x3().normalized()
        for sinal in (1.0, -1.0):
            corpo.esq.por(M, locais, cla, Quaternion(pa.FRENTE, math.radians(graus * sinal)).to_matrix() @ rot0)
            M = corpo.esq.fk(locais)
            if graus < 1e-6 or M[corpo.n('RightArm')].translation.z <= z0:
                return M
        return M

    def tocar(self, nome, k):
        """Toca o quadro da primeira pessoa; no saque de fora, a pegada parada (o saque de lá gira a
        arma por conta própria, e somado ao giro de trás torcia o braço)."""
        if nome == 'sacar' and SACAR_DE_TRAS and self.plano.tipo in ('fuzil', 'pistola'):
            self.rig.tocar('parado', 0)
        else:
            self.rig.tocar(nome, k)

    def sacar_de_tras(self, quadros, n, tipo):
        """O saque visto de fora: a arma vem de trás (Jhon, 23/11: "e já puxa de trás a arma",
        depois de lançar a granada). Na primeira pessoa ela sobe de baixo da câmera, e de fora
        aparecia do nada na frente do corpo. O fuzil começa pendurado na mão, com o braço solto
        e esticado um pouco afastado da coxa (SACAR_BRACO_FUZIL; colado nela a coronha entrava no
        quadril) e o cano para baixo e para a frente (SACAR_CANO_FUZIL); a pistola do mesmo jeito
        (SACAR_BRACO_PISTOLA, SACAR_CANO_PISTOLA; girada em volta do ombro ela entrava na coxa). As duas sobem
        até a pegada com a mão direita nelas o tempo todo; a esquerda fica pendurada, em concha,
        e entra na arma no fim; a cabeça acompanha a mão (olhar_a_arma)."""
        corpo = self.corpo
        G = aa.GRANADA
        for k, q in enumerate(quadros):
            u = k / max(1, n)
            w = aa.suave(u)
            M = corpo.esq.fk(q['locais'])
            if SACAR_OMBRO_DESCE:
                # o ombro direito desce junto com o peso da arma (Jhon: "braço e ombro podem descer
                # mais"), e volta enquanto ela sobe
                M = self.baixar_ombro(M, q['locais'], SACAR_OMBRO_DESCE * (1.0 - w))
            S_ = M[corpo.n('RightArm')].translation
            # A arma vem pendurada na mão: ela começa na pose do braço solto, ao lado da
            # coxa, e sobe até a pegada levando a arma presa nela (girá-la em volta do ombro
            # abria o cotovelo para o lado, torcia o braço e a pistola entrava na coxa).
            self.rig.tocar('parado', 0)
            self.rig.colocar(q['col'])
            fp = self.rig.mao('Right')
            F_fp = Matrix.Translation(fp['palma']) @ orient(fp['ao_longo'], fp['dentro']).to_4x4()
            # a mão no fim do braço, esticado até quase o limite (sem travar o cotovelo), para a
            # arma sair o mais de baixo que o braço deixa (Jhon: "puxa a arma mais de baixo")
            braco0, cano0 = (SACAR_BRACO_FUZIL, SACAR_CANO_FUZIL) if tipo == 'fuzil' else (SACAR_BRACO_PISTOLA, SACAR_CANO_PISTOLA)
            _, r0 = aa.quadro_mao(corpo, q['locais'], (0.0, 0.0, 0.0), SACAR_EIXOS_FUZIL)
            # e o cano para baixo, um pouco para a frente e para fora: pendurado reto ele
            # atravessava a coxa e a panturrilha
            cano_na_palma = F_fp.to_3x3().inverted() @ self.rig.eixos_arma()[1]
            quer_cano = (aa.quadro_mira(corpo) @ Vector(cano0)).normalized()
            r0 = (r0 @ cano_na_palma).rotation_difference(quer_cano).to_matrix() @ r0
            punho_fp = (mm.quadro(self.rig.m('hand_r')) @ self.ger.K[('Right', 'Hand')]).translation
            punho_na_palma = F_fp.inverted() @ punho_fp
            desce = (aa.quadro_mira(corpo) @ Vector(braco0)).normalized()
            W0 = S_ + desce * (self.ger.braco['Right'].alcance() * SACAR_ESTICA)
            p0 = W0 - r0 @ punho_na_palma
            pos = p0.lerp(fp['palma'], w)
            rot = r0.to_quaternion().slerp(F_fp.to_3x3().to_quaternion(), w).to_matrix()
            q['col'] = (Matrix.Translation(pos) @ rot.to_4x4()) @ F_fp.inverted() @ q['col']
            if os.environ.get('DEPURAR_SACAR') and k in (0, n // 2):
                print(f'OMBRO z {S_.z:.3f}', flush=True)
                self.rig.colocar(q['col'])
                alvo_ = mm.quadro(self.rig.m('hand_r')) @ self.ger.K[('Right', 'Hand')]
                print(f'SACAR k{k}: palma-ombro {(pos - S_).length:.3f} punho-ombro {(alvo_.translation - S_).length:.3f} '
                      f'alcance {self.ger.braco["Right"].alcance():.3f} punho rel {tuple(round(x, 3) for x in (alvo_.translation - S_))}', flush=True)
            junta = aa.suave(max(0.0, (u - 0.55) / 0.45))
            # a esquerda pendurada ao lado da coxa, o braço esticado como o direito (na cintura,
            # com o cotovelo para fora, ela ficava de mão no quadril)
            M = corpo.esq.fk(q['locais'])
            S_e = M[corpo.n('LeftArm')].translation
            _, rs_ = aa.quadro_mao(corpo, q['locais'], (0.0, 0.0, 0.0), SACAR_EIXOS_E)
            desce_e = (aa.quadro_mira(corpo) @ Vector(SACAR_BRACO_E)).normalized()
            ps_ = S_e + desce_e * (self.ger.braco['Left'].alcance() * SACAR_ESTICA + SACAR_PALMA_E)
            q['esquerda'] = ('quadro', ps_, 1.0 - junta, rs_)
            q['relaxa_e'] = 1.0 - junta
            q['solto_e'] = 1.0 - junta
            q['solto_d'] = 1.0 - w
            self.olhar_a_arma(q, w)

    def sacar_do_bolso(self, quadros, n):
        """A granada sai do bolso (Jhon, 23/11): a mão direita começa no bolso da frente
        (GRANADA_BOLSO), com a granada na palma virada para a coxa, e sobe até a pegada da primeira
        pessoa, girando no caminho; o braço vem pendurado (o cotovelo para trás) e a cabeça
        acompanha. O conjunto toca o saque de lá (é o que o jogo toca): a granada é posta para a mão
        de lá ficar onde a daqui vai."""
        corpo, rig = self.corpo, self.rig
        Q = aa.quadro_mira(corpo)
        for k, q in enumerate(quadros):
            u = k / max(1, n)
            w = aa.suave(u)
            rig.tocar('sacar', k)
            rig.colocar(q['col'])
            fp = rig.mao('Right')
            F_fp = Matrix.Translation(fp['palma']) @ orient(fp['ao_longo'], fp['dentro']).to_4x4()
            p0 = aa.no_peito(corpo, q['locais'], GRANADA_BOLSO)
            r0 = orient(Q @ Vector(GRANADA_BOLSO_EIXOS[0]), Q @ Vector(GRANADA_BOLSO_EIXOS[1]))
            pos = p0.lerp(fp['palma'], w)
            rot = r0.to_quaternion().slerp(F_fp.to_3x3().to_quaternion(), w).to_matrix()
            q['col'] = (Matrix.Translation(pos) @ rot.to_4x4()) @ F_fp.inverted() @ q['col']
            q['solto_d'] = 1.0 - w
            self.olhar_a_arma(q, w)

    def olhar_a_arma(self, q, w):
        """A cabeça acompanha, de leve, a mão que saca (Jhon, 23/11: "ao sacar a arma, o personagem
        deve olhar levemente para a direção em que a arma está sendo sacada"): o pescoço e a cabeça
        giram OLHAR_SACAR do caminho da frente até a mão, no máximo OLHAR_SACAR_MAX graus, e voltam
        para a frente (a mira) quando a arma chega."""
        if OLHAR_SACAR <= 0.0:
            return
        corpo = self.corpo
        self.rig.tocar('parado', 0)
        self.rig.colocar(q['col'])
        mao = self.rig.mao('Right')['palma']
        M = corpo.esq.fk(q['locais'])
        olho = M[corpo.n('Head')].translation
        giro = pa.FRENTE.rotation_difference((mao - olho).normalized())
        quanto = min(OLHAR_SACAR * math.degrees(giro.angle), OLHAR_SACAR_MAX) / max(1e-6, math.degrees(giro.angle))
        meio = Quaternion().slerp(giro, quanto * (1.0 - w) * 0.5).to_matrix()
        for nome in ('Neck', 'Head'):
            n_ = corpo.n(nome)
            corpo.esq.por(M, q['locais'], n_, meio @ M[n_].to_3x3())
            M = corpo.esq.fk(q['locais'])

    def aproximar(self, nome, quadros):
        """Quando o braço do personagem (mais curto) não chega na mão de lá, a arma vem para perto do
        corpo (como no animacoes_armas.py, passo 2): o máximo numa janela, depois suave."""
        corpo, rig, ger = self.corpo, self.rig, self.ger
        falta = []
        for k, q in enumerate(quadros):
            M = corpo.esq.fk(q['locais'])
            self.tocar(nome, k)
            rig.colocar(q['col'])
            empurra = Vector()
            for s, lado in LADOS:
                alvo = mm.quadro(rig.m(f'hand_{s}')) @ ger.K[(lado, 'Hand')]
                br = ger.braco[lado]
                S = M[br.up].translation
                d = (alvo.translation - S).length - br.alcance() * 0.965
                e = q.get('esquerda') if lado == 'Left' else None
                if e and e[0] == 'quadro':
                    # a esquerda solta (o começo do saque) não segura a arma: não puxa ela para si
                    d *= 1.0 - e[2]
                if d > 0:
                    v = (S - alvo.translation).normalized() * d
                    if v.length > empurra.length:
                        empurra = v
            falta.append(empurra)
        picos = [max(falta[max(0, k - 5):k + 6], key=lambda v: v.length) for k in range(len(falta))]
        for k, q in enumerate(quadros):
            soma, peso = Vector(), 0.0
            for j in range(max(0, k - 4), min(len(picos), k + 5)):
                w_ = math.exp(-((j - k) / 2.5) ** 2)
                soma += picos[j] * w_
                peso += w_
            suave = soma / peso
            if suave.length > 1e-5:
                q['col'] = Matrix.Translation(suave) @ q['col']

    def cano(self):
        """Para onde o cano da pistola aponta, no mundo (o -y do osso da arma, ficha 'cano_osso')."""
        co = self.rig.ficha.get('cano_osso')
        osso = co['osso'] if co else 'ik_hand_gun'
        return (self.rig.rot(osso) @ Vector((0.0, -1.0, 0.0))).normalized()

    def nivelar(self, col):
        """O giro (em volta do pulso direito) que põe o cano na horizontal, sem mudar para onde
        ele aponta de lado."""
        self.rig.colocar(col)
        pulso_d = (mm.quadro(self.rig.m('hand_r')) @ self.ger.K[('Right', 'Hand')]).translation.copy()
        c = self.cano()
        h = Vector((c.x, c.y, 0.0)).normalized()
        R = c.rotation_difference(h).to_matrix().to_4x4()
        return Matrix.Translation(pulso_d) @ R @ Matrix.Translation(-pulso_d)

    def _giro_em(self, M, col, eixos, pref_d, pref_e, reto=False):
        """Quanto os dois punhos ficam além de PUNHO_PISTOLA do de lá com a pistola
        girada (yaw, pitch em graus) em volta do pulso direito e erguida eixos[2] (m).
        `reto`: o punho tem de ficar reto (PUNHO_RETO), não como o de lá, e a arma anda
        também para a frente (eixos[3]) e para o lado (eixos[4])."""
        rig, ger, corpo = self.rig, self.ger, self.corpo
        rig.colocar(col)
        pulso_d = (mm.quadro(rig.m('hand_r')) @ ger.K[('Right', 'Hand')]).translation.copy()
        R = (Quaternion(pa.CIMA, math.radians(eixos[0])) @ Quaternion(pa.DIREITA, math.radians(eixos[1]))).to_matrix().to_4x4()
        anda = pa.CIMA * eixos[2]
        if len(eixos) > 3:
            anda = anda + pa.FRENTE * eixos[3] + pa.DIREITA * eixos[4]
        giro = Matrix.Translation(anda) @ Matrix.Translation(pulso_d) @ R @ Matrix.Translation(-pulso_d)
        rig.colocar(giro @ col)
        custo = 0.0
        for s, lado, pref in (('r', 'Right', pref_d), ('l', 'Left', pref_e)):
            fp = rig.mao(lado)
            alvo = mm.quadro(rig.m(f'hand_{s}')) @ ger.K[(lado, 'Hand')]
            quer, folga = ((0.0, 0.0), PUNHO_RETO) if reto else ((fp['dobra'], fp['lado']), PUNHO_PISTOLA)
            r = ger.braco[lado].avaliar(M, alvo.translation, alvo.to_3x3().normalized(), pref, corpo.mao[lado], 0.03, 0.0,
                                        passo=7.5, alvo=quer)
            ex = max(0.0, abs(r['dobra'] - quer[0]) - folga) + max(0.0, abs(r['lado'] - quer[1]) - folga)
            custo += ex * ex + r['longe'] * 4000
        if reto:
            return custo + 0.05 * (eixos[0] ** 2 + eixos[1] ** 2) + 30.0 * (eixos[2] ** 2 + eixos[3] ** 2 + eixos[4] ** 2), giro
        return custo + 0.05 * (eixos[0] ** 2 + eixos[1] ** 2) + 300.0 * eixos[2] ** 2, giro

    def _reta(self, M0, col):
        """A pistola reta: nivelada, e andando (frente, lado, altura; uns graus de lado e de
        cima) até os dois punhos ficarem retos. Devolve (custo, eixos, giro no mundo)."""
        nivel = self.nivelar(col)
        col_n = nivel @ col
        melhor = None
        centro = (0.0, 0.0, 0.0, 0.0, 0.0)
        lim = (4.0, 3.0, 0.16, 0.14, 0.10)
        for passo, passo_m in ((2.0, 0.04), (1.0, 0.015), (0.5, 0.005)):
            # cada passo anda até parar de melhorar (a arma pode ir até `lim` de onde estava)
            for _ in range(6):
                for d in __import__('itertools').product((-1, 0, 1), repeat=5):
                    e = tuple(max(-lim[i], min(lim[i], centro[i] + d[i] * (passo if i < 2 else passo_m))) for i in range(5))
                    c, g = self._giro_em(M0, col_n, e, COTOVELO_BAIXO, COTOVELO_BAIXO, reto=True)
                    if melhor is None or c < melhor[0]:
                        melhor = (c, e, g @ nivel)
                if melhor[1] == centro:
                    break
                centro = melhor[1]
        return melhor

    def giro_da_pistola(self, q, M):
        """A pistola gira inteira (com as duas mãos presas nela) em volta do pulso direito
        até os dois punhos ficarem retos (Jhon, 23/11: "pulso um pouco torto para baixo";
        "as mãos estão encostando umas nas outras, uma apertando a outra": girar cada mão
        sozinha enfiava a esquerda 1,5 cm na direita). Um giro por postura (o do primeiro
        quadro do parado e da mira), misturado nas transições."""
        if not hasattr(self, '_giros'):
            self._giros = {}
        def do_estado(est):
            if est not in self._giros:
                clipe = 'mira' if est == 'mira' else 'parado'
                n0, qs = mm.bases_do_clipe(self.corpo, self.rig, self.plano, clipe)
                q0 = qs[0]
                self.rig.tocar(clipe, 0)
                M0 = self.corpo.esq.fk(dict(q0['locais']))
                # na mira o cano fica na linha do olho: só uns graus, e a arma pode descer
                lim_ang = LIMITE_GIRO_MIRA if est == 'mira' else 15.0
                melhor = None
                centro = (0.0, 0.0, 0.0)
                if PISTOLA_RETA:
                    melhor = self._reta(M0, q0['col'])
                    self._giros[est] = (q0['col'].inverted() @ melhor[2] @ q0['col'], melhor[1])
                    e = melhor[1]
                    print(f'PISTOLA reta {est}: {e[0]:.1f}° de lado, {e[1]:.1f}° de cima, anda {e[3] * 100:.1f} cm para a frente, '
                          f'{e[4] * 100:.1f} para a direita, {e[2] * 100:.1f} para cima (custo {melhor[0]:.1f})', flush=True)
                    return self._giros[est][0]
                for passo, passo_z in ((5.0, 0.03), (2.0, 0.012), (0.8, 0.005)):
                    for dy in (-2, -1, 0, 1, 2):
                        for dp in (-2, -1, 0, 1, 2):
                            for dz in (-2, -1, 0, 1, 2):
                                e = (max(-lim_ang, min(lim_ang, centro[0] + dy * passo)),
                                     max(-lim_ang, min(lim_ang, centro[1] + dp * passo)),
                                     max(-DESCE_PISTOLA, min(0.04, centro[2] + dz * passo_z)))
                                c, g = self._giro_em(M0, q0['col'], e, COTOVELO_BAIXO, COTOVELO_BAIXO)
                                if melhor is None or c < melhor[0]:
                                    melhor = (c, e, g)
                    centro = melhor[1]
                # o giro em volta do pulso, no espaço do conjunto (vale para os outros quadros)
                self._giros[est] = (q0['col'].inverted() @ melhor[2] @ q0['col'], melhor[1])
                print(f'PISTOLA giro {est}: {melhor[1][0]:.1f}° de lado, {melhor[1][1]:.1f}° para cima/baixo, {melhor[1][2] * 100:.1f} cm de altura', flush=True)
            return self._giros[est][0]
        w = q.get('w')
        if w is None:
            local = do_estado(q['est'])
        else:
            a, b = do_estado('quadril'), do_estado('mira')
            local = Matrix.LocRotScale(a.translation.lerp(b.translation, w), a.to_quaternion().slerp(b.to_quaternion(), w), None)
        return q['col'] @ local @ q['col'].inverted()

    def arma_no_corpo(self):
        """Quanto a arma entra no corpo (m) — tronco, quadril e pernas do glb novo já posado —, e em
        que peça. A mão que segura fica de fora (ela é medida contra a arma em outro lugar)."""
        from mathutils.bvhtree import BVHTree
        dg = bpy.context.evaluated_depsgraph_get()
        verts, faces, grupo_da_face = [], [], []
        for o in self._novo_malhas:
            g = {x.index: x.name for x in o.vertex_groups}
            em = o.evaluated_get(dg).to_mesh()
            ok = {}
            for v in o.data.vertices:
                if v.groups:
                    n_ = g[max(v.groups, key=lambda x: x.weight).group]
                    ok[v.index] = any(t in n_ for t in ('Hips', 'Spine', 'UpLeg', 'Leg'))
            base = len(verts)
            verts.extend(o.matrix_world @ v.co for v in em.vertices)
            for pol in em.polygons:
                if all(ok.get(i) for i in pol.vertices):
                    faces.append([base + i for i in pol.vertices])
                    grupo_da_face.append(g[max(o.data.vertices[pol.vertices[0]].groups, key=lambda x: x.weight).group])
            o.evaluated_get(dg).to_mesh_clear()
        arvore = BVHTree.FromPolygons(verts, faces)
        pior, onde = 0.0, ''
        for pc in self.rig.pecas:
            m = self.rig.m(pc['osso'])
            for p_ in pc['amostra']:
                w_ = m @ p_
                r_ = arvore.find_nearest(w_, 0.08)
                if r_[0] is None:
                    continue
                d_ = -(w_ - r_[0]).dot(r_[1])
                if d_ > pior:
                    pior, onde = d_, f"{pc['osso']} em {grupo_da_face[r_[2]].replace(pa.PREFIXO, '')} {tuple(round(x, 2) for x in w_)}"
        return pior, onde

    def braco_no_tronco(self):
        """Quanto a pele dos braços e antebraços entra no tronco (m), por lado, no glb novo já posado."""
        from mathutils.bvhtree import BVHTree
        dg = bpy.context.evaluated_depsgraph_get()
        verts, faces, bracos = [], [], {'Right': [], 'Left': []}
        face_malha = []
        for o in self._novo_malhas:
            g = {x.index: x.name for x in o.vertex_groups}
            em = o.evaluated_get(dg).to_mesh()
            dono = {}
            for v in o.data.vertices:
                if v.groups:
                    dono[v.index] = g[max(v.groups, key=lambda x: x.weight).group]
            base = len(verts)
            verts.extend(o.matrix_world @ v.co for v in em.vertices)
            for pol in em.polygons:
                if all(any(t in dono.get(i, '') for t in ('Spine', 'Hips')) for i in pol.vertices):
                    faces.append([base + i for i in pol.vertices])
                    face_malha.append(o.name)
            for i, n_ in dono.items():
                for lado in ('Right', 'Left'):
                    if n_.endswith(lado + 'Arm') or n_.endswith(lado + 'ForeArm'):
                        bracos[lado].append((o.matrix_world @ em.vertices[i].co, o.name, n_))
            o.evaluated_get(dg).to_mesh_clear()
        arvore = BVHTree.FromPolygons(verts, faces)
        out = {}
        mw_ = self._novo_arm.matrix_world
        for lado, pts in bracos.items():
            pior = 0.0
            # só do meio do braço para baixo: perto do ombro (a axila) a pele encosta no tronco
            nome_ = next(b.name for b in self._novo_arm.pose.bones if b.name.endswith(lado + 'Arm'))
            ombro_ = mw_ @ self._novo_arm.pose.bones[nome_].head
            cot_ = mw_ @ self._novo_arm.pose.bones[nome_].tail
            corte_ = (cot_ - ombro_).length * 0.5
            onde = ''
            for p_, malha_, osso_ in pts:
                if (p_ - ombro_).length < corte_:
                    continue
                r_ = arvore.find_nearest(p_, 0.15)
                if r_[0] is not None:
                    d_ = -(p_ - r_[0]).dot(r_[1])
                    if d_ > pior:
                        pior, onde = d_, f'{malha_}/{osso_.replace(pa.PREFIXO, "")} contra {face_malha[r_[2]]} a {(p_ - ombro_).length * 100:.0f} cm do ombro'
            out[lado] = pior
            out[lado + '_onde'] = onde
        return out

    def posar_novo(self, locais, deltas, trans):
        """Põe o glb novo (importado uma vez) na pose do quadro."""
        if not hasattr(self, '_novo_arm'):
            antes = set(bpy.data.objects)
            bpy.ops.import_scene.gltf(filepath=self.glb)
            self._novo_arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o not in antes)
            self._novo_malhas = [o for o in bpy.data.objects if o.type == 'MESH' and o.parent == self._novo_arm]
            for o in self.corpo.malhas:
                o.hide_render = True
        for pb in self._novo_arm.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            if pb.name in deltas:
                pb.matrix_basis = Matrix.Translation(trans[pb.name]) @ deltas[pb.name].to_matrix().to_4x4()
            else:
                pb.matrix_basis = Matrix.Identity(4)
                if pb.name in locais:
                    pb.rotation_quaternion = locais[pb.name]
        bpy.context.view_layer.update()

    def foto(self, nome, k, M, saida):
        """Duas fotos do quadro (de lado e de frente), com a arma de lá onde o .json a põe."""
        if not getattr(self, '_cena_fotos', False):
            aa.preparar_fotos()
            for o in bpy.data.objects:
                if o.name.startswith('Plane'):
                    o.hide_render = True
            bpy.context.scene.cycles.samples = 12
            bpy.context.scene.render.resolution_x = bpy.context.scene.render.resolution_y = 420
            self._cena_fotos = True
        peito = M[self.corpo.n('Spine2')].translation
        c = peito + pa.FRENTE * 0.15
        out = []
        vistas = [('lado', c + pa.DIREITA * 2.4 + pa.CIMA * 0.2), ('frente', c + pa.FRENTE * 2.6 - pa.DIREITA * 0.9 + pa.CIMA * 0.3)]
        if os.environ.get('FOTOS_COSTAS'):
            vistas.append(('costas', c - pa.FRENTE * 2.2 + pa.DIREITA * 1.3 + pa.CIMA * 0.2))
        fov = 42
        if os.environ.get('FOTOS_OMBRO'):
            # de perto, o braço de cima direito (a pele do ombro)
            c = (M[self.corpo.n('RightArm')].translation + M[self.corpo.n('RightForeArm')].translation) * 0.5
            vistas = [('ombro_frente', c + pa.FRENTE * 1.2 + pa.DIREITA * 0.5 + pa.CIMA * 0.2),
                      ('ombro_costas', c - pa.FRENTE * 1.2 + pa.DIREITA * 0.5 + pa.CIMA * 0.2),
                      ('ombro_cima', c + pa.CIMA * 1.2 + pa.DIREITA * 0.3 - pa.FRENTE * 0.2)]
            fov = 110
        if os.environ.get('FOTOS_MAOS'):
            # de perto, as duas mãos (a pegada: os dedos, os polegares, uma mão na outra)
            c = (M[self.corpo.n('RightHand')].translation + M[self.corpo.n('LeftHand')].translation) * 0.5
            vistas = [('maos_esquerda', c - pa.DIREITA * 0.9 + pa.FRENTE * 0.2 + pa.CIMA * 0.15),
                      ('maos_direita', c + pa.DIREITA * 0.9 + pa.FRENTE * 0.2 + pa.CIMA * 0.15),
                      ('maos_cima', c + pa.CIMA * 0.9 - pa.FRENTE * 0.25)]
            fov = 110
        for vista, de in vistas:
            arq = os.path.join(saida, f'_{nome}_{k:03d}_{vista}.png')
            pa.foto(arq, de, c, fov)
            out.append(arq)
        return out

    def medir_maos(self, nome, k, locais, deltas, trans):
        """A pele de uma mão dentro da outra (mm) e o vão entre elas, no glb novo posado."""
        from mathutils.bvhtree import BVHTree
        if not hasattr(self, '_novo_arm'):
            antes = set(bpy.data.objects)
            bpy.ops.import_scene.gltf(filepath=self.glb)
            self._novo_arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o not in antes)
            self._novo_malhas = [o for o in bpy.data.objects if o.type == 'MESH' and o.parent == self._novo_arm]
        arm = self._novo_arm
        for pb in arm.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            if pb.name in deltas:
                pb.matrix_basis = Matrix.Translation(trans[pb.name]) @ deltas[pb.name].to_matrix().to_4x4()
            else:
                pb.matrix_basis = Matrix.Identity(4)
                if pb.name in locais:
                    pb.rotation_quaternion = locais[pb.name]
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        lados = {'r': [], 'l': []}
        for o in self._novo_malhas:
            g = {x.index: x.name for x in o.vertex_groups}
            em = o.evaluated_get(dg).to_mesh()
            for v, ve in zip(o.data.vertices, em.vertices):
                if not v.groups:
                    continue
                n = g[max(v.groups, key=lambda x: x.weight).group]
                for s in ('r', 'l'):
                    if n in nomes_de_mao(s) or n in (f'{d}_04_{s}' for d in mt.TODOS) or n == pa.PREFIXO + ('Right' if s == 'r' else 'Left') + 'Hand':
                        lados[s].append((o.matrix_world @ ve.co, (o.matrix_world.to_3x3() @ ve.normal).normalized()))
            o.evaluated_get(dg).to_mesh_clear()
        arvore = BVHTree.FromPolygons([p for p, _ in lados['r']], [], all_triangles=False) if False else None
        # sem malha fechada por lado: a distância de cada ponto da esquerda ao ponto mais perto
        # da direita, com o sinal pela normal de lá
        from mathutils import kdtree
        kd = kdtree.KDTree(len(lados['r']))
        for i, (p, _) in enumerate(lados['r']):
            kd.insert(p, i)
        kd.balance()
        dentro, perto = 0.0, 1.0
        for p, n in lados['l']:
            co, i, d = kd.find(p)
            nr = lados['r'][i][1]
            sinal = (p - co).dot(nr)
            if sinal < 0 and d < 0.03:
                dentro = max(dentro, -sinal)
            perto = min(perto, d)
        return dentro, perto

    def _quadros_foto(self, nome, n):
        if not hasattr(self, '_fotos_feitas'):
            self._fotos_feitas = {}
        so = os.environ.get('FOTOS_CLIPES')
        if so and nome not in so.split(','):
            return ()
        partes = int(os.environ.get('FOTOS_PARTES', 6))
        return sorted({round(n * i / (partes - 1)) for i in range(partes)})

    def clipe(self, nome):
        corpo, rig, ger = self.corpo, self.rig, self.ger
        n, quadros = mm.bases_do_clipe(corpo, rig, self.plano, nome)
        tipo = self.plano.tipo
        if tipo in ('faca', 'granada'):
            # o corpo inteiro (o golpe ampliado, o tronco girando, a guarda; o lance da
            # granada): a coreografia do animacoes_armas.py, quadro a quadro.
            # O fim do lance da granada: a mão termina na frente da barriga com a palma para
            # o corpo (Jhon, 23/11: "braço de lançamento fica torto": cruzava até o quadril
            # esquerdo com a palma para fora, o antebraço torcido)
            if tipo == 'granada':
                aa.GRANADA['segue'] = SEGUE_GRANADA
                aa.GRANADA['mao_segue'] = MAO_SEGUE_GRANADA
            info = {}
            rig.colocar(self.plano.colocacoes['quadril'])
            rig.tocar('parado', 0)
            fp0 = rig.mao('Right')
            info['palma0'] = fp0['palma'].copy()
            info['palma_parado'] = info['palma0']
            info['rot_parado'] = orient(fp0['ao_longo'], fp0['dentro'])
            if 'armado' in rig.clipes():
                rig.tocar('armado', 0)
                info['palma_armado0'] = rig.mao('Right')['palma'].copy()
            if tipo == 'granada' and nome in ('armar', 'armado', 'arremesso'):
                self.lance(nome, quadros, n, info)
            if tipo == 'faca':
                self.faca(nome, quadros, n)
            for k, q in enumerate(quadros):
                if (tipo == 'granada' and nome in ('armar', 'armado', 'arremesso')) or tipo == 'faca':
                    continue
                aa.coreografia(corpo, rig, self.plano, nome, k, n, q, info)
                if tipo == 'granada' and nome == 'arremesso' and BRACO_E_FIXO_NO_LANCE:
                    # O braço oposto desce junto com o lance (Jhon, 23/11: "o braço esquerdo deve
                    # abaixar ao lançar, de forma sincronizada, abaixo e lançado ao mesmo tempo"):
                    # sai de onde estava com a granada armada (apontando o alvo) e chega ao lado do
                    # corpo quando o braço da granada termina o lance. Antes puxava para o quadril.
                    G = aa.GRANADA
                    t_ = k / aa.FPS
                    w_ = aa.suave(min(1.0, t_ / aa.LANCE[1]))
                    pa_, ra_ = aa.quadro_mao(corpo, q['locais'], G['aponta'], G['mao_aponta_e'])
                    ps_, rs_ = aa.quadro_mao(corpo, q['locais'], G['solta_e'], G['mao_solta_e'])
                    # desce girando no ombro, esticado (Jhon: "deve ir descendo esticando até ficar
                    # numa pose natural"): a direção do ombro à mão gira de "apontando" para
                    # "solto ao lado", com o braço no comprimento de esticado; só no fim encolhe
                    # para o da pose solta (o cotovelo relaxa)
                    S_ = corpo.esq.fk(q['locais'])[corpo.n('LeftArm')].translation
                    da_, ds_ = pa_ - S_, ps_ - S_
                    direcao = da_.normalized().slerp(ds_.normalized(), w_) if da_.angle(ds_) > 1e-4 else da_.normalized()
                    encolhe = aa.suave(max(0.0, (w_ - 0.6) / 0.4))
                    comp = da_.length + (ds_.length - da_.length) * encolhe
                    q['esquerda'] = ('quadro', S_ + direcao * comp, 1.0,
                                     ra_.to_quaternion().slerp(rs_.to_quaternion(), w_).to_matrix())
                    q['relaxa_e'] = w_
                    # e a mão que lançou: solta a granada (0,11 s) e os dedos vão para o repouso,
                    # como os da outra (Jhon: "ao lançar a granada, os dedos voltam para o estado de
                    # repouso, mesmo da mão oposta")
                    q['relaxa_d'] = aa.suave(max(0.0, min(1.0, (t_ - aa.LANCE[0]) / 0.12)))
                    q['cotovelo_e'] = None
            if tipo == 'granada' and nome == 'sacar':
                self.sacar_do_bolso(quadros, n)
        else:
            if tipo == 'pistola' and PUNHO_PISTOLA > 0:
                # o giro da pistola antes do alcance (o alcance corrige o que ele tirar)
                for q in quadros:
                    q['col'] = self.giro_da_pistola(q, None) @ q['col']
            if nome == 'sacar' and SACAR_DE_TRAS:
                self.sacar_de_tras(quadros, n, tipo)
            self.aproximar(nome, quadros)
            if nome.startswith('recarga') and RECARGA_TROCA:
                self.trocar_pente(nome, quadros, n)
        out = aa.Clipe(nome, n + 1)
        out.posicoes = []
        out.carregador = getattr(self, '_tempos_pente', {}).get(nome)
        # o pino da granada: quando sai da granada (a esquerda o leva) e quando ela o larga (cai no chão)
        out.pino = [round(LANCE_PINO_SAI, 4), round(LANCE_PINO_SAI + LANCE_LARGA, 4)] if (tipo == 'granada' and nome == 'armar') else None
        pior_falta = 0.0
        pior_ponta = 0.0
        pior_torcao = 0.0
        torcao_lado = {}
        cotovelo_lado = {}
        punho_lado = {}
        por_osso = {}
        for k, q in enumerate(quadros):
            locais = dict(q['locais'])
            M = corpo.esq.fk(locais)
            self.tocar(nome, k)
            rig.colocar(q['col'])
            des = self.plano.desenhos[q['est']]
            deltas, trans = {}, {}
            for s, lado in LADOS:
                pref = (q.get('cotovelo_d') or des['cotovelo_d']) if lado == 'Right' else (q.get('cotovelo_e') or des['cotovelo_e'])
                if tipo in ('fuzil', 'pistola'):
                    pref = COTOVELO_BAIXO
                br = ger.braco[lado]
                guarda_ = q.get('guarda_d' if lado == 'Right' else 'guarda_e', 0.0) if tipo == 'faca' else 0.0
                if guarda_ > 0.0:
                    pref = tuple(a + (b - a) * guarda_ for a, b in zip(pref, FACA_COTOVELO))
                br.torcao_livre = TORCAO_LIVRE
                br.peso_cotovelo = PESO_COTOVELO
                ref_d = q.get('ref_d', 0.0) if lado == 'Right' else 0.0
                if ref_d > 0.5:
                    # o braço da referência: o cotovelo onde o dela está, e a torção é a dela
                    br.peso_cotovelo = PESO_COTOVELO_LANCE
                    br.torcao_livre = TORCAO_LANCE
                solto = q.get('solto_d' if lado == 'Right' else 'solto_e', 0.0)
                if solto > 0.0:
                    # o braço pendurado (o começo do saque): o cotovelo para trás, que é para onde
                    # ele dobra com o braço solto, e sem torção (Jhon: "nunca entregue nada torcido")
                    pref = tuple(a + (b - a) * solto for a, b in zip(pref, SACAR_COTOVELO))
                    br.torcao_livre = TORCAO_LIVRE + (SACAR_TORCAO_LIVRE - TORCAO_LIVRE) * solto
                alto = 0.24 if (tipo == 'granada' and lado == 'Right') else 0.03
                peso_t = 1.0 if k else 0.0
                rig.colocar(q['col'])
                fp = rig.mao(lado)
                alvo = mm.quadro(rig.m(f'hand_{s}')) @ ger.K[(lado, 'Hand')]
                pulso, rot = alvo.translation.copy(), alvo.to_3x3().normalized()
                e = q.get('esquerda') if lado == 'Left' else None
                if e:
                    mi = corpo.mao['Left']
                    if e[0] == 'desloca':
                        pulso = pulso + e[1]
                    elif e[0] == 'punho':
                        # o punho e a mão já dados (no mundo)
                        pulso = pulso.lerp(e[1], e[2])
                        rot = rot.to_quaternion().slerp(e[3].to_quaternion(), e[2]).to_matrix()
                    elif e[0] == 'quadro':
                        quer = e[3] @ orient(mi['dedos'], mi['palma']).inverted()
                        p_alvo = e[1] - quer @ (mi['nos'] * aa.ALINHA)
                        pulso = pulso.lerp(p_alvo, e[2])
                        rot = Quaternion().slerp((quer @ rot.inverted()).to_quaternion(), e[2]).to_matrix() @ rot
                # a mão livre (a esquerda da faca) e a da granada: o punho dentro do que dobra. A esquerda
                # puxando o pino não: ela é a de lá, com o dedo na argola
                pino_ = lado == 'Left' and tipo == 'granada' and q.get('pino_e', False)
                livre = ((tipo == 'granada' and ref_d < 0.5) or (tipo == 'faca' and lado == 'Left')) and not pino_
                alvo_p = (0.0, 0.0) if (livre or (tipo == 'pistola' and PISTOLA_RETA)) else (fp['dobra'], fp['lado'])
                if livre:
                    antes_ = rot
                    # A mão na direção do antebraço (Jhon, 23/11: "apenas alinha a mão à direção do
                    # antebraço"; "o braço da granada terá o mesmo princípio"): as duas mãos da
                    # granada em todos os clipes (só no lance, o punho saltava na passagem da
                    # granada armada para o arremesso), com o punho quase reto; na faca, o de antes.
                    alinhado = tipo == 'granada'
                    lim_ = dict(dobra=(-PUNHO_ALINHADO, PUNHO_ALINHADO), lado=(-PUNHO_ALINHADO, PUNHO_ALINHADO),
                                giro=80.0) if alinhado else aa.LIMITE_PUNHO
                    rot = aa.limitar_punho(br, M, pulso, rot, pref, corpo.mao[lado], alto, peso_t, alvo_p, lim=lim_)
                    if lado == 'Right':
                        # a granada vai junto com a palma
                        q['col'] = (Matrix.Translation(pulso) @ rot.to_4x4()) @ (Matrix.Translation(pulso) @ antes_.to_4x4()).inverted() @ q['col']
                        rig.colocar(q['col'])
                r = br.resolver(M, locais, pulso, rot, pref, corpo.mao[lado], cotovelo_max_z=alto, peso_tempo=peso_t, alvo=alvo_p)
                if os.environ.get('MEDIR_PUNHO') and nome in os.environ['MEDIR_PUNHO'].split(',') and k in (0, n // 2):
                    cano_ = self.cano()
                    print(f'PUNHO {nome} q{k} {lado}: de lá {fp["dobra"]:.0f}°/{fp["lado"]:.0f}°, aqui {r["dobra"]:.0f}°/{r["lado"]:.0f}° '
                          f'(dobra/lado); cano {math.degrees(math.asin(max(-1.0, min(1.0, cano_.dot(pa.CIMA))))):.0f}° acima da horizontal', flush=True)
                if pino_:
                    # a ponta do indicador daqui na de lá (a mão daqui é maior ou menor e os dedos dela,
                    # de outro comprimento: com o punho no de lá, o dedo ficava fora da argola)
                    j2, j3 = mm.quadro(rig.m('index_02_l')).translation, mm.quadro(rig.m('index_03_l')).translation
                    ponta_fp = j3 + (j3 - j2) * 0.9
                    # no jogo a granada cresce em volta do meio dela, e a argola vai junto
                    meio_g = rig.m('Bolt') @ Vector(rig.ficha['centro'])
                    ponta_fp = meio_g + (ponta_fp - meio_g) * GRANADA_CRESCE
                    for _ in range(3):
                        Mx = corpo.esq.fk(locais)
                        i2, i3 = Mx[corpo.n('LeftHandIndex2')].translation, Mx[corpo.n('LeftHandIndex3')].translation
                        erro = ponta_fp - (i3 + (i3 - i2) * 0.9)
                        if erro.length < 0.002:
                            break
                        pulso = pulso + erro
                        r = br.resolver(M, locais, pulso, rot, pref, corpo.mao[lado], cotovelo_max_z=alto, peso_tempo=peso_t, alvo=alvo_p)
                if os.environ.get('DEPURAR_SACAR') and nome == 'sacar' and k == 0:
                    M2 = corpo.esq.fk(locais)
                    S2 = M2[br.up].translation
                    print(f'IK {lado}: alvo {tuple(round(x, 3) for x in (pulso - S2))} punho {tuple(round(x, 3) for x in (M2[br.ha].translation - S2))} '
                          f'cotovelo {tuple(round(x, 3) for x in (M2[br.lo].translation - S2))} M_igual {(M[br.ha].translation - M2[br.ha].translation).length:.3f}', flush=True)
                if not livre:
                    # o punho daqui contra o de lá (graus a mais de dobra e de lado)
                    punho_lado[lado] = max(punho_lado.get(lado, 0.0), abs(r['dobra'] - alvo_p[0]), abs(r['lado'] - alvo_p[1]))
                    if os.environ.get('DEPURAR_PUNHO') and k == 0:
                        print(f'PUNHO {nome} {lado}: dobra {r["dobra"]:.0f}° (lá {alvo_p[0]:.0f}°), lado {r["lado"]:.0f}° (lá {alvo_p[1]:.0f}°), giro {r["giro"]:.0f}°', flush=True)
                if lado == 'Right' and tipo in ('faca', 'granada'):
                    # se o braço não chega aonde a mão devia ir, é a arma que vem até a mão
                    real = M[br.ha]
                    real = Matrix.Translation(real.translation) @ real.to_3x3().normalized().to_4x4()
                    falta_ = real @ (Matrix.Translation(pulso) @ rot.to_4x4()).inverted()
                    if falta_.translation.length > 2e-4 or falta_.to_quaternion().angle > 2e-3:
                        q['col'] = falta_ @ q['col']
                        rig.colocar(q['col'])
                    r['falta'] = 0.0 if e is None else r['falta']
                pior_falta = max(pior_falta, r['falta'] if not (tipo in ('faca', 'granada') and lado == 'Left') else 0.0)
                br_ = ger.braco[lado]
                Yu_ = (M[br_.lo].translation - M[br_.up].translation).normalized()
                Yf_ = (M[br_.ha].translation - M[br_.lo].translation).normalized()
                H_ = Yu_.cross(Yf_)
                if H_.length > 1e-5:
                    t_ = abs(br_.torcao_do_ombro(M, Yu_, H_))
                    if os.environ.get('DEPURAR_SACAR') and nome == 'sacar':
                        print(f'TORCAO sacar k{k} {lado}: {t_:.0f}° dobra do cotovelo {math.degrees(Yu_.angle(Yf_)):.0f}°', flush=True)
                    pior_torcao = max(pior_torcao, t_)
                    torcao_lado[lado] = max(torcao_lado.get(lado, 0.0), t_)
                    S_, W_ = M[br_.up].translation, M[br_.ha].translation
                    eixo_ = (W_ - S_).normalized()
                    cot_ = M[br_.lo].translation - S_
                    cot_ = cot_ - eixo_ * cot_.dot(eixo_)
                    baixo_ = -pa.CIMA - eixo_ * (-pa.CIMA).dot(eixo_)
                    if cot_.length > 1e-4 and baixo_.length > 1e-4:
                        cotovelo_lado[lado] = max(cotovelo_lado.get(lado, 0.0), math.degrees(cot_.angle(baixo_)))
                        if os.environ.get('MEDIR_COTOVELO') and nome in os.environ['MEDIR_COTOVELO'].split(','):
                            print(f'COTOVELO {nome} q{k} {lado}: {math.degrees(cot_.angle(baixo_)):.0f}° do para baixo (dobra {cot_.length * 100:.1f} cm fora da linha ombro-pulso)', flush=True)
                self.rolar_braco(M, locais, ger.braco[lado], lado)
                relaxa = 0.0
                if tipo == 'granada' and lado == 'Left':
                    relaxa = q.get('relaxa_e', 1.0 if q.get('forma_e') == 'relaxada' else 0.0)
                elif tipo == 'granada' and lado == 'Right':
                    # a granada de fora é maior que a de lá (HeldWeapon.GrenadeHeldGrow): a mão abre
                    # um pouco para envolvê-la
                    relaxa = max(q.get('relaxa_d', 0.0), GRANADA_MAO_ABRE)
                elif lado == 'Left' and 'relaxa_e' in q:
                    relaxa = q['relaxa_e']
                for nm in nomes_de_mao(s):
                    loc, rot, _ = rig.pb[nm].matrix_basis.decompose()
                    if relaxa > 0.0:
                        # a mão solta em concha (a pose de repouso da mão do Manny, Jhon: "mãos
                        # abertas em concha"), e não a de lá, que segura o que não se vê
                        rot = rot.slerp(Quaternion(), relaxa)
                        loc = loc * (1.0 - relaxa)
                    deltas[nm] = rot.normalized()
                    # na primeira pessoa a palma fecha deslizando os nós (até 2,4 cm): o osso de
                    # lá tem translação, em centímetros (o conjunto tem escala 0,01)
                    trans[nm] = loc * 0.01
            # o osso de torção do braço de cima (torcao_braco.py): o giro contrário ao do braço em
            # volta de si, para a pele perto do ombro só balançar (Jhon: "braço da granada torcendo")
            torcoes = {}
            for lado in ('Right', 'Left'):
                nt = pa.PREFIXO + lado + 'ArmTwist'
                if nt in self.novo:
                    torcoes[lado + 'ArmTwist'] = (nt, self.torcao_do_braco(locais, lado, nt))
                    locais[nt] = torcoes[lado + 'ArmTwist'][1]
            if os.environ.get('FOTOS_ANIM') and k in self._quadros_foto(nome, n):
                self.posar_novo(locais, deltas, trans)
                self._fotos_feitas.setdefault(nome, []).append((k, self.foto(nome, k, M, os.environ['FOTOS_ANIM'])))
            if os.environ.get('MEDIR_CABECA') and tipo == 'faca':
                o_, f_, _, _ = rig.eixos_arma()
                cab_ = M[corpo.n('Head')].translation
                perto_ = min(((o_ + f_ * (x / 10.0) * 0.18) - cab_).length for x in range(11))
                print(f'CABECA {nome} q{k}: a lâmina a {perto_ * 100:.0f} cm do centro da cabeça', flush=True)
            if os.environ.get('MEDIR_BRACO') and nome in os.environ['MEDIR_BRACO'].split(','):
                self.posar_novo(locais, deltas, trans)
                b_ = self.braco_no_tronco()
                print(f'BRACO {nome} q{k}: o braço entra no tronco D {b_["Right"] * 1000:.0f} mm E {b_["Left"] * 1000:.0f} mm ({b_["Right_onde"]} | {b_["Left_onde"]})', flush=True)
            if os.environ.get('MEDIR_PINO') and tipo == 'granada' and nome == 'armar':
                # a ponta do indicador esquerdo (daqui e de lá) contra a argola (o osso Magazine)
                Mf = corpo.esq.fk(locais)
                i2, i3 = Mf[corpo.n('LeftHandIndex2')].translation, Mf[corpo.n('LeftHandIndex3')].translation
                ponta = i3 + (i3 - i2) * 0.9
                rig.colocar(q['col'])
                arg = next(pc for pc in rig.pecas if pc['osso'] == 'Magazine')
                meio_g = rig.m('Bolt') @ Vector(rig.ficha['centro'])
                pts_ = [meio_g + (rig.m('Magazine') @ p_ - meio_g) * GRANADA_CRESCE for p_ in arg['amostra']]      # a argola crescida, como no jogo
                meio_ = sum(pts_, Vector()) / len(pts_)
                j2, j3 = mm.quadro(rig.m('index_02_l')).translation, mm.quadro(rig.m('index_03_l')).translation
                ponta_fp = meio_g + (j3 + (j3 - j2) * 0.9 - meio_g) * GRANADA_CRESCE
                print(f'PINO q{k}: a ponta do indicador a {min((ponta - p_).length for p_ in pts_) * 1000:.0f} mm da argola '
                      f'({(ponta - meio_).length * 1000:.0f} mm do meio dela); a de lá a {min((ponta_fp - p_).length for p_ in pts_) * 1000:.0f} mm '
                      f'({(ponta_fp - meio_).length * 1000:.0f} mm do meio); daqui à de lá {(ponta - ponta_fp).length * 1000:.0f} mm', flush=True)
            if os.environ.get('MEDIR_CORPO') and nome in os.environ['MEDIR_CORPO'].split(','):
                self.posar_novo(locais, deltas, trans)
                rig.colocar(q['col'])
                fundo_, peca_ = self.arma_no_corpo()
                print(f'CORPO {nome} q{k}: a arma entra {fundo_ * 1000:.0f} mm ({peca_})', flush=True)
            if os.environ.get('MEDIR_MAOS') and tipo == 'pistola' and k in (0, n // 2):
                dentro_, perto_ = self.medir_maos(nome, k, locais, deltas, trans)
                print(f'MAOS {nome} q{k}: a esquerda entra na direita até {dentro_ * 1000:.1f} mm; o ponto mais perto a {perto_ * 1000:.1f} mm', flush=True)
            jogo, mundo = corpo.no_jogo(M)
            ossos = {n_: jogo[corpo.n(n_)] for n_ in aa.OSSOS if corpo.n(n_) in jogo and n_ not in pa.FALANGES}
            posicoes = {}
            for nm, dq in deltas.items():
                ossos[nm] = self.quatérnio_do_jogo(nm, dq)
                posicoes[nm] = self.posicao_do_jogo(nm, dq, trans[nm])
            for curto, (nt, dq) in torcoes.items():
                ossos[curto] = self.quatérnio_do_jogo(nt, dq)
            out.ossos.append(ossos)
            out.posicoes.append(posicoes)
            conj = EIXOS @ q['col'] @ EIXOS.inverted()
            out.conjunto.append(mundo[corpo.n('Spine2')].inverted() @ conj)
            out.locais.append(locais)
            out.colocacoes.append(q['col'].copy())
            # a cabeça de cada osso daqui (pela cadeia do glb novo) contra a de lá, no mundo
            for s, lado in LADOS:
                Mw = {pa.PREFIXO + lado + 'Hand': M[corpo.n(lado + 'Hand')]}
                for nm in nomes_de_mao(s):
                    d = self.novo[nm]
                    Mw[nm] = Mw[d['pai']] @ (self.novo[d['pai']]['ml'].inverted() @ d['ml']) @ Matrix.Translation(trans[nm]) @ deltas[nm].to_matrix().to_4x4()
                    if tipo in ('faca', 'granada') and lado == 'Left':
                        continue   # a esquerda vai para a guarda, não para a de lá
                    e_ = (Mw[nm].translation - rig.m(nm).translation).length
                    por_osso[nm] = max(por_osso.get(nm, 0.0), e_)
                    pior_ponta = max(pior_ponta, e_)
        if os.environ.get('DEPURAR'):
            print('OSSOS', nome, {k_: round(v_ * 1000, 1) for k_, v_ in sorted(por_osso.items(), key=lambda x: -x[1])[:10]})
        print(f'CLIPE {nome:12s} {out.quadros:3d} q: mão a {pior_falta * 1000:.1f} mm do lugar; junta de dedo daqui contra a de lá: até {pior_ponta * 1000:.1f} mm; ombro torcido até {pior_torcao:.0f}° (direito {torcao_lado.get("Right", 0):.0f}°, esquerdo {torcao_lado.get("Left", 0):.0f}°); cotovelo longe do para baixo até D {cotovelo_lado.get("Right", 0):.0f}° E {cotovelo_lado.get("Left", 0):.0f}°; punho torto a mais que o de lá até D {punho_lado.get("Right", 0):.0f}° E {punho_lado.get("Left", 0):.0f}°', flush=True)
        return out

def gravar(personagem, arma, clipes, escala=1.0):
    pasta = os.path.join(RAIZ, 'assets/personagens', personagem, 'animacoes')
    os.makedirs(pasta, exist_ok=True)
    velhos = set(pa.FALANGES)
    nomes = [n for n in aa.OSSOS if n not in velhos]
    novos = []
    for s, _ in LADOS:
        novos += nomes_de_mao(s)
    nomes += novos
    # os ossos de torção, se o personagem tiver (torcao_braco.py)
    nomes += [t for t in ('RightArmTwist', 'LeftArmTwist') if clipes and t in clipes[0].ossos[0]]
    dados = {'posicoes': novos, '_': 'Feito por tools/personagens/blender/mao_manny_anim.py. Por clipe e por quadro: a rotação '
                  'local de cada osso de "ossos" (x, y, z, w; inteiros de 16 bits ÷ 32767) e o conjunto da '
                  'primeira pessoa no espaço do Spine2 (posição ÷ 10000 m, rotação x, y, z, w ÷ 32767), em base64. '
                  'Os ossos da mão têm os nomes do Manny e copiam a primeira pessoa osso por osso (rotação e posição: a palma de lá fecha deslizando os nós; "posicoes" lista os ossos e "pos" dá a posição local deles em quadro, x, y, z em inteiros de 16 bits ÷ 10000 m). '
                  '"escala": quantas vezes o conjunto cresce neste personagem.',
             'fps': aa.FPS, 'escala': escala, 'ossos': nomes, 'clipes': {}}
    for c in clipes:
        valores = []
        for k in range(c.quadros):
            ossos = c.ossos[k]
            for nome in nomes:
                q = ossos.get(nome, Quaternion())
                if q.w < 0:
                    q = -q
                valores += [aa.q16(q.x), aa.q16(q.y), aa.q16(q.z), aa.q16(q.w)]
            m = c.conjunto[k]
            t = m.translation
            q = m.to_quaternion().normalized()
            if q.w < 0:
                q = -q
            valores += [aa.p16(t.x), aa.p16(t.y), aa.p16(t.z), aa.q16(q.x), aa.q16(q.y), aa.q16(q.z), aa.q16(q.w)]
        pos = []
        for k in range(c.quadros):
            for nome in novos:
                v = c.posicoes[k][nome]
                pos += [max(-32767, min(32767, int(round(v.x * 10000)))), max(-32767, min(32767, int(round(v.y * 10000)))),
                        max(-32767, min(32767, int(round(v.z * 10000))))]
        dados['clipes'][c.nome] = {'quadros': c.quadros, 'laco': c.nome in aa.LACOS,
                                   'dados': base64.b64encode(struct.pack('<%dh' % len(valores), *valores)).decode('ascii'),
                                   'pos': base64.b64encode(struct.pack('<%dh' % len(pos), *pos)).decode('ascii')}
        if getattr(c, 'carregador', None):
            # a troca de pente vista de fora (Copia.trocar_pente): quando a mão solta o velho, quando
            # pega o novo na cintura e quando ele encaixa (segundos do clipe)
            dados['clipes'][c.nome]['carregador'] = c.carregador
        if getattr(c, 'pino', None):
            # o pino da granada (Copia.lance): quando sai e quando a esquerda o larga (segundos do clipe)
            dados['clipes'][c.nome]['pino'] = c.pino
    caminho = os.path.join(pasta, arma + '.json')
    with open(caminho, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(dados, f, ensure_ascii=False, separators=(',', ':'))
        f.write('\n')
    return caminho


def main():
    personagem, arma = sys.argv[1], sys.argv[2]
    c = Copia(personagem, arma)
    c.preparar()
    clipes = []
    # SO_CLIPES=a,b: só estes (para testar um ajuste depressa); aí não grava o .json
    so = [x for x in os.environ.get('SO_CLIPES', '').split(',') if x]
    for nome in sorted(aa.CLIPES[c.plano.tipo], key=lambda x: (x != 'parado', x != 'mira')):
        if nome in c.rig.clipes() and (not so or nome in so):
            clipes.append(c.clipe(nome))
    if so:
        print('SO_CLIPES: nada gravado')
        return
    print('GRAVADO', gravar(personagem, arma, clipes))


if __name__ == '__main__':
    main()
