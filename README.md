# Conversational Dataset Generator

Ferramenta para construir **bases conversacionais anotadas em português brasileiro**: ingestão de
formatos variados, sanitização, pseudonimização que preserva contexto, anotação humana assistida,
validação por métricas de concordância e exportação de datasets rastreáveis.

O domínio **forense/criminal é o estudo de caso mais restritivo**, não o escopo da ferramenta.

Projeto de pesquisa aplicada em processamento de linguagem natural para o português
brasileiro.

## Estado

Fase 1 do [roadmap](docs/roadmap/001-fundacao-verificavel.md), a fundação verificável: o núcleo
(.NET), o serviço de linguagem (Python), o banco de desenvolvimento e o aparato que os verifica.
Não há ainda módulo de domínio nem material real; a árvore e os comandos estão em
[020](docs/operacao/020-codigo-no-repositorio.md).

## Como isto funciona

| Onde | O que vive lá |
|---|---|
| `docs/` | **A fonte da verdade.** Requisitos, decisões, políticas, arquitetura |
| Issues | As tarefas para chegar ao que `docs/` descreve |

Toda mudança passa por pull request com aprovação do dono do repositório, e a verificação
automática precisa passar. Ver [`CONTRIBUTING.md`](CONTRIBUTING.md), e [`AGENTS.md`](AGENTS.md) para
a hierarquia de autoridade entre os documentos.

## Verificação local

```
python tools/verify-docs/verify.py
python tools/verify-docs/verify.py --only data,links
python tools/verify-docs/verify.py --list
```

O código passa pelo mesmo comando que a verificação contínua executa, na mesma ordem:

```
python tools/verify-code/verify.py
python tools/verify-code/verify.py --list
python tools/verify-code/verify.py --step pr_size --base origin/main
```

A ordem é única: `--step test_core` sozinho, em árvore limpa, falha porque testa com `--no-build`;
`build_core` vem antes, e a execução sem argumento segue essa ordem. O passo `lint_tools` exige
`ruff` e `pyright` nas versões de `tools/verify-code/lint-requirements.txt` (`pip install -r`).

O passo `test_core` deixa de fora os testes que sobem o serviço real (trait `Category=RealService`)
e passa sem `uv`; o passo `test_real_service` roda só esses, com `uv` instalado, lista os testes
pulados com o motivo e escreve a cobertura que o `coverage` soma à do `test_core` antes do
`floors`. O passo `ai_attribution` lê as mensagens dos commits entre a base e o HEAD
(`--base origin/main`) e reprova o trailer `Co-authored-by` de assistente de IA e a linha "Generated
with", nomeando o hash e a linha; co-autoria humana e `dependabot[bot]` passam.

O passo `coverage` publica a cobertura por área de `docs/operacao/014-modulos-criticos-e-pisos.md`
a partir dos relatórios presentes em `.verify-code/`; `test_tools` só mede com o `coverage` do
mesmo arquivo de requisitos instalado. O passo `floors` reprova a área abaixo do piso, o código
medido sem área e o projeto ou pacote sem linha na tabela ou que nenhum relatório leu, como em
`python tools/verify-code/verify.py --step build_service test_service coverage floors`; com
`--previous` apontando para uma cópia do by-area.md de outra execução, avisa a queda que não cruza
o piso.

Na CI, o push no `main` guarda o by-area.md dos jobs `dotnet` e `python` em cache
(`cobertura-<job>-<sha>`) e como artefato (`cobertura-<job>-<sha>`, 90 dias), que é o registro do
número medido. No pull request, cada job restaura o cache mais recente do `main` e roda o `floors`
com `--previous`: a queda por área que não cruza o piso aparece no resumo do job e como aviso do
pull request, sem reprovar. Sem cache (o `main` ainda não guardou, ou o cache expirou depois de 7
dias sem uso), o `floors` roda sem `--previous`, diz no resumo que não há medição para comparar e
não reprova; o artefato permanece, e a comparação volta no próximo push no `main`.

