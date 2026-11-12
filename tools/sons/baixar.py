"""Baixa as gravações de origem (só do GitHub). Uso: python3 baixar.py <pasta>"""
import os, sys, urllib.parse, subprocess, concurrent.futures as cf
RAW = sys.argv[1]
os.makedirs(RAW, exist_ok=True)
META = "facebookincubator/Meta-open-sound", "corpora/oculus-audio-pack/ogg/"
FIRE = "buddingmonkey/FreeFirearmsSFXLibrary"
K = {
 "ui": ("Calinou/kenney-ui-audio", "addons/kenney_ui_audio/"),
 "if": ("Calinou/kenney-interface-sounds", "addons/kenney_interface_sounds/"),
 "rpg": ("Boyquotes/kenney-rpg-audio-for-godot", "addons/kenney rpg audio/"),
 "imp": ("Boyquotes/kenney-impact-sounds-for-godot", "addons/kenney impact sounds/"),
 "sci": ("Boyquotes/kenney-sci-fi-sounds-for-godot", "addons/kenney sci-fi sounds/"),
}
items = []
meta = """AMBForst_Night_LOOP_01 CLOTHMvmt_MovementWithGear_OS_01 CLOTHMvmt_MovementWithGear_OS_02 CLOTHMvmt_MovementWithGear_OS_03 CLOTHMvmt_MovementWithGear_OS_04
CREAHmn_OgreDeath_OS_01 CREAHmn_OgreDeath_OS_02 CREAHmn_OgreDeath_OS_03 CREAHmn_OgreEffort_OS_01 CREAHmn_OgreHurt_OS_01 CREAHmn_OgreHurt_OS_02 CREAHmn_OgreHurt_OS_03
CREAHmn_TrollDeath_OS_01 CREAHmn_TrollDeath_OS_02 CREAHmn_TrollEffort_OS_01 CREAHmn_TrollHurt_OS_01 CREAHmn_TrollHurt_OS_02 CREAHmn_TrollHurt_OS_03
CREAMnstr_CreatureCreepy_OS_01 CREAMnstr_CreatureCreepy_OS_02 CREAMnstr_CreatureCreepy_OS_03
FEETHmn_ShoeConcreteLand_OS_01 FEETHmn_ShoeConcreteLand_OS_02 FEETHmn_ShoeConcreteRun_OS_01 FEETHmn_ShoeConcreteRun_OS_02 FEETHmn_ShoeConcreteRun_OS_03 FEETHmn_ShoeConcreteRun_OS_04 FEETHmn_ShoeConcreteRun_OS_05
FEETHmn_ShoeDirtLand_OS_01 FEETHmn_ShoeDirtLand_OS_02 FEETHmn_ShoeDirtRun_OS_01 FEETHmn_ShoeDirtRun_OS_02 FEETHmn_ShoeDirtRun_OS_03 FEETHmn_ShoeDirtRun_OS_04 FEETHmn_ShoeDirtRun_OS_05
FGHTImpt_PunchHarder_OS_01 FGHTImpt_PunchHarder_OS_02 FGHTImpt_PunchHarder_OS_03 FGHTImpt_PunchHarder_OS_04
GOREBone_BonesBreaking_OS_01 GOREBone_BonesBreaking_OS_02 GOREBone_BonesBreaking_OS_03 HMNBrth_MaleBreathing_OS_01
MUSCStngr_LossMallet_OS_01 MUSCStngr_LossOrchestral_OS_01 MUSCStngr_MarchingSnare_OS_01 MUSCStngr_VictoryOrchestral_OS_01
SWSH_Swoosh_OS_01 SWSH_Swoosh_OS_02 SWSH_Swoosh_OS_03 SWSH_Swoosh_OS_04 SWSH_Swoosh_OS_05 SWSH_Swoosh_OS_06 UIMisc_BelltreeUp_OS_01
VOXMale_Male01Death_OS_01 VOXMale_Male01Death_OS_02 VOXMale_Male01Death_OS_03 VOXMale_Male01Hurt_OS_01 VOXMale_Male01Hurt_OS_02 VOXMale_Male01Hurt_OS_03
VOXMale_Male02Death_OS_01 VOXMale_Male02Death_OS_02 VOXMale_Male02Death_OS_03 VOXMale_Male02Hurt_OS_01 VOXMale_Male02Hurt_OS_02 VOXMale_Male02Hurt_OS_03
WHSH_WhooshMedium_OS_01 WHSH_WhooshMedium_OS_02 WHSH_WhooshMedium_OS_03 WHSH_WhooshSmall_OS_01 WHSH_WhooshSmall_OS_02 WHSH_WhooshSmall_OS_03""".split()
for m in meta: items.append((META[0], META[1] + m + ".ogg", "oc_" + m + ".ogg"))
fire_prep = {"AR-15": ["D_32P", "D_24P"], "SKS": ["U_14P", "U_19P"], "AK-47": ["C_28P", "C_31P"],
             "Carl Gustav M45": ["G_31P", "G_20P"], "Nova": ["O_21P", "O_17P"], "Walther PPQ": ["X_39P", "X_31P"]}
for d, fs in fire_prep.items():
    for f in fs: items.append((FIRE, f"Prepared SFX/{d}/{f}.wav", f"ff_{f}.wav"))
fire_master = {"AR-15": ["D_5", "D_4", "D_10", "D_13"], "AK-47": ["C_10", "C_13"], "Carl Gustav M45_Swedish K": ["G_8", "G_10"],
               "Walther PPQ": ["X_5", "X_4", "X_17"], "Nova": ["O_6", "O_3"]}
for d, fs in fire_master.items():
    for f in fs: items.append((FIRE, f"Master Tracks/{d}/{f}.wav", f"ff_{f}.wav"))
ken = {"ui": "rollover1.wav rollover2.wav rollover3.wav click1.wav click2.wav click3.wav",
       "if": "error_004.wav tick_001.wav tick_002.wav tick_004.wav confirmation_002.wav bong_001.wav",
       "rpg": "cloth_1.ogg cloth_belt.ogg cloth_belt_2.ogg metal_click.ogg metal_latch.ogg knife_slice.ogg knife_slice_2.ogg",
       "imp": "impact_generic_light_000.ogg impact_generic_light_001.ogg impact_generic_light_002.ogg impact_metal_light_000.ogg impact_metal_light_001.ogg impact_metal_light_002.ogg impact_soft_heavy_000.ogg impact_soft_heavy_001.ogg impact_soft_heavy_002.ogg",
       "sci": "explosion_crunch_000.ogg explosion_crunch_001.ogg explosion_crunch_002.ogg explosion_crunch_003.ogg explosion_crunch_004.ogg low_frequency_explosion_000.ogg low_frequency_explosion_001.ogg"}
for k, names in ken.items():
    repo, base = K[k]
    for n in names.split(): items.append((repo, base + n, f"kn_{n}"))

def get(it):
    repo, path, out = it
    dst = os.path.join(RAW, out)
    if os.path.exists(dst) and os.path.getsize(dst) > 0: return out, "cache"
    url = f"https://raw.githubusercontent.com/{repo}/HEAD/" + urllib.parse.quote(path)
    r = subprocess.run(["curl", "-sfL", "-m", "180", "-o", dst, url])
    return out, "ok" if r.returncode == 0 else f"ERRO {r.returncode} {url}"
with cf.ThreadPoolExecutor(8) as ex:
    res = list(ex.map(get, items))
bad = [r for r in res if not r[1] in ("ok", "cache")]
print(len(items), "itens,", len(bad), "falhas")
for b in bad: print(b)
