# O servidor de partida: um programa à parte, exportado do mesmo projeto.
#
# Duas etapas (docs/SERVIDOR_DEDICADO.md):
#
#   1. "exporta": o editor do Godot (.NET) importa o projeto e exporta o
#      preset "Servidor (Linux)" — servidor dedicado: cada textura e cada
#      material viram uma casca vazia, o C# sai compilado em Release e o
#      executável é o modelo de exportação (sem editor). Como a arte vira
#      casca, a importação é leve (tools/servidor/preparar_exportacao.py:
#      texturas a 64 px, modelos sem LOD) — a colisão não muda.
#   2. a imagem que roda: só o executável, o .pck e o .NET dele, em cima do
#      runtime-deps (as bibliotecas de sistema do .NET). Sem SDK, sem
#      editor, sem o código-fonte e sem a arte.
#
# Ele se apresenta ao servidor do WolfLeague (ALCATEIA_API), recebe as
# partidas que lhe cabem e abre a porta 7777/UDP para os jogadores. Nasce
# servidor sozinho (a marca dedicated_server; ver scripts/core/Inicio.cs).
#
#   docker build -f servidor/partida.Dockerfile -t wolfleague-partida .

# ---------------------------------------------------------------- 1. exporta
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS exporta
ARG GODOT=4.7.2
ARG TARGETARCH

RUN apt-get update \
	&& apt-get install -y --no-install-recommends unzip ca-certificates curl libfontconfig1 python3 \
	&& rm -rf /var/lib/apt/lists/*

# O editor, da mesma arquitetura da máquina que vai rodar (x86_64 no Fly;
# arm64 no docker compose de um Mac com Apple Silicon).
RUN arch=$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x86_64) \
	&& curl -fsSL -o /tmp/godot.zip \
		"https://github.com/godotengine/godot/releases/download/${GODOT}-stable/Godot_v${GODOT}-stable_mono_linux_${arch}.zip" \
	&& unzip -q /tmp/godot.zip -d /opt \
	&& mv /opt/Godot_v${GODOT}-stable_mono_linux_${arch} /opt/godot \
	&& ln -s /opt/godot/Godot_v${GODOT}-stable_mono_linux.${arch} /usr/local/bin/godot \
	&& rm /tmp/godot.zip

# Os modelos de exportação do Linux, da mesma arquitetura (o pacote
# tem todas as plataformas, ~1,2 GB; fica só o que serve). Esta camada só
# é baixada de novo quando muda a versão do Godot.
RUN arch=$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x86_64) \
	&& destino="/root/.local/share/godot/export_templates/${GODOT}.stable.mono" \
	&& mkdir -p "$destino" \
	&& curl -fsSL -o /tmp/modelos.tpz \
		"https://github.com/godotengine/godot/releases/download/${GODOT}-stable/Godot_v${GODOT}-stable_mono_export_templates.tpz" \
	&& unzip -q -j /tmp/modelos.tpz "templates/linux_release.${arch}" "templates/linux_debug.${arch}" "templates/version.txt" -d "$destino" \
	&& rm /tmp/modelos.tpz

WORKDIR /game
COPY . .

# Prepara a cópia (importação leve, a arquitetura), importa e exporta. Se a
# exportação não deixou o .pck e o .NET, o build para e mostra o porquê.
RUN arch=$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x86_64) \
	&& python3 tools/servidor/preparar_exportacao.py --arquitetura "$arch" \
	&& dotnet build WolfLeague.csproj --nologo -v quiet \
	&& (godot --headless --path . --import > /tmp/importacao.log 2>&1 || true) \
	&& mkdir -p /saida \
	&& (godot --headless --path . --export-release "Servidor (Linux)" /saida/WolfLeagueServer > /tmp/exportacao.log 2>&1 || true) \
	&& if [ ! -f /saida/WolfLeagueServer.pck ] || ! ls -d /saida/data_* > /dev/null 2>&1; then \
		echo "A exportação do servidor falhou:"; \
		grep -E "ERROR|error" /tmp/exportacao.log | head -40; \
		cat /root/.local/share/godot/mono/build_logs/*/msbuild_issues.csv 2>/dev/null | head -20; \
		exit 1; \
	fi \
	&& du -sh /saida/*

# ---------------------------------------------------------------- 2. roda
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0
WORKDIR /game
COPY --from=exporta /saida/ /game/

ENV ALCATEIA_API=http://servidor:8080 \
	ALCATEIA_GAME_PORT=7777
EXPOSE 7777/udp

# O usuário sem privilégio que a imagem do .NET já traz.
USER app
ENTRYPOINT ["/game/WolfLeagueServer", "--headless"]
