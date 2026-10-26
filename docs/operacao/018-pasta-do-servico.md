# 018 — A pasta do serviço de linguagem

> Operacional. 2026-10-26.

A [decisão 0041](../decisoes/0041-distribuicao-do-servico-de-linguagem.md) entrega o serviço numa
pasta ao lado do núcleo. Este documento é como a pasta se monta e o que ela garante. Quem a localiza
e confere ao iniciar é o núcleo, e isso não está aqui.

## Montar

```
python tools/build-service/service_folder.py --system linux-x64 \
    --model pt_core_news_md --output SAIDA
```

A pasta nasce com o nome language-service dentro de SAIDA. O sistema tem de ser o da máquina que
monta, porque o interpretador vem de `uv python install` e esse comando instala o do sistema em que
roda. Pasta já existente não se sobrescreve: o script recusa. A rede é usada só aqui; a pasta pronta
roda sem ela.

**Provado:** `linux-x64`, num contêiner sem rede, com a pasta copiada para outro caminho. **Não
provado:** `win-x64`. O script o declara, mas ninguém o rodou em Windows, e o contrato abaixo para
ele é intenção.

## O que entra

- O interpretador 3.12.13, fixado em `PYTHON_VERSION` no script, copiado inteiro para `python/`.
- As dependências do `language-service/uv.lock`, com hash, e o grupo do modelo: spaCy e o pacote do
  modelo estão travados ali. Tudo vai para dentro do interpretador, sem ambiente virtual.
- O próprio serviço, instalado, nunca o código-fonte do repositório.

O modelo é um grupo de dependência do `language-service/pyproject.toml` com o nome do pacote. Só o
pt_core_news_md existe; outro entra por esse arquivo e pelo `language-service/uv.lock`, não por
opção do script.

## O contrato com o núcleo

Dentro da pasta, o interpretador fica em python/bin/python3 no Linux e em python/python.exe no
Windows. O serviço se inicia por `-m language_service --port N`. O manifesto SHA256SUMS, na raiz da
pasta, segue o formato do `sha256sum`, com caminhos relativos e `/`, e cobre todo arquivo da pasta
menos ele mesmo.

## Rodar não pode mudar a pasta

O Python reescreve bytecode quando a cópia traz data nova. O script impede isso de três modos:
remove o marcador `EXTERNALLY-MANAGED` do interpretador, apaga todo `__pycache__` e recompila tudo,
biblioteca padrão incluída, com `compileall -f --invalidation-mode checked-hash`. Sem `-f`, o
compileall pula o bytecode que o próprio interpretador escreveu; sem `checked-hash`, a data decide.

Antes de terminar, o script importa o serviço, carrega o modelo e confere o manifesto de novo. Se
algo mudou, a montagem falha nomeando o arquivo e apaga a pasta.

## Dois artefatos

O núcleo é um executável único. O serviço é a pasta, de algumas centenas de MiB, ao lado dele.
Atualizar o serviço é trocar a pasta; o núcleo não muda.
