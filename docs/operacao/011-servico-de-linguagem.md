# 011 — O serviço de linguagem no ambiente

> Descrição. 2026-10-04.

Texto ainda não sanitizado atravessa a fronteira de processo até o serviço de linguagem: é o único
ponto do sistema em que dado pessoal sai do processo que detém a chave. O que trafega e sob que
transporte é a [decisão 0011](../decisoes/0011-protocolo-com-o-servico-de-linguagem.md); o que o
ambiente responde por está aqui.

## O transporte é local, e isso está decidido

O serviço escuta **apenas na interface local**, nunca numa interface de rede, e autentica por
segredo que o núcleo gera na inicialização. Os dois processos moram, portanto, na mesma máquina.

Quem impõe é configuração de ambiente, e é camada que falha em silêncio: porta aberta numa interface
de rede não derruba nada, só deixa texto anterior à sanitização ao alcance de quem alcance a rede. A
cifra da [decisão 0003](../decisoes/0003-cifra-em-repouso.md) protege o que está em repouso, não o
que trafega entre processos.

Mais de uma instância do serviço, fora da máquina, **não é ajuste de implantação**: reabre a decisão
0011 e exige transporte autenticado e cifrado, por decisão que a substitua. Escalar por conveniência
é decidir sem escrever.

## O que nunca entra em registro

Nem o texto, nem trecho dele, nem os deslocamentos. Não se habilita modo de depuração que imprima
corpo de requisição, e a decisão 0011 não deixa existir um para habilitar.

A razão do caso dos deslocamentos é menos óbvia: deslocamento com tipo, somado ao texto que o núcleo
tem, reconstrói o dado pessoal. Registrar só os números é registrar o dado por tabela.

O log recebe identificador da requisição, tamanho em caracteres, tempo, versão do modelo e contagem
de trechos por tipo. É com isso que se diagnostica, e é de propósito que não dê para ver mais: sem
texto em log, reproduzir um reconhecimento errado exige reexecutar sobre o material original, cuja
decifragem é operação auditada.

## O que fica aberto, e a regra que não depende disso

Como um serviço que exige interpretador e modelo no destino se concilia com a entrega por executável
único é ponto aberto da [decisão 0002](../decisoes/0002-stack.md), e por isso a política de
[texto fora do banco](../privacidade/003-texto-fora-do-banco.md) não afirma o que o serviço carrega
para funcionar. Daqui também não sai exigência sobre volume somente leitura: dependeria da mesma
decisão.

A regra que vale de qualquer modo: **conteúdo do acervo gravado em disco fora da cifra é
incidente**, não detalhe de implantação. Vale para arquivo temporário, cache, despejo de memória e
qualquer coisa que sobreviva ao pedido.
