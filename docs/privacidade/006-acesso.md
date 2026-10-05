# 006 — Acesso a material sensível

> Norma vigente. 2026-10-04.

Ninguém alcança nada por omissão. Pessoa cadastrada e sem concessão explícita vê lista vazia, não
tela de erro — e a diferença importa, porque tela de erro informa que existe material atrás dela.

A concessão é por domínio e por função, nunca global. Quem anota num domínio não alcança outro.

| Função | O que alcança |
|---|---|
| Anotação | Texto sanitizado dos domínios concedidos; grava anotação |
| Arbitragem | O mesmo, mais as anotações divergentes do item e a resolução |
| Curadoria do acervo | O mesmo, mais texto bruto do próprio domínio, ingestão, aprovação de lote e exportação |
| Auditoria | Registro de eventos e de acesso. **Nenhum conteúdo** |
| Administração técnica | Operação do sistema. **Nenhum conteúdo** |

Reversão de pseudônimo não é alcance de função nenhuma: é ato autorizado, tratado em
[reversão](007-reversao-da-pseudonimizacao.md).

## Quem impõe

Duas camadas, e importa saber qual delas segura.

A aplicação avalia política de autorização antes do código da rota, como a
[decisão 0002](../decisoes/0002-stack.md) prevê. É a camada que dá a resposta certa e esconde o que
não cabe na tela.

O banco impõe a mesma regra por política de linha com `FORCE`, pela
[decisão 0006](../decisoes/0006-dominios-coexistem.md). É a camada que segura quando a primeira
está errada: consulta sem o filtro não devolve material de outro domínio — não devolve nada.

## Onde isso não alcança

Quem administra o banco lê o que a função não concede. A política não finge o contrário; ela limita
o que esse acesso rende. Pela [decisão 0003](../decisoes/0003-cifra-em-repouso.md) a chave não está
no banco, então quem alcança o banco obtém texto sanitizado e bytes — não texto bruto, não o mapa
de reversão.

Isso só continua verdade enquanto a guarda da chave e a credencial do banco estiverem em mãos
diferentes. É separação de pessoas, não mecanismo, e é a condição que faz a cifra valer na prática.

## Termo e finalidade

Antes da primeira concessão, a pessoa assume por escrito que não copia material para fora do
sistema, não tenta reidentificar ninguém e relata incidente ao perceber. Nada técnico impõe isso: o
sistema registra a concessão com a referência ao termo assumido, o que torna a falta detectável, não
impedida — a distância entre uma proteção que o banco recusa e uma que depende de alguém olhar.

Toda concessão declara uma finalidade em palavras. Acesso fora da finalidade declarada é violação
mesmo quando tecnicamente permitido, e é a finalidade vigente no momento do acesso que fica no
registro — não a atual, que pode ter sido reescrita depois.

## O que fica registrado

Quem, quando, qual item, sob qual finalidade. Leitura de texto sanitizado na tela de anotação
também entra: sem isso não se responde quem viu o quê, que é parte da pergunta que a custódia
existe para responder. O registro é gravado por extensão do servidor, de modo que a aplicação não
consegue omitir o próprio acesso, e é acrescentável como o resto da cadeia.
