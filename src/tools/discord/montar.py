#!/usr/bin/env python3
"""Monta o servidor do Discord do WolfLeague a partir de servidor.json.

O servidor vazio você cria pela sua conta (o "+" na lista de servidores do
Discord): bot comum não cria servidor. O resto — cargos, categorias, canais,
permissões, ícone, as mensagens de regras e de boas-vindas e o convite —
sai daqui, por um bot.

Uma vez só:
  1. https://discord.com/developers/applications → New Application
     ("WolfLeague"). Em "Bot": Reset Token e copie o token.
  2. Convide o bot para o servidor (APP_ID é o Application ID, em
     "General Information"):
       https://discord.com/oauth2/authorize?client_id=APP_ID&scope=bot&permissions=8
     8 = administrador, só para montar: depois expulse o bot, ou tire dele o
     administrador, se não for usar para mais nada.
  3. No Discord: Configurações → Avançado → Modo de desenvolvedor. Botão
     direito no servidor → Copiar ID do servidor.

Rodar (só biblioteca padrão do Python):
  export DISCORD_TOKEN='...'     # o token do bot; nunca no git nem no chat
  python3 tools/discord/montar.py --servidor 123456789012345678 --seco   # só mostra o que faria
  python3 tools/discord/montar.py --servidor 123456789012345678

Pode rodar de novo quando mudar o servidor.json: o que já existe (pelo nome)
é ajustado, não duplicado, e mensagem só é postada em canal vazio.

--limpar apaga os canais e categorias que não estão no servidor.json (num
servidor novo: "Canais de texto", "Canais de voz" e o canal de voz "Geral"
que o Discord cria). Apagar canal não tem volta: rode antes com --seco.

Nas mensagens, {#canal} vira o link do canal e {download} (e qualquer chave
de "links") vira o link do servidor.json.
"""
import argparse
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = os.environ.get('DISCORD_API', 'https://discord.com/api/v10').rstrip('/')

# Os bits de permissão do Discord (docs: Permissions → Bitwise Permission Flags).
PERMISSOES = {
	'CREATE_INSTANT_INVITE': 0, 'KICK_MEMBERS': 1, 'BAN_MEMBERS': 2, 'ADMINISTRATOR': 3,
	'MANAGE_CHANNELS': 4, 'MANAGE_GUILD': 5, 'ADD_REACTIONS': 6, 'VIEW_AUDIT_LOG': 7,
	'PRIORITY_SPEAKER': 8, 'STREAM': 9, 'VIEW_CHANNEL': 10, 'SEND_MESSAGES': 11,
	'SEND_TTS_MESSAGES': 12, 'MANAGE_MESSAGES': 13, 'EMBED_LINKS': 14, 'ATTACH_FILES': 15,
	'READ_MESSAGE_HISTORY': 16, 'MENTION_EVERYONE': 17, 'USE_EXTERNAL_EMOJIS': 18,
	'CONNECT': 20, 'SPEAK': 21, 'MUTE_MEMBERS': 22, 'DEAFEN_MEMBERS': 23, 'MOVE_MEMBERS': 24,
	'USE_VAD': 25, 'CHANGE_NICKNAME': 26, 'MANAGE_NICKNAMES': 27, 'MANAGE_ROLES': 28,
	'MANAGE_WEBHOOKS': 29, 'MANAGE_GUILD_EXPRESSIONS': 30, 'USE_APPLICATION_COMMANDS': 31,
	'MANAGE_EVENTS': 33, 'MANAGE_THREADS': 34, 'CREATE_PUBLIC_THREADS': 35,
	'CREATE_PRIVATE_THREADS': 36, 'SEND_MESSAGES_IN_THREADS': 38, 'MODERATE_MEMBERS': 40,
}

# Quem lê um canal "só leitura" não escreve nem abre conversa nele.
ESCREVER = ('SEND_MESSAGES', 'CREATE_PUBLIC_THREADS', 'CREATE_PRIVATE_THREADS', 'SEND_MESSAGES_IN_THREADS')

TIPOS = {'texto': 0, 'voz': 2, 'categoria': 4}


def bits(nomes):
	total = 0
	for n in nomes:
		if n not in PERMISSOES:
			sys.exit(f'permissão desconhecida no servidor.json: {n} (veja PERMISSOES em montar.py)')
		total |= 1 << PERMISSOES[n]
	return total


def igual(a, b):
	return (a or '').casefold() == (b or '').casefold()