O passo `secrets` roda o `gitleaks` na versão fixada em `tools/verify-code/steps/gitleaks_binary.py`
e recusa um binário cuja soma SHA-256 não confira; sem `--base` varre o histórico inteiro. Os testes
de `tools/` o usam. A única permissão são os marcadores sintéticos `ZQX-…`, em `.gitleaks.toml`.

O passo `trivy`, também de binário fixado, varre lockfiles, segredos e Dockerfile; HIGH e CRITICAL
reprovam, a menos que `tools/verify-code/accepted-risks.toml` os aceite com prazo e motivo.
Nenhum código sai da máquina: ele só baixa o banco de vulnerabilidades e as regras de
configuração, com telemetria e checagem de versão desligadas. Não analisa workflows.

O passo `static_analysis` roda o `semgrep` de `tools/verify-code/static-analysis-requirements.txt`
(`pip install -r`) com as regras próprias de `tools/verify-code/semgrep`, lidas do disco e com
métricas desligadas, sobre `core`, `language-service` e `tools`, testes inclusive. Antes da
varredura, o `semgrep --test` confere cada regra contra os exemplos marcados em
`tools/verify-code/tests/semgrep`. Achado que não é defeito se dispensa no ponto, com
`nosemgrep: <regra> <motivo>` num comentário na linha ou na anterior; em `core/src`, nunca.

O passo `package` clona o commit de HEAD duas vezes numa pasta temporária, nunca a árvore de
trabalho, e publica em cada cópia o executável único de `linux-x64` e o de `win-x64`, com
restauração travada e a propriedade ContinuousIntegrationBuild, que troca o caminho da cópia por
um fixo. Reprova se um arquivo publicado diferir entre as duas, nomeado pelo caminho relativo; se a
pasta de um alvo tiver algo além do executável e do arquivo de configuração
`core/src/Core.Host/appsettings.json`; se o executável não declarar o commit, a árvore limpa e o
alvo, ou não carregar a plataforma de execução; ou se o núcleo construído para um alvo diferente da
máquina, e rodado nela, não recusar subir com o código 69. Fora do passo,
`core/Directory.Build.props` só liga essa propriedade na CI, e a depuração local vê os caminhos
reais.
Na CI, o mesmo job repete o passo sem rede, com o cache HTTP do NuGet vazio: a construção só usa
pacotes já baixados. Por isso `core/Directory.Packages.props` deixa a auditoria do NuGet desligada:
ela consulta a fonte quando o cache de vulnerabilidades vence, em cerca de 30 minutos.

O passo `mutation` roda o teste de mutação sobre exatamente as áreas que a coluna Crítica de
`docs/operacao/014-modulos-criticos-e-pisos.md` marca `sim` e julga, pelos números de
`docs/operacao/017-teste-de-mutacao.md`, a parcela de sobreviventes por módulo e o tempo por
linguagem. Cada sobrevivente sai com arquivo, linha e alteração. A suíte que conta em cada módulo é
a do projeto dele; no serviço, só os arquivos de teste que o importam. No núcleo roda o
Stryker.NET fixado em `core/dotnet-tools.json`; no serviço,
o mutmut de `language-service/uv.lock`, que exige o `uv` de
`tools/verify-code/service-requirements.txt` e **só roda no Linux**: em outra plataforma o passo
pula o serviço com aviso. A configuração das duas ferramentas é gerada a cada execução na pasta de
saída do verificador, a partir da tabela; uma seção `[tool.mutmut]` em
`language-service/pyproject.toml`, ou noutro arquivo igual do serviço, reprova, porque o mutmut a
leria no lugar dela. Cada linguagem é cortada no teto. Com a máquina livre, o passo soma cerca de
4 minutos à execução sem argumento.

O passo `format_core` roda `dotnet format core/Core.slnx --verify-no-changes`: qualquer mudança que
o `.editorconfig` exija reprova. O passo `properties` roda as propriedades das duas linguagens (no
núcleo, a trait `Kind=Property`; no serviço, a marca `property`) e reprova o lado que não executou
nenhuma. O passo `offline_start` sobe o núcleo publicado em contêineres, só com o loopback, sob
`strace`, e reprova a conexão externa, a consulta de DNS e a partida que não chega à linha de
pronto. O passo `ruler_proof` planta, numa cópia descartável, uma propriedade falsa, um defeito em
módulo crítico, código sem teste e testes fracos, e exige que a régua recuse cada um.

