#!/usr/bin/env python3
"""
Gera resources/weapons/*.tres a partir da tabela abaixo.

Os números são os de docs/ARSENAL.md, primeira leva de implementação
(M16A2, SG-552, AK-47, UMP-45, M3) mais a M9, que é a secundária do
conjunto padrão, e o AR-15, o fuzil padrão desde 15/11. O dano está como no documento, calibrado para 100 HP;
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
    # 15/11: o fuzil padrão passou a ser o AR-15 (modelo de TastyTony,
    # animações do Jhon no pacote da InfimaGames). Começa com os números
    # da M16A2 de 14/11, que era a padrão; a M16A2 continua no arsenal.
    dict(Id="ar15", DisplayName="AR-15", Class="Rifle", WpCost=0,
         Description="Fuzil padrão. Referência do arsenal: nada excepcional, e esse é o ponto.",
         Damage=27, HeadshotMultiplier=2.5, Rpm=650, Mode="Auto", Magazine=30, Reserve=90,
         ReloadTactical=2.3, ReloadEmpty=2.9, DrawTime=0.5,
         # O guardar do Jhon, no tempo dele (15/11): 15 quadros a 30 por
         # segundo. Igual ao "guardar" do ar15 em assets/armas/fp/bracos.json.
         HolsterTime=0.5,
         SpreadBase=0.15, SpreadPerShot=0.09, SpreadMax=1.8, SpreadRecovery=12.0, SpreadDelay=0.05,
         MoveSpreadMultiplier=1.6, CrouchSpreadMultiplier=0.7,
         RecoilVertical=1.0, RecoilVerticalLate=0.55, RecoilEarlyShots=4,
         RecoilHorizontal=0.18, RecoilRecovery=7.0,
         Range=150, FalloffStart=35, FalloffEnd=60, FalloffMin=0.7,
         SpeedMultiplier=1.0, NoiseRadius=45, AimFov=55, AimSpreadMultiplier=0.2, AimSpeedMultiplier=0.7,
         stars=(2.0, 2.5, 3.5, 3.0, 2.5, 2.5, 3.0)),
    # As outras armas de fogo saíram em 19/11: só a pistola é gerada aqui
    # (a família AR-15 tem os .tres escritos à mão).
    dict(Id="m9", DisplayName="M9", Class="Pistol", WpCost=0,
         Description="Pistola padrão. Saque rápido e a melhor mobilidade do arsenal.",
         Damage=25, HeadshotMultiplier=2.4, Rpm=420, Mode="Semi", Magazine=15, Reserve=60,
         ReloadTactical=1.9, ReloadEmpty=2.4, DrawTime=0.3,
         SpreadBase=0.5, SpreadPerShot=0.3, SpreadMax=4.0, SpreadRecovery=9.0,
         MoveSpreadMultiplier=1.5, CrouchSpreadMultiplier=0.8,
         RecoilVertical=1.2, RecoilVerticalLate=1.2, RecoilEarlyShots=1,
         RecoilHorizontal=0.15, RecoilRecovery=9.0,
         Range=100, FalloffStart=18, FalloffEnd=35, FalloffMin=0.6,
         SpeedMultiplier=1.12, NoiseRadius=30, AimFov=65, AimSpreadMultiplier=0.6, AimSpeedMultiplier=0.7,
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
