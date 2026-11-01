# 019 — O que o passo floors lê nos arquivos de áreas

> Operacional. 2026-10-29.

O passo `floors` de `tools/verify-code` julga a cobertura de cada área contra o piso da
[014](014-modulos-criticos-e-pisos.md). Este documento é o que ele aceita e reprova nos arquivos de
áreas além do número do piso. A pasta `areas/` continua sendo o único lugar dos pisos.

## Valores

`isento` é o único valor não numérico aceito: a área está declarada e nada se compara. Área medida
fora das áreas reprova; piso ausente não é piso zero. Área com piso e sem linha de código a cobrir
(`0 de 0`)
aparece sem código e não reprova, salvo se a área tem projeto ou pacote e nenhum relatório o leu.

## Pasta e módulo de primeiro nível

Código fora de toda área listada conta na área do projeto ou pacote que o contém, e é por isso que
um módulo novo ali passaria sem que ninguém o nomeasse. Para impedir, toda pasta de primeiro nível
de um projeto de `core/src` e todo módulo de primeiro nível do pacote `language_service` tem arquivo
próprio em `areas/` ou consta no arquivo "demais" do seu projeto ou pacote.

O Motivo do arquivo "demais" lista o que ela cobre depois de `Absorve:`: os nomes entre crases,
separados por vírgula, até o primeiro `;`:

```
Absorve: `Versions`, `Clock`; portas e metadado, sem material
```

O passo reprova, nomeando o caminho ou o nome:

- a pasta ou o módulo sem arquivo próprio que o arquivo "demais" não lista;
- o nome listado em `Absorve:` que já não existe no disco, porque a pasta foi renomeada ou apagada;
- o projeto ou pacote que nenhum arquivo possui.

## O que fica fora do julgamento

Ficam de fora, e contam na área do projeto ou pacote que os contém:

- `bin`, `obj` e a pasta sem arquivo de código: não são código escrito;
- o arquivo na raiz de um projeto C#: o módulo é a pasta, e o projeto já tem área;
- os arquivos `__init__` e `__main__` do serviço: são a entrada do pacote, não um módulo de domínio.
