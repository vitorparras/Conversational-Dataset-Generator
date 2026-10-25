# 0041 — Distribuição do serviço de linguagem

> Decisão pendente. 2026-10-19.

## Contexto

A [0002](0002-stack.md) recusa Python e Java no núcleo por exigirem interpretador ou máquina virtual
no destino, mantém o serviço em Python e fixa "executável único, sem instalar nada". A
[0011](0011-protocolo-com-o-servico-de-linguagem.md) deixou o conjunto em aberto: o tamanho do
modelo decide se o executável único vale para ele ou só para o núcleo. A
[medição](../historico/003-medicao-do-modelo.md) dá interpretador autônomo, spaCy e modelo, sem as
dependências HTTP do serviço, em 359, 395 e 946 MiB instalados e 117, 145 e 647 comprimidos (com
compressões diferentes, ver nela) para pt_core_news_sm, pt_core_news_md e pt_core_news_lg. Em toda
alternativa vale o [requisito 022](../requisitos/022-operacao-sem-servico-externo.md): nada se instala nem se busca
fora, e o modelo ausente falha na partida. Hoje o núcleo inicia o serviço pela chave obrigatória
`LanguageService:Executable`, com `-m language_service --port N` (`ServiceProcessLauncher`).

## Decisão

**Pendente: o operador escolhe entre as alternativas abaixo.** A medição não inviabiliza nenhuma.

| | A. Binário próprio do serviço | B. Pasta com interpretador autônomo | C. Artefato único que se extrai |
|---|---|---|---|
| Executável único vale para | Só o núcleo | Só o núcleo | O conjunto |
| Artefatos e tamanho do serviço | 2 lado a lado: núcleo e serviço congelado (PyInstaller, Nuitka ou PyOxidizer) com o modelo dentro. Tamanho: da ordem dos totais do Contexto, menos o que o congelador descarta; não medido | 2 lado a lado: núcleo e pasta do serviço (interpretador de `uv python install`, ambiente relocável, modelo), montada no build. Tamanho: os totais do Contexto (medidos) | 1: o núcleo embute o serviço e o extrai para a pasta da instalação na primeira execução. Tamanho: o artefato leva a parte comprimida; extraído, comprimido e instalado coexistem: cerca de 1,6 GiB com `lg` |
| Como o núcleo o localiza | Iniciador e configuração mudam (caminho resolvido contra a pasta do núcleo, chave deixa de ser obrigatória); o binário talvez não aceite `-m language_service`, a provar | Iniciador e configuração mudam, como em A; os argumentos atuais servem | Como B, só depois da extração; o núcleo passa a saber extrair e onde |
| Modelo ausente ou corrompido | Dentro do binário: ausência só por binário truncado; corrupção só aparece se o serviço conferir soma gravada no build | Soma por arquivo gravada no build e conferida na partida, nomeando o arquivo; ler tudo custa cerca de 0,6 GiB em `lg` por partida, ou só na instalação | Como B, mais a extração interrompida (disco cheio, processo morto), que pede marca de extração concluída |
| Build, atualização, antivírus e disco | Um build por sistema, pois o congelador não gera binário de outro; importações dinâmicas do spaCy podem pedir lista de importações ocultas, a provar. Atualizar refaz o binário inteiro. Antivírus e disco: binário grande congelado pode ser sinalizado e, em arquivo único, extrair tudo para pasta temporária a cada partida (cerca de 1 GiB em `lg`); a provar por congelador; em modo pasta vira B | Sem compilar: interpretador e rodas do sistema; em tese dispensa máquina de cada sistema; ambiente relocável não experimentado, a provar. Atualizar troca uma subpasta. Antivírus e disco: arquivos soltos, sem extração; risco menor, não nulo | O mais caro: B mais embutir e extrair, por sistema. Atualizar troca o artefato inteiro, e extrai sobre a versão anterior. Antivírus e disco: grava executável e biblioteca no destino, em pasta que pode não ser gravável; mesma triagem de antivírus; o que se extrai não é acervo, então não é incidente pela [política de texto fora do banco](../privacidade/003-texto-fora-do-banco.md) |

## Alternativas recusadas desde já

- Reconhecimento embarcado no núcleo por ONNX: é a condição de reabertura da 0011; aqui reabriria a 0002 e perderia a análise gramatical junto do reconhecimento, que a [0018](0018-contrato-da-fronteira.md) exige.
- Imagem de contêiner: instala Docker no destino, contra a 0002 e o requisito 022.
- Baixar o modelo na primeira execução: contra o requisito 022; exige rede e falha no meio do uso.

## O que substitui quando vier a valer (por arquivo e seção; quem decide edita)

As decisões 0002 e 0011 só trocam a linha de autoridade ("Decisão substituída por 0041 quanto à
distribuição do serviço"), como a 0003 substituída pela 0030; o texto não se edita. Editam-se:

- `docs/operacao/011-servico-de-linguagem.md`: a seção "O que fica aberto".
- `docs/privacidade/003-texto-fora-do-banco.md`: "Um ponto da decisão 0002 segue aberto" e "feche
  o ponto aberto da 0002".
- `docs/operacao/001-ambiente.md`: "distribuído como executável único" e "o único ponto aberto".
- `docs/roadmap/001-fundacao-verificavel.md`: as frases sobre o "executável único".
- `docs/roadmap/012-decisoes-pendentes.md`: a tabela "Fechadas" ganha a linha desta decisão.

## Consequências

Enquanto pendente, a entrega do serviço não se implementa: implementar é decidir. A e B acionam a
condição de reabertura da 0002 ("se o executável único deixar de ser requisito"); cabe ao operador
decidir se ela se realiza para o conjunto ou só para o núcleo. Piora, em A, o build por sistema; em
B, os dois artefatos e o iniciador; em C, o artefato maior e a escrita no destino. Em A e C o modelo
some dentro de um binário e o aviso de licença (ver a [medição](../historico/003-medicao-do-modelo.md)) vai à parte.

## O que reabre esta decisão

Outro modelo ou motor, de outra ordem de tamanho: a medição se repete por
[015](../operacao/015-medicao-do-modelo-de-linguagem.md). Protótipo de A que não congele o spaCy ou
de B que o ambiente relocável recuse. Destino que o build escolhido não alcance.
