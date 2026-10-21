# Conversational Dataset Generator

Ferramenta para construir **bases conversacionais anotadas em português brasileiro**: ingestão de
formatos variados, sanitização, pseudonimização que preserva contexto, anotação humana assistida,
validação por métricas de concordância e exportação de datasets rastreáveis.

O domínio **forense/criminal é o estudo de caso mais restritivo**, não o escopo da ferramenta.

Projeto de pesquisa aplicada em processamento de linguagem natural para o português
brasileiro.

## Estado

Repositório em fundação. Contém o aparato de verificação e revisão; a documentação entra em
seguida, um pull request por documento.

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

O passo `coverage` publica a cobertura por área de `docs/operacao/014-modulos-criticos-e-pisos.md`
a partir dos relatórios presentes em `.verify-code/`, sem julgar piso, como em
`python tools/verify-code/verify.py --step build_service test_service coverage`; `test_tools` só
mede com o `coverage` do mesmo arquivo de requisitos instalado.

O passo `secrets` roda o `gitleaks` na versão fixada em `tools/verify-code/steps/gitleaks_binary.py`
e recusa um binário cuja soma SHA-256 não confira; sem `--base` varre o histórico inteiro. Os testes
de `tools/` o usam. A única permissão são os marcadores sintéticos `ZQX-…`, em `.gitleaks.toml`.

O passo `trivy`, também de binário fixado, varre lockfiles, segredos e Dockerfile; HIGH e CRITICAL
reprovam, a menos que `tools/verify-code/accepted-risks.toml` os aceite com prazo e motivo.
Nenhum código sai da máquina: ele só baixa o banco de vulnerabilidades e as regras de
configuração, com telemetria e checagem de versão desligadas. Não analisa workflows.

## Núcleo

O núcleo é uma solução .NET em `core/Core.slnx`. A versão exata do SDK está em `global.json`, e a
única fonte de pacotes, em `core/nuget.config`. Os comandos abaixo rodam a partir da raiz do
repositório, nesta ordem:

```
dotnet restore --locked-mode core/Core.slnx
dotnet build core/Core.slnx
dotnet test core/Core.slnx
PY="$(uv run --frozen --project language-service python -c 'import sys; print(sys.executable)')"
dotnet run --project core/src/Core.Host -- --Host:Urls=http://127.0.0.1:5080 \
  --LanguageService:Executable="$PY"
```

O núcleo inicia o serviço de linguagem e só abre a porta depois que ele responde. A chave
`LanguageService:Executable` é o interpretador do ambiente virtual de `language-service/`, em
caminho absoluto, porque `dotnet run` roda a partir da pasta do projeto; o `$(...)` acima o obtém, e
o `uv run --frozen` já cria o ambiente a partir de `uv.lock` se ele não existir. Sem a chave, o
núcleo recusa a configuração. Os testes do núcleo usam o mesmo comando para achar o interpretador.
`LanguageService:AnalyzeTimeoutSeconds` (padrão 10, de 1 a 3600 segundos) limita cada análise:
vencido o prazo, o desfecho é serviço indisponível, nunca lista vazia; valor fora desse intervalo
recusa a configuração.

`Recognizer:Kind` escolhe o reconhecedor, e só aceita `http` (padrão) ou `fake`, escritos assim.
Com `http`, o núcleo pergunta ao serviço de linguagem. Com `fake`, não inicia o serviço e decide
pelo próprio texto: cada `ZQX-FAKE-PERSON` vira uma ocorrência; `ZQX-FAKE-NOTHING` dá análise sem
ocorrência; qualquer outro texto fica sem análise, como serviço inalcançável. Com `fake`, a chave
`LanguageService:Executable` continua exigida, mas não é usada. Os testes do núcleo sobem o host
com `fake`; o ensaio contra o serviço real tem `Category=RealService` e é saltado com aviso quando
o `uv` não resolve o ambiente do serviço.

Com o núcleo de pé, `http://127.0.0.1:5080/status` responde `alive`, `ready` e `checks[]`, com o
resultado de cada conferência de inicialização. O teste de arquitetura em
`core/tests/Core.Tests.Architecture` impõe as regras de código a cada construção; as fixtures que
ele reprova de propósito ficam em `core/tests/fixtures/code-rules`, fora de qualquer projeto.

## Serviço de linguagem

O serviço é um projeto Python 3.12 em `language-service/`, com dependências fixadas em
`language-service/uv.lock`; o estilo e a tipagem vêm de `ruff.toml` e `pyrightconfig.json` na
raiz. Ele escuta só em `127.0.0.1` e lê o segredo de inicialização da entrada padrão.

```
uv sync --locked --directory language-service
uv run --directory language-service pytest
```

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
