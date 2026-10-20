# 002 — Medidas do executável único do núcleo

> Histórico. 2026-10-19.

Registro do tamanho e do tempo de subida do executável único do núcleo, medidos em 2026-10-19 e
2026-10-20 no pull request que o introduziu, para que crescimento futuro tenha com o que se
comparar. É o que valeu naquela máquina, naqueles dias; não é regra nem previsão. Só o núcleo: o
serviço de linguagem não entra no artefato, e como ele acompanha a distribuição é a decisão 0041,
ainda não tomada.

## Condições

| Item | Valor |
|---|---|
| Sistema | Ubuntu 24.04.5 LTS, x86_64; 4 núcleos; 16 GiB de memória; SDK .NET 10.0.401 |
| Árvore medida | a deste pull request, limpa, antes deste documento |
| Carga média de 1 minuto | 27,44 no início e 28,52 no fim da medida de subida, com 4 núcleos |
| Plataforma de execução no ambiente do processo | nenhuma: `PATH` só com `/usr/bin` e `/bin`, sem `DOTNET_ROOT` |

**A máquina estava ocupada por outros processos.** O tempo de subida é limite superior do que ela
faria livre.

## Tamanho

Em bytes. Comprimido é `gzip -6 -n -c` (gzip 1.12) do executável, perto do que o artefato do
workflow guarda; varia alguns kB com o commit que o executável grava.

| Alvo | Executável | Comprimido | Ao lado dele |
|---|---|---|---|
| `linux-x64` | 101 147 058 | 44 497 455 | `core/src/Core.Host/appsettings.json`, 56 |
| `win-x64` | 104 238 896 | 44 936 517 | `core/src/Core.Host/appsettings.json`, 56 |

## Tempo de subida

Do início do processo até /status responder `ready: true`, com a configuração mínima: o endereço
e o interpretador do serviço de linguagem, que o núcleo inicia e espera responder antes de abrir a
porta. O tempo inclui, portanto, a subida do serviço. Sete processos novos, em sequência, só
`linux-x64`: mediana 1,71 s; menor 1,35 s; maior 2,42 s. Na véspera, com carga entre 13 e 19, a
mesma medida deu mediana 1,59 s. O tempo de
`win-x64` não foi medido: a máquina não roda Windows, e a medida fica para o operador.

## O que o processo carregou do sistema

Lido do mapa de memória do processo de `linux-x64` com /status apto: `libc`, `libm`, `libdl`,
`librt`, `libpthread`, `libgcc_s`, `libstdc++` e a OpenSSL 3 (`libssl` e `libcrypto`). Nenhuma
biblioteca da plataforma de execução veio de fora do executável.

## Comandos

A publicação é a do `.github/workflows/package.yml`, executada a partir da raiz:

```
for target in linux-x64 win-x64; do
  dotnet restore core/src/Core.Host/Core.Host.csproj --locked-mode -p:RuntimeIdentifier=$target
  dotnet publish core/src/Core.Host/Core.Host.csproj --no-restore --configuration Release \
    -p:ContinuousIntegrationBuild=true --runtime $target --output publish/$target
done
```

A subida mediu-se com o executável `linux-x64` que esse laço publica, iniciado com `--Host:Urls`
numa porta livre e `--LanguageService:Executable` apontando o interpretador do ambiente de
`language-service/`, consultando /status a cada 20 ms e encerrando o processo com `SIGTERM`.