O passo `dependencies` audita a árvore completa das duas partes, transitivas inclusive, com a
auditoria do NuGet e o `pip-audit` de `tools/verify-code/audit-requirements.txt`. Lock ausente ou
base de vulnerabilidades fora de alcance reprovam: sem rede e sem base recente em cache, o passo
nunca passa. Achado sem correção só passa com risco aceito naquele mesmo arquivo; no núcleo, também
com o item NuGetAuditSuppress do mesmo aviso em `core/Directory.Packages.props`.

## Núcleo

O núcleo é uma solução .NET em `core/Core.slnx`. A versão exata do SDK está em `global.json`, e a
única fonte de pacotes, em `core/nuget.config`. Os comandos abaixo rodam a partir da raiz do
repositório, nesta ordem:

```
dotnet restore --locked-mode core/Core.slnx
dotnet build core/Core.slnx
dotnet test core/Core.slnx
PY="$(uv run --frozen --project language-service python -c 'import sys; print(sys.executable)')"
export DOTNET_ENVIRONMENT=Development
dotnet run --project core/src/Core.Host -- --Host:Urls=http://127.0.0.1:5080 \
  --Installation:Kind=demonstracao --LanguageService:Executable="$PY"
```

`Installation:Kind` é obrigatória, sem valor padrão, e só aceita `real` ou `demonstracao`, escritos
assim. A demonstração roda sem cópia de guarda da chave e sem recuperação testada, e diz isso no
nome da instalação (`(DEMONSTRAÇÃO)`), em `/status` e `/versions` e numa linha de aviso na partida.

O núcleo inicia o serviço de linguagem e só abre a porta depois que ele responde. A chave
`LanguageService:Executable` é opcional e serve só ao desenvolvimento: vale apenas com
`DOTNET_ENVIRONMENT=Development`, e fora desse ambiente a chave, se escrita, recusa a partida e é
nomeada na recusa; nenhuma configuração dispensa a conferência da pasta. A chave é o interpretador
do ambiente virtual de `language-service/`, em caminho absoluto, porque `dotnet run` roda a partir
da pasta do projeto; o `$(...)` acima o obtém, e o `uv run --frozen` já cria o ambiente a partir de
`uv.lock` se ele não existir. Com a chave, em Development, o núcleo usa esse executável e não
confere a pasta (o registro de inicialização diz que ela não foi conferida). Sem a chave, usa o
interpretador da pasta `language-service/` ao lado do executável e, antes de iniciar, confere cada
arquivo contra o `SHA256SUMS`; manifesto ausente, vazio ou ilegível, arquivo ausente ou soma
divergente recusa a partida e nomeia o arquivo (o manifesto, pelo caminho absoluto). O núcleo
inicia o interpretador em modo isolado (`-I`): `PYTHONHOME` e `PYTHONPATH` herdados do ambiente não
alcançam o serviço. Os testes do núcleo usam o mesmo comando para achar o interpretador.
`LanguageService:AnalyzeTimeoutSeconds` (padrão 10, de 1 a 3600 segundos) limita cada análise:
vencido o prazo, o desfecho é serviço indisponível, nunca lista vazia; valor fora desse intervalo
recusa a configuração.

`Recognizer:Kind` escolhe o reconhecedor, e só aceita `http` (padrão) ou `fake`, escritos assim.
Com `http`, o núcleo pergunta ao serviço de linguagem. Com `fake`, não inicia o serviço e decide
pelo próprio texto: cada `ZQX-FAKE-PERSON` vira uma ocorrência; `ZQX-FAKE-NOTHING` dá análise sem
ocorrência; qualquer outro texto fica sem análise, como serviço inalcançável. Com `fake`, a chave
`LanguageService:Executable` e a pasta `language-service/` são dispensáveis. Os testes do núcleo
sobem o host com `fake`; o ensaio contra o serviço real tem `Category=RealService` e é saltado
com aviso quando o `uv` não resolve o ambiente do serviço.

