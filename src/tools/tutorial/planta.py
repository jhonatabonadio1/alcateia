import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Circle, Polygon, FancyArrowPatch, Wedge
import numpy as np

BG="#0a1020"; LINE="#9fb3d1"; DIM="#5d7191"; TXT="#e6edf7"
C1="#22d3ee"; C2="#f59e0b"; C3="#a78bfa"; C4="#38bdf8"; C5="#f97316"; C6="#34d399"; CH="#e5e7eb"

fig=plt.figure(figsize=(18,21.6),dpi=130,facecolor=BG)
ax=fig.add_axes([0.02,0.365,0.96,0.582],facecolor=BG)
ax.set_xlim(-17,167); ax.set_ylim(118,-17); ax.set_aspect("equal"); ax.axis("off")

def room(x,y,w,h,c,alpha=0.14,lw=2.2,ls="-"):
    ax.add_patch(Rectangle((x,y),w,h,fc=c,ec="none",alpha=alpha,zorder=1))
    ax.add_patch(Rectangle((x,y),w,h,fc="none",ec=LINE,lw=lw,ls=ls,zorder=3))
def floor(x,y,w,h,c,alpha=0.14):
    ax.add_patch(Rectangle((x,y),w,h,fc=c,ec="none",alpha=alpha,zorder=1))
def wall(x,y,w,h,c=LINE,fc="#1b2a44",lw=1.4,z=4):
    ax.add_patch(Rectangle((x,y),w,h,fc=fc,ec=c,lw=lw,zorder=z))
def t(x,y,s,size=9,c=TXT,ha="center",va="center",w="normal",z=8,**k):
    ax.text(x,y,s,fontsize=size,color=c,ha=ha,va=va,fontweight=w,zorder=z,**k)
def dot(x,y,n,c):
    ax.add_patch(Circle((x,y),1.7,fc=c,ec=BG,lw=1.2,zorder=9)); t(x,y+0.05,str(n),8.5,BG,w="bold",z=10)
def target(x,y,c="#fb7185",r=0.9):
    ax.add_patch(Circle((x,y),r,fc=c,ec=BG,lw=0.8,zorder=7))
    ax.add_patch(Circle((x,y),r*0.5,fc=BG,ec="none",zorder=8)); ax.add_patch(Circle((x,y),r*0.2,fc=c,ec="none",zorder=9))
def door(x,y,horiz=True,c="#facc15",L=3.2):
    if horiz: ax.plot([x-L/2,x+L/2],[y,y],color=c,lw=3.5,zorder=6,solid_capstyle="butt")
    else: ax.plot([x,x],[y-L/2,y+L/2],color=c,lw=3.5,zorder=6,solid_capstyle="butt")


# ================= AMBIENTE: skyline, muro, vegetação, props =================
VG="#4ade80"; VGD="#14532d"; VGM="#166534"; STONE="#94a3b8"; WOOD="#a16207"
rng=np.random.default_rng(11)
NEON=["#f0abfc","#22d3ee","#fbbf24","#fb7185","#a78bfa"]
def tower(x,y,w,h,side):
    # side: 'N','S','W','E' — torre de silhueta fora do muro
    if side=="N": r=(x,y-h,w,h)
    elif side=="S": r=(x,y,w,h)
    elif side=="W": r=(x-h,y,h,w)
    else: r=(x,y,h,w)
    ax.add_patch(Rectangle((r[0],r[1]),r[2],r[3],fc="#0d1730",ec="#1e2d52",lw=0.8,zorder=0.6))
    for _ in range(int(max(w,h)*0.9)):
        if side in("N","S"):
            px=r[0]+rng.uniform(0.3,w-0.3); py=r[1]+rng.uniform(0.3,h-0.3)
        else:
            px=r[0]+rng.uniform(0.3,h-0.3); py=r[1]+rng.uniform(0.3,w-0.3)
        ax.plot(px,py,marker="s",ms=1.6,color=NEON[rng.integers(0,5)],alpha=0.75,zorder=0.7,ls="")
x=-17
while x<167:
    w=rng.uniform(3,7); tower(x,-3,w,rng.uniform(4,13.5),"N"); x+=w+rng.uniform(0.2,1.2)
x=-17
while x<167:
    w=rng.uniform(3,7); tower(x,103,w,rng.uniform(4,13.5),"S"); x+=w+rng.uniform(0.2,1.2)
y=-3
while y<103:
    w=rng.uniform(3,7); tower(-3,y,w,rng.uniform(4,12.5),"W"); tower(153,y,w,rng.uniform(4,12.5),"E"); y+=w+rng.uniform(0.2,1.2)
ax.add_patch(Rectangle((-3,-3),156,106,fc="none",ec="#7dd3fc",lw=1.6,ls=(0,(6,3)),zorder=2))
pass
t(75,-15.2,"SKYLINE DO DISTRITO NEON · torres de vidro e cromo de 40 a 105 m, telões holográficos, vistas por cima do muro",7.2,"#c084fc",w="bold")

def tree(x,y,r=2.2):
    ax.add_patch(Circle((x+0.35,y+0.35),r,fc="#000000",ec="none",alpha=0.25,zorder=2))
    ax.add_patch(Circle((x,y),r,fc=VGM,ec=VG,lw=0.8,zorder=2.2))
    ax.add_patch(Circle((x-r*0.25,y-r*0.25),r*0.5,fc=VG,ec="none",alpha=0.35,zorder=2.3))