class Discord:
	"""O REST do Discord com o token do bot. Espera o 429 (limite de
	pedidos) o tempo que o Discord mandar e repete erro passageiro."""

	def __init__(self, token, seco):
		self.token = token
		self.seco = seco
		self._falsos = 0

	def pedir(self, metodo, caminho, corpo=None):
		if self.seco and metodo != 'GET':
			self._falsos += 1
			print(f'   (seco) {metodo} {caminho}')
			return {'id': f'seco{self._falsos}', 'code': 'seco'}
		dados = json.dumps(corpo).encode() if corpo is not None else None
		passageiro = 0
		while True:
			req = urllib.request.Request(API + caminho, data=dados, method=metodo)
			req.add_header('Authorization', 'Bot ' + self.token)
			req.add_header('User-Agent', 'DiscordBot (https://github.com/jhonatabonadio1/wolfleague, 1.0)')
			req.add_header('Content-Type', 'application/json')
			try:
				with urllib.request.urlopen(req, timeout=30) as r:
					texto = r.read().decode()
					return json.loads(texto) if texto.strip() else None
			except urllib.error.HTTPError as e:
				texto = e.read().decode(errors='replace')
				if e.code == 429:
					try:
						espera = float(json.loads(texto).get('retry_after', 1))
					except ValueError:
						espera = 1.0
					time.sleep(espera + 0.1)
					continue
				if e.code in (500, 502, 503, 504) and passageiro < 4:
					passageiro += 1
					time.sleep(2 * passageiro)
					continue
				dica = {
					401: ' — o token está errado (DISCORD_TOKEN é o token do Bot, não o Client Secret)',
					403: ' — o bot não está no servidor ou não tem a permissão (convide com permissions=8)',
					404: ' — não achei: confira o ID do servidor (Copiar ID do servidor, com o modo de desenvolvedor)',
				}.get(e.code, '')
				sys.exit(f'{metodo} {caminho}: {e.code} {texto[:300]}{dica}')
			except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
				if passageiro < 4:
					passageiro += 1
					time.sleep(2 * passageiro)
					continue
				sys.exit(f'{metodo} {caminho}: sem resposta do Discord ({e})')


def sobrescritas(guild, cargos, regra):
	"""As permissões de um canal ou categoria (privado_para, so_leitura,
	escrevem) em sobrescritas do Discord. @everyone é o id do servidor."""
	por_cargo = {}

	def mexe(alvo, permitir=(), negar=()):
		a, n = por_cargo.get(alvo, (0, 0))
		por_cargo[alvo] = (a | bits(permitir), n | bits(negar))

	def cargo(nome):
		if nome not in cargos:
			sys.exit(f'cargo "{nome}" usado num canal mas não está em "cargos"')
		return cargos[nome]

	privado = regra.get('privado_para')
	if privado:
		mexe(guild, negar=['VIEW_CHANNEL'])
		for nome in privado:
			mexe(cargo(nome), permitir=['VIEW_CHANNEL'])
	if regra.get('so_leitura'):
		mexe(guild, negar=ESCREVER)
		for nome in regra.get('escrevem', []):
			mexe(cargo(nome), permitir=ESCREVER)
	return [{'id': alvo, 'type': 0, 'allow': str(a), 'deny': str(n)} for alvo, (a, n) in por_cargo.items()]


