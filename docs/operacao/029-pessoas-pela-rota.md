# 029 — Pessoas seguintes: rotas com sessão

> Operacional. 2026-11-03.

Depois da primeira pessoa ([028](028-criar-primeira-pessoa.md)), as demais entram, saem e trocam
de senha por três rotas que pedem sessão. Só quem tem a função **Administração técnica** as usa:
a guarda de tela confere a função a cada pedido (política 006) e responde 404 a quem não a tem. O autor de
cada evento (`PersonCreated`, `PersonDeactivated`, `PersonPasswordSet`) é a pessoa da sessão,
nunca o sistema.

| Rota | O que faz | Corpo |
|---|---|---|
| `POST /people` | cria a pessoa e define a senha inicial | `{"name","function","password"}` |
| `POST /people/{id}/deactivate` | desativa a pessoa | `{}` |
| `PUT /people/{id}/password` | repõe a senha | `{"password"}` |

## O que cada resposta diz

- **401**: sem sessão. **404**: sessão de quem não tem a função, como a de uma rota que não existe; nada é gravado. **403** só se a função for retirada entre a guarda e o ato.
- **400**: o campo fora da regra vem nomeado em `field` (`nome`, `função` ou `senha`).
- **409**: nome repetido, pessoa já desativada ou a **última pessoa ativa com Administração
  técnica**, que não se desativa, porque a instalação ficaria sem quem cria ou repõe pessoas.
- **500** em `POST /people`: a pessoa foi criada e a senha não; a resposta diz o id, e a senha se
  define pela rota `PUT`. É a saída do código 23 do comando quando já há mais de uma pessoa.

## O que fica garantido

- A pessoa desativada perde na hora as sessões abertas, não entra mais, e o que ela assinou na
  cadeia continua dela: desativar não altera nem apaga evento, e a verificação da cadeia segue
  passando ([007](007-verificacao-da-custodia.md)).
- Repor a senha de uma pessoa encerra as sessões que ela tinha abertas.
- A senha nunca aparece em resposta, log ou evento.
