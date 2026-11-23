# Fase 5 — pseudonimização

> Norma pendente. 2026-11-22. Aguarda a aprovação do dono antes do primeiro código.

Pacote da fase: o desenho da pseudonimização e da reversão, feito antes do código, para que nenhuma
fatia replaneje no meio. Fecha as issues #99 a #108 e as telas #614, #615 e #616 em seis fatias
verticais, a F em três partes. Cada fatia entrega o esquema, o domínio, a rota, a tela, os testes
e o documento de uma funcionalidade usável.

A pseudonimização roda na entrada, logo depois da sanitização e no mesmo comando que grava o item:
aplica o que a entrada decidiu, sem decidir de novo ([dado
pessoal](../../../privacidade/001-dado-pessoal.md), linhas 7 a 10). O marcador é sorteado, nunca
calculado do original, e se repete na conversa e nunca em outra
([0057](../../../decisoes/0057-escopo-do-marcador-e-a-conversa.md)); o mapa de reversão já tem
tabela e finalidade próprias (`core/src/Core.Persistence/Migrations/Sql/0010-content-text.sql`,
linhas 14 a 25). O pseudonimizado é cifrado com a chave do item
([0030](../../../decisoes/0030-qual-texto-fica-em-claro.md), linha 24). Onde os documentos ainda
divergem, o pacote segue a opção que a documentação recomenda e o PR a lista em aberto.

## Índice

| Documento | Conteúdo |
|---|---|
| [Modelo de dados](modelo-de-dados.md) | Migrações das fatias, com nome e motivo |
| [Contratos](contratos.md) | O que cada módulo oferece ao outro, e quem chama quem |
| [Eventos da reversão](eventos-da-reversao.md) | O que cada evento das fatias C e D aponta na cadeia |
| [Riscos](riscos.md) | Concorrência, falha no meio, rede, prazo vencido, cadeia quebrada, precisão, dado pessoal |
| [Fatia A](fatia-a-marcador-e-unidade.md) | Marcador estável, unidade do deslocamento e deslocamento de datas (#99, #100, #108) |
| [Fatia B](fatia-b-mapa-e-pseudonimizado.md) | Mapa cifrado e gravação do pseudonimizado (#101) |
| [Fatia C](fatia-c-pedido-e-autorizacao.md) | Pedido e autorização da reversão, telas R1 e R2 (#103, #104, #614, #615) |
| [Fatia D](fatia-d-revelacao.md) | Registro antes de revelar, tela R3 (#105, #616) |
| [Fatia E](fatia-e-varredura-e-registro.md) | Varredura das leituras e registro sem conteúdo nos dois processos (#106, #107) |
| [Fatia F](fatia-f-correferencia.md) | O referente pela correferência: modelo e medição, serviço, núcleo (F1, F2, F3; #102) |
| [Protótipos](prototipos/index.html) | As telas navegáveis, com todos os estados |

## Roteiro

1. **Fatia A — o marcador e as unidades.** O sorteio do marcador por tipo, nome de lugar incluído,
   a recusa do deslocamento que não passou pelo conversor e o deslocamento de datas de cada domínio.
2. **Fatia B — o mapa e o pseudonimizado.** O item nasce com o pseudonimizado e com o mapa dele,
   cifrado; a tela do lote mostra quantos marcadores por tipo, só números.
3. **Fatia C — pedir e autorizar.** A Curadoria pede a reversão de um marcador num item; outra
   pessoa da Curadoria do mesmo domínio autoriza ou recusa, com prazo.
4. **Fatia D — revelar.** Quem pediu revela uma vez; o registro é gravado antes, sem a identidade.
5. **Fatia E — a prova do sistema.** A varredura das leituras por valor semeado e o registro sem
   conteúdo no núcleo e no serviço de linguagem.
6. **Fatia F — o referente.** F1 escolhe e mede o modelo de correferência em pt-BR, com a decisão
   que põe o grupo na resposta; F2 o serviço devolve o grupo; F3 o núcleo o usa como referente,
   liga a troca à submissão e dá o primeiro pseudonimizado aos itens gravados antes.

A fatia 3 proposta para a reversão (#103 a #107 e as três telas) foi partida em C, D e E: junta,
passaria do teto duro de 1.500 linhas da [0054](../../../decisoes/0054-teto-por-fatia-e-migracao-por-data.md).
De A a E, cada fatia depende da anterior, porque lê o que a anterior grava. A F corre ao lado: F1 e
F2 dependem da A, e a F3, da F2 e da B. A E depende também da F3, e a fase só fecha depois das duas.

## Base

- O roadmap: [fase 5](../../../roadmap/005-pseudonimizacao.md).
- As telas R1 a R3: [telas de reversão](../../017-telas-reversao-esvaziamento-e-relatos.md).
- O módulo: [pseudonimização](../../007-modulo-pseudonimizacao.md); a política:
  [reversão](../../../privacidade/007-reversao-da-pseudonimizacao.md).
- O pacote da fase 4, de que este parte: [fase 4](../004-sanitizacao/README.md).
- Os padrões de cada peça: [padrões de peça](../../padroes/README.md).

Ver [regras de escrita](../../../regras-de-escrita.md).