A chave opcional `Host:LogFile` grava o registro também no arquivo que ela nomeia, em caminho
absoluto ou relativo à pasta do executável, com as linhas e a redação do console. Sem ela, o
registro vai só ao console; com ela, um arquivo que não abre para escrita impede a inicialização.
Se a escrita no arquivo falhar depois, por disco cheio, o núcleo avisa uma vez na saída de erro e
segue registrando só no console.

SIGTERM ou CTRL+C encerra o núcleo: ele conclui as requisições em curso, encerra o serviço de
linguagem, fecha o registro e sai com código 0. Uma conferência de inicialização que falha sai com
69, e a configuração recusada, com 78. Uma falha que o encerra ao abrir a porta ou ao servir, como a
porta já ocupada, sai com 70 (salvo memória esgotada ou corrompida, que o processo não relata) e a
saída de erro nomeia o tipo da falha, nunca a mensagem.

Com o núcleo de pé, `http://127.0.0.1:5080/status` responde `alive`, `ready`, `recognizer`,
`languageService`, `checkedAt` e `checks[]`: o reconhecedor em vigor, o estado atual do serviço
de linguagem, quando as conferências terminaram e o resultado de cada conferência de
inicialização. Se o processo do serviço termina, `languageService` passa a `Unavailable` e `ready`
a falso, e o núcleo não o reinicia sozinho. Com `fake`, nenhum serviço é vigiado: `languageService`
é `null`, e `ready` depende só das conferências.
`http://127.0.0.1:5080/versions` responde a versão do núcleo, o commit de que ele foi construído e
se a árvore tinha mudança não registrada (`workingTree`, que vale `Unknown` se a construção não
conseguiu perguntar ao `git`); versão que o núcleo não obteve vem `null`, nunca vazia. A
`contractVersion`, a `languageServiceVersion` e a `modelVersion` são as que o serviço de linguagem
informa em `/version`, lidas a cada consulta sem guardar a anterior, e cada uma vem `null` por
conta própria quando o serviço não a informa (campo vazio ou só com espaços conta como ausente).
Com `fake` (sem conexão), com o serviço indisponível, recusando o segredo, fora do tempo ou
respondendo algo que não é versão, as três vêm `null`. O contrato que o serviço informa pode
diferir do que o núcleo fala nas requisições; o relatório mostra o do serviço. A `schemaVersion` é
a versão que a conferência de inicialização confirmou no banco, fixada na partida e não relida a
cada consulta: vem `null` sem banco configurado. A mesma lista sai no registro, na linha
`Versões em execução`, com o esquema já preenchido e as três do serviço lidas uma vez, depois que
o serviço tem porta e segredo, sem esperar além do limite de leitura; ausentes aparecem como
`<absent>`.

`POST /analysis` recebe `text` e `language` e analisa o texto pelo reconhecedor em vigor. Análise
concluída responde 200 com `reference`, `occurrences`, `kinds` (contagem por tipo, escrito como o
reconhecedor o escreve) e `modelVersion`. Sem análise, responde 503 com `reference`, `reason` e
`cause`, e nada é gravado. Retomar é pedir de novo com `resumes` igual à `reference` da recusa, e o
registro liga as duas pela referência. Campo desconhecido, ou `text` e `language` ausentes ou nulos,
recusam o pedido com 400.

Publicado para um alvo, o núcleo é um executável único, com a plataforma de execução dentro dele, e
só o `core/src/Core.Host/appsettings.json` ao lado; o `.github/workflows/package.yml`, disparado à
mão, guarda os dois como artefatos do workflow. O mesmo, para `linux-x64`, a partir da raiz:

```
dotnet restore core/src/Core.Host/Core.Host.csproj --locked-mode -p:RuntimeIdentifier=linux-x64
dotnet publish core/src/Core.Host/Core.Host.csproj --no-restore --configuration Release \
  -p:ContinuousIntegrationBuild=true --runtime linux-x64 --output publish/linux-x64
```

