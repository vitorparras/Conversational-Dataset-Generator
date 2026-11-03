# 028 — Criar a primeira pessoa

> Operacional. 2026-11-03.

A primeira pessoa da instalação nasce de um comando só, `./Core.Host create-person --name <nome>
--function <função>`, rodado pelo operador depois da migração ([016](016-migracao-do-esquema.md))
e antes de qualquer entrada. Usa a **credencial da aplicação** do banco, a mesma com que o
servidor roda, e **nunca a de dono** ([010](010-credencial-de-dono-do-banco.md)), que só migra:
com a de dono o comando recusa e não grava nada. A senha vem pela **entrada padrão**, nunca por
argumento. Não há endpoint HTTP para isso.

## Como rodar

1. **Tenha a configuração do `migrate`** ([016](016-migracao-do-esquema.md)), mas com o usuário e
   a senha do banco da **aplicação**, e o fator de trabalho da derivação de senha
   (`Passwords:WorkFactor`, [decisão 0047](../decisoes/0047-fator-de-trabalho-das-senhas.md)).
2. **Envie a senha sem deixá-la em argumento nem no histórico do terminal.** Em `bash`:

   ```
   read -rs SENHA; printf '%s\n' "$SENHA" | ./Core.Host create-person --name <nome> --function "Administração técnica"; unset SENHA
   ```

   O `printf` é interno do shell, então a senha não aparece na lista de processos. A senha é a
   primeira linha da entrada; a quebra de linha no fim não faz parte dela.
3. **Guarde a senha** onde a instalação guarda segredos de operação
   ([decisão 0029](../decisoes/0029-identidade-e-sessao.md)).

A função é uma das seis da política de acesso, escrita como ela: Anotação, Arbitragem, Curadoria
do acervo, Avaliação, Auditoria ou Administração técnica. Quem abre a instalação é, em geral, a
Administração técnica.

## O que o comando faz

Só cria quando o cadastro **não tem ninguém**: a conferência e a gravação são uma instrução numa
transação serializável, e de dois comandos ao mesmo tempo um é recusado. Grava a pessoa, ativa,
com o registro da mudança e o evento `PersonCreated` na cadeia; em seguida define a senha e grava
`PersonPasswordSet`. Os dois eventos têm o **sistema** como autor e o ato administrativo como
motivo. O nome e a senha nunca entram na cadeia nem na saída. Com o cadastro ocupado, o comando
recusa: as pessoas seguintes entram pelas rotas com sessão ([029](029-pessoas-pela-rota.md)).

## Códigos de saída

- **0**: pessoa criada, com a senha definida; a saída diz o id.
- **21**: o cadastro já tem alguém. Nada foi gravado.
- **22**: recusa sem gravar: nome vazio ou com espaço nas pontas, função fora da lista, senha
  vazia, credencial de dono, banco sem o cadastro (rode a migração) ou cadeia que recusou.
- **23**: o banco não respondeu, ou a pessoa foi criada e a senha não foi definida; a mensagem
  diz qual dos dois.
- **78**: faltou o nome ou a função, a senha veio por argumento, o fator de trabalho falta ou não
  é aceito, ou a configuração do banco está incompleta.

**Código 23 com pessoa criada**: o comando não roda de novo. A senha dela se define pela rota de
reposição ([029](029-pessoas-pela-rota.md)); se ela for a única pessoa, restaure a instalação a
partir da cópia ([006](006-restauracao.md)) e repita.

## Depois

Uma vez que a pessoa existe, o que ela grava na cadeia leva o nome dela como autor e **continua
dela depois de desativada**: desativar não altera nem apaga evento, e a verificação da cadeia
segue passando ([007](007-verificacao-da-custodia.md)).
