# 032 — Configuração desconhecida impede a inicialização

> Norma vigente. 2026-10-07.

**O sistema recusa iniciar quando a configuração traz chave que ele não reconhece, ou falta chave
que ele exige.**

## Por que existe

Configuração silenciosamente ignorada é a forma mais discreta de desligar uma proteção.

Quem escreve o nome de uma chave com erro de digitação acredita ter configurado. O sistema não
reconhece aquela chave, ignora, e segue com o valor que já tinha. Nada falha, nada avisa — e a
instalação opera com uma garantia a menos, convencida de que a tem.

O risco é maior para o que protege: errar o nome da chave que fixa um limite de suficiência, ou a
que declara a condição do material, deixa valer o que estava antes.

## Critérios de aceitação

**CA-1** — *Dado* um arquivo de configuração com chave não reconhecida, *quando* o sistema iniciar,
*então* ele recusa iniciar e nomeia a chave desconhecida.

**CA-2** — *Dado* uma chave exigida que não foi declarada, *quando* o sistema iniciar, *então*
recusa iniciar e nomeia o que falta — sem assumir valor padrão para o que protege.

**CA-3** — *Dado* um valor fora do conjunto aceito, *quando* for carregado, *então* a recusa diz
qual valor foi encontrado e quais eram aceitos.

**CA-4** — *Dado* que o sistema iniciou, *quando* alguém consultar a configuração em vigor, *então*
ela é apresentada integralmente, com a origem de cada valor.

## O que não exige

Não exige que toda chave seja obrigatória: há valores com padrão legítimo, e a política de cada área
diz quais. O que nenhuma área admite é padrão para limite de proteção.

Não exige recarga sem reinício: mudar configuração que protege é operação deliberada.
