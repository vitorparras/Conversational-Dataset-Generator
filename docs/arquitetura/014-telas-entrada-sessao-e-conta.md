# 014 — Telas de entrada, sessão e conta

> Descrição. 2026-11-04.
> Estado no código: nenhuma tela existe; as rotas estão em `core/src/Core.Host/Endpoints/`.

Telas que toda pessoa usa, qualquer que seja a função. Seguem os padrões da
[0051](../decisoes/0051-navegacao-gestao-e-relatorio.md) e o [índice](013-mapa-de-telas.md).

| ID | Tela | Função | Mostra | Faz | Fase | Issue |
|---|---|---|---|---|---|---|
| E1 | Entrada | Toda pessoa | Nome e senha; a mesma recusa para nome inexistente e senha errada; retardo; "cadastro vazio" com o passo no servidor | Envia a entrada | 3 | #592 |
| E2 | Saída | Toda pessoa | Botão no cabeçalho | Encerra a sessão no servidor | 3 | #593 |
| E3 | Sessão vencida | Toda pessoa | Aviso e retorno à entrada, depois ao ponto de origem, sem perder o submetido | Reentra e retoma | 7 | #493 |
| E4 | Cabeçalho e navegação | Toda pessoa | Só o que a função e o domínio concedem; lista vazia, não erro; seletor de domínio | Navega | 3 | #594 |
| E5 | Avisos | Toda pessoa | Concessão a vencer, senha reposta que encerrou sessões, domínio somente leitura | Marca como lido | 3 | #595 |
| C1 | Minha conta | Toda pessoa | Função, concessões por domínio, finalidade e prazo, vencidas | Só leitura | 3 | #596 |
| C2 | Trocar a própria senha | Toda pessoa | A senha atual e a nova | Troca e encerra as outras sessões | 3 | #597 |
| C3 | Minhas sessões | Toda pessoa | Sessões abertas: início e última atividade | Encerra uma ou todas | 3 | #598 |

## Observações

- A entrada nasce na fase 3 e o retorno ao ponto de origem só se prova com uma tela que guarde
  trabalho: a #493 fica na fase 7 e a E1 declara que o retorno é dela.
- A recusa da E1 é a mesma para toda falha (requisito 017) e conta no retardo da
  [0048](../decisoes/0048-retardo-da-entrada.md); a sessão é a da
  [0029](../decisoes/0029-identidade-e-sessao.md).
- A rota de entrada e a de saída já existem (#70, #71, #495); o que falta é a tela.
- A troca da própria senha (C2) não estava documentada: a 0029 previa só a reposição
  administrativa, que é a tela A2 de [015](015-telas-administracao-tecnica.md).
- A lista de avisos vem vazia para quem não tem concessão, nunca como erro.
- Acessibilidade: #591. Teste de navegador: #590.
