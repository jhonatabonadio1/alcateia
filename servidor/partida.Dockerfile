# O servidor de partida: o próprio jogo, sem tela, esperando partidas.
#
# É o Godot (.NET) rodando o projeto com "-- --servidor". Ele se
# apresenta ao servidor do Alcateia (ALCATEIA_API), recebe as partidas
# que lhe cabem e abre a porta 7777/UDP para os jogadores.
#
#   docker build -f servidor/partida.Dockerfile -t alcateia-partida .

FROM mcr.microsoft.com/dotnet/sdk:8.0
ARG GODOT=4.7.2
ARG TARGETARCH

RUN apt-get update \
	&& apt-get install -y --no-install-recommends unzip ca-certificates curl libfontconfig1 \
	&& rm -rf /var/lib/apt/lists/*

# O Godot .NET do mesmo número do projeto, para a arquitetura da máquina.
RUN arch=$([ "$TARGETARCH" = "arm64" ] && echo arm64 || echo x86_64) \
	&& curl -fsSL -o /tmp/godot.zip \
		"https://github.com/godotengine/godot/releases/download/${GODOT}-stable/Godot_v${GODOT}-stable_mono_linux_${arch}.zip" \
	&& unzip -q /tmp/godot.zip -d /opt \
	&& mv /opt/Godot_v${GODOT}-stable_mono_linux_${arch} /opt/godot \
	&& ln -s /opt/godot/Godot_v${GODOT}-stable_mono_linux.${arch} /usr/local/bin/godot \
	&& rm /tmp/godot.zip

WORKDIR /jogo
COPY . .

# Compila o C# do jogo e deixa o Godot importar os recursos uma vez,
# para o contêiner subir rápido.
RUN dotnet build alcateia.csproj -c Debug --nologo -v quiet \
	&& (godot --headless --path . --import > /dev/null 2>&1 || true)

ENV ALCATEIA_API=http://servidor:8080 \
	ALCATEIA_GAME_PORT=7777
EXPOSE 7777/udp

ENTRYPOINT ["godot", "--headless", "--path", ".", "--", "--servidor"]
