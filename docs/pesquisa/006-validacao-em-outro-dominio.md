# 006 — Validação do método em outro domínio

> Descrição. 2026-10-04.

O projeto afirma que o método de construir uma base anotada se transfere para outro domínio. A
afirmação só se sustenta com um segundo caso concreto, rodado de ponta a ponta — nunca com argumento
sobre flexibilidade, como a [decisão 0001](../decisoes/0001-as-duas-contribuicoes.md) já estabelece.
"Funcionar" tem quatro significados observáveis, e nenhum deles é o código executar sem erro.

## O que precisa ser verdade

1. **O domínio novo entra sem mudar código.** A taxonomia dele carrega do arquivo próprio, o
   material cabe numa das formas de unidade de análise — ou a adaptação e a perda estão declaradas
   —, e nenhuma validação o reprova por não ter categoria que ali não existe
2. **A anotação é mensurável com as mesmas medidas.** Nos mesmos escopos e com os mesmos
   denominadores, lado a lado com o primeiro domínio, como o requisito de
   [concordância comparável](../requisitos/013-concordancia-comparavel.md) já exige. O valor pode
   ser mais baixo: exige-se que seja calculável e comparável, não que seja igual
3. **A sanitização se comporta.** A verificação de resíduo passa sobre material cujas formas de dado
   pessoal são outras. Método que só funciona onde as formas foram antecipadas não foi validado
4. **A exportação distingue.** Cada artefato declara seu domínio e sua taxonomia, e os dois datasets
   não se confundem depois de publicados

## Como se mede

Pelo que a passagem custou, não pela impressão de quem a fez: quantas mudanças de código foram
necessárias fora de configuração — zero é a expectativa, e cada exceção é registrada com o motivo
—, quais medidas saíram em cada domínio, e quais divergências exigiram decisão nova.

## O que não conta como validação

| Aparência | Por que não vale |
|---|---|
| Material do domínio novo adaptado até parecer o do primeiro | Mede a adaptação, não o método |
| Categorias do novo acrescentadas à taxonomia do primeiro | É um domínio com mais rótulos |
| Comparação feita à mão, fora da ferramenta | O que não é rotina não se repete |

A terceira é a mais fácil de deixar passar, e é a que a
[decisão 0006](../decisoes/0006-dominios-coexistem.md) já antecipa: comparação que vira
procedimento manual deixa de ser feita.
