# 0049 — Cada item tem a chave dele, e uma época a embrulha

> Decisão vigente. 2026-11-01, decisões A, B e C do dono na issue #514.

## Contexto

Esvaziar um item (#73) apaga o conteúdo, mas o byte cifrado continua em cópia do banco, no WAL e na
linha antiga, e a chave de domínio ([0013](0013-chave-de-cifra-por-dominio.md)) abre qualquer um.
Sem uma chave que se possa destruir, esvaziar não é irrecuperável.

## Decisão

**Cada linha de conteúdo protegido tem uma chave aleatória própria, guardada embrulhada no banco,
e o embrulho depende de um segredo de época que vive fora do banco.**

- A chave da linha tem 32 bytes e cifra as colunas protegidas dela no envelope da 0013. Há uma por
  linha do texto do item e uma por linha do mapa de reversão, esta sob a finalidade do mapa.
- O banco guarda a chave cifrada sob HKDF da chave do rótulo
  ([0045](0045-formato-do-rotulo-de-derivacao.md)), com o segredo da época como sal e
  `kek/<época>` como info. A coluna da época guarda o número dela em claro.
- A época é um segredo global de 32 bytes num terceiro arquivo de custódia, `<n>:<base64>` por
  linha, que emenda a [0044](0044-arquivo-de-custodia-da-chave.md). A mais nova embrulha o que se
  grava. O procedimento de rotação a rotaciona todo mês.
- Esvaziar apaga o conteúdo e a chave embrulhada no mesmo comando, e o banco recusa a linha
  esvaziada que ainda guarde chave (a restrição de chave da tabela).
- A partida recusa a linha embrulhada por época que o arquivo não tem, e nomeia a época.
- O pseudonimizado será cifrado com a chave do item: regra da
  [0030](0030-qual-texto-fica-em-claro.md) agora, implementação na fase 5.

## Onde cada segredo vive e por que a destruição não volta

| Segredo | Onde vive | Quem o destrói |
|---|---|---|
| Chave-mestra e rótulos | Arquivos da 0044 e cópia de guarda | Ninguém, por item |
| Época | Terceiro arquivo e a cópia do custodiante | O custodiante, na rotação |
| Chave da linha | Banco, embrulhada pela época | O esvaziamento, na hora |

Cópia, WAL e linha antiga só têm a chave embrulhada, e a época não está no banco: destruída a
antiga, a cópia anterior não abre nem com a chave-mestra, os rótulos e a época nova.

## Limites declarados

- O item esvaziado fica irrecuperável em até 30 dias, na rotação seguinte, e não no ato.
- Backup anterior a uma rotação deixa de restaurar; resto em SSD e snapshot só cai com a época.
- O conteúdo gravado antes das chaves não migra: a 0017 recusa aplicar sobre ele.
- O pseudonimizado fica em claro, também no WAL, até a fase 5.

## Alternativas recusadas

- **Apagar só a linha.** Cópia e WAL guardam o byte, e a chave de domínio o abre.
- **Rotacionar a chave de domínio por item.** Reescreve o acervo a cada remoção.
- **Guardar a época no banco.** Voltaria junto com a cópia que se quer inutilizar.

## Consequências

A rotação mensal vira tarefa de operação, e cada leitura abre a chave da linha antes do conteúdo.

## O que reabre esta decisão

O custodiante não poder destruir a cópia antiga com registro, ou 30 dias não bastarem.
