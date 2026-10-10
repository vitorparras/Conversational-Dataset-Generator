# 0039 — Idioma: código em inglês, texto em português

> Decisão vigente. 2026-10-10.

## Contexto

A regra 9 de [regras de escrita](../regras-de-escrita.md) fixa português para os documentos e
inglês para identificadores de código, e nada diz sobre nome de pasta, de arquivo, de workflow ou
de objeto do banco. O verificador de documentação nasceu em português — pasta, arquivo e
identificadores — e contraria a regra que ele próprio impõe. Com o código a entrar, a mistura se
multiplicaria a cada arquivo novo, e nenhuma fronteira diria onde um idioma termina.

## Decisão

**O que uma ferramenta resolve é inglês; o que uma pessoa lê é português.**

| Inglês | Português |
|---|---|
| Pastas de código (`core/`, `language-service/`, `tools/`), arquivos, projetos, namespaces, identificadores | `docs/` inteira: pastas, nomes de arquivo, conteúdo |
| Objetos do banco, chaves de configuração, campos de log, opções de linha de comando | Prosa de issue, pull request, revisão e descrição de commit (o tipo do commit segue inglês) |
| Nome de arquivo e identificador de job de workflow | O nome exibido do workflow; rótulos e marcos |
| Nome de teste | Mensagem que o sistema mostra a quem opera ou anota; frase de uma linha de log |

A fronteira é a pasta `docs/`: dentro, português; fora, inglês. O que já existe fora dela em
português é renomeado antes do primeiro código, enquanto nada o cita.

## Recusados

| Opção | Por quê |
|---|---|
| Tudo em português, inclusive código | Biblioteca, plataforma e erro de compilador são em inglês; cada linha ficaria bilíngue |
| Tudo em inglês, inclusive documentos | Quem decide, anota e opera lê em português, e o nome do arquivo é parte do título do documento |
| Pasta de código em português, conteúdo em inglês | É a mistura atual com uma regra a mais para lembrar e nenhuma fronteira que se enxergue |

## Consequências

Renomear a pasta de ferramentas e seus identificadores toca workflow, rotulagem automática,
donos de código e todo documento que cita o caminho — e o próprio verificador aponta citação
esquecida, porque recusa caminho que não existe.

Uma linha de log passa a ter campo em inglês e frase em português, de propósito: o campo é
filtrado por ferramenta, a frase é lida por pessoa.

O software é de código aberto, e quem o clonar de fora lê código monolíngue; a documentação
continua exigindo português, e isso é aceito.

## O que reabre

Se o projeto passar a ter quem opere ou anote sem ler português, a coluna da direita perde o
leitor e a decisão perde objeto.
