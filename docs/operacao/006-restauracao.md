# 006 — Restauração

> Operacional. 2026-10-04.

Restaurar é devolver o acervo ao estado da cópia **sem alterar a ordem da sequência de eventos**. A
ordem não é detalhe de armazenamento: cada evento carrega o resumo do anterior, e verificar é
recalcular a sequência a partir dessas ligações. Restauração que reinsira eventos em outra ordem
produz cadeia que não fecha — e, embora a
[verificação da custódia](../privacidade/005-verificacao-da-custodia.md) saiba separar dano
acidental de adulteração, a separação se apoia em existir janela operacional que explique o dano.
Restauração malfeita e sem registro é dano sem explicação.

## Antes de restaurar

Não restaure sobre a instalação afetada. Restaure em ambiente separado, confira, e só então decida
o que fazer com o original — que fica intacto até lá, porque é a única evidência do que aconteceu.

Tenha em mão o topo da cadeia anotado fora do conjunto ([005](005-backup.md)) e a taxonomia da
versão correspondente. Abra a janela antes de começar: quem restaura, de qual cópia, por quê.

## O procedimento

1. Restaure o banco inteiro, em uma operação, com o sistema sem escrever
2. Confira que cada evento conserva o número de sequência e o resumo do antecessor que tinha na
   cópia. A ordem a preservar é a **registrada** — nem a de inserção física, nem a do horário
3. Percorra a sequência da origem ao topo e compare o valor obtido com o anotado fora
4. Confira a contagem de eventos por item: remoção no fim da sequência não deixa elo quebrado
5. Confira que as garantias do servidor voltaram — política de linha com `FORCE`, permissão de
   coluna, gatilho de imutabilidade. Restauração que recria tabela sem elas devolve o dado e perde a
   garantia, e é a falha mais silenciosa deste procedimento
6. Carregue a taxonomia e confira que todo rótulo gravado existe nela
7. Só então libere a escrita, e feche a janela com o resultado

## O que nunca se faz

Reinserir eventos por consulta ordenada por horário, reescrever número de sequência, ou reconstruir
ligação que não fechou. Qualquer uma das três produz cadeia válida e falsa.

## Quem restaura não lê conteúdo

A [política de acesso](../privacidade/006-acesso.md) não concede conteúdo à administração técnica, e
restaurar é trabalho dela. Todas as conferências acima se fazem sobre estrutura, contagem e resumo:
nenhuma exige abrir texto. Procedimento que precise ler material para validar restauração está
pedindo acesso que a política nega.

## A restauração é um evento

Ela não desaparece depois de dar certo: um evento novo declara o que foi restaurado, de qual cópia,
por quem, e qual era o topo antes e depois. Restauração silenciosa deixa na sequência um salto que a
próxima verificação encontra sem explicação — e trata como incidente, corretamente.

Se a cadeia não fechar, pare aqui e siga [009](009-incidente-cadeia-quebrada.md).
