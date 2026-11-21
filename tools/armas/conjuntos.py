#!/usr/bin/env python3
"""
Os conjuntos de primeira pessoa das armas que o Jhon trouxe em 19/11 (M4A4, AK-74, SCAR-H,
SVD, Desert Eagle e a granada V40; ``reais.py``) e da faca do jogo, "com precisão e
naturalidade igual à do Unreal".

    python3 tools/armas/conjuntos.py [m4a4 ak74 scar svd deagle faca choque]
        [--ficha assets/armas/fp/bracos.json]

Cada conjunto é o ``ar15.glb`` com a arma trocada: o mesmo esqueleto, as mesmas mãos (no
jogo, as mãos padrão) e a câmera. A arma é rigada como o AR-15: o corpo no osso ``Grip``,
o carregador (com as balas dele) no ``Magazine``, o ferrolho no ``Bolt``.

De onde vem o movimento:

- **Fuzis** (M4A4, AK-74, SCAR-H, SVD): das animações do AR-15 — as do Jhon e as do
  tutorial da Unreal. A arma faz o mesmo movimento (com o coice e o peso de cada uma), o
  ferrolho vai e volta a cada tiro, a mão direita fica no punho com o indicador no gatilho
  e a esquerda é levada para o guarda-mão e para o carregador de cada arma; os braços são
  recalculados.
- **Desert Eagle, faca e granada**: animadas aqui, por chaves, sobre a respiração, o
  balanço do andar e o saque do AR-15 (é o que dá a mesma "mão" a todas).

Depois de montar, a mira (``mira.py``) e a coronhada (``coronhada.py``) entram por cima,
como no AR-15.
"""
import json, os, shutil, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from esqueleto import *
import reais, recarga, uasset

AQUI = os.path.dirname(os.path.abspath(__file__))
RAIZ = os.path.dirname(os.path.dirname(AQUI))
FP = os.path.join(RAIZ, 'assets', 'armas', 'fp')
FONTE = os.path.join(FP, 'ar15.glb')
FPS = 60.0
OSSO = {'': 'Grip', 'Magazine': 'Magazine', 'Ferrolho': 'Bolt', 'Pino': 'Trigger'}


def na_arma(x=0.0, y=0.0, z=0.0):
    """Um deslocamento dado no espaço da arma (m), como 4×4 no osso da arma (cm)."""
    return mover(-100 * x, 100 * z, 100 * y)


def levar(m, M):
    """A malha (no espaço da arma) levada pela 4×4 M."""
    R = M[:3, :3]
    return dict(pos=m['pos'] @ R.T + M[:3, 3], normal=m['normal'] @ R.T, uv=m['uv'])


def abrir(id_, no_arma='fuzil', na_mao=None, osso=None, antes=None):
    """O conjunto novo: o AR-15 com as malhas da arma trocadas. Devolve (novo, o AR-15 de origem).
    ``na_mao``: onde a peça fica em relação ao punho (faca, granada); ``osso``: o osso de todas as peças;
    ``antes``: uma função que rearruma as peças antes de irem para a mão (a granada separa a argola do pino)."""
    destino = os.path.join(FP, id_ + '.glb'); shutil.copy(FONTE, destino)
    E = Esqueleto(destino); AR = Esqueleto(FONTE); A = reais.arma(id_)
    mat = material_texturas(E, A['texturas'], id_) if 'texturas' in A else E.material_paleta(reais.paleta(), 'paleta', metal=0.45, rugosidade=0.5)
    pecas = [(a, no, m if na_mao is None else levar(m, na_mao)) for a, no, m in (antes(A['pecas']) if antes else A['pecas'])]
    corpo = [(m, (osso.get(no) if isinstance(osso, dict) else osso) or OSSO[no]) for _, no, m in pecas if no != 'Magazine']
    mag = [(m, 'Magazine') for _, no, m in pecas if no == 'Magazine']
    E.por_malha('fuzil', corpo, mat, None if no_arma == 'fuzil' else no_arma)
    if mag: E.por_malha('carregador', mag, mat)
    else: E.tirar_no('carregador')
    return E, AR


def material_texturas(E, pasta, nome):
    """O material da faca do jogo: a cor, o metal e o relevo dela (os .jpg de ``dados/``), embutidos."""
    j = E.j
    def textura(arquivo):
        dados = open(os.path.join(pasta, arquivo + '.jpg'), 'rb').read()
        while len(E.g.bin) % 4: E.g.bin.append(0)
        off = len(E.g.bin); E.g.bin += dados
        j['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': len(dados)})
        j.setdefault('images', []).append({'bufferView': len(j['bufferViews']) - 1, 'mimeType': 'image/jpeg', 'name': arquivo})
        j.setdefault('samplers', []).append({'magFilter': 9729, 'minFilter': 9987})
        j.setdefault('textures', []).append({'sampler': len(j['samplers']) - 1, 'source': len(j['images']) - 1})
        return len(j['textures']) - 1
    j['materials'].append({'name': nome, 'doubleSided': True, 'normalTexture': {'index': textura('knifeNormal')},
                           'pbrMetallicRoughness': {'baseColorTexture': {'index': textura('knifeColor')}, 'metallicRoughnessTexture': {'index': textura('knifeMetallic')}, 'roughnessFactor': 0.55}})
    return len(j['materials']) - 1


