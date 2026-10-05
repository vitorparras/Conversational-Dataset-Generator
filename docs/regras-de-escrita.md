# Regras de escrita

> Operacional. Vale para todo documento deste repositório.

A verificação automática (`ferramentas/verifica-docs/verificar.py`) impõe a maior parte destas
regras e reprova o pull request que as viole.

## 1. Declare a autoridade

Logo após o título, uma linha de citação diz se o documento vincula. Sem ela a verificação reprova.

```markdown
# 0002 — A stack

> Decisão vigente. 2026-10-04.
```

Vocabulário fechado: `Norma vigente` · `Norma substituída por X` · `Norma revogada` ·
`Decisão vigente` · `Decisão substituída por X` · `Decisão revogada` · `Descrição` · `Histórico` ·
`Operacional`.

`Descrição` descreve o sistema: se divergir do código, o código ganha. `Histórico` nunca é regra,
mesmo que pareça.

## 2. Documento curto

Até **60 linhas**. Decisão, requisito ou política que passa disso está se justificando demais.

Se a vigésima linha ainda não disse o que foi decidido, o documento falhou — por mais correto que
esteja o resto. Documento histórico é exceção: não se divide nem se resume, porque isso falsificaria
o registro.

## 3. Um fato, um lugar

Os demais documentos referenciam; nunca copiam. Duas cópias divergem com o tempo, e a divergência só
aparece quando alguém age sobre a versão errada.

## 4. Nada de dado pessoal, segredo ou caminho de máquina

Nem em exemplo, nem em teste, nem em documentação. Exemplos usam identificadores sintéticos
(`PESSOA_1`, `REMETENTE_A`), nunca nomes plausíveis — um nome inventado pode coincidir com pessoa
real.

É a regra sem margem de discussão.

## 5. Só cite o que existe

Arquivo, caminho, classe ou identificador citado em `código inline` precisa existir. A verificação
confere.

## 6. Nada de fora do repositório

Não cite documento, projeto ou contexto que quem clona não possa consultar. Um argumento que precisa
de referência externa para se sustentar não se sustenta.

## 7. Formatação

Até 100 colunas por linha, exceto tabela, link e bloco de código. Espaços, nunca tab. Sem espaço no
fim da linha. No máximo duas linhas vazias seguidas. Arquivo termina com quebra de linha.

## 8. Nomes de arquivo

`NNN-slug.md`, numeração sequencial nunca reutilizada, slug em minúsculas com hífens, sem acento.
Documento substituído mantém o número e muda a linha de autoridade.

## 9. Idioma

Português brasileiro, com acentuação correta. Identificadores de código em inglês.
