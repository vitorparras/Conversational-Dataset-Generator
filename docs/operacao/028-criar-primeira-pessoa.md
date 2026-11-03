# 028 — Criar a primeira pessoa

> Operacional. 2026-11-03.

A primeira pessoa da instalação nasce de um comando só, `./Core.Host create-person --name <nome>
--function <função>`, rodado pelo operador depois da migração ([016](016-migracao-do-esquema.md)) e
antes de qualquer entrada. Ele usa a **credencial da aplicação** do banco, a mesma com que o servidor
roda, e **nunca a de dono** ([010](010-credencial-de-dono-do-banco.md)), que só migra: com a de
dono o comando recusa e não grava nada. A senha vem pela **entrada padrão**, nunca por argumento.
Não há endpoint HTTP para a primeira pessoa; as seguintes entram pelas rotas com sessão, mais abaixo.

## Como rodar

1. **Tenha a configuração do `migrate`** ([016](016-migracao-do-esquema.md)), mas com o usuário e a
   senha do banco da **aplicação**, e o fator de trabalho da derivação de senha
   (`Passwords:WorkFactor`, [decisão 0047](../decisoes/0047-fator-de-trabalho-das-senhas.md)).
2. **Envie a senha sem deixá-la em argumento nem no histórico do terminal.** Em `bash`:

   ```
   read -rs SENHA; printf '%s\n' "$SENHA" | ./Core.Host create-person --name <nome> --function "Administração técnica"; unset SENHA
   ```

   O `printf` é interno do shell, então a senha não aparece na lista de processos. A senha é a
   primeira linha da entrada; quebra de linha no fim não faz parte dela.
3. **Guarde a senha** onde a instalação guarda segredos de operação. O sistema só guarda o que a
   derivação deixou dela ([decisão 0029](../decisoes/0029-identidade-e-sessao.md)).

A função é uma das seis da política de acesso, escrita exatamente como ela: Anotação, Arbitragem,
Curadoria do acervo, Avaliação, Auditoria ou Administração técnica. Quem abre a instalação é, em
geral, a Administração técnica.

## O que o comando faz

Numa só chamada, grava a pessoa, ativa, com o registro da mudança, e o evento `person_created` na
cadeia de custódia; em seguida define a senha e grava `person_password_set`. Os dois eventos têm o
**sistema** como autor, porque ainda não existe pessoa para assinar. O nome e a senha nunca entram
na cadeia nem na saída.

Só cria quando o cadastro **não tem ninguém**. A conferência e a gravação são uma só instrução
numa transação serializável: dois comandos ao mesmo tempo fazem uma pessoa, e o outro é recusado.
Com o cadastro ocupado, o comando recusa e diz que as pessoas seguintes entram pela rota com
sessão, não por ele.

## Códigos de saída

- **0**: pessoa criada, com a senha definida; a saída diz o id.
- **21**: o cadastro já tem alguém. Nada foi gravado.
- **22**: recusa que não grava nada: o nome vazio ou com espaço nas pontas, a função fora da lista
  ou a senha vazia (a mensagem nomeia cada campo), a credencial de dono, o banco sem o cadastro
  (rode a migração antes) ou a cadeia que recusou o evento.
- **23**: o banco não respondeu, ou a pessoa foi criada e a senha não foi definida; a mensagem diz
  qual dos dois.
- **78**: faltou o nome ou a função, a senha veio por argumento, o fator de trabalho falta ou não é
  aceito, ou a configuração do banco está incompleta.

**Código 23 com pessoa criada**: o cadastro já não está vazio e o comando não roda de novo. A senha dessa pessoa
se define pela rota `PUT /people/{id}/password`, abaixo, com a sessão de outra administradora; se ela
for a única pessoa, restaure a instalação a partir da cópia ([006](006-restauracao.md)) e repita.

## As pessoas seguintes: rotas com sessão

Depois da primeira, as pessoas entram, saem e trocam de senha por três rotas que pedem sessão.
Só quem tem a função **Administração técnica**
as usa; a função é conferida dentro da rota, a cada pedido, até a autorização chegar na fase 6.
O autor de cada evento (`person_created`, `person_deactivated`, `person_password_set`) é a pessoa da
sessão, nunca o sistema.

| Rota | O que faz | Corpo |
|---|---|---|
| `POST /people` | cria a pessoa e define a senha inicial | `{"name","function","password"}` |
| `POST /people/{id}/deactivate` | desativa a pessoa | `{}` |
| `PUT /people/{id}/password` | repõe a senha | `{"password"}` |

- **401**: sem sessão. **403**: sessão de quem não tem a função; nada é gravado.
- **400**: o campo fora da regra vem nomeado em `field` (`nome`, `função` ou `senha`).
  **409**: nome repetido, pessoa já desativada ou a **última pessoa ativa com Administração técnica**,
  que não se desativa, porque a instalação ficaria sem quem cria ou repõe pessoas.
- **500** em `POST /people`: a pessoa foi criada e a senha não; a resposta diz o id, e a senha se
  define pela rota `PUT`. É a saída do código 23 do comando quando já há mais de uma pessoa.
- A pessoa desativada perde na hora as sessões abertas, não entra mais, e o que ela assinou na
  cadeia continua dela.
- A senha nunca aparece em resposta, log ou evento.

## O que fica registrado

Os eventos da criação e da senha têm o sistema como autor e o ato administrativo como motivo. Uma
vez que a pessoa existe, o que ela grava na cadeia leva o nome dela como autor, e **continua dela
depois de desativada**: desativar não altera nem apaga evento algum, e a verificação da cadeia
segue passando ([007](007-verificacao-da-custodia.md)).