def fechar(E, ficha, entrada):
    """Tira os clipes do AR-15 que não foram refeitos, grava e põe a entrada na ficha."""
    feitos = set(entrada['animacoes'])
    E.j['animations'] = [a for a in E.j['animations'] if a['name'] in feitos]
    E.tirar_no('bracos'); E.podar()       # os braços do AR-15 não vão junto: no jogo entram as mãos padrão (ViewModel.Maos)
    E.salvar()
    if ficha:
        entrada['camera'] = CAMERA                                        # o jogo gira a câmera com este osso (ViewModel.Rig)
        d = json.load(open(ficha, encoding='utf-8')); d[entrada.pop('chave')] = entrada
        json.dump(d, open(ficha, 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True); open(ficha, 'a', encoding='utf-8').write('\n')


def clipes_da_ficha(E, nomes):
    return {n: {'clipe': n, 'de': 0.0, 'ate': round(E.duracao(n), 4)} for n in nomes}


# ---------------------------------------------------------------- o movimento de referência
class Movimento:
    """
    O que a arma e a câmera fazem em cada animação de fuzil do tutorial (os .uasset em
    ``assets/pacotes/infima/``): por clipe, o movimento da arma a partir da pose parada,
    **no espaço dela** (serve para qualquer arma, em qualquer lugar da tela), e o giro
    da cabeça, que no jogo vira o movimento da câmera.
    """
    ARQUIVOS = dict(parado='Idle_Loop', andar='Walk_F_Loop', tiro='Fire', sacar='Equip', guardar='Holster', recarga='Reload')

    def __init__(self, AR, fonte=recarga.FONTE):
        self.fps = 30.0; self.clipes = {}
        P = AR.parado; Ro = rot(AR.R2O)
        def poses(arquivo):
            ossos, n, _ = uasset.curvas(recarga.achar(f'A_FP_AssaultRifle_{arquivo}.uasset', fonte)); out = []
            for k in range(n):
                p = dict(P)
                for nome, (T, Q) in ossos.items():
                    if nome != 'root' and nome in AR.c.names: p[AR.N(nome)] = (T[k], Q[k])
                out.append(p)
            return out
        ref = poses('Idle_Pose')[0]; G0 = AR.arma_de(ref); H0 = rot(AR.mundo(ref, 'head'))
        for nome, arquivo in self.ARQUIVOS.items():
            ds, cs = [], []
            for p in poses(arquivo):
                ds.append(np.linalg.inv(G0) @ AR.arma_de(p))
                R = Ro @ rot(AR.mundo(p, 'head')) @ H0.T @ Ro.T
                cs.append(np.degrees([np.arcsin(np.clip(-R[1, 2], -1, 1)), np.arctan2(R[0, 2], R[2, 2]), np.arctan2(R[1, 0], R[1, 1])]))
            self.clipes[nome] = (ds, np.array(cs))

    def duracao(self, nome): return (len(self.clipes[nome][0]) - 1) / self.fps

    def _em(self, nome, t):
        ds, cs = self.clipes[nome]; x = min(max(t * self.fps, 0.0), len(ds) - 1.0); i = min(int(x), len(ds) - 2); return ds, cs, i, x - i

    def arma(self, nome, t, forca=1.0):
        """O movimento da arma no instante t do clipe (4×4 no espaço do osso da arma), vezes ``forca``."""
        ds, _, i, u = self._em(nome, t); return parte(entre(ds[i], ds[i + 1], u), forca)

    def camera(self, nome, t, forca=1.0):
        """(pitch, yaw, roll) da câmera, em graus."""
        _, cs, i, u = self._em(nome, t); return (cs[i] * (1 - u) + cs[i + 1] * u) * forca


CAMERA = 'interaction'      # o osso que carrega o movimento da câmera: a translação dele é (pitch, yaw, roll) em graus


def construir(E, nome, dur, quadro, base):
    """
    Grava um clipe a partir de ``quadro(t)``, que diz, para cada instante:

    - ``G``: a arma (4×4 no espaço da raiz);
    - ``pega``: ``{'r': 4×4, 'l': 4×4}``, cada mão presa na arma (no espaço do osso dela);
    - ``livre``: ``{'l': 4×4}``, mão solta, com o alvo no espaço da raiz;
    - ``dedos``: ``{'r': pose de dedos, 'l': ...}``;
    - ``pecas``: ``{osso: 4×4}``, o movimento de cada peça no espaço da arma;
    - ``cam``: (pitch, yaw, roll) da câmera, em graus;
    - ``pulso``: os lados (``'r'``, ``'l'``) em que o antebraço se alinha com a mão (``pulso_reto``).

    ``base`` é a pose de partida (o corpo e o lado para onde os cotovelos dobram).
    """
    n = int(round(dur * FPS)) + 1; quadros = []; falta = 0.0
    for k in range(n):
        pose, f = posar(E, quadro(min(k / FPS, dur)), base); falta = max(falta, f); quadros.append(pose)
    E.gravar_clipe(nome, quadros, dur)
    return falta


def posar(E, q, base):
    """A pose de um quadro (o que ``construir`` grava): devolve (pose, o alcance que faltou)."""
    # ``corpo``: a pose de onde saem os ombros e o lado para onde os cotovelos dobram neste quadro (por omissão, a de base)
    cam = E.N(CAMERA); corpo = q.get('corpo', base); pose = dict(corpo); falta = 0.0
    for lado, dedos in q.get('dedos', {}).items(): E.por_dedos(pose, dedos)
    falta = max(falta, E.prender(pose, q['G'], q.get('pega', {}), corpo))
    for lado, alvo in q.get('livre', {}).items():
        falta = max(falta, E.braco(pose, lado, alvo, corpo)); E.por_local(pose, f'ik_hand_{lado}', E.mundo(pose, E.mao[lado]))
    for lado in q.get('braco', ()): antebraco_pela_mao(E, pose, lado)
    for lado in q.get('pulso', ()):                                       # o pulso não quebra: a mão cede, e a arma vai com ela
        K = pulso_reto(E, pose, lado)
        if K is not None and lado in q.get('pega', {}):
            pose = dict(base)
            for _, dedos in q.get('dedos', {}).items(): E.por_dedos(pose, dedos)
            E.prender(pose, K @ q['G'], q.get('pega', {}), base)
            for l2, alvo in q.get('livre', {}).items(): E.braco(pose, l2, alvo, base); E.por_local(pose, f'ik_hand_{l2}', E.mundo(pose, E.mao[l2]))
    for osso, D in q.get('pecas', {}).items():
        i = E.N(osso); m = D @ trs(*base[i]); pose[i] = (m[:3, 3], matq(rot(m)))
    pose[cam] = (np.array(q.get('cam', (0.0, 0.0, 0.0)), float), base[cam][1])
    return pose, falta


def dobra_do_pulso(E, pose, lado):
    """Quantos graus a mão está dobrada em relação ao antebraço (0 = em linha, como na pose de costura)."""
    la, hd = E.N(f'lowerarm_{lado}'), E.N(f'hand_{lado}'); Wl, Wh = E.mundo(pose, la), E.mundo(pose, hd); f = Wh[:3, 3] - Wl[:3, 3]; f /= np.linalg.norm(f)
    Bh, _ = E.ligacao(f'hand_{lado}'); Bl, _ = E.ligacao(f'lowerarm_{lado}'); Rh = rot(Bh); Rh = Rh / np.linalg.norm(Rh, axis=0, keepdims=True); d = Bh[:3, 3] - Bl[:3, 3]
    Rw = rot(Wh); Rw = Rw / np.linalg.norm(Rw, axis=0, keepdims=True)
    return float(np.degrees(np.arccos(np.clip(np.dot(f, Rw @ (Rh.T @ (d / np.linalg.norm(d)))), -1.0, 1.0))))


def pulso_reto(E, pose, lado, maximo=40.0):
    """
    Não deixa o pulso quebrar (relato do Jhon, 19/11, com a granada: "cuidado com as articulações da mão,
    assim ficou torto e estranho"). Com a mão solta (faca, granada), ela ia para onde o gesto pedia e o
    braço era resolvido depois: o antebraço chegava de baixo e a mão dobrava 90° no pulso.
    O ombro e o cotovelo ficam onde estão (o braço vem de onde um braço vem); quem cede é a mão: gira em
    torno do pulso até sobrar no máximo ``maximo`` graus de dobra. Devolve esse giro (4×4 no espaço da
    raiz, para levar a arma junto) ou None se o pulso já está bom.
    """
    la, hd = E.N(f'lowerarm_{lado}'), E.N(f'hand_{lado}')
    Wl, Wh = E.mundo(pose, la), E.mundo(pose, hd); C, H = Wl[:3, 3], Wh[:3, 3]; f = (H - C) / np.linalg.norm(H - C)
    Bh, _ = E.ligacao(f'hand_{lado}'); Bl, _ = E.ligacao(f'lowerarm_{lado}')
    Rh = rot(Bh); Rh = Rh / np.linalg.norm(Rh, axis=0, keepdims=True); d = Bh[:3, 3] - Bl[:3, 3]
    Rw = rot(Wh); Rw = Rw / np.linalg.norm(Rw, axis=0, keepdims=True)
    reto = Rw @ (Rh.T @ (d / np.linalg.norm(d)))                         # onde o antebraço estaria com o pulso reto
    ang = float(np.degrees(np.arccos(np.clip(np.dot(f, reto), -1.0, 1.0))))
    if ang <= maximo: return None
    eixo = np.cross(reto, f); eixo /= np.linalg.norm(eixo)
    K = trs(np.zeros(3), eixo_angulo(eixo, ang - maximo)); K[:3, 3] = H - K[:3, :3] @ H
    return K


def com_pulso(E, G, pega, base, lado='r'):
    """A arma em G já corrigida pelo ``pulso_reto`` da mão ``lado`` (até três voltas): para quem precisa saber onde a
    arma fica de verdade antes de montar o quadro (peças que saem dela, a outra mão que vem até ela)."""
    for _ in range(3):
        pose = dict(base); E.prender(pose, G, pega, base); K = pulso_reto(E, pose, lado)
        if K is None: break
        G = K @ G
    return G


def mao_pelo_braco(E, base, lado, olho, rolo=0.0, dobra=(0.0, 0.0)):
    """
    A mão a partir do braço, para o pulso nunca quebrar: o pulso vai ao ponto ``olho`` (x, y, z no espaço
    do olho, m), o cotovelo sai da cinemática inversa com o ombro de ``base``, e a mão fica na linha do
    antebraço, girada ``rolo`` graus em torno dele (pronação) e com uma dobra pequena de pulso
    (``dobra`` = graus em dois eixos da mão). Devolve a mão (4×4 no espaço da raiz).
    """
    ua, la, hd = (E.N(f'{o}_{lado}') for o in ('upperarm', 'lowerarm', 'hand'))
    S = E.mundo(base, ua)[:3, 3]; W = (E.O2R @ np.append(np.asarray(olho, float), 1.0))[:3]
    L1, L2 = np.linalg.norm(base[la][0]), np.linalg.norm(base[hd][0]); d = min(np.linalg.norm(W - S), (L1 + L2) * 0.985); eixo = (W - S) / np.linalg.norm(W - S)
    a = (L1 ** 2 - L2 ** 2 + d * d) / (2 * d); h = np.sqrt(max(L1 ** 2 - a * a, 0.0))
    polo = E.mundo(base, la)[:3, 3] - S; polo = polo - np.dot(polo, eixo) * eixo; polo /= np.linalg.norm(polo)
    C = S + eixo * a + polo * h; f = (S + eixo * d - C) / L2
    Bh, _ = E.ligacao(f'hand_{lado}'); Bl, _ = E.ligacao(f'lowerarm_{lado}'); Rb = rot(Bh); Rb = Rb / np.linalg.norm(Rb, axis=0, keepdims=True)
    db = Bh[:3, 3] - Bl[:3, 3]; db /= np.linalg.norm(db)
    # Sem torcer o antebraço: a mão parte de onde o antebraço resolvido a deixaria com o pulso solto (a mesma conta do
    # braço: o antebraço da pose de base levado para a direção nova), e o rolo é o que se torce a partir daí. Girar a mão
    # a partir da pose de costura torcia o pulso em 90° e a manga estrangulava ("o pulso ficou torto", Jhon, 19/11).
    Wl = E.mundo(base, la); fb = E.mundo(base, hd)[:3, 3] - Wl[:3, 3]; Rl = rot(Bl); Rl = Rl / np.linalg.norm(Rl, axis=0, keepdims=True)
    R = rot(trs(np.zeros(3), eixo_angulo(f, rolo))) @ arco(fb, f) @ rot(Wl) @ (Rl.T @ Rb) @ rot(giro(pitch=dobra[0], yaw=dobra[1]))
    H = np.eye(4); H[:3, :3] = R; H[:3, 3] = W
    return H


def antebraco_pela_mao(E, pose, lado, maximo=18.0):
    """O contrário de ``pulso_reto``, para movimento que já veio de um animador (a faca): a mão fica exatamente onde a
    animação a pôs e quem se ajeita é o braço — o antebraço vem para a linha da mão (sobra no máximo ``maximo`` graus de
    dobra) e o cotovelo e o ombro, fora da tela, vão para onde for preciso."""
    cl, ua, la, hd = (E.N(f'{o}_{lado}') for o in ('clavicle', 'upperarm', 'lowerarm', 'hand'))
    Wu, Wl, Wh = (E.mundo(pose, i) for i in (ua, la, hd)); S, C, H = Wu[:3, 3], Wl[:3, 3], Wh[:3, 3]
    L1, L2 = np.linalg.norm(C - S), np.linalg.norm(H - C); f = (H - C) / L2
    Bh, _ = E.ligacao(f'hand_{lado}'); Bl, _ = E.ligacao(f'lowerarm_{lado}'); Rh = rot(Bh); Rh = Rh / np.linalg.norm(Rh, axis=0, keepdims=True)
    d = Bh[:3, 3] - Bl[:3, 3]; Rw = rot(Wh); Rw = Rw / np.linalg.norm(Rw, axis=0, keepdims=True); reto = Rw @ (Rh.T @ (d / np.linalg.norm(d)))
    ang = float(np.degrees(np.arccos(np.clip(np.dot(f, reto), -1.0, 1.0))))
    if ang <= maximo: return
    th = np.radians(ang); t = 1.0 - maximo / ang; f2 = np.sin((1 - t) * th) * f + np.sin(t * th) * reto; f2 /= np.linalg.norm(f2)
    C2 = H - L2 * f2; S2 = C2 - L1 * (C2 - S) / np.linalg.norm(C2 - S)
    pai = E.mundo(pose, E.c.parent[cl]); pose[cl] = (pose[cl][0] + rot(pai).T @ (S2 - S), pose[cl][1])
    Wc = E.mundo(pose, cl); Ru = arco(C - S, C2 - S2) @ rot(Wu); Rl = arco(H - C, H - C2) @ rot(Wl)
    pose[ua] = (pose[ua][0], matq(rot(Wc).T @ Ru)); pose[la] = (pose[la][0], matq(Ru.T @ Rl)); pose[hd] = (pose[hd][0], matq(Rl.T @ rot(Wh)))


def por_ombro(E, pose, lado, x, y, z):
    """Leva o ombro para um ponto do olho (m). O do rig do AR-15 fica acima da câmera: com a mão
    solta (faca, granada), o braço atravessaria a tela vindo de cima."""
    cl = E.N(f'clavicle_{lado}'); ua = E.N(f'upperarm_{lado}')
    alvo = (E.O2R @ np.array([x, y, z, 1.0]))[:3]; pai = E.mundo(pose, E.c.parent[cl])
    pose[cl] = (pose[cl][0] + rot(pai).T @ (alvo - E.mundo(pose, ua)[:3, 3]), pose[cl][1])


def arma_no_olho(E, x, y, z, pitch=0.0, yaw=0.0, roll=0.0):
    """A arma (o osso dela, no espaço da raiz) com o punho em (x, y, z) do olho e o giro dado."""
    return rigida(E.O2R @ mover(x, y, z) @ giro(pitch, yaw, roll) @ np.linalg.inv(ARMA))


def no_olho(E, x, y, z, pitch=0.0, yaw=0.0, roll=0.0):
    """Um alvo solto (para uma mão), dado no espaço do olho, como 4×4 da raiz."""
    return rigida(E.O2R @ mover(x, y, z) @ giro(pitch, yaw, roll) @ np.linalg.inv(ARMA))


# ---------------------------------------------------------------- armas longas
# lugar: a arma em relação à do AR-15, no olho (m). apoio: a mão esquerda em relação ao guarda-mão do AR-15.
# carregador: o carregador em relação ao do AR-15. ferrolho: quanto ele recua no tiro (m).
LONGAS = dict(
    m4a4=dict(lugar=(0.0, -0.004, 0.014), recarga_forca=1.12, apoio=na_arma(), carregador=na_arma(z=-0.008, y=-0.004), ferrolho=0.07,
              tiro=dict(dur=0.5, forca=1.0, lento=1.0, cam=1.0), sacar=0.5, guardar=0.5, parado=dict(dur=2.3, forca=1.0), andar=dict(dur=0.8, forca=1.0), recarga=2.3),
    ak74=dict(fechar=dict(), lugar=(0.0, -0.004, 0.0), apoio=na_arma(z=-0.030, y=-0.010), carregador=na_arma(z=-0.035, y=-0.025), ferrolho=0.09,
              tiro=dict(dur=0.5, forca=1.3, lento=1.0, cam=1.3), sacar=0.55, guardar=0.5, parado=dict(dur=2.3, forca=1.0), andar=dict(dur=0.82, forca=1.15), recarga=2.5),
    scar=dict(fechar=dict(), lugar=(0.006, -0.022, 0.0), apoio=na_arma(z=-0.020, y=0.004), carregador=na_arma(z=-0.018, y=0.010), ferrolho=0.08,
              tiro=dict(dur=0.5, forca=1.55, lento=1.1, cam=1.6), sacar=0.6, guardar=0.55, parado=dict(dur=2.3, forca=1.1), andar=dict(dur=0.86, forca=1.35), recarga=2.9),
    svd=dict(fechar=dict(), lugar=(0.030, -0.050, -0.040), apoio=na_arma(z=-0.070), carregador=na_arma(z=-0.058, y=0.030), ferrolho=0.09,
             tiro=dict(dur=0.8, forca=2.4, lento=1.4, cam=2.2), sacar=0.6, guardar=0.5, parado=dict(dur=2.6, forca=1.0), andar=dict(dur=0.86, forca=1.1), recarga=3.0),
)


def _guarda_mao(AR, P, G0, id_, frente=0.20, tras=0.08):
    """(a pose do AR-15 com as mãos onde ficam na arma ``id_``, o guarda-mão dela como sólido)."""
    import pegada
    pega = {'r': AR.mao_na_arma(AR.parado, 'r'), 'l': LONGAS[id_]['apoio'] @ AR.mao_na_arma(AR.parado, 'l')}
    prova = dict(P); AR.prender(prova, G0, pega, P)
    zm = (np.linalg.inv(G0 @ ARMA) @ AR.mundo(prova, AR.N('hand_l')))[2, 3]
    tri = np.vstack([m['pos'] for _, _, m in reais.arma(id_)['pecas']]); q = tri.reshape(-1, 3)
    fundo = q[(q[:, 2] > zm - 0.16) & (q[:, 2] < zm - 0.06)][:, 1].min() - 0.012      # o fundo do guarda-mão: o carregador, atrás, fica de fora
    return prova, pegada.Solido.de_malha(tri, zm - frente, zm + tras, acima=fundo)


def fechar_no_guarda_mao(AR, P, G0, id_, reta=(-0.25, -1.0, 0.0)):
    """A mão esquerda fechada no guarda-mão da arma ``id_`` **como na M4A4**, que é a pegada aprovada: a palma tão
    encostada quanto lá e cada falange à distância do guarda-mão que tem lá. Devolve (o apoio novo — no lugar de
    ``LONGAS[id]['apoio']`` —, os dedos, um resumo das medidas em mm)."""
    import pegada
    pele = pegada.Pele(); M = Maos(AR)
    la, solido_m4 = _guarda_mao(AR, P, G0, 'm4a4'); ref = pegada.distancias(AR, la, 'l', solido_m4, pele)
    prova, solido = _guarda_mao(AR, P, G0, id_); antes = pegada.distancias(AR, prova, 'l', solido, pele)
    andou, dedos, ficou = pegada.pegar_como(AR, prova, 'l', solido, ref, AR.dedos_de(P, 'l'), M.fechada('l', 1.0), reta, pele=pele)
    pontas = [(d, '03') for d in pegada.FALANGES]
    resumo = dict(palma=(round(ref['palma'] * 1000, 1), round(antes['palma'] * 1000, 1), round(ficou['palma'] * 1000, 1)),
                  pontas_dos_dedos={d: (round(ref[k] * 1000, 1), round(antes[k] * 1000, 1), round(ficou[k] * 1000, 1)) for d, k in zip(pegada.FALANGES, pontas)})
    return na_arma(*(-andou)) @ LONGAS[id_]['apoio'], dedos, resumo


RECARGA_PERTO = 0.0          # quanto a arma longa vem para a câmera no meio da recarga (m): com os ombros do animador, não precisa


def longa(id_, mov, ficha=None):
    S = LONGAS[id_]; E, AR = abrir(id_)
    P = AR.parado; G0 = AR.arma_de(P)
    lugar = rigida(AR.O2R @ mover(*S['lugar']) @ AR.R2O)                 # o deslocamento, no espaço da raiz
    Gp = lugar @ G0                                                       # a arma parada
    pega = {'r': AR.mao_na_arma(P, 'r'), 'l': S['apoio'] @ AR.mao_na_arma(P, 'l')}
    dedos_r, sobra = Maos(AR).no_gatilho(AR.dedos_de(P, 'r'), reais.arma(id_)['gatilho']); P = dict(P); AR.por_dedos(P, dedos_r)
    print(f"  {id_}: o indicador fica a {sobra:.1f} mm do gatilho")
    apoio = S['apoio']; dedos_l = AR.dedos_de(P, 'l')
    if 'fechar' in S:
        # A mão esquerda fecha no guarda-mão **desta** arma (Jhon, 19/11: "todas as armas devem estar com os dedos
        # segurando a arma, M4A4 tá perfeita"): com os dedos do AR-15, num guarda-mão mais baixo as pontas ficavam no ar,
        # acima dele. A palma encosta por baixo e cada dedo dobra até ficar como fica na M4A4 (``pegada.pegar_como``).
        apoio, dedos_l, medidas = fechar_no_guarda_mao(AR, P, G0, id_, **S['fechar']); pega['l'] = apoio @ AR.mao_na_arma(AR.parado, 'l'); AR.por_dedos(P, dedos_l)
        print(f"  {id_}: a mão esquerda no guarda-mão, em mm (na M4A4, aqui antes, aqui agora) — palma {medidas['palma']}; pontas dos dedos {medidas['pontas_dos_dedos']}")
    mag = AR.N('Magazine'); Mm0 = trs(*P[mag])
    feitos = {}
    def laco(nome, dur, forca):                                          # parado e andar: o clipe da referência, no tempo pedido
        k = mov.duracao(nome) / dur
        return construir(E, nome, dur, lambda t: dict(G=Gp @ mov.arma(nome, t * k, forca), pega=pega, cam=mov.camera(nome, t * k)), P)
    feitos['parado'] = laco('parado', S['parado']['dur'], S['parado']['forca'])
    feitos['andar'] = laco('andar', S['andar']['dur'], S['andar']['forca'])
    # as bases da mira: a mesma respiração e o mesmo passo, quase parados e sem câmera (``mirar`` as usa e as tira)
    kp, ka = mov.duracao('parado') / S['parado']['dur'], mov.duracao('andar') / S['andar']['dur']
    feitos['parado_mira'] = construir(E, 'parado_mira', S['parado']['dur'], lambda t: dict(G=Gp @ mov.arma('parado', t * kp, 0.22), pega=pega), P)
    feitos['andar_mira'] = construir(E, 'andar_mira', S['andar']['dur'], lambda t: dict(G=Gp @ mov.arma('andar', t * ka, 0.25), pega=pega), P)
    T = S['tiro']                                                        # o tiro: o coice da referência, mais forte e (no fuzil de precisão) mais lento para voltar
    vai = Chaves((0, 0), (0.008, 0), (0.035, 1), (0.05, 1), (0.095, 0), (T['dur'], 0), facil=False)      # o ferrolho recua e volta
    feitos['tiro'] = construir(E, 'tiro', T['dur'], lambda t: dict(G=Gp @ mov.arma('tiro', t / T['lento'] if t > 0.07 else t, T['forca']), pega=pega,
                                                                  pecas={'Bolt': na_arma(z=S['ferrolho'] * vai(t))},
                                                                  cam=mov.camera('tiro', t / T['lento'] if t > 0.07 else t, T['cam'])), P)
    for nome in ('sacar', 'guardar'):                                    # o saque e o guardar da referência (1,0 e 0,7 s), no tempo do jogo
        k = mov.duracao(nome) / S[nome]
        feitos[nome] = construir(E, nome, S[nome], lambda t, nome=nome, k=k: dict(G=Gp @ mov.arma(nome, t * k), pega=pega, cam=mov.camera(nome, t * k, 0.6)), P)
    # a recarga: a do AR-15 (a da referência, já sobre a pose do Jhon), com a mão esquerda levada ao carregador desta arma
    dur = S['recarga']; k = AR.duracao('recarga') / dur; kc = mov.duracao('recarga') / dur
    def recarregar(t):
        ta = t * k; antes = AR.pose_do_clipe('recarga', ta)
        G = Gp @ parte(np.linalg.inv(G0) @ AR.arma_de(antes), S.get('recarga_forca', 1.3)); w = recarga_peso(ta)      # o gesto da arma, maior (Jhon, 19/11: "mais movimento na câmera e no braço")
        # No meio do gesto, enquanto a mão esquerda está fora do guarda-mão, a arma vem um nada para a câmera: o começo do
        # braço, que aparecia no giro para encaixar o carregador, fica fora da tela (Jhon, 19/11: "dá para ver resquícios
        # do movimento onde não deveria; aproxima pouquíssima da câmera já resolve").
        G = rigida(AR.O2R @ mover(0.0, 0.0, RECARGA_PERTO * (1 - w)) @ AR.R2O) @ G
        leva = entre(S['carregador'], apoio, w)
        D = trs(*antes[mag]) @ np.linalg.inv(Mm0)                         # o movimento do carregador, no espaço da arma
        # os ombros e os cotovelos são os do animador neste instante (com os da pose parada, o cotovelo esquerdo passava na
        # frente da câmera quando a mão ia buscar o carregador: um vulto escuro de um décimo de segundo)
        return dict(G=G, corpo=antes, pega={'r': pega['r'], 'l': leva @ AR.mao_na_arma(antes, 'l')}, dedos={'r': dedos_r, 'l': misturar_dedos(AR.dedos_de(antes, 'l'), dedos_l, w) if 'fechar' in S else AR.dedos_de(antes, 'l')},
                    pecas={'Magazine': S['carregador'] @ D @ np.linalg.inv(S['carregador'])}, cam=mov.camera('recarga', recarga.relogio(ta), 2.8))
    feitos['recarga'] = construir(E, 'recarga', dur, recarregar, P)
    for nome, falta in feitos.items(): print(f"  {id_}.{nome}: {E.duracao(nome):.2f} s, alcance que faltou {falta * 10:.1f} mm")
    entrada = dict(chave=id_, armas=[id_], arquivo=f'res://assets/armas/fp/{id_}.glb', ajuste=[0.0, 0.0, 0.0], fiel=True, fov=63.09, sacar=True,
                   animacoes=clipes_da_ficha(E, feitos), cano=boca(E, reais.arma(id_)['cano']))
    fechar(E, ficha, entrada)
    return E.path


def recarga_peso(t_jogo):
    """Na recarga do AR-15 (2,3 s), quanto a mão esquerda está no guarda-mão (1) ou no carregador (0)."""
    tau = recarga.relogio(float(t_jogo))
    return max(1 - suave((tau - recarga.SAIDA[0]) / (recarga.SAIDA[1] - recarga.SAIDA[0])),
               suave((tau - recarga.VOLTA[0]) / (recarga.VOLTA[1] - recarga.VOLTA[0])))


def boca(E, ponto):
    """A boca do cano (dada no espaço da arma) no espaço do olho, com a arma parada."""
    G = E.arma_de(E.pose_do_clipe('parado', 0.0))
    p = E.R2O @ G @ ARMA @ np.append(np.asarray(ponto, float), 1.0)
    return [round(float(x), 4) for x in p[:3]]


# ---------------------------------------------------------------- o que as três armas curtas têm em comum
ESPELHO = np.diag([-1.0, 1, 1, 1])        # o plano da arma, no espaço do osso dela
PONTO = np.diag([-1.0, -1, -1, 1])        # no manequim, a mão de um lado é a do outro com os eixos trocados de sinal


def da_arma(m):
    """Uma 4×4 dada no espaço da arma (m) como 4×4 no osso da arma (cm)."""
    return ARMA @ m @ np.linalg.inv(ARMA)


class Maos:
    """As poses de mão tiradas do AR-15: a direita no punho, a esquerda no guarda-mão, a
    esquerda segurando um carregador e a mão aberta."""
    def __init__(self, AR):
        self.AR = AR; P = AR.parado
        self.punho = AR.mao_na_arma(P, 'r')                               # a mão direita no punho (osso da arma)
        self.dedos_punho = AR.dedos_de(P, 'r')
        self.dedos_apoio = AR.dedos_de(P, 'l')
        segura = AR.pose_do_clipe('recarga', 1.25)                        # a esquerda trazendo o carregador novo
        self.dedos_carregador = AR.dedos_de(segura, 'l')
        self.mao_no_carregador = np.linalg.inv(AR.mundo(segura, 'Magazine')) @ AR.mundo(segura, AR.mao['l'])
        aberta = AR.pose_do_clipe('recarga', 0.09)                        # a esquerda saindo do guarda-mão, aberta
        self.dedos_aberta = AR.dedos_de(aberta, 'l')

    def fechada(self, lado, aperto=1.0, indicador=1.0):
        """A mão fechada num cabo: a do punho, com o indicador dobrado como os outros;
        ``aperto`` > 1 fecha mais (cabo fino), < 1 abre (objeto grosso)."""
        AR = self.AR; pose = dict(AR.parado); AR.fechar_indicador(pose, 'r', indicador)
        d = AR.dedos_de(pose, 'r'); aberta = AR.espelhar_dedos(self.dedos_aberta, 'r')
        d = {i: (T, slerp(aberta[i][1], Q, aperto)) for i, (T, Q) in d.items()}
        return d if lado == 'r' else AR.espelhar_dedos(d, 'l')

    def aberta(self, lado): return self.dedos_aberta if lado == 'l' else self.AR.espelhar_dedos(self.dedos_aberta, 'r')

    def no_gatilho(self, dedos, gatilho):
        """Leva o indicador direito ao gatilho (ponto no espaço da arma, m): o dedo é resolvido como um
        braço de dois ossos, do nó da mão ao começo da última falange — que é o que encosta no gatilho
        no AR-15. Devolve (dedos, distância que sobrou em mm)."""
        AR = self.AR; pose = dict(AR.parado); AR.por_dedos(pose, dedos)
        G = AR.arma_de(pose); alvo = (G @ ARMA @ np.append(np.asarray(gatilho, float), 1.0))[:3]
        i1, i2, i3 = (AR.N(f'index_{k}_r') for k in ('01', '02', '03'))
        toque = np.append(pose[i3][0] / np.linalg.norm(pose[i3][0]) * 0.7, 1.0)       # 7 mm depois da última junta
        W1, W2, W3 = (AR.mundo(pose, i) for i in (i1, i2, i3)); pai = AR.mundo(pose, AR.c.parent[i1])
        A, B0, C0 = W1[:3, 3], W2[:3, 3], (W3 @ toque)[:3]
        L1, L2 = np.linalg.norm(B0 - A), np.linalg.norm(C0 - B0)
        T = alvo.copy(); d = np.linalg.norm(T - A)
        d2 = min(max(d, abs(L1 - L2) * 1.02), (L1 + L2) * 0.995); T = A + (T - A) / d * d2
        eixo = (T - A) / d2; a = (L1 ** 2 - L2 ** 2 + d2 * d2) / (2 * d2); h = np.sqrt(max(L1 ** 2 - a * a, 0.0))
        polo = B0 - A; polo = polo - np.dot(polo, eixo) * eixo; polo /= np.linalg.norm(polo)
        B = A + eixo * a + polo * h
        R1 = arco(B0 - A, B - A) @ rot(W1); R2 = arco(C0 - B0, T - B) @ rot(W2)
        out = dict(dedos)
        out[i1] = (dedos[i1][0], matq(rot(pai).T @ R1)); out[i2] = (dedos[i2][0], matq(R1.T @ R2))
        AR.por_dedos(pose, out); fica = (AR.mundo(pose, i3) @ toque)[:3]
        return out, float(np.linalg.norm(fica - alvo)) * 10


    def espelhada(self, ajuste=np.eye(4)):
        """A mão esquerda como espelho da direita no punho (o apoio da pistola), com um ajuste no espaço do osso da arma."""
        return ajuste @ ESPELHO @ self.punho @ PONTO


def misturar_dedos(a, b, u): return {i: (a[i][0], slerp(a[i][1], b[i][1], u)) for i in a}


def camadas(mov, Gp, nome, t, k, forca):
    """A arma parada mais o que a referência faz com ela neste clipe."""
    return Gp @ mov.arma(nome, t * k, forca)


def comuns(E, mov, Gp, pose, base, tempos, forcas, cam=1.0):
    """parado, andar, sacar e guardar de uma arma curta: os da referência, no lugar e no tempo dela."""
    feitos = {}
    for nome in ('parado', 'andar', 'sacar', 'guardar'):
        if nome not in tempos: continue
        k = mov.duracao(nome) / tempos[nome]; f = forcas.get(nome, 1.0)
        feitos[nome] = construir(E, nome, tempos[nome], lambda t, nome=nome, k=k, f=f: dict(G=camadas(mov, Gp, nome, t, k, f), cam=mov.camera(nome, t * k, cam), **pose), base)
    return feitos


# ---------------------------------------------------------------- a pistola
class RefPistola:
    """
    As animações de pistola que o Jhon trouxe (19/11, "referência pistola": "pistol-43-tactical-fps-animations",
    ``Glock_Anim.fbx``), assadas por ``tools/armas/blender/glock.py`` em ``dados/glock.npz``: quadro a quadro (60 por
    segundo), a câmera, a pistola (``Wpn_body``), o carregador, o ferrolho, os pulsos e a base dos dedos.

    Daqui sai o movimento, não o lugar: a pistola da referência é segurada com uma mão só e em outro ponto da tela; a
    nossa fica onde o Jhon a acertou, e recebe o que a da referência faz a partir do primeiro quadro do tiro. Também o
    carregador e o ferrolho (em relação à arma), a mão esquerda (em relação à arma) e o giro da câmera.
    """
    FPS = 60.0
    UNIDADE = 0.186 / 8.57          # metros por unidade do arquivo: a Glock tem 18,6 cm e 8,57 unidades

    def __init__(self, AR, punho):
        d = np.load(os.path.join(reais.DADOS, 'glock.npz')); self.d = d; self.o = [str(x) for x in d['ossos']]
        def rig(m):
            m = m.astype(float).copy(); m[:3, :3] /= np.linalg.norm(m[:3, :3], axis=0, keepdims=True); return m
        self.rig = rig
        ref = d['Shot_ossos'][0]; self.Wref = rig(ref[self.o.index('Wpn_body')]); self.Cref = rig(d['Shot_cam'][0])
        # da arma da referência (unidades, osso Wpn_body: x à esquerda de quem olha, y para cima, z para a frente) para a
        # nossa (m: x direita, y cima, z trás), com a origem posta de modo que o pulso direito caia na nossa mão
        B = np.diag([-1.0, 1.0, -1.0, 1.0]); S = np.diag([self.UNIDADE] * 3 + [1.0])
        pulso = (np.linalg.inv(self.Wref) @ rig(ref[self.o.index('r_wrist')]))[:3, 3]
        nossa = (np.linalg.inv(ARMA) @ punho)[:3, 3]
        self.A = mover(*(nossa - (B @ S @ np.append(pulso, 1.0))[:3])) @ B @ S; self.Ai = np.linalg.inv(self.A)
        self.rel_ref = {n: np.linalg.inv(self.Wref) @ rig(ref[self.o.index(n)]) for n in ('Magazine', 'Slide')}

    def duracao(self, acao): return (len(self.d[acao + '_cam']) - 1) / self.FPS

    def _q(self, acao, t):
        C, O = self.d[acao + '_cam'], self.d[acao + '_ossos']; x = min(max(t * self.FPS, 0.0), len(C) - 1.001); k = int(x); u = x - k
        return [entre(self.rig(C[k]), self.rig(C[k + 1]), u)], [entre(self.rig(O[k][i]), self.rig(O[k + 1][i]), u) for i in range(len(self.o))]

    def arma(self, acao, t, forca=1.0):
        """O movimento da arma (espaço da nossa arma, m) desde o primeiro quadro do tiro, visto da câmera, vezes ``forca``."""
        (C,), O = self._q(acao, t)
        Wc = np.linalg.inv(C) @ O[self.o.index('Wpn_body')]; Wc0 = np.linalg.inv(self.Cref) @ self.Wref
        return parte(self.A @ np.linalg.inv(Wc0) @ Wc @ self.Ai, forca)

    def peca(self, acao, nome, t, forca=1.0):
        """O movimento de uma peça (Magazine, Slide) em relação à arma, no espaço da nossa arma (m)."""
        _, O = self._q(acao, t); W = O[self.o.index('Wpn_body')]
        rel = np.linalg.inv(W) @ O[self.o.index(nome)]
        return parte(self.A @ rel @ np.linalg.inv(self.rel_ref[nome]) @ self.Ai, forca)

    def camera(self, acao, t, forca=1.0):
        """(pitch, yaw, roll) da câmera em graus, desde o primeiro quadro do tiro."""
        (C,), _ = self._q(acao, t); R = rot(self.Cref).T @ rot(C)
        return np.degrees([np.arcsin(np.clip(-R[1, 2], -1, 1)), np.arctan2(R[0, 2], R[2, 2]), np.arctan2(R[1, 0], R[1, 1])]) * forca

    def esquerda(self, acao, t):
        """O referencial da base dos dedos da mão esquerda (origem no pulso) no espaço da nossa arma (m)."""
        _, O = self._q(acao, t); Wi = np.linalg.inv(O[self.o.index('Wpn_body')])
        p = [(self.A @ Wi @ O[self.o.index(n)])[:3, 3] for n in ('l_wrist', 'l_middle_low', 'l_index_low', 'l_pinky_low')]
        return p


def referencial(o, m, i, p):
    """Um referencial da mão: x do pulso à base do dedo médio, y do mindinho ao indicador."""
    x = (m - o) / np.linalg.norm(m - o); y = i - p; z = np.cross(x, y); z /= np.linalg.norm(z); y = np.cross(z, x)
    F = np.eye(4); F[:3, :3] = np.c_[x, y, z]; F[:3, 3] = o; return F


def abracar(AR, M, base, apoio, id_):
    """A mão esquerda abraçando a direita no punho da pistola, **sem uma passar por dentro da outra** (Jhon, 19/11): a
    direita (com os dedos já no punho e no gatilho) e o punho da arma viram um sólido; a esquerda encosta a palma nele
    pelo lado e fecha dedo por dedo até a pele tocar (``pegada.pegar``). Devolve (o apoio novo, os dedos, as medidas)."""
    import pegada
    pele = pegada.Pele(); G0 = AR.arma_de(AR.parado); prova = dict(base); AR.prender(prova, G0, {'r': M.punho, 'l': apoio}, base)
    Q = pele.em(AR, prova, np.linalg.inv(G0 @ ARMA)); de = lambda *nomes: np.array([n.endswith('_r') and n.startswith(nomes) for n in pele.dono])
    dedos_r, palma_r = Q[de('index', 'middle', 'ring', 'pinky')], Q[de('hand', 'thumb')]
    tri = np.vstack([m['pos'] for _, _, m in reais.arma(id_)['pecas']])
    solido = pegada.Solido(dedos_r, palma_r).mais(pegada.Solido.de_malha(tri, dedos_r[:, 2].min() - 0.03, palma_r[:, 2].max()))
    # cada dedo dobra inteiro, da mão aberta para a fechada, e para quando encosta: o abraço de quem segura com as duas mãos
    onde, dedos, medidas = pegada.pegar(AR, prova, 'l', solido, np.eye(4), M.aberta('l'), M.fechada('l', 1.0), reta=(-1.0, 0.0, 0.0), pele=pele, junto=True, faixa=(-0.45, 1.1))
    return na_arma(*(-onde[:3, 3])) @ apoio, dedos, medidas


def pistola(mov, ficha=None):
    id_ = 'deagle'; E, AR = abrir(id_); M = Maos(AR)
    L = reais.arma(id_); LUGAR = (0.075, -0.068, -0.295); Gp = arma_no_olho(E, *LUGAR, pitch=0.0, yaw=5.0, roll=2.0)      # Jhon, 19/11: "suba ela um pouco e coloque um pouco para frente"
    # a mão esquerda abraça a direita: o espelho dela, um pouco para fora, para baixo e para a frente
    apoio = M.espelhada(da_arma(mover(-0.012, -0.012, -0.006) @ giro(pitch=-8, yaw=-6)))
    base = dict(AR.parado)
    dedos_r, sobra = M.no_gatilho(M.dedos_punho, reais.arma(id_)['gatilho'])
    print(f"  {id_}: o indicador fica a {sobra:.1f} mm do gatilho")
    AR.por_dedos(base, dedos_r)
    apoio, dedos_l, medidas = abracar(AR, M, base, apoio, id_); AR.por_dedos(base, dedos_l)
    print(f"  {id_}: a mão esquerda abraçando a direita — a pele fica a {medidas['mm']} mm dela e da arma (negativo = atravessa)")
    pose = dict(pega={'r': M.punho, 'l': apoio}, braco=('l',))            # o antebraço esquerdo alinha com a mão (o pulso dobrava 78°)
    feitos = comuns(E, mov, Gp, pose, base, dict(parado=2.0, andar=0.72, sacar=0.36, guardar=0.3), dict(parado=1.4, andar=1.9), cam=0.7)

    # ---- o que vem da referência (``RefPistola``): o tiro, as duas recargas, o andar, a corrida e o inspecionar
    ref = RefPistola(AR, M.punho)
    Wb = lambda n: E.mundo(base, E.N(n))
    Fm = referencial(Wb('hand_l')[:3, 3], Wb('middle_01_l')[:3, 3], Wb('index_01_l')[:3, 3], Wb('pinky_01_l')[:3, 3])
    Hl0 = Wb('hand_l'); Rm = np.eye(4); Rm[:3, :3] = rot(Hl0); Rm[:3, 3] = Hl0[:3, 3]; Ql = np.linalg.inv(Fm) @ Rm
    def esquerda_da_ref(G, acao, t):
        Fw = referencial(*ref.esquerda(acao, t)); X = G @ ARMA; F = np.eye(4)
        F[:3, :3] = rot(X) @ Fw[:3, :3]; F[:3, 3] = (X @ np.append(Fw[:3, 3], 1.0))[:3]; return F @ Ql
    dedos_mag = M.fechada('l', 0.85)
    def da_ref(acao, nome, dur, forca=1.0, cam=0.1, ferrolho=1.0, carregador=False, solta=None, ergue=None):
        k = ref.duracao(acao) / dur
        def arma_em(u):
            G = Gp @ da_arma(ref.arma(acao, u, forca))
            return G if ergue is None else rigida(E.O2R @ mover(*ergue(u)) @ E.R2O) @ G
        def q(t):
            u = t * k; D = ref.arma(acao, u, forca); G = arma_em(u); R = D[:3, :3]
            # a câmera da referência não se mexe (o arquivo põe todo o movimento nas mãos): a cabeça acompanha a arma, um pouco
            olhar = np.degrees([np.arcsin(np.clip(-R[1, 2], -1, 1)), np.arctan2(R[0, 2], R[2, 2]), np.arctan2(R[1, 0], R[1, 1])]) * cam
            out = dict(G=G, pega={'r': M.punho}, cam=olhar,
                                                                              pecas={'Bolt': da_arma(ref.peca(acao, 'Slide', u, ferrolho))})
            if callable(carregador):                                      # a recarga: o carregador reto pelo punho, a mão esquerda presa nele
                Dm, Dmao, w = carregador(u, arma_em)
                out['pecas']['Magazine'] = da_arma(Dm); out['livre'] = {'l': entre(G @ apoio, G @ da_arma(Dmao) @ pega_mag, w)}
                out['dedos'] = {'l': misturar_dedos(dedos_l, M.dedos_carregador, w)}; out['braco'] = ('l',)
                return out
            if carregador: out['pecas']['Magazine'] = da_arma(ref.peca(acao, 'Magazine', u))
            if solta is None: out['pega']['l'] = apoio; out['braco'] = ('l',)
            else:                                                         # a esquerda larga o apoio, faz o que a da referência faz e volta
                w = solta(t); out['livre'] = {'l': entre(G @ apoio, esquerda_da_ref(G, acao, u), w)}
                out['dedos'] = {'l': misturar_dedos(dedos_l, dedos_mag, w)}
            return out
        feitos[nome] = construir(E, nome, dur, q, base)
    # o tiro da referência: o cano sobe 28°, mais que o dobro do de antes (Jhon, 19/11: "o tiro do revólver precisa dar mais coice")
    # (19/11, depois de jogar: "pistola pode mexer bem menos quando atira" — o coice inteiro da referência subia o cano 28°; fica 40% dele)
    da_ref('Shot', 'tiro', 0.30, forca=0.40, cam=0.10)

    # ---- o carregador nas recargas (Jhon, 19/11: "ficou estranha na saída do cartucho", "cartuchos em posição estranha e
    # incorreta"). Antes ele ia preso ao gesto da mão esquerda da referência: saltava para cima da arma e atravessava o
    # punho na volta. Agora o caminho é dele: sai e entra **reto, pelo eixo do punho** (o poço do carregador, medido na
    # malha: 8° de inclinação), e só fora do punho a mão o leva embora, para baixo e para a esquerda. A mão esquerda vai
    # presa nele (a pegada do carregador do AR-15). Os tempos são os da referência: quando o carregador dela começa a
    # sair e quando a mão dela volta ao punho.
    EIXO = np.array([0.0, -0.9897, 0.1435]); CURSO = 0.118               # para baixo, pelo poço; quanto anda até soltar do punho (m)
    MEIO = np.array([0.0, -0.060, 0.036])                                # o meio do carregador
    BOCA = mover(*(EIXO * CURSO))
    LONGE = mover(*(EIXO * (CURSO + 0.21) + np.array([-0.07, 0.0, 0.05]))) @ em_torno(MEIO, giro(pitch=-14, roll=24))      # fora da tela, na mão
    # onde a mão esquerda segura o carregador: a pegada do AR-15, levada ao terço de baixo do carregador da pistola
    pega_mag = da_arma(mover(*(np.array([0.0, -0.080, 0.037]) - np.array([0.007, -0.115, -0.114])))) @ trs(*base[E.N('Magazine')]) @ M.mao_no_carregador
    def saindo(tau):                                                     # tau: há quanto tempo começou a sair
        if tau <= 0: return np.eye(4)
        if tau <= 0.13: return mover(*(EIXO * CURSO * (tau / 0.13) ** 2))                    # reto, acelerando
        q = min((tau - 0.13) / 0.20, 1.0); return entre(BOCA, LONGE, 1 - (1 - q) ** 2)       # solto: a mão o leva, sem parar na boca
    def entrando(tau):                                                   # tau: quanto falta para assentar
        if tau <= 0: return np.eye(4)
        if tau <= 0.085: return mover(*(EIXO * CURSO * (tau / 0.085) ** 1.25))               # reto, pelo eixo, e bate no fim
        if tau <= 0.115: return mover(*(EIXO * (CURSO + 0.006 * (tau - 0.085) / 0.03)))      # um instante na boca, alinhando
        q = min((tau - 0.115) / 0.20, 1.0); return entre(mover(*(EIXO * (CURSO + 0.006))), LONGE, q * q * (3 - 2 * q))
    t_pega = next(k for k in range(200) if np.linalg.norm(ref.peca('Reloat_t', 'Magazine', k / 60)[:3, 3]) > 0.015) / 60
    def mao_w(acao, u): return referencial(*ref.esquerda(acao, u))
    ENC = mao_w('Reloat_t', t_pega)[:3, 3]
    def t_encaixe(acao, depois):                                         # quando a mão esquerda da referência volta ao punho
        return min((k / 60 for k in range(int(depois * 60), int(ref.duracao(acao) * 60))), key=lambda u: np.linalg.norm(mao_w(acao, u)[:3, 3] - ENC))
    # A pistola fica com o punho na borda de baixo da tela: sem subir, a troca do carregador acontecia toda fora dela.
    # Durante a troca a arma sobe 6 cm (e vem um pouco para o meio): dá para ver o carregador sair e o novo entrar.
    def erguer(de, ate): return lambda u: np.array([-0.018, 0.060, 0.0]) * suave((u - de) / 0.20) * (1 - suave((u - ate) / 0.30))
    def tatica():
        t_sai, t_entra = t_pega - 0.03, t_encaixe('Reloat_t', t_pega + 0.45) + 0.03; meio = (t_sai + 0.33 + t_entra - 0.315) / 2
        tatica.ergue = erguer(t_sai - 0.24, t_entra + 0.08)
        print(f"  {id_}: recarga — o carregador sai aos {t_sai:.2f} s e o novo assenta aos {t_entra:.2f} s")
        def c(u, arma_em):
            Dm = saindo(u - t_sai) if u < meio else entrando(t_entra - u)
            return Dm, Dm, suave(u / (t_sai - 0.02)) * (1 - suave((u - t_entra - 0.05) / 0.30))
        return c
    def vazia():
        acao = 'Reloat_f'; ff = [np.linalg.norm(ref.peca(acao, 'Magazine', k / 60)[:3, 3]) for k in range(int(ref.duracao(acao) * 60) + 1)]
        t_cai = next(k for k, d in enumerate(ff) if d > 0.015) / 60; t_some = next(k for k, d in enumerate(ff) if d > 0.4) / 60
        t_volta = next(k for k in range(int(t_some * 60), len(ff)) if ff[k] < 0.01) / 60
        t_entra = t_encaixe(acao, max(t_some + 0.25, t_volta - 0.2)); troca = t_entra - 0.33
        print(f"  {id_}: recarga vazia — o velho cai aos {t_cai:.2f} s, o novo assenta aos {t_entra:.2f} s")
        vazia.ergue = erguer(t_cai - 0.24, t_entra + 0.08)
        GRAV = 8.0; te = np.sqrt(2 * CURSO / GRAV); baixo = rot(E.O2R) @ np.array([0.0, -1.0, 0.0]); baixo /= np.linalg.norm(baixo)
        def c(u, arma_em):
            novo = entrando(t_entra - u)                                  # o novo (e a mão que o traz): fora da tela até vir
            if u >= troca: Dm = novo
            else:                                                         # o velho: solto, escorrega pelo poço e cai no mundo, girando
                tau = u - t_cai
                if tau <= te: Dm = mover(*(EIXO * 0.5 * GRAV * max(tau, 0.0) ** 2))
                else:
                    Ge = arma_em(t_cai + te) @ ARMA; dt = tau - te; v = rot(Ge) @ EIXO * (GRAV * te * 100)
                    queda = mover(*(v * dt + baixo * (490.0 * dt * dt)))
                    Dm = np.linalg.inv(arma_em(u) @ ARMA) @ queda @ Ge @ BOCA @ em_torno(MEIO, giro(pitch=-160 * dt, roll=60 * dt))
            return Dm, novo, suave((u - 0.05) / 0.30) * (1 - suave((u - t_entra - 0.05) / 0.30))
        return c
    # a recarga gira a arma 86° na referência; a nossa pistola fica mais perto do olho, e com o giro inteiro ela tapava a
    # tela: 60% do gesto (o carregador e a mão esquerda seguem a arma, então continuam se encontrando)
    c = tatica(); da_ref('Reloat_t', 'recarga', ref.duracao('Reloat_t'), forca=0.6, cam=0.2, carregador=c, ergue=tatica.ergue)
    c = vazia(); da_ref('Reloat_f', 'recarga_vazia', ref.duracao('Reloat_f'), forca=0.6, cam=0.2, carregador=c, ergue=vazia.ergue)
    # O andar é **o do AR-15**, igual ao da M4A4: mesmo passo (0,8 s), mesma força, mesma câmera (Jhon, 19/11: "ao andar
    # não precisa mexer muito para os lados, faça igual à animação da AR-15"). O da referência da pistola, mesmo a um
    # terço, levava a arma 15 mm de um lado ao outro e a girava 6°; o do AR-15 são 5 mm e meio grau.
    ka = mov.duracao('andar') / 0.8
    feitos['andar'] = construir(E, 'andar', 0.8, lambda t: dict(G=camadas(mov, Gp, 'andar', t, ka, 1.0), cam=mov.camera('andar', t * ka), **pose), base)
    da_ref('Run', 'correr', ref.duracao('Run'), cam=0.12)
    da_ref('Inspect', 'inspecionar', ref.duracao('Inspect'), forca=0.7, cam=0.08, carregador=True, solta=lambda t, d=ref.duracao('Inspect'): suave(t / 0.25) * (1 - suave((t - (d - 0.35)) / 0.35)))
    feitos['parado_mira'] = construir(E, 'parado_mira', 2.0, lambda t: dict(G=camadas(mov, Gp, 'parado', t, mov.duracao('parado') / 2.0, 0.25), **pose), base)
    feitos['andar_mira'] = construir(E, 'andar_mira', 0.72, lambda t: dict(G=camadas(mov, Gp, 'andar', t, mov.duracao('andar') / 0.72, 0.3), **pose), base)
    # ---- a coronhada (0,40 s; pedido do Jhon, 19/11): só a mão da arma. Recolhe um instante, vira a pistola de lado e bate
    # com a lateral dela, da direita para a esquerda; a outra mão solta e volta.
    bate = Chaves((0, np.zeros(6)), (0.045, np.array((0.035, 0.02, 0.05, 4, -16, -25.0))), (0.09, np.array((-0.10, 0.035, -0.11, -4, 58, -72.0))),
                  (0.15, np.array((-0.125, 0.03, -0.14, -4, 62, -76.0))), (0.24, np.array((-0.09, 0.02, -0.08, -2, 40, -50.0))), (0.40, np.zeros(6)))
    larga = Chaves((0, 0.0), (0.06, 1.0), (0.26, 1.0), (0.40, 0.0))
    embaixo = no_olho(E, -0.10, -0.30, -0.12, pitch=-20, yaw=20, roll=30)
    def coronhada(t):
        v = bate(t); G = arma_no_olho(E, LUGAR[0] + v[0], LUGAR[1] + v[1], LUGAR[2] + v[2], pitch=v[3], yaw=5.0 + v[4], roll=2.0 + v[5])
        return dict(G=G, pega={'r': M.punho}, livre={'l': entre(G @ apoio, embaixo, larga(t))}, pulso=('r',),
                    cam=(0.8 * mola(t - 0.09, 5, 0.5), -1.8 * mola(t - 0.085, 4.5, 0.55), -0.9 * mola(t - 0.085, 4.5, 0.55)))
    feitos['coronhada'] = construir(E, 'coronhada', 0.40, coronhada, base)
    for nome, falta in feitos.items(): print(f"  {id_}.{nome}: {E.duracao(nome):.2f} s, alcance que faltou {falta * 10:.1f} mm")
    entrada = dict(chave=id_, armas=[id_], arquivo=f'res://assets/armas/fp/{id_}.glb', ajuste=[0.0, 0.0, 0.0], fiel=True, fov=63.09, sacar=True,
                   animacoes=clipes_da_ficha(E, feitos), cano=boca(E, L['cano']))
    fechar(E, ficha, entrada)
    return E.path


# ---------------------------------------------------------------- a faca
SOBE = np.array([0.0, 0.958, -0.287])       # o eixo do punho, para cima (o punho do AR-15 é inclinado 17°)


def no_punho(eixo_y, eixo_z, de, para):
    """A 4×4 que põe uma peça na mão: o eixo y e o z dela vão para as direções dadas (no espaço da
    arma) e o ponto ``de`` dela vai para ``para``."""
    y = np.asarray(eixo_y, float); z = np.asarray(eixo_z, float); M = np.eye(4)
    M[:3, 0] = np.cross(y, z); M[:3, 1] = y; M[:3, 2] = z; M[:3, 3] = np.asarray(para, float) - M[:3, :3] @ np.asarray(de, float)
    return M


def faca(mov, ficha=None):
    """
    A faca: todo o movimento da animação de faca que o Jhon trouxe ("knife animated", DJMaesen; 19/11: "pode colocar
    as outras animações da faca como referência, todos os movimentos"). ``dados/faca_movimento.npz`` guarda, quadro a
    quadro e no espaço do olho, a faca, os pulsos, os cotovelos e a base dos dedos dos dois braços do arquivo.

    - a mão direita segue o pulso direito do arquivo; a faca segue a faca do arquivo, presa à mão no osso ``Bolt``
      (no giro de inspecionar ela anda na mão, como no arquivo);
    - a mão esquerda segue o pulso esquerdo do arquivo (a orientação vem da base dos dedos: o eixo dos dedos e a
      linha do indicador ao mindinho);
    - o braço é recalculado e se ajeita à mão: o pulso não quebra (``antebraco_pela_mao``).

    Clipes, da linha do tempo do arquivo (30 quadros/s): parado (quadros 1–40, vai e volta), golpe (a estocada, 42–62),
    golpe2 (o corte de lado, 62–82), inspecionar (o giro da faca, 83–121) e sacar (126–136). Os golpes tocam 1,6 vez
    mais rápido ("o ataque de faca deve ser animação rápida"). O andar é o parado com o passo da referência do AR-15.
    """
    id_ = 'faca'
    # a lâmina sai do punho fechado; a faca presa ao osso Bolt (filho do Grip), que a deixa andar na mão
    FRENTE = np.array([0.0, -0.287, -0.958])
    na_mao = no_punho(FRENTE, -SOBE, (0.0, 0.0, -0.052), (0.0, -0.025, 0.038)) @ giro(roll=180)
    E, AR = abrir(id_, 'faca', na_mao=na_mao, osso='Bolt')
    M = Maos(AR); base = dict(AR.parado); por_ombro(E, base, 'r', 0.22, -0.26, 0.06)
    dedos_r = M.fechada('r', 1.0); dedos_l = M.fechada('l', 0.55, indicador=0.45)
    AR.por_dedos(base, dedos_r); AR.por_dedos(base, dedos_l)

    mv = np.load(os.path.join(reais.DADOS, 'faca_movimento.npz'))
    LARGO, PERTO = 1.30, 0.68            # a câmera deste conjunto é mais aberta (63° contra 50,5°); a faca é 68% da do arquivo
    def no_tempo(nome, t):
        """A 4×4 de um nó do arquivo no instante t, no olho do jogo: rotação pura, posição levada à nossa câmera."""
        W = mv[nome].astype(float); x = min(max(t * 60.0, 0.0), len(W) - 1.001); k = int(x); w = W[k] * (1 - (x - k)) + W[k + 1] * (x - k)
        U, _, Vt = np.linalg.svd(w[:3, :3]); m = np.eye(4); m[:3, :3] = U @ Vt
        m[:3, 3] = (w[0, 3] * LARGO * PERTO, (w[1, 3] + 0.04) * LARGO * PERTO, w[2, 3] * PERTO); return m
    def ponto(nome, t): return no_tempo(nome, t)[:3, 3]
    CM = np.diag([0.01, 0.01, 0.01, 1.0])
    def na_raiz(F): return E.O2R @ F @ CM                                  # uma pose do olho (m) para a raiz (cm)

    # a faca do arquivo -> o osso da arma que a põe lá (a malha da faca do jogo, 68% do tamanho)
    K = np.eye(4); K[:3, :3] = np.array([[0, -1, 0], [1, 0, 0], [0, 0, 1.0]]) * 0.0068; K[:3, 3] = (0.0, 0.0, -0.0112)
    AKi = np.linalg.inv(ARMA @ na_mao @ K)
    def arma_da_faca(t):
        T = no_tempo('faca', t).copy(); T[:3, :3] = T[:3, :3] * 0.0068; return E.O2R @ T @ AKi
    # a mão direita: o pulso do arquivo, com a pegada que põe a faca exatamente onde ela está no primeiro quadro
    Qr = np.linalg.inv(na_raiz(no_tempo('R_wrist', 0.0))) @ arma_da_faca(0.0) @ M.punho
    # a mão esquerda: o referencial da base dos dedos, no arquivo e no manequim
    def referencial(o, m, i, p):
        x = (m - o) / np.linalg.norm(m - o); y = i - p; z = np.cross(x, y); z /= np.linalg.norm(z); y = np.cross(z, x)
        F = np.eye(4); F[:3, :3] = np.c_[x, y, z]; F[:3, 3] = o; return F
    Wb = lambda n: E.mundo(base, E.N(n))
    Fm = referencial(Wb('hand_l')[:3, 3], Wb('middle_01_l')[:3, 3], Wb('index_01_l')[:3, 3], Wb('pinky_01_l')[:3, 3])
    Hl0 = Wb('hand_l'); Rm = np.eye(4); Rm[:3, :3] = rot(Hl0); Rm[:3, 3] = Hl0[:3, 3]
    Ql = np.linalg.inv(Fm) @ Rm
    def mao_esquerda(t):
        Fs = referencial(*(ponto(f'L_{n}', t) for n in ('wrist', 'middle1', 'point1', 'pink1')))
        F = na_raiz(Fs); F[:3, :3] = rot(F); return F @ Ql
    Grip = trs(*base[E.N('Grip')]); Gi = np.linalg.inv(Grip)
    def quadro(t, cam=(0.0, 0.0, 0.0), D=None, so_direita=False):
        G = na_raiz(no_tempo('R_wrist', t)) @ Qr @ np.linalg.inv(M.punho)
        L = mao_esquerda(t)
        if D is not None: G, L = D @ G, (L if so_direita else D @ L)
        faca_d = Gi @ np.linalg.inv(G) @ (arma_da_faca(t) if D is None else D @ arma_da_faca(t)) @ Grip
        return dict(G=G, pega={'r': M.punho}, livre={'l': L}, braco=('r', 'l'), dedos={'r': dedos_r, 'l': dedos_l}, pecas={'Bolt': faca_d}, cam=cam)

    feitos = {}
    def trecho(nome, de, ate, ritmo=1.0, cam=None, vai_e_volta=False):
        dur = (ate - de) / ritmo * (2 if vai_e_volta else 1)
        def q(t):
            u = t * ritmo
            if vai_e_volta and u > ate - de: u = 2 * (ate - de) - u
            return quadro(de + u, cam(t) if cam else (0.0, 0.0, 0.0))
        feitos[nome] = construir(E, nome, dur, q, base)
    q = lambda f: (f - 1) / 30.0                                          # quadro do arquivo -> segundos
    trecho('parado', q(1), q(40), vai_e_volta=True)
    RITMO = 1.8                                                           # 19/11, jogando: "um pouco mais rápido" (era 1,6)
    trecho('golpe', q(42), q(62), RITMO, lambda t: (1.4 * mola(t - 0.125, 5.5, 0.5), -0.6 * mola(t - 0.12, 5, 0.5), -0.4 * mola(t - 0.12, 5, 0.5)))
    trecho('golpe2', q(62), q(82), RITMO, lambda t: (-1.0 * mola(t - 0.105, 5, 0.5), 2.2 * mola(t - 0.1, 4.5, 0.55), 1.3 * mola(t - 0.1, 4.5, 0.55)))
    # O arquivo só tem esses dois golpes; o Jhon quer quatro, passando um a um (19/11). Os outros dois são os mesmos com a
    # mão direita (e a faca) girada em torno de um eixo do olho, em sino — começam e acabam na pose parada, como os
    # originais: golpe3, o corte deitado (quase na horizontal); golpe4, a estocada alta, com a lâmina de lado.
    def girado(nome, de, ate, cam, graus, centro=(0.06, -0.10, -0.30)):
        dur = (ate - de) / RITMO
        def q_(t):
            w = suave(t / (0.22 * dur)) * (1 - suave((t - 0.62 * dur) / (0.38 * dur)))
            D = rigida(E.O2R @ em_torno(np.array(centro), giro(pitch=graus[0] * w, yaw=graus[1] * w, roll=graus[2] * w)) @ E.R2O)
            return quadro(de + t * RITMO, cam(t), D, so_direita=True)
        feitos[nome] = construir(E, nome, dur, q_, base)
    G3, G4 = (0.0, 0.0, 28.0), (10.0, -6.0, -28.0)                     # pitch, yaw, roll (graus) de cada variante
    girado('golpe3', q(62), q(82), lambda t: (-0.4 * mola(t - 0.1, 5, 0.5), 2.4 * mola(t - 0.09, 4.5, 0.55), 0.6 * mola(t - 0.09, 4.5, 0.55)), G3)
    girado('golpe4', q(42), q(62), lambda t: (1.8 * mola(t - 0.11, 5.5, 0.5), -0.3 * mola(t - 0.11, 5, 0.5), -0.8 * mola(t - 0.11, 5, 0.5)), G4)
    trecho('inspecionar', q(83), q(121))
    trecho('sacar', q(126), q(136), cam=lambda t: mov.camera('sacar', t * mov.duracao('sacar') / (q(136) - q(126)), 0.6))
    # o andar: a pose parada com o passo da referência (as duas mãos vão juntas com a arma)
    ka = mov.duracao('andar') / 0.72; G0 = quadro(0.0)['G']
    feitos['andar'] = construir(E, 'andar', 0.72, lambda t: quadro(0.0, mov.camera('andar', t * ka, 0.6), G0 @ mov.arma('andar', t * ka, 2.2) @ np.linalg.inv(G0)), base)

    for nome, falta in feitos.items(): print(f"  {id_}.{nome}: {E.duracao(nome):.2f} s, alcance que faltou {falta * 10:.1f} mm")
    anim = clipes_da_ficha(E, feitos); anim['golpe']['contato'] = round(q(48) - q(42), 4) / RITMO; anim['golpe2']['contato'] = round((q(67) - q(62)) / RITMO, 4)
    anim['golpe']['contato'] = round(anim['golpe']['contato'], 4); anim['golpe3']['contato'] = anim['golpe2']['contato']; anim['golpe4']['contato'] = anim['golpe']['contato']
    entrada = dict(chave=id_, armas=[id_], arquivo=f'res://assets/armas/fp/{id_}.glb', ajuste=[0.0, 0.0, 0.0], fiel=True, fov=63.09, animacoes=anim, cano=boca(E, (0.0, 0.2, -0.05)))
    fechar(E, ficha, entrada)
    return E.path


# ---------------------------------------------------------------- a granada
GRANADA = dict(centro=(-0.015, -0.070, 0.050), tamanho=1.18)           # onde a bola fica na mão antes de encostar (arma, m); o tamanho (1 = o real, bola de 6 cm)
GRANADA_LUGAR = (0.105, -0.090, -0.30, 10.0, 55.0, 5.0)               # a mão parada: x, y, z do punho no olho (m); pitch, yaw, roll (graus)
GRANADA_PRES = (0.045, -0.035, -0.33, 55.0, 60.0, 70.0)               # a granada apresentada à mão esquerda: deitada na mão, a argola virada para a esquerda e para baixo
# o braço do arremesso, pelo pulso (``mao_pelo_braco``): x, y, z do pulso no olho (m); o giro da mão em torno do antebraço (2 valores somados) e a dobra do pulso para a palma (graus)
# armado: a mão ao lado da cabeça, alta e para trás (Jhon, 20/11: "a mão da granada deve ir mais alta e mais para trás da
# câmera"); era (0,21, −0,03, −0,215): o pulso 3 cm abaixo do olho e a 21 cm à frente. Agora 3,5 cm acima e a 16,5 cm:
# a granada fica no canto de cima da tela, metade para fora.
GRANADA_ATRAS = (0.205, 0.035, -0.165, 0.0, -18.0, 0.0)
# Com 'meio', x, y, z são do **meio da granada** (não do pulso): é ele que tem de passar pela linha da cruz.
# A granada sai **do meio da mão, na linha da cruz, indo para ela** (Jhon, 19/11, jogando: "a granada tem que ser lançada
# no meio da linha da mão, na direção exata do crosshair"). Antes a mão soltava 11 cm à direita da cruz, cortando a tela
# em diagonal (22° para o lado), e a granada de verdade convergia para a cruz no voo: parecia sair de lado, por cima da
# mão. Agora a mão vem de trás, entra na linha da cruz **antes** de soltar, solta com a granada em cima dela (um pouco
# acima da cruz, para não a tapar) e ainda acompanha o lance para a frente antes de cair.
GRANADA_ARCO = ((0.045, (0.215, 0.06, -0.13, 0.0, -26.0, -8.0)),      # recua um nada, o pulso arma para trás
                (0.085, (0.010, 0.066, -0.39, 0.0, -6.0, 0.0), 'meio'),      # já na linha da cruz, indo para a frente
                (0.11, (0.0, 0.072, -0.55, 0.0, 6.0, 4.0), 'meio'),   # solta aqui (SOLTA): em cima da linha da cruz
                (0.145, (-0.002, 0.060, -0.615, 0.0, 20.0, 22.0), 'meio'),   # a mão acompanha o lance
                # o pulso termina o chicote dobrando para a frente, de leve (Jhon, 20/11: "um leve movimento do pulso dobrando para
                # frente ao arremessar, no final da animação") — eram 20° e 28°, e voltava a 12° e 16° logo depois
                (0.22, (-0.012, -0.045, -0.59, 0.0, 27.0, 33.0), 'meio'),    # o pulso termina o chicote e a mão cai
                (0.36, (0.03, -0.26, -0.40, 0.0, 22.0, 27.0)),
                (0.55, (0.10, -0.50, -0.22, 0.0, 4.0, 8.0)))
GRANADA_SOLTA = 0.11


def choque(mov, ficha=None):
    """
    A granada (V40). A peça, como veio (``reais.por_osso``): a bola com a espoleta em cima; a **alavanca**, que passa
    por cima da espoleta e desce por um lado da bola; o **pino**, que atravessa a espoleta; e a **argola** (a alça do
    pino, um aro de 6 cm), pendurada na cabeça do pino, do lado oposto ao da alavanca.

    Na mão direita (Jhon, 19/11: "o gatilho dela tá entrando dentro da mão"; "nada pode atravessar os armamentos, mas
    deve encostar sempre"): em pé, a alavanca para trás — na forquilha entre o polegar e o indicador, que é onde a mão
    a aperta —, a argola para a esquerda, à mão para a outra mão. A mão não é a do punho de arma: fecha em volta da
    bola, dedo por dedo, até encostar (``pegada.pegar``). Tudo o que encosta é medido na pele das mãos do jogo, e o
    que sai aqui impresso é essa medida.
    """
    import pegada, pickle
    id_ = 'choque'; TAM = GRANADA['tamanho']; MEIO = np.array([-0.005, -0.010, 0.0])       # o meio da bola, na peça
    def esc(p): return MEIO + (np.asarray(p, float) - MEIO) * TAM          # a peça é maior que a real na primeira pessoa ("ficou meio pequena")
    O = {n: esc(t) for n, t in reais.por_osso(id_).items()}; al = O['handle'].reshape(-1, 3)
    solido = pegada.Solido(O['grenade'], al[al[:, 1] > esc((0, 0.034, 0))[1]], al[al[:, 1] <= esc((0, 0.036, 0))[1]])      # a bola e a espoleta; a alavanca: o topo e a descida
    OLHAL = esc((0.011, 0.030, 0.015)); EZ = np.array([0.0, 0.0, 1.0])    # a cabeça do pino, onde a argola gira; o eixo do pino
    AR0 = Esqueleto(FONTE); M0 = Maos(AR0); pele = pegada.Pele()
    memo = os.environ.get('GR_MEMO')                                       # (para afinar o gesto sem refazer a pegada a cada vez)
    if memo and os.path.exists(memo) and pickle.load(open(memo, 'rb'))[0] == GRANADA: _, na_mao, dedos_bola, medidas = pickle.load(open(memo, 'rb'))
    else:
        na_mao, dedos_bola, medidas = pegada.pegar(AR0, dict(AR0.parado), 'r', solido, no_punho(SOBE, (-1.0, 0.0, 0.0), MEIO, GRANADA['centro']), M0.aberta('r'), M0.fechada('r', 1.0), pele=pele)
        if memo: pickle.dump((GRANADA, na_mao, dedos_bola, medidas), open(memo, 'wb'))
    print(f"  {id_}: a mão direita na granada — a pele fica a {medidas['mm']} mm dela (0 = encostada; negativo = atravessando)")
    na_mao_i = np.linalg.inv(na_mao)
    def Mg(G): return G @ ARMA @ na_mao                                    # da peça (m) para a raiz (cm)
    def pontos(tri):                                                      # os vértices e os meios das arestas
        t = tri.reshape(-1, 3, 3); return np.vstack([t.reshape(-1, 3), (t[:, 0] + t[:, 1]) / 2, (t[:, 1] + t[:, 2]) / 2, (t[:, 2] + t[:, 0]) / 2])
    lado_de = lambda l: np.array([n.endswith('_' + l) and n.startswith(('hand',) + pegada.FALANGES) for n in pele.dono])
    mao_r, mao_l = lado_de('r'), lado_de('l'); braco_de = lambda l: np.array([n.endswith('_' + l) for n in pele.dono]); todo_r, todo_l = braco_de('r'), braco_de('l')
    def fora_de(pts, tri):                                                # como ``fora_da_malha``, mas longe da pele o sinal não vale (a malha é aberta no ombro)
        d = pegada.fora_da_malha(pts, tri); return np.where(d < -0.012, -d, d)
    def pele_na_peca(esq, pose): return pele.em(esq, pose, np.linalg.inv(Mg(esq.arma_de(pose))))        # toda a pele, no espaço da peça (m)

    # a argola: gira na cabeça do pino. Como veio, cola na bola (ficaria dentro dos dedos); abre até pousar por fora deles
    segura = dict(AR0.parado); AR0.por_dedos(segura, dedos_bola); Pr = pele_na_peca(AR0, segura); tri_r = pele.triangulos(Pr, todo_r); aro = pontos(O['pinmove'])
    def folga_aro(graus):
        q = aro @ giro(pitch=graus)[:3, :3].T; q = q + OLHAL - giro(pitch=graus)[:3, :3] @ OLHAL
        return min(solido.fora(q).min(), fora_de(q, tri_r).min())
    ABRE_G = next((-g for g in np.arange(10.0, 91.0, 2.0) if folga_aro(-g) > 0.0008), -90.0); ABRE = em_torno(OLHAL, giro(pitch=ABRE_G))
    print(f"  {id_}: a argola abre {-ABRE_G:.0f}° e pousa a {folga_aro(ABRE_G) * 1000:.1f} mm dos dedos")
    def separar(pecas):                                                   # o tamanho; e o pino e a argola são peças (e ossos) diferentes
        out = []
        for a, no, m in pecas:
            m = dict(m, pos=esc(m['pos']))
            if no != 'Pino': out.append((a, no, m)); continue
            e_argola = np.array([np.abs(O['pinmove'].reshape(-1, 9) - x.reshape(9)).sum(1).min() < 1e-6 for x in m['pos']])
            out.append((a, 'Pino', {k: v[~e_argola] for k, v in m.items()}))
            out.append((a, 'Argola', levar({k: v[e_argola] for k, v in m.items()}, ABRE)))
        return out
    E, AR = abrir(id_, 'granada', na_mao=na_mao, osso={'': 'Bolt', 'Pino': 'Trigger', 'Argola': 'Magazine'}, antes=separar)   # o corpo voa pelo osso Bolt; o pino sai pelo Trigger; a argola balança no Magazine
    M = Maos(AR); base = dict(AR.parado); fechada = dedos_bola; aberta = M.aberta('r'); AR.por_dedos(base, fechada); por_ombro(E, base, 'r', 0.22, -0.26, 0.06)

    # parada: a mão virada para dentro, para a granada, a alavanca e a argola aparecerem (antes só se via o dorso da mão)
    LUGAR = np.array(GRANADA_LUGAR, float)
    def lugar(v): return arma_no_olho(E, *v)
    so_r = {'r': M.punho}; Gp = com_pulso(E, lugar(LUGAR), so_r, base)
    escondida = no_olho(E, -0.24, -0.44, 0.0, pitch=-30)
    pose = dict(pega=so_r, livre={'l': escondida}, pulso=('r',))
    feitos = comuns(E, mov, Gp, pose, base, dict(parado=2.2, andar=0.72, sacar=0.36), dict(parado=1.6, andar=2.2), cam=0.6)
    def peca(X): return da_arma(na_mao @ X @ na_mao_i)                     # um movimento dado na peça (m) -> no osso da arma
    LONGE_DAQUI = peca(mover(0.0, -30.0, 0.0)); sem = {'Trigger': LONGE_DAQUI, 'Magazine': LONGE_DAQUI}        # o pino e a argola depois de arrancados: fora da tela

    # ---- o braço do arremesso: a mão sai do braço, em linha com o antebraço (Jhon, 19/11: "punho de lançamento torto")
    def braco_r(v):
        """v = (x, y, z do pulso no olho; giro da mão em torno do antebraço; dobra do pulso, 2 eixos) -> a arma (raiz)."""
        return mao_pelo_braco(E, base, 'r', v[:3], rolo=v[3], dobra=(v[4], v[5])) @ np.linalg.inv(M.punho)
    ATRAS = np.array(GRANADA_ATRAS, float); G_ATRAS = braco_r(ATRAS)

    # ---- a argola e o pino (Jhon, 19/11: "ele deve puxar o gatilho da granada com os dedos, ele tem uma alça")
    # A argola gira no olhal do pino e o pino corre no furo da espoleta (o eixo z da peça). O indicador esquerdo entra
    # na argola e puxa: primeiro a argola vira para o dedo (a folga), depois arrasta o pino pelo furo — é a conta de
    # uma biela: o olhal fica no eixo do pino, a LEFF do dedo — e, quando o pino sai (CURSO_PINO), os dois vão embora
    # pendurados no dedo.
    Pa = O['pinmove'].reshape(-1, 3) @ ABRE[:3, :3].T + ABRE[:3, 3]; aro_aberto = pontos(Pa)
    ponta = Pa[np.argsort(np.linalg.norm(Pa - OLHAL, axis=1))[-12:]].mean(0); LR = np.linalg.norm(ponta - OLHAL); r0 = (ponta - OLHAL) / LR
    CURSO_PINO = 0.030 * TAM; EX = np.array([1.0, 0.0, 0.0])
    def normal(r): n = np.cross(EX, r); return n / np.linalg.norm(n)       # a normal do aro: por onde o dedo passa
    PUXAO = np.array([0.0, -0.30, 1.0]); PUXAO /= np.linalg.norm(PUXAO)   # para onde o dedo puxa (na peça): pelo eixo do pino, um pouco para baixo
    def X_pino(s): return mover(0.0, 0.0, s)
    def X_argola(s, d):
        R = np.eye(4); R[:3, :3] = arco(r0, d); return mover(0.0, 0.0, s) @ em_torno(OLHAL, R)

    # A mão esquerda: fechada, o indicador esticado. Ele entra no aro de ponta, pelo eixo do aro, até a dobra da ponta; do
    # punho para trás nada chega ao plano do aro. (Em gancho, com o punho ao lado, o polegar e o médio cruzavam o arame.)
    dedos_gancho = M.fechada('l', 1.0, indicador=0.0); solta_ = M.aberta('l'); RETO = 0.45
    for i in dedos_gancho:
        if 'index' in E.nome[i]: dedos_gancho[i] = (dedos_gancho[i][0], slerp(solta_[i][1], dedos_gancho[i][1], RETO))
    prova = dict(base); AR.por_dedos(prova, dedos_gancho); Whi = np.linalg.inv(E.mundo(prova, E.N('hand_l')))
    j2, j3 = ((Whi @ E.mundo(prova, E.N(f'index_0{k}_l')))[:3, 3] for k in (2, 3)); f_h = (j3 - j2) / np.linalg.norm(j3 - j2); k_h = j3
    def eixos(a, b):
        e1 = a / np.linalg.norm(a); e2 = b - (b @ e1) * e1; e2 /= np.linalg.norm(e2); return np.c_[e1, e2, np.cross(e1, e2)]
    Fh = eixos(f_h, -k_h)
    def gancho(K, n, r):
        """A mão esquerda (raiz) com a ponta do indicador em K, a falange ao longo de n e a palma para o lado r (o do puxão)."""
        R = eixos(n, r) @ Fh.T; H = np.eye(4); H[:3, :3] = R; H[:3, 3] = K - R @ k_h; return H

    # ---- armar (Jhon, 19/11: "foque na granada para deixar ela mais fluida combinando movimentos")
    # Um gesto só, sem paradas (``Curva``): enquanto a direita traz a granada para o meio e vira a argola para a
    # esquerda, a esquerda já vem subindo; o dedo levanta a argola e entra nela, as duas mãos se afastam — a esquerda
    # puxa, a direita empurra e, no estalo do pino, dá um tranco — e a direita continua o mesmo arco até lá atrás, ao
    # lado da cabeça, enquanto a esquerda sai por baixo com o pino pendurado no dedo.
    DUR, T_GANCHO, T_PUXA = 0.64, 0.19, 0.205
    T_EIXO = T_GANCHO - 0.09                                              # daqui até entrar, o dedo já está alinhado com o aro e só avança pelo eixo dele
    PRES = np.array(GRANADA_PRES, float); SAIDA = no_olho(E, -0.36, -0.48, -0.26, pitch=-30)
    puxa = Curva((0, 0.0), (T_PUXA, 0.0), (T_PUXA + 0.03, 0.010), (T_PUXA + 0.09, 0.070), (T_PUXA + 0.20, 0.17))
    def gesto(levanta, leff):
        """O armar com a argola levantada ``levanta`` graus pelo dedo e a ponta do dedo a ``leff`` do olhal."""
        r1 = giro(pitch=-levanta)[:3, :3] @ r0; K1 = OLHAL + r1 * leff
        def biela(K):
            v = K - OLHAL; b = float(v @ EZ); c = float(v @ v) - leff ** 2
            s = 0.0 if c <= 1e-12 else max(0.0, b - np.sqrt(max(b * b - c, 0.0)))
            d = v - s * EZ; return s, d / np.linalg.norm(d)
        def aro_em(t):                                                    # (quanto o pino saiu, para onde a argola aponta, onde está a ponta do dedo, quanto falta o dedo girar): na peça
            if t < T_GANCHO:
                # o dedo vem de ponta pelo eixo do aro, 3 mm para dentro do arame; no fim corre para fora e o arame assenta nele
                w = suave((t - (T_GANCHO - 0.05)) / 0.05); r = giro(pitch=-levanta * w)[:3, :3] @ r0
                return 0.0, r, OLHAL + r * (leff - 0.003 * (1 - suave((t - (T_GANCHO - 0.03)) / 0.03))) - normal(r) * 0.075 * (1 - suave((t - T_EIXO) / (T_GANCHO - 0.02 - T_EIXO))), 0.0
            K = K1 + PUXAO * puxa(t); s, d = biela(K); return s, d, K, 0.0
        T_FORA = next(k / 240 for k in range(int(DUR * 240)) if aro_em(k / 240)[0] >= CURSO_PINO)
        tranco = lambda t: mola(t - T_FORA, 5.0, 0.45)
        vai_r = Curva((0, LUGAR), (T_EIXO + 0.03, PRES), (T_FORA, PRES + np.array((0.012, 0.004, 0.004, 1, -3, 2.0))), (DUR, PRES + np.array((0.03, 0.03, 0.02, 4, -8, 4.0))))
        sobe = Curva((0, 0.0), (T_FORA - 0.01, 0.0), (DUR - 0.09, 0.93), (DUR, 1.0)); memo_g = {}
        def G_de(t):                                                      # onde a arma fica de verdade (o pulso da direita não quebra)
            k = round(t * 960)
            if k not in memo_g:
                w = sobe(t); G = entre(lugar(vai_r(t) + np.array((0.010, 0.004, 0.0, 1.5, -2.0, 0.0)) * tranco(t)), G_ATRAS, w)
                G = rigida(E.O2R @ mover(0.03 * np.sin(np.pi * w), 0.05 * np.sin(np.pi * w), 0.0) @ E.R2O) @ G       # o caminho é um arco, não uma reta
                memo_g[k] = com_pulso(E, G, so_r, base)
            return memo_g[k]
        def esquerda(t):
            tf = min(t, T_FORA); M_ = Mg(G_de(tf)); s, d, K, falta_girar = aro_em(tf)
            if t > T_FORA: K = K1 + PUXAO * puxa(t)
            H = gancho((M_ @ np.append(K, 1.0))[:3], rot(M_) @ normal(d), rot(M_) @ d)
            H = entre(escondida, H, suave(t / T_EIXO)); w = suave((t - T_FORA - 0.04) / 0.26)
            H[:3, 3] = H[:3, 3] * (1 - w) + SAIDA[:3, 3] * w; return H        # sai por baixo sem virar a mão: o braço não cruza a frente do rosto
        fora = dict(L=esquerda(T_FORA), M=Mg(G_de(T_FORA)), a=aro_em(T_FORA))
        def pecas_em(t, G, L):                                            # o pino e a argola (na peça)
            if t <= T_FORA:
                s, d, _, _ = aro_em(t); return X_pino(s), X_argola(s, d)
            s, d, K, _ = fora['a']; leva = np.linalg.inv(Mg(G)) @ L @ np.linalg.inv(fora['L']) @ fora['M']      # arrancados: vão na mão esquerda
            return leva @ X_pino(s), leva @ X_argola(s, d)
        def armar(t):
            G = G_de(t); L = esquerda(t); Xp, Xa = pecas_em(t, G, L)
            return dict(G=G, pega=so_r, livre={'l': L}, dedos={'l': dedos_gancho}, braco=('l',), pecas={'Trigger': peca(Xp), 'Magazine': peca(Xa)},
                        cam=(-0.5 * suave(t / 0.15) + 1.5 * suave((t - T_FORA) / (DUR - T_FORA)), -0.5 * suave(t / 0.15) + 0.8 * suave((t - T_FORA) / (DUR - T_FORA)) + 0.5 * tranco(t), 0.0))
        def medir(t):
            """No instante t: (a que distância o aro fica do dedo, a que distância a mão esquerda fica da direita e da granada), m."""
            q = armar(t); p, _ = posar(E, q, base); P = pele_na_peca(E, p); Xp, Xa = pecas_em(t, q['G'], q['livre']['l'])
            aro_t = aro_aberto @ Xa[:3, :3].T + Xa[:3, 3]; e = P[mao_l]
            return float(fora_de(aro_t, pele.triangulos(P, todo_l)).min()), float(min(solido.fora(e).min(), fora_de(e, pele.triangulos(P, todo_r)).min()))
        def culpados(t):
            q = armar(t); p, _ = posar(E, q, base); P = pele_na_peca(E, p); Xp, Xa = pecas_em(t, q['G'], q['livre']['l']); aro_t = aro_aberto @ Xa[:3, :3].T + Xa[:3, 3]
            import collections; from scipy.spatial import cKDTree
            d = fora_de(aro_t, pele.triangulos(P, todo_l)); ruins = aro_t[d < 0]; _, i = cKDTree(P[todo_l]).query(ruins); return collections.Counter(pele.dono[todo_l][i]).most_common(4), round(float(d.min()) * 1000, 1)
        return dict(armar=armar, medir=medir, T_FORA=T_FORA, culpados=culpados)
    # Quanto o dedo levanta a argola: o menor que deixa a mão esquerda inteira fora da direita e da granada. E a ponta
    # do dedo o mais longe do olhal que o aro deixa: o arame assenta no dedo, sem cortar.
    LEFF = LR - 0.016; levanta = 0.0
    for levanta in np.arange(0.0, 76.0, 5.0):
        if gesto(levanta, LEFF)['medir'](T_GANCHO)[1] > 0.0015: break
    a, b = LR - 0.030, LR - 0.002                                         # a: o dedo solto dentro do aro; b: o arame dentro do dedo
    for _ in range(12):
        m = (a + b) / 2
        if gesto(levanta, m)['medir'](T_PUXA)[0] > 0.0004: a = m
        else: b = m
    LEFF = a; g = gesto(levanta, LEFF); T_FORA = g['T_FORA']
    if os.environ.get('GR_DEBUG'):
        for t in (0.12, 0.14, 0.15, 0.16, 0.17): print('        culpados', t, g['culpados'](t))
    feitos['armar'] = construir(E, 'armar', DUR, g['armar'], base)
    print(f"  {id_}: armar — o dedo levanta a argola {levanta:.0f}° e entra nela aos {T_GANCHO:.2f} s; o pino sai aos {T_FORA:.2f} s")
    for rotulo, ts in (('entrando', np.arange(T_EIXO, T_GANCHO, 0.01)), ('puxando', np.arange(T_GANCHO, T_FORA, 0.01)), ('levando', np.arange(T_FORA, T_FORA + 0.12, 0.02))):
        ms = np.array([g['medir'](t) for t in ts]) * 1000
        if os.environ.get('GR_DEBUG'): print('        ', rotulo, [(round(float(t), 2), round(float(a_), 1), round(float(b_), 1)) for t, (a_, b_) in zip(ts, ms)])
        print(f"      {rotulo}: o aro fica a {ms[:, 0].min():.1f} mm do dedo (no mínimo); a mão esquerda, a {ms[:, 1].min():.1f} mm da direita e da granada")

    # armado (o botão segurado): a mão lá atrás não congela — respira, pesando a granada
    solta_l = dict(pega=so_r, livre={'l': escondida})
    feitos['armado'] = construir(E, 'armado', 1.8, lambda t: dict(G=braco_r(ATRAS + np.array((0.004, 0.006, 0.0, 0.0, 1.5, 0.0)) * np.sin(2 * np.pi * t / 1.8) + np.array((0.0, 0.0, 0.004, 1.2, 0.0, 0.0)) * np.sin(4 * np.pi * t / 1.8)),
                                                                   pecas=sem, **solta_l), base)
    # arremesso: vem lá de trás num arco só, solta na frente (``soltar``), o pulso termina o chicote e a mão sai por baixo
    SOLTA = GRANADA_SOLTA; D_ARR = GRANADA_ARCO[-1][0]
    CENTRO = na_mao[:3, :3] @ MEIO + na_mao[:3, 3]
    def chave(t, v, onde=None):
        v = np.array(v, float)
        if onde == 'meio':                                                # dado o meio da granada: acha o pulso que o põe lá
            alvo = v[:3].copy()
            for _ in range(10): v[:3] += alvo - (E.R2O @ braco_r(v) @ ARMA @ np.append(CENTRO, 1.0))[:3]
        return t, v
    vai = Curva((0, ATRAS), *(chave(*k) for k in GRANADA_ARCO))
    abre = Curva((0, 0.0), (SOLTA - 0.03, 0.0), (SOLTA + 0.07, 0.45), (0.34, 0.30), (D_ARR, 0.18))      # solta e relaxa: dedos meio curvos, não a mão espalmada
    G_solta = braco_r(vai(SOLTA))
    def meio_no_olho(t): return (E.R2O @ braco_r(vai(t)) @ ARMA @ np.append(CENTRO, 1.0))[:3]
    if os.environ.get('GR_DEBUG'):
        for t in np.arange(0.0, 0.26, 0.01): print('        arremesso', round(float(t), 2), 'meio da granada no olho (mm):', (meio_no_olho(t) * 1000).round(0))
    c0 = meio_no_olho(SOLTA); c1 = meio_no_olho(SOLTA + 0.02); c_1 = meio_no_olho(SOLTA - 0.02)
    print(f"  {id_}.arremesso: ao soltar, o meio da granada está a {c0[0] * 1000:+.0f} mm da linha da cruz para o lado e {np.degrees(np.arctan2(c0[1], -c0[2])):+.1f}° acima dela, a {np.linalg.norm(c0) * 100:.0f} cm do olho; "
          f"a mão vai a {np.linalg.norm(c1 - c_1) / 0.04:.1f} m/s, {np.degrees(np.arctan2((c1 - c_1)[0], -(c1 - c_1)[2])):+.0f}° para o lado e {np.degrees(np.arctan2((c1 - c_1)[1], -(c1 - c_1)[2])):+.0f}° para cima")
    def arremessar(t):
        q = max(0.0, t - SOLTA); G = braco_r(vai(t))                      # solta: a granada segue pelo mundo, para a frente e para cima, girando
        voa = np.eye(4) if q <= 0 else np.linalg.inv(G) @ rigida(E.O2R @ mover(-0.45 * q, 2.2 * q - 4.9 * q * q, -8.0 * q) @ E.R2O) @ G_solta @ da_arma(em_torno(CENTRO, giro(pitch=-520 * q)))
        c = (1.0 - 2.0 * suave(t / 0.16) + 1.0 * suave((t - 0.16) / 0.39), 0.3 * (1 - suave(t / 0.25)), 0.3 * mola(t - 0.09, 3, 0.5))
        return dict(G=G, dedos={'r': misturar_dedos(fechada, aberta, abre(t))}, pecas={'Bolt': voa, **sem}, cam=c, **solta_l)
    feitos['arremesso'] = construir(E, 'arremesso', D_ARR, arremessar, base)
    for nome, falta in feitos.items():
        dobra = max(dobra_do_pulso(E, E.pose_do_clipe(nome, t), 'r') for t in np.arange(0.0, E.duracao(nome) + 1e-6, 1 / 30))
        print(f"  {id_}.{nome}: {E.duracao(nome):.2f} s, alcance que faltou {falta * 10:.1f} mm, o pulso direito dobra no máximo {dobra:.0f}°")
    anim = clipes_da_ficha(E, feitos); anim['arremesso']['soltar'] = SOLTA
    entrada = dict(chave='granada', armas=[id_], arquivo=f'res://assets/armas/fp/{id_}.glb', ajuste=[0.0, 0.0, 0.0], fiel=True, fov=63.09, animacoes=anim, cano=boca(E, (0.0, 0.0, -0.1)))
    # o meio da granada no osso que a leva (Bolt): é de onde a granada de verdade nasce quando a mão solta (ViewModel.HandPoint)
    p0 = E.pose_do_clipe('parado', 0.0); c = np.linalg.inv(E.mundo(p0, E.N('Bolt'))) @ E.arma_de(p0) @ ARMA @ na_mao @ np.append(MEIO, 1.0)
    entrada['centro'] = [round(float(x), 3) for x in c[:3]]
    fechar(E, ficha, entrada)
    return E.path


CURTAS = dict(deagle=pistola, faca=faca, choque=choque)


# ---------------------------------------------------------------- a mira e a coronhada
# a que distância do olho fica a mira de trás (m).
# AK-74: a 21 cm a tampa da caixa da culatra ficava aquém do plano de corte da câmera, dentro da tela, e via-se a arma por
# dentro (Jhon, 19/11: "AK47 escopada tá dando para ver por dentro dela"); de 27 cm em diante nada dela é cortado na tela
# (conferido ponto a ponto na malha); fica a 28.
# M4A4: a 7–8,5 cm o bloco da alça tapava o meio da tela (Jhon, 19/11, jogando: "tá tampando e ficando ruim de enxergar o
# oponente"). A 14 cm ele fica com metade do tamanho, e a linha de mira passa por cima dele, na ponta do pino da massa
# (``reais.ONDE``): o centro da tela e tudo acima dele ficam livres.
MIRAS = dict(m4a4=0.14, ak74=0.28, scar=0.07, svd=0.075, deagle=0.118)
# quanto a linha de mira fica abaixo do centro da tela (m, na mira de trás): a pistola mira mais baixa e mais perto
# (Jhon, 19/11: "na mira da pistola, desça ela mais e aproxima mais")
BAIXO = dict(deagle=0.011)
# quanto do coice do tiro fica na mira (o padrão de ``mira.py`` é 0,55): a M4A4 atira mirada quase sem sair do lugar
# (Jhon, 19/11: "precisão da M4A1 mirada precisa ser mais estável")
COICE_NA_MIRA = dict(m4a4=0.26, deagle=0.25)


def mirar(id_, ficha=None):
    """Os clipes de mira (``mira.py``, o mesmo do AR-15) com a linha de mira desta arma: a alça e a
    massa dela — no Falcão, o eixo da luneta, da lente de trás à da frente."""
    import mira
    L = reais.arma(id_); path = os.path.join(FP, f'{id_}.glb')
    if 'luneta' in L: a, m = L['luneta']['tras'], L['luneta']['frente']
    else: a, m = L['mira']['alca'], L['mira']['massa']
    def medir(c, clip):
        G = c.world(clip, c.gun, 0.0) @ ARMA; sobe = np.array([0.0, BAIXO.get(id_, 0.0), 0.0])      # mede acima da mira: a arma fica abaixo
        return (G @ np.append(a + sobe, 1.0))[:3], (G @ np.append(m + sobe, 1.0))[:3], None, 0.0, 0.0
    # Na mira a arma quase não mexe (Jhon, 19/11: "precisa de precisão, igual a AR-15"): os clipes de mira saem das bases
    # quietas (``parado_mira``, ``andar_mira``), que tomam o lugar do parado e do andar só enquanto ``mira.py`` roda.
    from glb import Glb
    def renomear(pares, tirar=()):
        g = Glb(path); g.json['animations'] = [a for a in g.json['animations'] if a['name'] not in tirar]
        for a in g.json['animations']: a['name'] = pares.get(a['name'], a['name'])
        recarga.compactar(g); g.save(path)
    renomear({'parado': '_parado', 'andar': '_andar', 'parado_mira': 'parado', 'andar_mira': 'andar'})
    antes = mira.medir, mira.abrir_alca, mira.OLHO, mira.RECUA
    mira.medir, mira.abrir_alca, mira.OLHO, mira.RECUA = medir, (lambda c, parado: False), MIRAS[id_], COICE_NA_MIRA.get(id_, mira.RECUA)
    try: mira.fazer(path, ficha)
    finally:
        mira.medir, mira.abrir_alca, mira.OLHO, mira.RECUA = antes
        renomear({'_parado': 'parado', '_andar': 'andar'}, tirar=('parado', 'andar'))
    if ficha:
        d = json.load(open(ficha, encoding='utf-8'))
        for e in d.values():
            if e.get('arquivo', '').endswith(f'/{id_}.glb'): e['animacoes'].pop('parado_mira', None); e['animacoes'].pop('andar_mira', None)
        json.dump(d, open(ficha, 'w', encoding='utf-8'), indent=1, ensure_ascii=False, sort_keys=True); open(ficha, 'a', encoding='utf-8').write('\n')


if __name__ == '__main__':
    args = sys.argv[1:]; ficha = None
    if '--ficha' in args:
        k = args.index('--ficha'); ficha = args[k + 1]; del args[k:k + 2]
    mov = Movimento(Esqueleto(FONTE))
    for id_ in args or list(LONGAS) + list(CURTAS):
        print(longa(id_, mov, ficha) if id_ in LONGAS else CURTAS[id_](mov, ficha))
        if id_ in MIRAS: mirar(id_, ficha)
        if id_ in LONGAS:
            import coronhada; coronhada.fazer(os.path.join(FP, f'{id_}.glb'), ficha)
    # O conjunto refeito sai com a malha e a paleta de antes: o acabamento de tools/armas/peles.py (a forma nova da faca
    # e da granada, o UV e o material das texturas) volta aqui, para a arma e para as peles dela — que assim também
    # recebem as animações novas. Não redesenha textura (segundos). SEM_PELES=1 pula.
    if not os.environ.get('SEM_PELES'):
        import peles; peles.reaplicar(set(args or list(LONGAS) + list(CURTAS)))
