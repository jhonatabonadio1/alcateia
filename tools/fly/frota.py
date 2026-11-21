#!/usr/bin/env python3
"""A frota de servidores de partida no Fly.io (veja docs/FLY.md).

Cada servidor de partida é uma máquina do app de partidas, com a SUA porta
UDP (7777, 7778, ...): o Fly manda o pacote da porta 7778 só para a máquina
que declarou a 7778. Por isso as máquinas não sobem com "fly deploy" (que
daria a mesma porta a todas) e sim por aqui, pela API de máquinas.

As máquinas nascem paradas. O servidor do jogo liga uma quando falta lugar
(ALCATEIA_FLY_APP + ALCATEIA_FLY_TOKEN), e ela desliga sozinha quando fica
ociosa (ALCATEIA_IDLE_EXIT). Parada, a máquina só cobra o disco.

    python3 tools/fly/frota.py estado
    python3 tools/fly/frota.py criar 4 --imagem registry.fly.io/wolfleague-partida:v0.3.0 \\
        --api http://wolfleague-api.internal:8080 --ip 137.66.x.x
    python3 tools/fly/frota.py atualizar --imagem registry.fly.io/wolfleague-partida:v0.4.0 --esperar 1200
    python3 tools/fly/frota.py ligar

Precisa de FLY_API_TOKEN (fly tokens create deploy -a <app>). O app vem de
--app ou de FLY_APP_PARTIDA (padrão: wolfleague-partida). Só biblioteca padrão.
"""
import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = os.environ.get('FLY_API_HOSTNAME', 'https://api.machines.dev').rstrip('/')
PARADAS = ('stopped', 'suspended', 'created')


def pedir(metodo, caminho, corpo=None):
	token = os.environ.get('FLY_API_TOKEN', '').strip()
	if not token:
		sys.exit('falta FLY_API_TOKEN')
	dados = json.dumps(corpo).encode() if corpo is not None else None
	req = urllib.request.Request(API + caminho, data=dados, method=metodo)
	req.add_header('Authorization', token if token.startswith('FlyV1 ') else 'Bearer ' + token)
	req.add_header('Content-Type', 'application/json')
	try:
		with urllib.request.urlopen(req, timeout=60) as r:
			texto = r.read().decode()
			return json.loads(texto) if texto.strip() else None
	except urllib.error.HTTPError as e:
		sys.exit(f'{metodo} {caminho}: {e.code} {e.read().decode()[:400]}')


def maquinas(app):
	return pedir('GET', f'/v1/apps/{app}/machines') or []


def porta(m):
	for s in m.get('config', {}).get('services') or []:
		if s.get('protocol') == 'udp':
			return s.get('internal_port')
	return None


def estado(a):
	ms = sorted(maquinas(a.app), key=lambda m: porta(m) or 0)
	if not ms:
		print(f'{a.app}: nenhuma máquina. Crie com: frota.py criar N ...')
	for m in ms:
		img = m.get('config', {}).get('image', '?').split(':')[-1]
		print(f"{m['id']}  {m.get('region', '?'):4}  udp {porta(m)}  {m.get('state', '?'):10}  {img}")


def configuracao(a, p, env_base):
	env = dict(env_base)
	env.update({
		'ALCATEIA_GAME_PORT': str(p),
		'ALCATEIA_BIND': 'fly-global-services',
		'ALCATEIA_IDLE_EXIT': str(a.ocioso),
		'ALCATEIA_MATCHES': str(a.partidas),
	})
	if a.api:
		env['ALCATEIA_API'] = a.api
	if a.ip:
		env['ALCATEIA_PUBLIC_ADDRESS'] = a.ip
	for par in a.env or []:
		k, _, v = par.partition('=')
		env[k] = v
	return {
		'image': a.imagem,
		'env': env,
		'guest': {'cpu_kind': 'shared', 'cpus': a.cpus, 'memory_mb': a.memoria},
		# Saiu com 0 (ociosa) fica parada; caiu, sobe de novo.
		'restart': {'policy': 'on-failure', 'max_retries': 3},
		'services': [{'protocol': 'udp', 'internal_port': p, 'ports': [{'port': p}]}],
		'metadata': {'papel': 'partida'},
	}


