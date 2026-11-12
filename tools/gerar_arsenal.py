#!/usr/bin/env python3
"""
Gera resources/weapons/*.tres a partir da tabela abaixo.

Os números são os de docs/ARSENAL.md, primeira leva de implementação
(M16A2, SG-552, AK-47, UMP-45, M3) mais a M9, que é a secundária do
conjunto padrão. O dano está como no documento, calibrado para 100 HP;
o jogo dobra na hora do tiro (Arsenal.DamageScale) porque o humano tem
200, e assim "tiros para matar" e TTK ficam iguais aos do documento.

Rodar de novo sobrescreve os .tres. Se uma arma for afinada no editor
do Godot, o .tres passa a ser a fonte — atualize a tabela aqui também.

Uso: python3 tools/gerar_arsenal.py
"""
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "resources", "weapons")

CLASSES = {"Rifle": 0, "Smg": 1, "Shotgun": 2, "Sniper": 3, "Pistol": 4}
MODES = {"Auto": 0, "Semi": 1, "Pump": 2, "Bolt": 3}

# estrelas: dano, penetração, precisão, cadência, mobilidade, recuo, alcance
WEAPONS = [
    dict(Id="m16a2", DisplayName="M16A2", Class="Rifle", WpCost=0,
         Description="Fuzil padrão. Referência do arsenal: nada excepcional, e esse é o ponto.",
         Damage=30, HeadshotMultiplier=2.5, Rpm=650, Mode="Auto", Magazine=30, Reserve=90,
         ReloadTactical=2.3, ReloadEmpty=2.9, DrawTime=0.5,
         SpreadBase=0.35, SpreadPerShot=0.22, SpreadMax=4.0, SpreadRecovery=9.0,
         MoveSpreadMultiplier=2.2, CrouchSpreadMultiplier=0.7,
         RecoilVertical=1.0, RecoilVerticalLate=0.55, RecoilEarlyShots=4,
         RecoilHorizontal=0.18, RecoilRecovery=7.0,
         Range=150, FalloffStart=35, FalloffEnd=60, FalloffMin=0.7,
         SpeedMultiplier=1.0, NoiseRadius=45,
         stars=(2.5, 2.5, 2.5, 3.0, 2.5, 2.5, 3.0)),
    dict(Id="sg552", DisplayName="SG-552", Class="Rifle", WpCost=320,
         Description="Fuzil de controle. A única arma que não pune atirar em movimento; recuo quase reto, decorável. Pior arma do arsenal contra lobo em espaço fechado.",
         Damage=26, HeadshotMultiplier=2.5, Rpm=550, Mode="Auto", Magazine=30, Reserve=90,
         ReloadTactical=2.6, ReloadEmpty=3.2, DrawTime=0.55,
         SpreadBase=0.25, SpreadPerShot=0.15, SpreadMax=2.8, SpreadRecovery=11.0,
         MoveSpreadMultiplier=1.5, CrouchSpreadMultiplier=0.65,
         RecoilVertical=0.65, RecoilVerticalLate=0.65, RecoilEarlyShots=4,
         RecoilHorizontal=0.08, RecoilRecovery=9.0,
         Range=150, FalloffStart=45, FalloffEnd=70, FalloffMin=0.8,
         SpeedMultiplier=0.95, NoiseRadius=45,
         stars=(2.0, 2.5, 4.0, 2.5, 2.5, 4.0, 3.5)),
    dict(Id="ak47", DisplayName="AK-47", Class="Rifle", WpCost=380,
         Description="Fuzil de impacto. Melhor TTK do jogo e morte com um tiro na cabeça. Feito para rajadas de 3 a 5, nunca para spray.",
         Damage=42, HeadshotMultiplier=2.4, Rpm=500, Mode="Auto", Magazine=30, Reserve=90,
         ReloadTactical=2.5, ReloadEmpty=3.3, DrawTime=0.6,
         SpreadBase=0.45, SpreadPerShot=0.40, SpreadMax=6.5, SpreadRecovery=7.0,
         MoveSpreadMultiplier=3.0, CrouchSpreadMultiplier=0.6,
         RecoilVertical=1.8, RecoilVerticalLate=0.9, RecoilEarlyShots=5,
         RecoilHorizontal=0.15, RecoilHorizontalLate=0.70, RecoilHorizontalAfter=6,
         RecoilRecovery=5.5,
         Range=150, FalloffStart=30, FalloffEnd=55, FalloffMin=0.75,
         SpeedMultiplier=0.92, NoiseRadius=55,
         stars=(4.0, 3.5, 1.5, 2.0, 2.0, 1.0, 2.5)),
    dict(Id="ump45", DisplayName="UMP-45", Class="Smg", WpCost=280,
         Description="SMG de dano. A arma anti-lobo: existe pela mobilidade do inimigo, não pela distância. É a que você puxa quando ouve o uivo.",
         Damage=28, HeadshotMultiplier=1.8, Rpm=750, Mode="Auto", Magazine=25, Reserve=100,
         ReloadTactical=2.1, ReloadEmpty=2.7, DrawTime=0.35,
         SpreadBase=0.55, SpreadPerShot=0.18, SpreadMax=3.5, SpreadRecovery=12.0,
         MoveSpreadMultiplier=1.3, CrouchSpreadMultiplier=0.8,
         RecoilVertical=0.5, RecoilVerticalLate=0.5, RecoilEarlyShots=4,
         RecoilHorizontal=0.25, RecoilRecovery=10.0,
         Range=100, FalloffStart=15, FalloffEnd=30, FalloffMin=0.6,
         SpeedMultiplier=1.08, NoiseRadius=35,
         stars=(2.5, 1.5, 2.0, 4.0, 4.5, 3.0, 1.5)),
    dict(Id="m3", DisplayName="M3", Class="Shotgun", WpCost=350,
         Description="Escopeta pump. Mata lobo de escudo leve com um tiro. O ciclo de 0,85 s é a janela em que o lobo te alcança; a recarga cartucho a cartucho pode ser interrompida pelo tiro.",
         Damage=13, HeadshotMultiplier=1.5, Pellets=9, WolfStun=0.4,
         Rpm=70, Mode="Pump", Magazine=8, Reserve=32,
         ReloadTactical=0.55, ReloadEmpty=0.55, ShellByShell=True, ShellTime=0.55, ShellEnter=0.3,
         DrawTime=0.6,
         SpreadBase=4.5, SpreadPerShot=0.0, SpreadMax=4.5, SpreadRecovery=99.0,
         MoveSpreadMultiplier=1.0, CrouchSpreadMultiplier=1.0,
         RecoilVertical=3.5, RecoilVerticalLate=3.5, RecoilEarlyShots=1,
         RecoilHorizontal=0.3, RecoilRecovery=4.0,
         Range=40, FalloffStart=8, FalloffEnd=20, FalloffMin=0.45,
         SpeedMultiplier=0.9, NoiseRadius=60,
         stars=(5.0, 1.0, 1.0, 0.5, 2.0, 1.0, 0.5)),
    dict(Id="m9", DisplayName="M9", Class="Pistol", WpCost=0,
         Description="Pistola padrão. Saque rápido e a melhor mobilidade do arsenal.",
         Damage=25, HeadshotMultiplier=2.4, Rpm=420, Mode="Semi", Magazine=15, Reserve=60,
         ReloadTactical=1.9, ReloadEmpty=2.4, DrawTime=0.3,
         SpreadBase=0.5, SpreadPerShot=0.3, SpreadMax=4.0, SpreadRecovery=9.0,
         MoveSpreadMultiplier=1.5, CrouchSpreadMultiplier=0.8,
         RecoilVertical=1.2, RecoilVerticalLate=1.2, RecoilEarlyShots=1,
         RecoilHorizontal=0.15, RecoilRecovery=9.0,
         Range=100, FalloffStart=18, FalloffEnd=35, FalloffMin=0.6,
         SpeedMultiplier=1.12, NoiseRadius=30,
         stars=(2.0, 1.5, 2.5, 3.0, 5.0, 3.0, 2.0)),
]

STAR_FIELDS = ["StarDamage", "StarPenetration", "StarAccuracy", "StarRate",
               "StarMobility", "StarRecoil", "StarRange"]


def value(v):
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, str):
        return '"' + v.replace('"', '\\"') + '"'
    if isinstance(v, float):
        return repr(v)
    return str(v)


def write(w):
    w = dict(w)
    stars = w.pop("stars")
    w["Class"] = CLASSES[w["Class"]]
    w["Mode"] = MODES[w["Mode"]]
    lines = ['[gd_resource type="Resource" script_class="WeaponDefinition" load_steps=2 format=3]', "",
             '[ext_resource type="Script" path="res://scripts/combat/WeaponDefinition.cs" id="1_def"]', "",
             "[resource]", 'script = ExtResource("1_def")']
    for k, v in w.items():
        lines.append(f"{k} = {value(v)}")
    for k, v in zip(STAR_FIELDS, stars):
        lines.append(f"{k} = {value(float(v))}")
    with open(os.path.join(OUT, f"{w['Id']}.tres"), "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for w in WEAPONS:
        write(w)
        print(f"  {w['Id']:7} {w['DisplayName']:7} {w['WpCost']:4} WP")
