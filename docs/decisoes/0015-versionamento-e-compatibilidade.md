# 0015 — Versionamento e compatibilidade

> Decisão vigente. 2026-10-04.

## Contexto

Um dataset exportado é lido depois, por quem não tem este sistema. A decisão 0004 faz a taxonomia
vir de arquivo, a 0006 manda a exportação declarar o recorte, a 0011 versiona o contrato com o
serviço de linguagem. Falta a regra que amarra as três: sob quais versões um artefato foi
produzido, e como ele continua interpretável quando o sistema que o produziu já mudou.

## Decisão

**Nada sai do sistema sem dizer sob quais versões foi produzido, e o artefato é autossuficiente.**

| O que versiona | Como | Onde aparece |
|---|---|---|
| Taxonomia | Número declarado no arquivo, por domínio | Em cada anotação gravada e no artefato |
| Pipeline | Versão do conjunto de etapas, mais a versão do modelo | Em cada material processado |
| Formato de exportação | Versão do esquema | No artefato, antes de qualquer dado |

Autossuficiente significa que o artefato **carrega a taxonomia inteira**, com a definição de cada
categoria, e não um ponteiro para ela. Quem recebe o dataset não tem o repositório, e "taxonomia 3"
não diz nada a quem não pode consultar o que isso era: interpretabilidade futura não pode depender
de acesso ao sistema que produziu o artefato.

O número da taxonomia é declarado, mas não confiado. O sistema registra a impressão digital do
arquivo ao lado do número e recusa subir se o conteúdo mudou sem o número mudar — esquecer de
incrementar é o erro que torna duas taxonomias diferentes indistinguíveis.

Para o formato de exportação vale a regra usual, nos dois sentidos: acrescentar campo muda a versão
menor, e o leitor antigo ignora o que não conhece; remover campo, ou mudar o que ele significa, muda
a maior, e o leitor antigo deve recusar o artefato em vez de adivinhar. O sistema lê o cabeçalho de
toda versão que ele mesmo já produziu, para poder conferir a integridade de artefato antigo.

## Recusados

| Opção | Por quê |
|---|---|
| Artefato apontando para a taxonomia no repositório | O dataset publicado deixa de ser interpretável sem acesso ao repositório, que pode nem estar acessível |
| Impressão digital como única versão | Não ordena nem se cita: "mudou" não é o mesmo que "mudou pela terceira vez" |
| Versão única para o sistema todo | Mudança na sanitização passaria a invalidar comparação de anotações que ela não tocou |
| Sem versão, confiando na data da exportação | Data não diz o que mudou, e dois artefatos do mesmo dia podem ter saído de pipelines diferentes |

## Consequências

O artefato repete a taxonomia a cada exportação, e fica maior. É cópia deliberada: a regra de um
fato num só lugar vale dentro do repositório, e o artefato é recorte congelado, não fonte.

Cada alteração de categoria ou de etapa obriga a mexer numa versão, e isso atrita o trabalho
pequeno. A conferência da impressão digital é o que impede que o atrito se resolva pulando o passo.

Manter leitura de formatos antigos é código que só cresce e que o uso corrente não exercita: precisa
de teste próprio, ou apodrece sem aviso e falha justamente quando um artefato antigo é conferido.

## O que reabre

Se os datasets passarem a ser publicados em formato de intercâmbio de terceiro, o formato próprio
vira intermediário, e a conversão ganha decisão sua.
