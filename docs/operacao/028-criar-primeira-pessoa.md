# 028 — Criar a primeira pessoa

> Operacional. 2026-11-03.

A primeira pessoa da instalação nasce de `./Core.Host create-person --name <nome> --function
<função>`, rodado depois da migração ([016](016-migracao-do-esquema.md)) e antes de qualquer
entrada. Usa a **credencial da aplicação** do banco, a do servidor, e **nunca a de dono**
([010](010-credencial-de-dono-do-banco.md)): com a de dono o comando recusa e não grava nada. A
senha vem pela **entrada padrão**, nunca por argumento. Não há endpoint HTTP para isso.

## Como rodar

1. **Tenha a configuração do `migrate`** ([016](016-migracao-do-esquema.md)), com usuário e senha
   do banco da **aplicação** e o fator de trabalho da derivação de senha (`Passwords:WorkFactor`, [decisão 0047](../decisoes/0047-fator-de-trabalho-das-senhas.md)), e
   `--Installation:Kind` (`real` ou `demonstracao`): sem ele vale `real`, e um fator abaixo do piso
   de 600000 sai com 78.
2. **Envie a senha sem deixá-la em argumento nem no histórico do terminal.** Em `bash`:

   ```
   read -rs SENHA; printf '%s\n' "$SENHA" | ./Core.Host create-person --name <nome> --function "Administração técnica"; unset SENHA
   ```

   O `printf` é interno do shell, então a senha não aparece na lista de processos. A senha é a
   primeira linha da entrada, sem a quebra de linha final.
3. **Guarde a senha** onde a instalação guarda segredos de operação ([decisão 0029](../decisoes/0029-identidade-e-sessao.md)).

Num contêiner operado de fora não há terminal para o `read`: a forma de lá está em
[033](033-primeira-pessoa-numa-maquina-operada-de-fora.md).

A função é uma das seis da política de acesso, escrita como ela: Anotação, Arbitragem, Curadoria
do acervo, Avaliação, Auditoria ou Administração técnica (em geral, a de quem abre a instalação).

## O que o comando faz

Só cria quando o cadastro **não tem ninguém**, numa instrução em transação serializável: de dois
comandos ao mesmo tempo um é recusado, e as pessoas seguintes entram pelas rotas com sessão ([029](029-pessoas-pela-rota.md)). Grava a
pessoa, ativa, com o registro da mudança e o evento `PersonCreated` na cadeia; define a senha e
grava `PersonPasswordSet`. Os dois eventos têm o **sistema** como autor e o ato administrativo
como motivo. O nome e a senha nunca entram na cadeia nem na saída.

## Códigos de saída

- **0**: pessoa criada, com a senha definida; a saída diz o id.
- **21**: o cadastro já tem alguém. Nada foi gravado.
- **22**: recusa sem gravar: nome vazio ou com espaço nas pontas, função fora da lista, senha
  vazia, credencial de dono, banco sem o cadastro (rode a migração) ou cadeia que recusou.
- **23**: o banco não respondeu, ou a pessoa foi criada e a senha não foi definida; a mensagem
  diz qual dos dois. Com pessoa criada o comando não roda de novo: a senha dela se define pela
  rota de reposição ([029](029-pessoas-pela-rota.md)); se ela for a única pessoa, restaure a
  instalação a partir da cópia ([006](006-restauracao.md)) e repita.
- **78**: faltou o nome ou a função, a senha veio por argumento, o fator de trabalho falta ou não
  é aceito, ou a configuração do banco está incompleta.

## Depois

O que a pessoa grava na cadeia leva o nome dela como autor e **continua dela depois de
desativada**: desativar não altera nem apaga evento, e a verificação da cadeia segue passando ([007](007-verificacao-da-custodia.md)).