O executável grava o alvo para o qual foi publicado e, posto num destino de outra arquitetura,
como `win-x64` sob emulação num Windows ARM, recusa subir com o código 69, nomeando o alvo e o
destino. Tamanho e tempo de subida medidos estão em
[002](docs/historico/002-medidas-do-artefato.md).

O teste de arquitetura em `core/tests/Core.Tests.Architecture` impõe as regras de código a cada
construção; as fixtures que ele reprova de propósito ficam em `core/tests/fixtures/code-rules`, fora
de qualquer projeto.

Com as chaves `Database:Host`, `Database:Name` e `Database:Username` e a senha em
`CDG_DATABASE__PASSWORD`, o núcleo confere as garantias do banco com o papel `app` antes do serviço
de linguagem e recusa iniciar se faltar alguma; sem `Database:Host`, a conferência `Database`
aparece como `NotConfigured`.

## Banco de dados de desenvolvimento

A imagem de `tools/database/Dockerfile` é PostgreSQL 17, fixado por digest, com a extensão pgaudit
pré-carregada. O script de `tools/database/init/01-database.sh` cria o banco `cdg`, o papel `owner`,
dono do banco e do esquema, e o papel `app`, que só conecta e não é dono de nada. O compose publica
a porta só em `127.0.0.1` e exige três senhas no ambiente, sem valor em arquivo algum:
`POSTGRES_PASSWORD` (superusuário), `DATABASE_OWNER_PASSWORD` e `DATABASE_APP_PASSWORD`, fora do
prefixo `CDG_`, que o núcleo lê do ambiente e recusa quando não reconhece.

```
docker compose -f tools/database/compose.yml up
```

O pgaudit e os papéis só entram quando o volume nasce vazio. Um volume criado por imagem anterior
fica como estava, e se recria com `docker compose -f tools/database/compose.yml down -v`, que apaga
os dados.

O esquema é aplicado pelo modo `migrate` do mesmo executável, com a credencial de dono, descrito em
[016](docs/operacao/016-migracao-do-esquema.md); a partida normal só confere a versão.

Os testes de banco de `core/tests/Core.Persistence.Tests` e de `core/tests/Core.Tests.Integration`
constroem a mesma imagem com Testcontainers e senhas aleatórias. Sem Docker, os testes de banco
saltam com o aviso "Docker indisponível: teste de banco saltado", e o passo `test_core` lista o
projeto e o motivo, passe ele ou não; com `CI=true`, como na verificação contínua, eles rodam
sempre, e a falta de Docker reprova.

## Serviço de linguagem

O serviço é um projeto Python 3.12 em `language-service/`, com dependências fixadas em
`language-service/uv.lock`; o estilo e a tipagem vêm de `ruff.toml` e `pyrightconfig.json` na
raiz. Ele escuta só em `127.0.0.1` e lê o segredo de inicialização da entrada padrão.

```
uv sync --locked --directory language-service
uv run --directory language-service pytest
```

A pasta `language-service/` que o núcleo publicado confere ao lado do executável é montada por
`tools/build-service`, como descreve [018](docs/operacao/018-pasta-do-servico.md).

No comando único, são os passos `build_service`, `test_service` e `lint_service`: exigem `uv` na
versão de `tools/verify-code/service-requirements.txt`; o segundo grava o relatório de testes e a
cobertura dentro da pasta de saída do verificador; o terceiro vem depois do primeiro, que cria o
ambiente que o `pyright` lê.

## Licença

O software é [AGPL-3.0](LICENSE): qualquer um pode usar, estudar, modificar e redistribuir,
inclusive comercialmente, e quem modificar — ou oferecer o sistema como serviço pela rede —
publica as alterações sob a mesma licença.

**O material e os datasets não acompanham o software e não se licenciam.** O acesso é concedido
caso a caso, por contrato que restringe o uso a finalidade acadêmica ou de pesquisa.

## Privacidade

Nenhum dado real acompanha este repositório. Exemplos usam identificadores sintéticos. Nome de
pessoa, texto bruto de amostra, chave e credencial não entram em nenhum arquivo — nem em teste. A
verificação automática reprova o pull request que os contenha.
