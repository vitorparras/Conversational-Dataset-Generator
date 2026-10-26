# 003 — Texto sensível fora do banco

> Norma vigente. 2026-10-04.

A [decisão 0002](../decisoes/0002-stack.md) põe o processamento de linguagem natural em serviço
separado, o que significa que texto ainda não sanitizado atravessa fronteira de processo. O que
trafega e sob que transporte está decidido na
[decisão 0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md): o serviço recebe texto e
devolve apontamentos, e o transporte é restrito à máquina local.

O que o serviço carrega para funcionar está na [decisão 0041](../decisoes/0041-distribuicao-do-servico-de-linguagem.md):
interpretador, ambiente e modelo numa pasta ao lado do núcleo, montada no build. Nada disso é
acervo. **Esta política afirma o que vale para o acervo**, seja qual for a forma de entrega.

## O que atravessa

O trecho a analisar, e nada que o situe. Nem identificação do acervo, nem do caso, nem do lote, nem
rótulo de domínio: o serviço recebe texto e devolve posições e tipos. Uma referência opaca liga a
resposta ao pedido, e só o núcleo sabe a que ela corresponde.

A consequência é que um despejo de memória do serviço expõe texto, nunca a que acervo ele pertence.
É pouco, mas é a diferença entre um vazamento e um vazamento atribuível.

## Nada do acervo persiste ali

Processou, respondeu, esqueceu. Nenhum conteúdo do acervo sobrevive ao pedido — nem em banco, nem em
cache, nem em arquivo temporário, nem em fila.

Os arquivos da pasta do serviço são outra coisa. A regra desta política não depende da forma de
entrega: **conteúdo do acervo em disco fora
da cifra da [decisão 0003](../decisoes/0003-cifra-em-repouso.md) é incidente**, não detalhe de
implantação. Quem impõe o contrário é configuração de ambiente — camada que falha em silêncio,
porque nada no sistema percebe um arquivo temporário que não deveria existir.

## Conteúdo nunca entra em registro

Nem mensagem de log, nem rastro de erro, nem métrica com amostra, nem corpo de requisição guardado
para depuração. Erro referencia o pedido pela referência opaca e por mais nada.

No núcleo isso é mecanismo: a plataforma redige campo sensível no próprio pipeline de log, por
anotação no tipo, como a decisão 0002 descreve. Quem escreve a mensagem não precisa lembrar.

No serviço de linguagem **não há mecanismo equivalente**, e vale dizer em voz alta: ali a garantia é
disciplina de quem escreve o código, a camada mais fraca desta política inteira. O contrapeso é
reduzir o que existe para registrar — o serviço não recebe metadado, então log acidental com texto
não diz de quem é o texto.

## A conversa entre os dois processos

Interface local e autenticada, sem porta exposta na rede, pela decisão 0011. Quem impõe é
configuração de ambiente. Se falhar, texto não sanitizado fica disponível a quem alcance a rede, em
claro: a cifra da decisão 0003 protege o que está em repouso, não o que trafega entre processos.

O transporte local é o que sustenta o piso acima, porque a cifra não cobre o trânsito. Afastar os
dois processos não é ajuste de implantação: **reabre a decisão 0011**, e quem a substituir responde
por cifrar o trânsito. Escalar por conveniência é decidir sem escrever — ver
o documento de operação sobre o serviço de linguagem.