def pine(x,y,r=2.0):
    n=9; ang=np.linspace(0,2*np.pi,2*n,endpoint=False)
    rad=np.where(np.arange(2*n)%2==0,r,r*0.5)
    pts=np.c_[x+rad*np.cos(ang),y+rad*np.sin(ang)]
    ax.add_patch(Polygon(pts,fc=VGD,ec=VG,lw=0.7,zorder=2.2))
    ax.plot(x,y,marker="o",ms=1.6,color=VG,zorder=2.3)
def hedge(x,y,w,h):
    ax.add_patch(Rectangle((x,y),w,h,fc=VGM,ec=VG,lw=0.8,zorder=2.1,joinstyle="round"))
def lawn(x,y,w,h,a=0.10):
    ax.add_patch(Rectangle((x,y),w,h,fc=VG,ec="none",alpha=a,zorder=0.9))
def bench(x,y,horiz=True):
    ax.add_patch(Rectangle((x,y),3 if horiz else 1,1 if horiz else 3,fc=WOOD,ec=BG,lw=0.6,zorder=2.4))
def rock(x,y,r=1.2):
    ax.add_patch(Polygon([[x-r,y],[x-r*0.4,y-r*0.9],[x+r*0.7,y-r*0.7],[x+r,y+r*0.2],[x+r*0.2,y+r*0.9],[x-r*0.8,y+r*0.6]],fc="#475569",ec=STONE,lw=0.6,zorder=2.3))
def path(pts,w=4.5,a=0.22):
    p=np.array(pts); ax.plot(p[:,0],p[:,1],color="#e2e8f0",lw=w,alpha=a,zorder=1,solid_capstyle="round")
def lamp(x,y):
    ax.add_patch(Circle((x,y),0.55,fc="#fde68a",ec="none",zorder=2.5)); ax.add_patch(Circle((x,y),1.6,fc="#fde68a",ec="none",alpha=0.12,zorder=1.2))

# ---- A. Jardim Suspenso (noroeste): gramado, bosque, torre da Iris, antena
lawn(4,3,40,32)
path([(12,38),(12,30),(18,24),(26,20),(34,22),(40,28)])
t(23,4.8,"JARDIM SUSPENSO",7.6,VG,w="bold"); t(20,7.2,"livre, sem etapa",6,DIM)
for (x,y,r) in ((7,12,2.6),(10.5,8.5,2.2),(7.5,19,2.0),(5.8,27,2.4),(16,13,2.5),(20,9,2.0),(25,31,2.4),(40,32,2.6),(41,10,2.2),(37,16,2.0)): tree(x,y,r)
hedge(6,33,10,1.4); hedge(20,33,10,1.4); hedge(31,26,1.4,6)
for x,y in ((15,26),(22,22),(30,24)): bench(x,y)
for x,y in ((11,34),(20,26),(36,25),(14,17)): lamp(x,y)
# torre da Iris + antena parabólica (antena como no pátio de referência)
ax.add_patch(Rectangle((29,6),9,9,fc="#1b2a44",ec=C1,lw=1.6,zorder=3)); t(33.5,11.3,"TORRE DE\nCONTROLE",6,C1,w="bold")
ax.add_patch(Circle((36.2,7.8),1.7,fc="none",ec=C1,lw=1.2,zorder=4)); ax.add_patch(Circle((36.2,7.8),0.5,fc=C1,ec="none",zorder=4))
t(33.5,17.2,"antena e observação",5.6,DIM)
# caixa d'água / tanque de apoio
ax.add_patch(Circle((41.5,22.5),1.9,fc="#1b2a44",ec=STONE,lw=1.2,zorder=3)); t(41.5,25.6,"cisterna",5.6,DIM)

# ---- D. Praça do Uivo (leste): estátua do lobo no centro, bancos, árvores
lawn(123,39,26,24,0.07)
ax.add_patch(Rectangle((123,39),26,24,fc="#e2e8f0",ec="none",alpha=0.05,zorder=0.9))
ax.add_patch(Circle((136,51),5.2,fc="none",ec=STONE,lw=1.4,zorder=2.2)); ax.add_patch(Circle((136,51),3.2,fc="#334155",ec=STONE,lw=1.2,zorder=2.3))
t(136,51,"lobo",6.2,TXT,w="bold"); t(136,43.2,"PRAÇA DO UIVO",7,"#fde68a",w="bold"); t(136,59.6,"estátua do lobo",5.6,DIM)
for (x,y) in ((126,42),(146,42),(126,61),(146,61)): tree(x,y,2.1)
for (x,y,h) in ((128,50,False),(143.5,50,False),(131,46.5,True),(138,46.5,True)): bench(x,y,h)
for x,y in ((129,46),(143,46),(129,54),(143,54)): lamp(x,y)
hedge(124,40.3,6,1.2); hedge(142,40.3,6,1.2)

