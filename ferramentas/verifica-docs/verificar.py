#!/usr/bin/env python3
"""Verifica a documentação do repositório. Roda na CI a cada pull request.

Cinco verificações, nesta ordem de gravidade:

1. dado pessoal e segredo  — padrão de documento, contato, chave, caminho de máquina
2. referências inexistentes — caminho citado na documentação que não existe no repositório
3. links quebrados          — link interno que não resolve
4. linha de autoridade      — todo documento diz se vincula ou não
5. formatação               — regras simples de consistência

Uso:
    python ferramentas/verifica-docs/verificar.py            # tudo
    python ferramentas/verifica-docs/verificar.py --apenas dados,links
    python ferramentas/verifica-docs/verificar.py --lista    # nomes das verificações

Sem dependência externa: só biblioteca padrão.
"""

from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[2]

#: Diretórios que não são documentação do projeto.
IGNORADOS = {".git", ".github", "ferramentas", "node_modules", "bin", "obj"}

# ---------------------------------------------------------------------------
# Vocabulário da linha de autoridade
# ---------------------------------------------------------------------------
# O repositório usa markdown puro, sem cabeçalho de metadados. A autoridade de
# cada documento vive numa linha de citação logo após o título. Este é o
# vocabulário fechado; as regras de escrita estão em docs/ (ver PR seguinte).

AUTORIDADES = (
    "Norma vigente",           # vincula
    "Norma substituída por",   # não vale mais; o sucessor diz o que vale
    "Norma revogada",          # não vale mais, e nada substitui
    "Norma pendente",          # falta decidir; o documento diz o que falta saber
    "Decisão vigente",         # vincula (usado em docs/decisoes/)
    "Decisão substituída por",
    "Decisão revogada",
    "Decisão pendente",        # falta decidir; o documento diz o que falta saber
    "Descrição",               # descreve o sistema; se divergir, o código ganha
    "Histórico",               # autoridade zero; nunca norma
    "Operacional",             # procedimento a seguir
)

# ---------------------------------------------------------------------------
# Padrões de dado pessoal e segredo
# ---------------------------------------------------------------------------

PADROES_SENSIVEIS: tuple[tuple[str, str], ...] = (
    (r"\b\d{3}\.\d{3}\.\d{3}-\d{2}\b", "possível CPF"),
    (r"\b\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}\b", "possível CNPJ"),
    (r"\b\d{2}\.\d{3}\.\d{3}-\d\b", "possível RG"),
    (r"\(\d{2}\)\s?9?\d{4}-\d{4}", "possível telefone"),
    (r"\b[A-Za-z0-9._%+-]+@(?!exemplo\.|example\.)[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", "e-mail"),
    (r"\b[A-Za-z]:[\\/](?:Users|Repos|Program Files)\b", "caminho absoluto de máquina"),
    (r"/(?:home|Users)/[a-z][a-z0-9_-]+/", "caminho absoluto de máquina"),
    (r"\bgh[pousr]_[A-Za-z0-9]{16,}", "token do GitHub"),
    (r"\bsk-[A-Za-z0-9]{20,}", "chave de API"),
    (r"\bAKIA[0-9A-Z]{16}\b", "chave de acesso AWS"),
    (r"-----BEGIN [A-Z ]*PRIVATE KEY-----", "chave privada"),
)

#: Extensões que, num caminho citado, indicam referência a arquivo do repositório.
EXTENSOES_DE_ARQUIVO = (
    ".md", ".cs", ".py", ".csproj", ".sln", ".json", ".yml", ".yaml",
    ".sql", ".cshtml", ".razor", ".toml", ".cff", ".txt",
)

LIMITE_COLUNA = 100


@dataclass(frozen=True)
class Achado:
    """Um problema encontrado, ancorado em arquivo e linha."""

    arquivo: Path
    linha: int
    verificacao: str
    mensagem: str

    def __str__(self) -> str:
        rel = self.arquivo.relative_to(RAIZ).as_posix()
        return f"{rel}:{self.linha}  [{self.verificacao}] {self.mensagem}"


