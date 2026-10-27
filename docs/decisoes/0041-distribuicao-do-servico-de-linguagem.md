# 0041 — Distribuição do serviço de linguagem

> Decisão vigente. 2026-10-26.

## Contexto

A [0002](0002-stack.md) recusa Python e Java no núcleo por exigirem interpretador ou máquina virtual
no destino, mantém o serviço em Python e fixa "executável único, sem instalar nada". A
[0011](0011-protocolo-com-o-servico-de-linguagem.md) deixou o conjunto em aberto: o tamanho do
modelo decide se o executável único vale para ele ou só para o núcleo. A
[medição](../historico/003-medicao-do-modelo.md) dá interpretador autônomo, spaCy e modelo, sem as
dependências HTTP do serviço, em 359, 395 e 946 MiB instalados e 117, 145 e 647 comprimidos (com
compressões diferentes, ver nela) para pt_core_news_sm, pt_core_news_md e pt_core_news_lg. Vale o [requisito 022](../requisitos/022-operacao-sem-servico-externo.md): nada se instala nem se busca
fora, e o modelo ausente falha na partida. Hoje o núcleo inicia o serviço pela chave obrigatória
`LanguageService:Executable`, com `-m language_service --port N` (`ServiceProcessLauncher`).

## Decisão

**Entregue o serviço numa pasta ao lado do núcleo, com interpretador autônomo, e mantenha o núcleo
como executável único.** A pasta leva o interpretador de `uv python install`, um ambiente relocável
e o modelo, e é montada no build. São dois artefatos lado a lado, e o tamanho do serviço é o dos
totais medidos do Contexto. O iniciador e a configuração mudam: o caminho se resolve contra a pasta
do núcleo, e a chave deixa de ser obrigatória; os argumentos atuais servem.

## Alternativas recusadas

- **Binário próprio do serviço**, congelado por PyInstaller, Nuitka ou PyOxidizer, com o modelo
  dentro. Pede um build por sistema, pois o congelador não gera binário de outro; as importações
  dinâmicas do spaCy podem pedir lista de importações ocultas; binário grande congelado pode ser
  sinalizado por antivírus e, em arquivo único, extrai cerca de 1 GiB em `lg` a cada partida. O
  modelo some dentro do binário, e o aviso de licença vai à parte.
- **Artefato único que se extrai** na primeira execução. É o mais caro: tudo o que a pasta exige,
  mais embutir e extrair, por sistema. Extraído, comprimido e instalado coexistem, cerca de 1,6 GiB
  com `lg`; grava executável no destino, em pasta que pode não ser gravável; a extração interrompida
  pede marca de extração concluída.
- Reconhecimento embarcado no núcleo por ONNX: é a condição de reabertura da 0011; aqui reabriria a 0002 e perderia a análise gramatical junto do reconhecimento, que a [0018](0018-contrato-da-fronteira.md) exige.
- Imagem de contêiner: instala Docker no destino, contra a 0002 e o requisito 022.
- Baixar o modelo na primeira execução: contra o requisito 022; exige rede e falha no meio do uso.

## Consequências

Substitui a 0002 e a 0011 quanto à distribuição do serviço; as duas só trocam a linha de
autoridade. O executável único segue requisito do núcleo, e é só o conjunto que deixa de sê-lo; por
isso a condição de reabertura da 0002 não se realiza, e Java não volta ao núcleo. A
pasta são arquivos soltos, sem extração: atualizar troca uma subpasta, e o risco de antivírus é
menor, não nulo. Não há compilação, apenas interpretador e rodas do sistema.

Piora: dois artefatos em vez de um, e o iniciador muda. O ambiente relocável não foi experimentado e
fica a provar. Modelo ausente ou corrompido se detecta por soma por arquivo, gravada no build e
conferida nomeando o arquivo; ler tudo custa cerca de 0,6 GiB em `lg` por partida, ou só na
instalação.

## O que reabre esta decisão

Outro modelo ou motor, de outra ordem de tamanho: a medição se repete por
[015](../operacao/015-medicao-do-modelo-de-linguagem.md). Protótipo em que o ambiente relocável
recuse a pasta. Destino que o build escolhido não alcance.