# ---- E. Pinhal da Matilha (sudeste): fecha o fundo da Arena da Matilha
lawn(121,65,28,34,0.10)
t(135,66.4,"PINHAL DA MATILHA",7.2,VG,w="bold"); t(135,68.4,"18 a 25 m, acima dos muros da arena",5.6,DIM)
path([(122,86),(130,82),(138,86),(146,80)],w=3.5,a=0.18)
for (x,y) in ((124,72),(128,76),(132,72),(137,75),(142,71),(146,76),(125,84),(130,91),(135,87),(140,93),(145,88),(123,95),(148,96),(134,96),(127,79),(144,82),(139,80)): pine(x,y,rng.uniform(1.8,2.6))
for x,y in ((132,80),(141,87),(126,89)): rock(x,y,rng.uniform(1.0,1.5))

# ---- Cortina de árvores atrás do muro: faixa norte e oeste
for x in range(5,148,9):
    if 44<x<107 or 106<=x<=146: pass
pass

# ---- Jardineiras do corredor e do hall (vegetação baixa que não atrapalha o jogo)
for x in (22,26,38,46,52,60):
    ax.add_patch(Rectangle((x,53.4),2.4,0.9,fc=VGM,ec=VG,lw=0.6,zorder=5))
for x in (22,26,38,46,52,60):
    ax.add_patch(Rectangle((x,45.7),2.4,0.9,fc=VGM,ec=VG,lw=0.6,zorder=5))

