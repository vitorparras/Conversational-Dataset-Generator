# 0052 — Montagem das telas e base do sistema de design

> Decisão vigente. 2026-11-06, issues #590, #591 e #658.

## Contexto

A [0010](0010-tecnologia-da-interface.md) fixou telas no servidor com camada de cliente fina, e a
[0051](0051-navegacao-gestao-e-relatorio.md), os padrões e o nível WCAG 2.1 AA. Faltava dizer como
uma tela é montada e servida, e de onde vem o visual. O dono pediu tudo componentizado,
usabilidade primeiro e vidro translúcido sem sair do AA. O publish desliga os static web assets.

## Decisão

**Monte cada tela com os componentes Razor de `core/src/Core.Host/Screens/`, renderizados no
servidor e devolvidos pela rota mínima, com todo valor visual tirado dos tokens.**

- A rota devolve `ScreenResult.Of`: a página base inteira, ou só a região pedida pelo cabeçalho
  `X-Region`. Não há modo interativo nem o script do Blazor; a política de acesso fica na rota.
- A camada de cliente é `client.js`, embutido no executável e servido por rota própria, sem
  biblioteca de terceiros. Faz três coisas: troca a região declarada (`data-swap`), põe o foco no
  elemento que a região declara (`data-focus`) e dispara o atalho declarado (`data-shortcut`),
  suprimido em campo de texto. Sem script, link e formulário carregam a página inteira.
- `tokens.css` é o único lugar de cor, tipo, espaço, raio, sombra, vidro e foco; `screens.css`
  tem uma seção por componente e só nomeia tokens. Os componentes são página base, cabeçalho,
  botão, campo com rótulo e erro, aviso de estado, painel de vidro e lista paginada. Peça nova
  entra como componente, nunca como marcação copiada.
- O vidro é só CSS (`backdrop-filter`), no cabeçalho e no painel. Vira superfície sólida sob
  `prefers-reduced-transparency`, `prefers-contrast: more` e no navegador que não desfoca. O
  contraste é medido sobre o pior ponto do fundo real e anotado na lista de revisão manual de
  acessibilidade.
- A fonte é a do sistema operacional: nada se baixa e nada vai embutido.
- O catálogo dos componentes é tela do host de teste em `core/tests/Core.Host.Tests.Browser/`.
- O código que o Razor gera para `Screens/` é a única exceção às regras de build: desliga os
  avisos 1591 e 1998 e escapa do analisador, então o componente só decide a apresentação.

## Alternativas recusadas

| Opção | Por quê |
|---|---|
| htmx copiado ao repositório | Código de fora sem auditoria de dependência na CI, para três funções |
| Blazor interativo (servidor ou WebAssembly) | Estado da tela no cliente ou no circuito, que a 0010 recusa |
| Isolamento de CSS do Razor | Depende dos static web assets, que o publish desliga |
| Fonte da web, baixada ou embutida | Pedido para fora numa instalação local, ou peso e licença no executável |
| Biblioteca de CSS (Bootstrap, Tailwind) | Etapa de build em Node ou código de fora; os valores deixam de ser nossos |
| Vidro em todo controle | O contraste de cada controle passaria a depender do fundo |

## Consequências

Toda tela herda salto para o conteúdo, cabeçalho, foco visível e tema escuro da página base.
Mudar um token muda toda tela de uma vez. O que piora: o desfoque custa processamento em máquina
fraca; a fonte muda de sistema para sistema, e com ela a métrica do texto; trocar um token de
fundo ou de vidro obriga a refazer a medida de contraste da 022; e o executável publicado sai sem
PDB, porque o código gerado leva o caminho absoluto de cada componente e tiraria a construção
reproduzível, então a pilha de exceção perde o número da linha. Sem script, a troca de região
vira página inteira: funciona, mais devagar.

## O que reabre esta decisão

Regra ou estado no cliente reabre a 0010. Vidro sem AA sobre um fundo que uma tela precise, ou
uma quarta função na camada de cliente, refaz esta decisão por escrito.
