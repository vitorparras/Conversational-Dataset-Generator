# 022 — Revisão manual de acessibilidade

> Descrição. 2026-11-06.
> Estado no código: a verificação automática é `core/tests/Core.Tests.Browser/Accessibility/`.

O teste de navegador roda o axe com as regras A e AA da WCAG 2.1 em toda tela
([0051](../decisoes/0051-navegacao-gestao-e-relatorio.md)). O axe não mede contraste sobre vidro
nem julga ordem e texto. Quem revisa uma tela nova percorre esta lista e anota o resultado no PR.

## Lista

| Item | Como se confere |
|---|---|
| Contraste | Só tokens de `tokens.css`; texto 4,5:1 e borda, foco e ícone 3:1 sobre o vidro composto |
| Ordem de leitura | Sem CSS, a página lê na ordem em que se vê; títulos sem salto de nível |
| Rótulos | Todo controle tem rótulo visível igual ao nome acessível; atalho em `aria-keyshortcuts` |
| Erro | Diz o que fazer, está ligado ao campo e aparece junto dele, não só no topo |
| Foco | Visível em todo controle, nunca escondido pelo cabeçalho fixo; vai à região nova na troca |
| Sem cor | Estado lido por palavra e forma: o aviso tem palavra e marca, o campo inválido, borda dupla |
| Ampliação | A 200% e a 320 px de largura, nada se corta e não há rolagem horizontal |
| Preferências | Escuro, transparência reduzida, contraste alto, cores forçadas e movimento reduzido |
| Sem script | Link e formulário funcionam com a página inteira |

## Contraste medido

Medido em 2026-11-06 (#658) sobre o pior ponto do fundo: o vidro composto com sua opacidade
sobre a tinta mais escura do fundo, no tema claro, e a mais clara, no escuro. Mudou um token de
fundo, vidro ou texto, refaça a medida.

| Par | Claro | Escuro |
|---|---|---|
| Texto sobre vidro | 14,6 | 12,7 |
| Texto secundário sobre vidro | 7,1 | 7,0 |
| Link sobre fundo | 6,5 | 7,1 |
| Botão principal | 8,8 | 10,2 |
| Botão de perigo | 7,1 | 9,6 |
| Erro de campo sobre vidro | 6,5 | 7,5 |
| Marca do aviso sobre sua superfície (menor dos quatro) | 6,1 | 8,6 |
| Borda do campo sobre vidro (3:1) | 4,0 | 4,3 |
| Foco sobre fundo (3:1) | 4,0 | 6,4 |