# ---- Rua Ignição: passarela entre os dois prédios (ideia da Estação)
ax.add_patch(Rectangle((124,12.6),4,2.2,fc="#1b2a44",ec=C5,lw=1.2,zorder=6)); t(126,11.4,"passarela",5.4,DIM)
# ---- Arsenal e letreiros (ideias do campo de tiro de referência)
ax.add_patch(Rectangle((46,2.6),9,2.2,fc="#1b2a44",ec=C2,lw=1.2,zorder=6)); t(50.5,3.8,"ARSENAL",5.6,C2,w="bold")
# faixas de perigo amarelas/pretas na linha de tiro
for k in range(0,50,2):
    ax.add_patch(Rectangle((50+k,28.9),1,0.6,fc="#facc15" if (k//2)%2==0 else "#111827",ec="none",zorder=5))

# ---------------- HUB ----------------
ax.add_patch(Circle((75,50),12,fc=CH,ec="none",alpha=0.07,zorder=1)); ax.add_patch(Circle((75,50),12,fc="none",ec=LINE,lw=2.4,zorder=3))
ax.add_patch(Circle((75,50),4,fc="none",ec=C1,lw=1.8,ls="--",zorder=5)); ax.add_patch(Circle((75,50),1.2,fc=C1,ec="none",zorder=6))
t(75,50+7.2,"NÚCLEO DA IRIS",9,CH,w="bold"); t(75,50-7.2,"holograma · portas",7.5,DIM)
# ---------------- Z1: despertar + corredor ----------------
room(4,38,16,24,C1)
t(11,42.2,"SALA DE\nATIVAÇÃO",7.2,C1,w="bold"); t(12,58.6,"16 × 24 m · teto 5 m",7,DIM)
ax.add_patch(Circle((12,50),2.2,fc="none",ec=C1,lw=1.6,zorder=6)); t(12,50,"SPAWN",6.5,C1,w="bold")
wall(18.8,39,1.2,5.5); wall(18.8,55.5,1.2,5.5)   # porta (abertura central)
room(20,47,43,6,C1,alpha=0.10)
# marcas no chão
for x in (24,27): ax.add_patch(Rectangle((x-0.6,49.2),1.2,1.6,fc=C1,ec="none",alpha=0.8,zorder=6))
# vão
ax.add_patch(Rectangle((31,47),4,6,fc="#05080f",ec="#fb7185",lw=1.6,hatch="////",zorder=6)); t(33,44.8,"VÃO 4 m",6.8,"#fb7185",w="bold"); t(33,55.2,"cai → volta à marca",6.2,DIM)
# viga
ax.add_patch(Rectangle((42,47),2,6,fc="#1b2a44",ec=C1,lw=1.4,zorder=6)); t(43,44.8,"VIGA 1,2 m",6.8,C1,w="bold"); t(43,55.2,"agachar",6.2,DIM)
# trecho silencioso
ax.add_patch(Rectangle((49,47.4),12,5.2,fc="none",ec=C1,lw=1.2,ls=":",zorder=6)); t(55,44.8,"TRECHO SILENCIOSO",6.8,C1,w="bold"); t(55,55.2,"Shift",6.2,DIM)
dot(12,45,"1.1",C1); dot(25.5,52.5,"1.2",C1); dot(33,50,"1.3",C1); dot(43,50,"1.4",C1); dot(55,50,"1.5",C1)
t(41,57.6,"CORREDOR DE CALIBRAGEM · 43 × 6 m",6.6,DIM)
# ---------------- Z2: galeria de tiro ----------------
room(45,2,60,32,C2)
t(75,5.2,"LINHA DE FOGO · 60 × 32 m",8.5,C2,w="bold")
ax.plot([50,100],[28,28],color=C2,lw=2,ls="--",zorder=5); t(75,29.6,"LINHA DE TIRO",6.8,C2)
for x in (50,60,70,80,90,100): ax.plot([x,x],[27,31],color=LINE,lw=1.4,zorder=5)
for x in (55,65,75,85,95): t(x,32.1,f"cabine {int((x-45)/10)}",5.6,DIM)
for x in (60,75,90): target(x,21)
for x in (58,75,92): target(x,13,r=0.8)
target(75,7.8,r=0.75); t(75,10,"alvo distante 20 m",6,DIM)
t(75,18.2,"alvos próximos (7 m)",6,DIM)
# estação pistola e corpo a corpo
floor(46,20,10,8,C2,0.10); t(51,22,"PISTOLA",6.5,C2,w="bold"); target(49,24.5,r=0.7); target(53,24.5,r=0.7)
floor(46,29.5,8,4,C2,0.10)
ax.add_patch(Circle((48,31.7),1.0,fc="#fb7185",ec=BG,zorder=7)); ax.add_patch(Circle((52,31.7),1.0,fc="#fb7185",ec=BG,zorder=7)); t(50,34.4,"corpo a corpo",5.8,DIM)
# poço de granada
wall(94,15,10,1.4); t(99,17.5,"muro",5.8,DIM); target(99,10,r=0.9); t(99,12.6,"alvo atrás do muro",5.6,DIM); t(99,7.6,"POÇO DA GRANADA",6.4,C2,w="bold")
dot(51,26.6,"2.4",C2); dot(75,24.5,"2.1",C2); dot(68,13,"2.3",C2); dot(103,20,"2.8",C2)
door(75,38,True,L=4); wall(73,34,0.7,4); wall(78.3,34,0.7,4); t(81,36.2,"acesso",5.8,DIM)
# ---------------- Z3: transformação ----------------
ax.add_patch(Circle((112,50),9,fc=C3,ec="none",alpha=0.14,zorder=1)); ax.add_patch(Circle((112,50),9,fc="none",ec=LINE,lw=2.2,zorder=3))
ax.add_patch(Circle((112,50),3,fc="none",ec=C3,lw=1.8,zorder=6)); t(112,50,"3",8,C3,w="bold")
t(112,44.2,"CÂMARA DE\nMETAMORFOSE",6.6,C3,w="bold"); t(112,57,"Ø 18 m · tecla 3",6.2,DIM)
room(87,47,16,6,C3,alpha=0.10)
dot(112,53.3,"3.1",C3)
# ---------------- Z4: Arena da Matilha ----------------
room(40,64,80,34,C4,alpha=0.10)
ax.add_patch(Rectangle((40,64),80,34,fc="none",ec=C4,lw=3,zorder=4))
t(80,66.4,"ARENA DA MATILHA · 80 × 34 m · paredes 14 m",8.5,C4,w="bold")
# reta de corrida
ax.annotate("",xy=(86,70.5),xytext=(44,70.5),arrowprops=dict(arrowstyle="-|>",color=C4,lw=1.6),zorder=6); t(65,72.5,"RETA DE CORRIDA 42 m (W W)",6.6,C4)
# plataformas de bote
for (x,y,w,h,alt) in ((58,82,6,6,"3 m"),(70,82,6,6,"6 m")):
    wall(x,y,w,h,c=C4); t(x+w/2,y+h/2,alt,6.4,C4)
target(61,78.8,r=0.8); target(73,78.8,r=0.8); t(67,79.6,"BOTE (botão direito no ar)",6,DIM)
# vão do salto
ax.add_patch(Rectangle((46,80),8,12,fc="#05080f",ec="#fb7185",lw=1.4,hatch="////",zorder=6)); t(50,78.2,"VÃO 8 m · SALTO",6.4,"#fb7185",w="bold"); target(50,96.2,r=0.8) if False else None
target(43,86,r=0.8); t(43,89,"alvo",5.6,DIM)
# parede de corrida
ax.plot([84,116],[97.2,97.2],color=C4,lw=6,zorder=6,alpha=0.9); t(100,94.4,"PAREDE DE CORRIDA 32 m × 14 m",6.4,C4)
# uivo e especial
ax.add_patch(Circle((106,78),3,fc="none",ec=C4,lw=1.6,ls="--",zorder=6)); t(106,78,"R",8,C4,w="bold"); t(106,83,"círculo do uivo",6,DIM)
for dx,dy in ((0,0),(2.2,1.6),(-2.2,1.6),(0,3.4)): target(90+dx,80+dy,r=0.8)
t(90,87,"AGRUPAMENTO · especial (Q)",6,DIM)
dot(44,72,"3.2",C4); dot(65,69,"3.4",C4); dot(50,95,"3.5",C4); dot(67,84.5,"3.6",C4); dot(118,95,"3.7",C4); dot(106,75,"3.9",C4); dot(90,77,"3.10",C4)
door(75,62,True,L=4); wall(73,62,0.7,4); wall(78.3,62,0.7,4)
# ---------------- Z5: rua das chamas ----------------
room(106,4,40,32,C5,alpha=0.12)
t(126,7,"RUA IGNIÇÃO · 40 × 32 m",8,C5,w="bold")
wall(112,10,12,8,c=C5); t(118,14,"prédio",6.4,DIM)
wall(128,10,12,8,c=C5); t(134,14,"prédio",6.4,DIM)
for x,l in ((118,"A"),(126,"B"),(134,"C"),(142,"D")):
    ax.add_patch(Polygon([[x-1.2,5.4],[x+1.2,5.4],[x,3.2]],fc="#facc15",ec="none",zorder=7)) if False else None
for x,l in ((111,"A"),(126,"B"),(141,"C")):
    ax.add_patch(Rectangle((x-1.6,19.5),3.2,1.4,fc="#facc15",ec=BG,zorder=7)); t(x,22.5,f"onda {l}",6.2,"#facc15")
for x,y in ((120,27),(132,26),(126,31)): wall(x,y,3,1.6,c=C5)
t(126,34,"caixas de cobertura",5.8,DIM)
ax.add_patch(Rectangle((140,26),5,8,fc=C5,ec="none",alpha=0.25,zorder=2)); t(142.5,30,"KP\nEXIBIDO",5.8,C5,w="bold")
dot(126,24.5,"4.1",C5); dot(143,12,"4.2",C5)
# corredor de ligação
room(109,36,6,5,C5,alpha=0.10)
# ---------------- Z6: renascer ----------------
room(4,70,26,26,C6)
t(17,72.6,"PONTO DE RETORNO · 26 × 26 m",7.6,C6,w="bold")
for i,x in enumerate((8,13.5,19,24.5)):
    ax.add_patch(Rectangle((x-2,78),4,4,fc=C6,ec=BG,alpha=0.35,zorder=6)); ax.add_patch(Rectangle((x-2,78),4,4,fc="none",ec=C6,lw=1.4,zorder=7)); t(x,80,str(i+1),9,TXT,w="bold")
t(16,84.4,"pedestais dos conjuntos 1–4",6.2,DIM)
ax.add_patch(Circle((26,90),2.2,fc="none",ec=C6,lw=1.4,ls="--",zorder=6)); t(26,90,"L",7.5,C6,w="bold"); t(26,93.6,"lobo",6,DIM)
ax.add_patch(Rectangle((5,92),9,3,fc=C6,ec=BG,alpha=0.3,zorder=6)); t(9.5,93.5,"PORTAL · JOGAR",6,C6,w="bold")
dot(17,76,"5.1",C6)
room(30,80,10,5,C6,alpha=0.10)

# ================= PREENCHIMENTO: nenhuma área vazia =================
Z=5
def tr(x,y,r=1.5):
    ax.add_patch(Circle((x,y),r,fc=VGM,ec=VG,lw=0.7,zorder=Z)); ax.add_patch(Circle((x-r*0.25,y-r*0.25),r*0.45,fc=VG,ec="none",alpha=0.35,zorder=Z+0.1))
def pn(x,y,r=1.5):
    n=8; ang=np.linspace(0,2*np.pi,2*n,endpoint=False); rad=np.where(np.arange(2*n)%2==0,r,r*0.5)
    ax.add_patch(Polygon(np.c_[x+rad*np.cos(ang),y+rad*np.sin(ang)],fc=VGD,ec=VG,lw=0.6,zorder=Z))
def hg(x,y,w,h): ax.add_patch(Rectangle((x,y),w,h,fc=VGM,ec=VG,lw=0.6,zorder=Z))
def bn(x,y,w=3,h=1): ax.add_patch(Rectangle((x,y),w,h,fc=WOOD,ec=BG,lw=0.5,zorder=Z+0.2))
def lp(x,y):
    ax.add_patch(Circle((x,y),0.5,fc="#fde68a",ec="none",zorder=Z+0.3)); ax.add_patch(Circle((x,y),1.4,fc="#fde68a",ec="none",alpha=0.12,zorder=Z))
def cont(x,y,w,h,c="#b45309"):
    ax.add_patch(Rectangle((x,y),w,h,fc=c,ec="#fbbf24",lw=0.8,alpha=0.55,zorder=Z))
    for k in range(1,int(w)): ax.plot([x+k,x+k],[y,y+h],color="#fbbf24",lw=0.3,alpha=0.5,zorder=Z)
def box(x,y,w,h,c,fc="#1b2a44",lw=1.2): ax.add_patch(Rectangle((x,y),w,h,fc=fc,ec=c,lw=lw,zorder=Z+0.1))
def cone(x,y): ax.add_patch(Polygon([[x,y-0.6],[x-0.5,y+0.5],[x+0.5,y+0.5]],fc="#fb923c",ec="none",zorder=Z+0.2))
def rk(x,y,r=1.1): rock(x,y,r); 
def paved(x,y,w,h,a=0.06): ax.add_patch(Rectangle((x,y),w,h,fc="#e2e8f0",ec="none",alpha=a,zorder=0.95))

# --- anéis de borda: pista de serviço (norte), alameda (oeste), canteiro (leste), pista de aquecimento (sul)
paved(-2.5,-2.5,155,3.7,0.08); ax.plot([-2.5,152.5],[-0.65,-0.65],color="#facc15",lw=0.9,ls=(0,(6,5)),zorder=1.2)
for x in range(2,150,10): lp(x,0.4)
for x in range(8,146,28): box(x,-1.9,2.6,1.2,C1,lw=0.8)     # baias de drone de serviço
paved(-2.5,1.2,5.9,100,0.05)
for y in range(4,100,9): tr(0.5,y,1.3); lp(2.6,y+4.5)
hg(147.3,4,2.4,94)
for y in range(6,98,11): tr(151,y,1.4)
for y in range(6,98,11): lp(148.5,y+5)
# pista de aquecimento (4 raias) ao sul
ax.add_patch(Rectangle((2,99.3),148,3.2,fc="#b45309",ec="none",alpha=0.30,zorder=1.1))
for k in range(1,4): ax.plot([2,150],[99.3+k*0.8]*2,color="#f1f5f9",lw=0.6,alpha=0.55,zorder=1.2)
t(36,101.0,"PISTA DE AQUECIMENTO · 4 raias · 148 m",5.6,"#fdba74",z=2)
for x in (10,60,110,140): ax.plot([x,x],[99.3,102.5],color="#f1f5f9",lw=1.0,zorder=1.3)

# --- Jardim Suspenso: espelho d'água e mais caminhos
ax.add_patch(Polygon([[18,17],[25,15.4],[30,17.6],[26,21],[19,20.5]],fc="#38bdf8",ec="#7dd3fc",lw=1,alpha=0.35,zorder=Z-1))
t(24,18.4,"espelho d'água",5.4,"#7dd3fc",z=Z+1); rk(18,21,0.9); rk(29.4,20.7,0.8)
path([(40,28),(41,35)],w=4,a=0.22)

# --- S1/S2: passagens sul (entre Sala de Ativação e Ponto de Retorno, e sul do corredor)
for x in (8,14,20,26): tr(x,65.6,1.5)
hg(5,67.6,24,0.9); lp(11,66); lp(23,66)
for x in (24,34,44,54): tr(x,62.8,1.3)
for x in (28,40,50): bn(x,59.7)
for x in (31,47): lp(x,61.4)
# --- S3: pátio técnico entre o Ponto de Retorno e a Arena (contêineres de material)
cont(31.2,66,7.4,3.2); cont(31.2,70.4,7.4,3.2); cont(31.2,74.8,3.2,3.4,"#0e7490"); lp(37.5,77.5)
t(35,64.9,"depósito",5.4,DIM,z=Z+1)
cont(31.2,87,7.4,3.2,"#0e7490"); cont(31.2,91.4,7.4,3.2); tr(37,96.6,1.3); lp(33,86)
# --- S4: terraço sul do Núcleo e da Câmara de Metamorfose
for (x,y) in ((90,57),(95,60),(100,56.5)): tr(x,y,1.5)
bn(91,59.2); bn(96,57.4,1,3); hg(88,62.2,14,0.9)
hg(104,62.4,16,0.8); lp(104,60.5); lp(120,60.5)
# --- S5: posto médico, quadra de agilidade, café
box(46,36.2,8.5,5.6,VG); ax.add_patch(Rectangle((49.3,37.4),1.9,3.2,fc=VG,ec="none",zorder=Z+0.3)); ax.add_patch(Rectangle((48.65,38.7),3.2,1.9,fc=VG,ec="none",zorder=Z+0.3))
t(50.2,43.2,"POSTO MÉDICO",5.4,VG,z=Z+1)
tr(58,40,1.4); tr(60,36.5,1.2); bn(56,43.2,3,0.9); lp(57,38)
ax.add_patch(Rectangle((22,37),9,7,fc="none",ec=C1,lw=1,zorder=Z)); ax.plot([26.5,26.5],[37,44],color=C1,lw=0.6,zorder=Z); 
for x,y in ((23.5,38.5),(25,40),(23.5,41.5),(25,43),(28,38.5),(29.5,40),(28,41.5),(29.5,43)): cone(x,y)
t(26.5,36.1,"QUADRA DE AGILIDADE",5.2,C1,z=Z+1)
for (x,y) in ((36,38),(41,38.5),(38,42)): tr(x,y,1.5)
bn(33.5,40.5,1,3)
box(90,36.6,7,4.4,"#f0abfc"); t(93.5,38.8,"CAFÉ DO\nSETOR",5,"#f0abfc",z=Z+1)
for (x,y) in ((100.5,38),(102.5,42),(98.3,43)): ax.add_patch(Circle((x,y),1.0,fc="#334155",ec="#f0abfc",lw=0.7,zorder=Z)); 
tr(88,43.5,1.3); tr(104.5,36.8,1.3); lp(95,43); lp(103.5,40)
tr(107,39,1.3); tr(119,38,1.3); hg(116,40.3,5,0.8); hg(104,40.3,4,0.8)
# --- Galeria: trilho do alvo móvel, bancada de recarga, depósito de alvos
ax.plot([78,100],[25.2,25.2],color=C2,lw=1.4,zorder=Z); ax.plot([78,78],[24.5,25.9],color=C2,lw=2,zorder=Z); ax.plot([100,100],[24.5,25.9],color=C2,lw=2,zorder=Z)
box(88,24.5,3,1.4,C2,lw=0.8); t(89,26.9,"alvo móvel · trilho",5.2,DIM,z=Z+1)
box(46.6,8.4,8,1.6,C2,lw=0.8); box(46.6,11.2,3.4,1.2,C2,lw=0.8); box(51.2,11.2,3.4,1.2,C2,lw=0.8); t(50.6,14.2,"bancada de recarga",5.2,DIM,z=Z+1)
box(95.5,21.5,7.5,3.6,C2,lw=0.8); t(99.2,23.3,"depósito\nde alvos",5,DIM,z=Z+1)
for x in (62,68,82,86): box(x,15.6,2.4,0.7,C2,lw=0.6)     # cobertura baixa
for x in (44.2,105.8): 
    for y in (6,14,22,30): ax.add_patch(Circle((x,y),0.6,fc="#fde68a",ec="none",zorder=Z))
# --- Sala de Ativação: armários, bancos e vasos
box(4.6,41,1.5,18,C1,lw=0.8); bn(8,59.4); bn(14,59.4); pass
tr(6.5,61,1.1); tr(17.5,61,1.1)
# --- corredor: faixas de LED no piso
ax.plot([21,62],[47.35,47.35],color=C1,lw=0.9,alpha=0.8,zorder=Z); ax.plot([21,62],[52.65,52.65],color=C1,lw=0.9,alpha=0.8,zorder=Z)
ax.add_patch(Circle((75,50),8.2,fc="none",ec=C1,lw=0.7,ls=":",zorder=Z))
# --- Arena da Matilha: rampa, pilares, rochas, mata baixa
for k in range(5): ax.add_patch(Polygon([[80+k*3,89],[82+k*3,90.5],[80+k*3,92],[81+k*3,92],[83+k*3,90.5],[81+k*3,89]],fc=C4,ec="none",alpha=0.6,zorder=Z))
box(79,87.6,16,5.8,C4,fc="none",lw=0.9); t(87,94.4,"RAMPA LONGA 18°",5.4,C4,z=Z+1)
for x in (58,63,68,73,78): ax.add_patch(Circle((x,95),0.9,fc="#334155",ec=C4,lw=0.8,zorder=Z))
t(68,92.7,"pilares 9 m (apoio do bote)",5.2,DIM,z=Z+1)
for (x,y,r) in ((111,70,1.5),(115,72,1.2),(100,70.5,1.1),(47,94.5,1.3),(51,96,1.0),(108,90,1.4),(113,88,1.1),(116,93,1.3)): rk(x,y,r)
for (x,y) in ((113,67.8),(116,69.4),(110,67.4),(117.5,72)): pn(x,y,1.2)
hg(118,66,1.4,30)
t(112,64.9,"mata baixa e rocha",5,DIM,z=Z+1)
# --- Rua Ignição: barricadas, portas, árvores de canteiro, passarela com escada
for (x,y,w) in ((109,29,4),(114,23.4,3),(136,22.6,4),(122,22,3.5),(138,32,3.4)): box(x,y,w,1.0,C5,lw=0.7)
for x in (116,132): ax.add_patch(Rectangle((x,17.4),3.2,0.9,fc="#facc15",ec=BG,lw=0.5,zorder=Z+0.2))
for (x,y) in ((108,6.2),(144.2,6.4),(108,34)): tr(x,y,1.2)
for k in range(5): ax.plot([123.2,123.2],[11+k*0.7,11.3+k*0.7],color=C5,lw=0.6,zorder=Z)
for (x,y) in ((110,12),(110,24),(145,22),(145,16)): lp(x,y)
# --- Ponto de Retorno: bancos e vasos
bn(8,87); bn(13.5,87); tr(5.8,75,1.1); tr(28.2,75,1.1)

# ---------------- ligações ----------------
room(73,34,4,4,C2,alpha=0.0,lw=0.0)
room(73,62,4,2,C4,alpha=0.0,lw=0.0)
room(112,59,6,5,C4,alpha=0.0,lw=0.0)
# ---------------- fluxo ----------------
def seta(p,q,rad=0.0,c="#ffffff"):
    ax.add_patch(FancyArrowPatch(p,q,arrowstyle="-|>",mutation_scale=16,lw=2,color=c,ls=(0,(5,3)),connectionstyle=f"arc3,rad={rad}",zorder=11,alpha=0.9))
pass
# ---------------- bússola / escala ----------------

pass

# ---------------- título e notas ----------------
fig.text(0.02,0.978,"WOLFLEAGUE · SETOR ZERO · CENTRO DE TREINAMENTO",fontsize=22,color=TXT,fontweight="bold",va="center")
fig.text(0.02,0.960,"Cyberpunk de dia · campus no alto do Distrito Neon · sol alto, telões e LED visíveis em pleno sol · planta v3 · 1 quadrado = 10 m · medidas aproximadas",fontsize=10.5,color=DIM,va="center")
fig.text(0.98,0.978,"fluxo:  ➊ Ativação  →  ➋ Linha de Fogo  →  ➌ Metamorfose + Arena da Matilha  →  ➍ Rua Ignição  →  ➎ Retorno",fontsize=9.5,color=C1,va="center",ha="right")
for gx in range(0,151,10): ax.plot([gx,gx],[0,100],color="#16233d",lw=0.5,zorder=0.3)
for gy in range(0,101,10): ax.plot([0,150],[gy,gy],color="#16233d",lw=0.5,zorder=0.3)

L1=[
("TEMA E HORA", "#f0abfc", ["Tema: cyberpunk de dia. Setor Zero, o centro de treinamento da","WolfLeague, num campus no alto do Distrito Neon.","Hora: dia, sol alto, céu limpo, sombras duras e nítidas. LED e","telões brilham mesmo ao sol; holograma da Iris só no Núcleo."]),
("DO DISTRITO NEON", C1, ["Skyline de vidro e cromo, com telões holográficos, por cima do muro.","Kit sci-fi do mapa: painéis de parede, escadas, postes e letreiros.","Céu `dia_claro` do jogo (já existe), sol com sombra; sem névoa.","LED forte nas bordas dos caminhos, na cor do bloco (lê bem ao sol)."]),
("DE OUTROS MAPAS", C2, ["Polar: faixa de perigo, contêiner, cisterna, letreiros de setor.","Estação: passarela elevada na Rua Ignição, lanternas de teto.","Cidade: bancos, postes e sebes do Jardim Suspenso.","Referência de campo de tiro: arsenal, antena, painéis de baia."]),
("REGRA: NADA SEM FUNÇÃO", C5, ["Cada objeto tem papel: cobertura, alvo, marca, orientação ou luz.","Sem área vazia: todo trecho tem caminho, construção, pista, jardim ou prop.","Nada de caixote solto, carro ou lixo; sem prop que bloqueie a mira.","Letreiros dizem o setor (LINHA DE FOGO, ARSENAL, ARENA DA MATILHA...)."]),
]
L2=notas=[
("PAISAGEM E VEGETAÇÃO", VG, ["Jardim Suspenso: gramado, bordos, sebes baixas e bancos.","Praça do Uivo: estátua do lobo, bancos, 4 árvores nos cantos.","Pinhal da Matilha de 18 a 25 m fecha o fundo da Arena da Matilha.","Bordas: pista de serviço (N), alameda (O), canteiro (L), pista de aquecimento (S)."]),
("FUNDO E LIMITE", "#7dd3fc", ["Muro perimetral de 5 m com painéis e telões, para o mapa fechar.","Atrás dele, o skyline em três camadas (perto, meio, longe).","Pinheiros do bosque passam acima do muro, a leste, quebrando a linha.","Barreira invisível só onde o muro não alcança."]),
("MEDIDAS E ALTURAS", C2, ["Corredores 6 m de largura; salas de 4 a 5 m de pé direito.","Linha de Fogo sem teto fechado (céu e painéis de luz).","Arena da Matilha: paredes de 14 m, rocha com musgo na base.","Parede de corrida: 32 m contínuos, sem janela nem quina."]),
("PARA CONSTRUIR", C6, ["Blender, blocos de 4 m (kit modular); exportar .glb.","Colisão simples (caixas) e colisão no teto das salas.","Alvos e bonecos: o Dummy do jogo (parado, móvel, ataca).","Árvores e pinheiros como poucos modelos repetidos (custo baixo)."]),
]
L3=[
("LUZ (padrão do jogo)", "#fde68a", ["Clima `dia`: sol quente e forte, sombra fria e clara, sol com tamanho.","Ambiente azul-violeta: vão, sala e beco legíveis, nunca pretos.","SSAO largo + SSIL, ACES (branco 6,5), névoa com perspectiva aérea.","Mapa aberto sem SDFGI; salas fechadas com `foco` de teto e sombra."]),
("TEXTURAS (detalhe das superfícies)", "#fb923c", ["Material `scifi.tres` + `superficie.gdshader`, grão projetado no mundo.","Piso: asfalto, piso, calçamento e faixa; pista de aquecimento em borracha.","Prédios: concreto, reboco, metal pintado e vidro que reflete o céu.","Verde: folhagem e tronco; rocha e brita nas bases; água no espelho."]),
("LED E LETREIROS", "#f0abfc", ["LED de verdade (`scifi_luz.png`) e lâmpada na cor de cada letreiro.","Postes com lâmpada quente sem sombra (`lampadas`), como no Neon.","Letreiros por `letreiros.py` (Overpass, Saira Stencil), sem Label3D.","Faixa de perigo e setor pintados no piso (`letreiro(..., chao=True)`)."]),
("REGRAS DE MAPA DO JOGO", "#7dd3fc", ["Duas camadas: `montar()` (colisão) e `acabamento()` (sem colisão).","Alturas: caixote 0,8 m; 2,2 m só o lobo; terraço 5 m; rampa p/ o bot.","Escada com chapa de rampa; barreira invisível só onde falta muro.","Dentro do campo, só o mapa da planta: sem peça fora do kit e da paleta."]),
]
def row(items,ybase):
    x0=0.02; w=0.235
    for i,(h,c,ls) in enumerate(items):
        x=x0+i*(w+0.012)
        fig.patches.append(Rectangle((x,ybase),w,0.105,transform=fig.transFigure,fc="#101a30",ec=c,lw=1.2))
        fig.text(x+0.008,ybase+0.092,h,fontsize=9.2,color=c,fontweight="bold",va="center")
        for j,l in enumerate(ls): fig.text(x+0.008,ybase+0.070-j*0.0182,"• "+l,fontsize=6.9,color=TXT,va="center")
row(L3,0.235); row(L1,0.123); row(L2,0.011)
fig.savefig("planta_treinamento.png",facecolor=BG)
fig.savefig("planta_treinamento.svg",facecolor=BG)
print("ok")