def documentos() -> list[Path]:
    """Todo .md do repositório, fora dos diretórios ignorados."""
    encontrados: list[Path] = []
    for caminho in RAIZ.rglob("*.md"):
        if any(parte in IGNORADOS for parte in caminho.relative_to(RAIZ).parts[:-1]):
            continue
        encontrados.append(caminho)
    return sorted(encontrados)


def _sem_blocos_de_codigo(linhas: list[str]) -> list[tuple[int, str]]:
    """Linhas numeradas (base 1), excluindo blocos cercados por ```.

    Bloco de código contém exemplo — inclusive exemplo de caminho fictício e de
    segredo de demonstração —, e verificar seu conteúdo gera falso positivo.
    """
    fora: list[tuple[int, str]] = []
    dentro_de_bloco = False
    for numero, linha in enumerate(linhas, start=1):
        if linha.lstrip().startswith("```"):
            dentro_de_bloco = not dentro_de_bloco
            continue
        if not dentro_de_bloco:
            fora.append((numero, linha))
    return fora


# ---------------------------------------------------------------------------
# 1. Dado pessoal e segredo
# ---------------------------------------------------------------------------

def verifica_dados(caminho: Path, linhas: list[str]) -> list[Achado]:
    """Procura padrão de dado pessoal, contato, chave e caminho de máquina.

    Roda sobre o arquivo INTEIRO, blocos de código incluídos: um segredo real
    colado dentro de um exemplo continua sendo um segredo real.
    """
    achados: list[Achado] = []
    for numero, linha in enumerate(linhas, start=1):
        for padrao, descricao in PADROES_SENSIVEIS:
            if re.search(padrao, linha):
                achados.append(Achado(caminho, numero, "dados", f"{descricao} nesta linha"))
    return achados


# ---------------------------------------------------------------------------
# 2. Referências a arquivos inexistentes
# ---------------------------------------------------------------------------

def verifica_referencias(caminho: Path, linhas: list[str]) -> list[Achado]:
    """Confere que todo caminho citado em `código inline` existe no repositório.

    Só considera o que parece caminho de arquivo do projeto: tem barra ou
    extensão conhecida. Evita confundir comando de terminal com caminho.
    """
    achados: list[Achado] = []
    for numero, linha in _sem_blocos_de_codigo(linhas):
        for trecho in re.findall(r"`([^`\n]+)`", linha):
            candidato = trecho.strip().rstrip(":,.;")
            if " " in candidato or candidato.startswith(("-", "$", "http")):
                continue
            parece_caminho = candidato.endswith(EXTENSOES_DE_ARQUIVO) or (
                "/" in candidato and not candidato.endswith("/")
            )
            if not parece_caminho:
                continue
            # Padrão não é caminho literal: glob (migrations/*.sql), modelo de
            # nomenclatura (NNN-slug.md) e marcador (<nome>, {id}) não existem
            # como arquivo e não devem ser cobrados.
            if "*" in candidato or "NNN" in candidato or "<" in candidato or "{" in candidato:
                continue
            if not (RAIZ / candidato).exists():
                achados.append(
                    Achado(caminho, numero, "referencias", f"`{candidato}` não existe")
                )
    return achados


# ---------------------------------------------------------------------------
# 3. Links internos quebrados
# ---------------------------------------------------------------------------

def verifica_links(caminho: Path, linhas: list[str]) -> list[Achado]:
    """Confere que todo link relativo resolve. Ignora link externo e âncora."""
    achados: list[Achado] = []
    for numero, linha in _sem_blocos_de_codigo(linhas):
        for destino in re.findall(r"\[[^\]]*\]\(([^)]+)\)", linha):
            if destino.startswith(("http://", "https://", "mailto:", "#")):
                continue
            alvo = (caminho.parent / destino.split("#", 1)[0]).resolve()
            if not alvo.exists():
                achados.append(Achado(caminho, numero, "links", f"link quebrado: {destino}"))
    return achados


# ---------------------------------------------------------------------------
# 4. Linha de autoridade
# ---------------------------------------------------------------------------

