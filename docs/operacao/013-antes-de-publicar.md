# 013 — Antes de tornar o repositório público

> Operacional. 2026-10-07.

Tornar o repositório público é irreversível no que importa: o que foi visto não se esquece, e o que
esteve no histórico continua recuperável mesmo depois de removido.

## O que se confere, e por quê

**Todo o histórico, não apenas o estado atual.** Um segredo ou trecho de material que tenha passado
por qualquer versão continua lá, alcançável por quem souber procurar. Remover o arquivo não remove a
versão anterior.

Esta é a conferência que mais se esquece, porque a aparência do repositório hoje não diz nada sobre
o que ele já conteve.

**A verificação automática sobre todas as versões**, não só a última. Os padrões que ela procura —
documento, contato, chave, caminho de máquina — valem igualmente para o passado.

**As dependências**, com a verificação de vulnerabilidade em dia. Publicar expõe a lista de
dependências a quem quiser cruzá-la com falhas conhecidas.

**A licença presente e correta**, e o regime do material declarado onde alguém procuraria.

**Nenhuma referência a caminho de máquina, nome de pessoa ou identificador de instalação**, em
qualquer arquivo e em qualquer versão.

## Se algo for encontrado

Material sensível em versão anterior não se resolve removendo o arquivo. Exige reescrever o
histórico, o que muda todas as versões seguintes — operação que precisa ser coordenada e que
invalida cópias já feitas por outras pessoas.

É por isso que a conferência acontece **antes**, e não depois de uma suspeita.

## O que publicar não muda

O material e os datasets continuam fora do repositório, sob o regime próprio deles. Publicar o
código não os torna acessíveis, e nada no repositório deve sugerir o contrário.
