# 018 — A pasta do serviço de linguagem

> Operacional. 2026-10-29.

A [decisão 0041](../decisoes/0041-distribuicao-do-servico-de-linguagem.md) entrega o serviço numa
pasta ao lado do núcleo. Este documento é como a pasta se monta e o que ela garante. Quem a localiza
e confere ao iniciar é o núcleo, e isso não está aqui.

## Montar

```
python tools/build-service/service_folder.py --system linux-x64 \
    --model pt_core_news_md --output SAIDA
```

A pasta nasce com o nome language-service dentro de SAIDA. O sistema tem de ser o da máquina que
monta, porque o interpretador vem de `uv python install`, que instala o do sistema em que roda.
Pasta já existente não se sobrescreve. A rede é usada só aqui; a pasta pronta roda sem ela.

**Provado:** `linux-x64`, num contêiner sem rede, e `win-x64` (Windows 11), ambos com a pasta
copiada para outro caminho e data nova em todo arquivo; no Windows, também com o `python.exe` da
cópia bloqueado no Firewall (com controle positivo), `PYTHONHOME` e `PYTHONPATH` com lixo e
`python.exe -I` respondendo `/health` 200 em duas partidas.

## O que entra

- O interpretador 3.12.13, fixado em `PYTHON_VERSION`, em python/bin/python3 no Linux e em
  python/python.exe no Windows. O serviço se inicia por `-m language_service --port N`.
- As dependências do `language-service/uv.lock`, com hash, e o grupo do modelo (só o
  pt_core_news_md; outro entra pelo `language-service/pyproject.toml`, não por opção do script).
  Tudo vai para dentro do interpretador. Sem hash, a instalação recusa: nada se resolve na hora.
- O próprio serviço, instalado de um wheel, nunca o código-fonte do repositório. O backend de
  construção (uv_build) vem do `language-service/build-requirements.txt`, com hash, e o wheel se
  constrói sem isolamento: a faixa do projeto não se resolve na montagem.
- O ORIGIN.json: commit, estado da árvore (`clean` ou `dirty`), sistema e versões do interpretador,
  do serviço e do pacote do modelo. Mudança não registrada não é recusada: a pasta diz `dirty`.
  Lido antes de instalar, que pode sujar a árvore.
- O manifesto SHA256SUMS, no formato do `sha256sum`, com caminhos relativos e `/`: cobre todo
  arquivo da pasta menos ele mesmo.

## Rodar não pode mudar a pasta

O Python reescreve bytecode quando a cópia traz data nova. O script remove o marcador
`EXTERNALLY-MANAGED`, apaga todo `__pycache__` e recompila tudo, biblioteca padrão incluída, com
`compileall -f --invalidation-mode checked-hash`. Antes de terminar, importa o serviço, carrega o
modelo e confere o manifesto de novo; se algo mudou, a montagem falha nomeando o arquivo.

## Dois artefatos

O núcleo é um executável único; o serviço é a pasta, de algumas centenas de MiB, ao lado dele.
Atualizar o serviço é trocar a pasta. O `.github/workflows/package.yml` publica, por alvo,
`nucleo-<alvo>` e `servico-<alvo>`, a pasta montada num runner do próprio sistema.