def verifica_autoridade(caminho: Path, linhas: list[str]) -> list[Achado]:
    """Todo documento declara, numa citação após o título, se vincula ou não.

    Sem cabeçalho de metadados, esta linha é o único lugar onde a autoridade
    fica explícita — e sem ela, histórico pode ser lido como norma.
    """
    if caminho.name in {"README.md", "CONTRIBUTING.md", "SECURITY.md",
                        "CODE_OF_CONDUCT.md", "CLAUDE.md", "AGENTS.md"}:
        return []  # arquivos de comunidade: autoridade óbvia pelo nome

    for numero, linha in enumerate(linhas[:12], start=1):
        if linha.startswith(">"):
            texto = linha.lstrip("> ").strip()
            if texto.startswith(AUTORIDADES):
                return []
            return [
                Achado(caminho, numero, "autoridade",
                       f"autoridade não reconhecida: {texto[:48]!r}")
            ]
    return [
        Achado(caminho, 1, "autoridade",
               "falta a linha de autoridade (citação após o título)")
    ]


# ---------------------------------------------------------------------------
# 5. Formatação
# ---------------------------------------------------------------------------

def verifica_formatacao(caminho: Path, linhas: list[str]) -> list[Achado]:
    """Regras simples de consistência: coluna, espaço final, linhas vazias, tab."""
    achados: list[Achado] = []
    vazias_seguidas = 0

    for numero, linha in enumerate(linhas, start=1):
        sem_quebra = linha.rstrip("\n")

        if "\t" in sem_quebra:
            achados.append(Achado(caminho, numero, "formatacao", "tab: use espaços"))

        if sem_quebra != sem_quebra.rstrip():
            achados.append(Achado(caminho, numero, "formatacao", "espaço no fim da linha"))

        # Tabela, link longo e linha de código não cabem no limite sem piorar a leitura.
        eh_excecao = sem_quebra.lstrip().startswith(("|", "    ")) or "](" in sem_quebra
        if len(sem_quebra) > LIMITE_COLUNA and not eh_excecao:
            achados.append(
                Achado(caminho, numero, "formatacao",
                       f"{len(sem_quebra)} colunas (limite {LIMITE_COLUNA})")
            )

        if not sem_quebra.strip():
            vazias_seguidas += 1
            if vazias_seguidas == 3:
                achados.append(
                    Achado(caminho, numero, "formatacao", "3+ linhas vazias seguidas")
                )
        else:
            vazias_seguidas = 0

    if linhas and not linhas[-1].endswith("\n"):
        achados.append(
            Achado(caminho, len(linhas), "formatacao", "arquivo sem quebra de linha final")
        )
    return achados


VERIFICACOES = {
    "dados": verifica_dados,
    "referencias": verifica_referencias,
    "links": verifica_links,
    "autoridade": verifica_autoridade,
    "formatacao": verifica_formatacao,
}


def main(argv: list[str] | None = None) -> int:
    analisador = argparse.ArgumentParser(description=__doc__)
    analisador.add_argument("--apenas", help="verificações a rodar, separadas por vírgula")
    analisador.add_argument("--lista", action="store_true", help="lista as verificações")
    args = analisador.parse_args(argv)

    if args.lista:
        for nome in VERIFICACOES:
            print(nome)
        return 0

    escolhidas = list(VERIFICACOES)
    if args.apenas:
        pedidas = [n.strip() for n in args.apenas.split(",") if n.strip()]
        desconhecidas = [n for n in pedidas if n not in VERIFICACOES]
        if desconhecidas:
            print(f"verificação desconhecida: {', '.join(desconhecidas)}")
            return 2
        escolhidas = pedidas

    arquivos = documentos()
    achados: list[Achado] = []
    for caminho in arquivos:
        linhas = caminho.read_text(encoding="utf-8").splitlines(keepends=True)
        for nome in escolhidas:
            achados.extend(VERIFICACOES[nome](caminho, linhas))

    if not achados:
        print(f"OK — {len(arquivos)} documentos, {len(escolhidas)} verificações, nenhum problema.")
        return 0

    por_verificacao: dict[str, int] = {}
    for achado in achados:
        print(achado)
        por_verificacao[achado.verificacao] = por_verificacao.get(achado.verificacao, 0) + 1

    resumo = " · ".join(f"{nome}: {total}" for nome, total in sorted(por_verificacao.items()))
    print(f"\nFALHOU — {len(achados)} problemas em {len(arquivos)} documentos ({resumo})")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