def montar(a):
	token = os.environ.get('DISCORD_TOKEN', '').strip()
	if not token:
		sys.exit('falta DISCORD_TOKEN (o token do bot, em discord.com/developers → Bot)')
	plano_path = os.path.abspath(a.plano)
	with open(plano_path, encoding='utf-8') as f:
		plano = json.load(f)
	pasta = os.path.dirname(plano_path)
	d = Discord(token, a.seco)
	g = a.servidor

	eu = d.pedir('GET', '/users/@me')
	servidor = d.pedir('GET', f'/guilds/{g}')
	print(f"bot {eu.get('username')} no servidor \"{servidor.get('name')}\"" + (' (seco: nada muda)' if a.seco else ''))

	# Cargos, de cima para baixo: cada cargo novo nasce logo acima do
	# @everyone, então criando nesta ordem o primeiro fica em cima.
	print('== cargos')
	existentes = d.pedir('GET', f'/guilds/{g}/roles')
	cargos = {'@everyone': g}
	for c in plano.get('cargos', []):
		corpo = {
			'name': c['nome'],
			'permissions': str(bits(c.get('permissoes', []))),
			'color': int(c.get('cor', '0'), 16),
			'hoist': bool(c.get('separado', False)),
			'mentionable': bool(c.get('mencionavel', False)),
		}
		achado = next((r for r in existentes if igual(r['name'], c['nome'])), None)
		if achado:
			d.pedir('PATCH', f"/guilds/{g}/roles/{achado['id']}", corpo)
			cargos[c['nome']] = achado['id']
			print(f"   {c['nome']}: ajustado")
		else:
			novo = d.pedir('POST', f'/guilds/{g}/roles', corpo)
			cargos[c['nome']] = novo['id']
			print(f"   {c['nome']}: criado")

	# Categorias e canais. O canal herda as regras da categoria e soma as dele.
	print('== canais')
	canais = d.pedir('GET', f'/guilds/{g}/channels')
	ids = {}
	cat_ids = []
	mensagens = []
	for pos_cat, cat in enumerate(plano.get('categorias', [])):
		corpo = {'name': cat['nome'], 'type': 4, 'position': pos_cat,
			'permission_overwrites': sobrescritas(g, cargos, cat)}
		achada = next((c for c in canais if c['type'] == 4 and igual(c['name'], cat['nome'])), None)
		if achada:
			d.pedir('PATCH', f"/channels/{achada['id']}", corpo)
			cat_id = achada['id']
			print(f"   [{cat['nome']}] ajustada")
		else:
			cat_id = d.pedir('POST', f'/guilds/{g}/channels', corpo)['id']
			print(f"   [{cat['nome']}] criada")
		cat_ids.append(cat_id)

		for pos, canal in enumerate(cat.get('canais', [])):
			tipo = TIPOS[canal.get('tipo', 'texto')]
			regra = {
				'privado_para': canal.get('privado_para', cat.get('privado_para')),
				'so_leitura': canal.get('so_leitura', cat.get('so_leitura', False)),
				'escrevem': canal.get('escrevem', cat.get('escrevem', [])),
			}
			corpo = {'name': canal['nome'], 'type': tipo, 'parent_id': cat_id, 'position': pos,
				'permission_overwrites': sobrescritas(g, cargos, regra)}
			if tipo == 0 and canal.get('assunto'):
				corpo['topic'] = canal['assunto']
			if tipo == 2 and canal.get('limite'):
				corpo['user_limit'] = int(canal['limite'])
			achado = next((c for c in canais if c['type'] == tipo and igual(c['name'], canal['nome'])), None)
			if achado:
				d.pedir('PATCH', f"/channels/{achado['id']}", corpo)
				canal_id = achado['id']
				print(f"      #{canal['nome']} ajustado")
			else:
				canal_id = d.pedir('POST', f'/guilds/{g}/channels', corpo)['id']
				achado = None
				print(f"      #{canal['nome']} criado")
			ids[canal['nome']] = canal_id
			if canal.get('mensagem'):
				mensagens.append((canal['nome'], canal_id, achado is None, canal['mensagem']))

	# O servidor: ícone, canal das mensagens do sistema, notificações.
	print('== servidor')
	corpo = {}
	if plano.get('icone'):
		caminho = os.path.join(pasta, plano['icone'])
		with open(caminho, 'rb') as f:
			corpo['icon'] = 'data:image/png;base64,' + base64.b64encode(f.read()).decode()
	if plano.get('canal_do_sistema') in ids:
		corpo['system_channel_id'] = ids[plano['canal_do_sistema']]
	if plano.get('notificacoes_so_mencoes'):
		corpo['default_message_notifications'] = 1
	if 'verificacao' in plano:
		corpo['verification_level'] = int(plano['verificacao'])
	if plano.get('nome'):
		corpo['name'] = plano['nome']
	if corpo:
		d.pedir('PATCH', f'/guilds/{g}', corpo)
		print('   ' + ', '.join(k for k in corpo))

	# Mensagens fixas: só em canal vazio, para rodar de novo sem repetir.
	print('== mensagens')
	links = plano.get('links', {})

	def preencher(texto):
		for nome, cid in ids.items():
			texto = texto.replace('{#' + nome + '}', f'<#{cid}>')
		for k, v in links.items():
			texto = texto.replace('{' + k + '}', v)
		return texto

	for nome, cid, novo, partes in mensagens:
		if not novo and not a.seco:
			if d.pedir('GET', f'/channels/{cid}/messages?limit=1'):
				print(f'   #{nome}: já tem mensagem, deixei como está')
				continue
		for parte in partes if isinstance(partes, list) else [partes]:
			texto = preencher(parte)
			if len(texto) > 2000:
				sys.exit(f'#{nome}: uma mensagem passou de 2000 caracteres ({len(texto)}); divida em partes')
			d.pedir('POST', f'/channels/{cid}/messages', {'content': texto, 'allowed_mentions': {'parse': []}})
		print(f'   #{nome}: postada')

	if a.limpar:
		print('== limpar (fora do servidor.json)')
		guardados = set(ids.values()) | set(cat_ids)
		for c in d.pedir('GET', f'/guilds/{g}/channels') if not a.seco else canais:
			if c['id'] in guardados or c['id'] == corpo.get('system_channel_id'):
				continue
			d.pedir('DELETE', f"/channels/{c['id']}")
			print(f"   apagado: {'[' + c['name'] + ']' if c['type'] == 4 else '#' + c['name']}")

	if plano.get('convite') in ids:
		convite = d.pedir('POST', f"/channels/{ids[plano['convite']]}/invites", {'max_age': 0, 'max_uses': 0, 'unique': False})
		print(f"== convite permanente: https://discord.gg/{convite.get('code')}")


def main():
	ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
	ap.add_argument('--servidor', required=True, help='o ID do servidor (Copiar ID do servidor)')
	ap.add_argument('--plano', default=os.path.join(os.path.dirname(os.path.abspath(__file__)), 'servidor.json'))
	ap.add_argument('--seco', action='store_true', help='só mostra o que faria; não muda nada')
	ap.add_argument('--limpar', action='store_true', help='apaga canais e categorias que não estão no servidor.json')
	a = ap.parse_args()
	if not a.servidor.isdigit():
		sys.exit(f'--servidor precisa do número do servidor (ex. 1301234567890123456), não "{a.servidor}". '
			'No Discord: Configurações → Avançado → Modo de desenvolvedor; depois botão direito no '
			'ícone do servidor → Copiar ID do servidor.')
	sys.stdout.reconfigure(line_buffering=True)
	montar(a)


if __name__ == '__main__':
	main()
