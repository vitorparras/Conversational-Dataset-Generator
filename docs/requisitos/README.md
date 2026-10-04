# Requisitos

> Descrição. O que entra nesta pasta.

**Um requisito é uma garantia observável do sistema, não uma tarefa.** Ele descreve o que o sistema
assegura e por quê; sobrevive a qualquer replanejamento. O trabalho para atingi-lo vive nas issues.

| Não é requisito | É requisito |
|---|---|
| "Implementar transação na gravação de auditoria" | "Dois eventos gravados ao mesmo tempo produzem cadeia válida, ou a gravação falha visivelmente" |
| "Criar tela de arbitragem" | "Duas anotações divergentes do mesmo item são resolvidas por uma terceira pessoa, e a resolução é rastreável" |

A coluna da esquerda cita solução; a da direita, garantia. **Requisito que cita tecnologia está
quase sempre errado** — tecnologia é escolha de decisão, e muda sem que a garantia mude.

## Formato

Nome `NNN-slug.md`. Autoridade `Norma vigente`. Seções:

1. **A garantia** — uma frase. Se não couber em uma, provavelmente são dois requisitos
2. **Por que existe** — a exigência de origem, citada
3. **Critérios de aceitação** — em Dado / Quando / Então, verificáveis
4. **O que não exige** — evita que a implementação assuma restrição que o requisito não impõe

Ver [regras de escrita](../regras-de-escrita.md).