def criar(a):
	if not a.imagem or not a.api or not a.ip:
		sys.exit('criar precisa de --imagem, --api e --ip (o IPv4 dedicado do app)')
	usadas = {porta(m) for m in maquinas(a.app)}
	p = a.porta
	for _ in range(a.quantas):
		while p in usadas:
			p += 1
		usadas.add(p)
		m = pedir('POST', f'/v1/apps/{a.app}/machines', {
			'name': f'partida-{p}', 'region': a.regiao, 'skip_launch': True,
			'config': configuracao(a, p, {}),
		})
		print(f"criada {m['id']} na porta {p}/udp (parada)")


def atualizar(a):
	"""Troca a imagem (e o --env) de todas. A parada troca na hora e continua
	parada; a ligada pode estar no meio de uma partida, então espera ela
	desligar sozinha (até --esperar segundos) e troca depois."""
	if not a.imagem:
		sys.exit('atualizar precisa de --imagem')
	prazo = time.time() + a.esperar
	feitas = set()
	while True:
		faltam = []
		for m in maquinas(a.app):
			cfg = m.get('config', {})
			if m['id'] in feitas or (cfg.get('image') == a.imagem and not a.env):
				continue
			if m.get('state') in PARADAS:
				cfg['image'] = a.imagem
				for par in a.env or []:
					k, _, v = par.partition('=')
					cfg.setdefault('env', {})[k] = v
				pedir('POST', f"/v1/apps/{a.app}/machines/{m['id']}", {'config': cfg, 'skip_launch': True})
				feitas.add(m['id'])
				print(f"{m['id']} (udp {porta(m)}): atualizada, continua parada")
			elif m.get('state') != 'destroyed':
				faltam.append(m)
		if not faltam:
			print('frota toda na imagem nova')
			return
		if time.time() >= prazo:
			nomes = ', '.join(f"{m['id']} (udp {porta(m)})" for m in faltam)
			# Anotação do GitHub Actions: aparece no resumo do lançamento.
			print(f'::warning::ficaram na imagem antiga, ligadas com partida: {nomes}. '
				'Rode "frota.py atualizar" de novo quando desligarem.')
			return
		print(f'{len(faltam)} ligada(s), esperando desligar...')
		time.sleep(20)


def ligar(a):
	for m in maquinas(a.app):
		if m.get('state') in ('stopped', 'suspended'):
			pedir('POST', f"/v1/apps/{a.app}/machines/{m['id']}/start")
			print(f"liguei {m['id']} (udp {porta(m)})")
			return
	print('nenhuma parada para ligar')


def main():
	ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
	ap.add_argument('--app', default=os.environ.get('FLY_APP_PARTIDA', 'wolfleague-partida'))
	sub = ap.add_subparsers(dest='cmd', required=True)
	sub.add_parser('estado').set_defaults(f=estado)
	sub.add_parser('ligar').set_defaults(f=ligar)

	c = sub.add_parser('criar')
	c.add_argument('quantas', type=int)
	c.add_argument('--imagem', required=True)
	c.add_argument('--api', help='onde o servidor do jogo atende pela rede interna, ex. http://wolfleague-api.internal:8080')
	c.add_argument('--ip', help='o IPv4 dedicado do app de partidas (fly ips list)')
	c.add_argument('--porta', type=int, default=7777, help='a primeira porta UDP')
	c.add_argument('--regiao', default='gru')
	c.add_argument('--partidas', type=int, default=4, help='partidas ao mesmo tempo por máquina')
	c.add_argument('--ocioso', type=int, default=600, help='segundos ociosa antes de desligar')
	c.add_argument('--memoria', type=int, default=1024)
	c.add_argument('--cpus', type=int, default=1)
	c.add_argument('--env', action='append', help='K=V a mais (repete)')
	c.set_defaults(f=criar)

	u = sub.add_parser('atualizar')
	u.add_argument('--imagem', required=True)
	u.add_argument('--esperar', type=int, default=0, help='segundos esperando as ligadas desligarem')
	u.add_argument('--env', action='append', help='K=V a trocar (repete)')
	u.set_defaults(f=atualizar)

	a = ap.parse_args()
	a.f(a)


if __name__ == '__main__':
	main()
