# 006 — Acesso a material sensível

> Norma vigente. 2026-10-04.

Ninguém alcança nada por omissão. Pessoa cadastrada e sem concessão explícita vê lista vazia, não
tela de erro — tela de erro informa que existe material atrás dela. A concessão é por domínio e por
função, nunca global: quem anota num domínio não alcança outro.

| Função | O que alcança |
|---|---|
| Anotação | Texto pseudonimizado dos domínios concedidos; grava anotação |
| Arbitragem | O mesmo, mais as anotações divergentes do item e a resolução |
| Curadoria do acervo | O mesmo, mais texto bruto e sanitizado do próprio domínio, ingestão, aprovação de lote e exportação |
| Avaliação | Contagens, métricas e estado de campanha. **Nenhum conteúdo** |
| Auditoria | Registro de eventos e de acesso. **Nenhum conteúdo** |
| Administração técnica | Operação do sistema. **Nenhum conteúdo** |

**Avaliação e auditoria não se confundem:** a auditoria alcança quem fez o quê, a avaliação alcança
o que o trabalho produziu. Sem a de avaliação, mostrar o andamento exigiria conceder acesso ao texto
([requisito 029](../requisitos/029-metrica-sem-texto.md)).

Reversão de pseudônimo não é alcance de função nenhuma: é ato autorizado, tratado em
[reversão](007-reversao-da-pseudonimizacao.md).

## Quem impõe

Duas camadas, e importa saber qual delas segura.

A aplicação avalia a política antes do código da rota
([0002](../decisoes/0002-stack.md)): é a camada que dá a resposta certa e esconde o que não cabe na
tela. O banco impõe a mesma regra por política de linha com `FORCE`
([0006](../decisoes/0006-dominios-coexistem.md)): é a camada que segura quando a primeira está
errada — consulta sem o filtro não devolve material de outro domínio, não devolve nada.

## Onde isso não alcança

Quem administra o banco lê o que a função não concede, e a política limita o que esse acesso rende:
a chave não está no banco ([0003](../decisoes/0003-cifra-em-repouso.md)), então quem o alcança obtém
o texto **pseudonimizado** e bytes — não o bruto, não o sanitizado, não o mapa de reversão
([0030](../decisoes/0030-qual-texto-fica-em-claro.md)).

Isso só continua verdade enquanto a guarda da chave e a credencial do banco estiverem em mãos
diferentes. É separação de pessoas, não mecanismo, e é a condição que faz a cifra valer na prática.

## Termo e finalidade

Antes da primeira concessão, a pessoa assume por escrito que não copia material para fora, não
tenta reidentificar ninguém e relata incidente ao perceber. Nada técnico impõe isso: o sistema
registra a concessão com referência ao termo, o que torna a falta **detectável, não impedida**.

Toda concessão declara uma finalidade em palavras, e acesso fora dela é violação mesmo quando
tecnicamente permitido. Fica no registro a finalidade vigente no momento do acesso — não a atual,
que pode ter sido reescrita depois.

## O que fica registrado

Quem, quando, qual item, sob qual finalidade. Leitura na tela de anotação também entra, e consulta
de métrica igualmente: sem isso não se responde quem viu o quê. O registro é gravado por extensão do
servidor, de modo que a aplicação não consegue omitir o próprio acesso, e é acrescentável como o
resto da cadeia.
