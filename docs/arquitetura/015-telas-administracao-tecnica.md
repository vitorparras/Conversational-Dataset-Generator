# 015 — Telas da administração técnica

> Descrição. 2026-11-04.
> Estado no código: nenhuma tela existe; as rotas estão em `core/src/Core.Host/Endpoints/`.

Função de administração técnica (política de acesso, [006](../privacidade/006-acesso.md)): quem
cadastra e vê o estado do sistema, sem alcance a conteúdo. Instalação, migração, cópia,
restauração, chave e ensaio de recuperação ficam na linha de comando do servidor, assim como a
primeira pessoa e o primeiro domínio.

| ID | Tela | Função | Mostra | Faz | Fase | Issue |
|---|---|---|---|---|---|---|
| A1 | Pessoas | Administração | Lista de pessoas, função declarada, estado, sem conteúdo | Cadastra com função e nenhum alcance | 3 | #599 (regra #109) |
| A2 | Reposição de senha | Administração | Quem, para quem, quantas sessões foram encerradas; o canal usado (e-mail ou entrega manual), nunca o link | Repõe por ato registrado e gera um link temporário de uso único, que a pessoa abre para definir a senha | 6 | #600, #635 (regra #110) |
| A3 | Concessões | Quem responde pelo acervo | Por pessoa e domínio: finalidade, referência ao termo, prazo, vencimento | Concede, renova, revoga; nunca a si mesmo | 6 | #601 (regra #112–#114) |
| A4 | Domínios | Administração | Domínios, taxonomia e versão; "nenhum domínio" com o passo no servidor | Só leitura | 3 | #602 |
| A5 | Configuração em vigor | Administração | Todos os valores com a origem de cada um; redigidos onde a visão exige | Só leitura; a alteração é a A9 | 3 | #603 |
| A6 | Estado do sistema | Administração | Vivo e apto, conferências da subida, versões, esquema, serviço de linguagem | Só leitura | 3 | #604, #632 |
| A7 | Custódia | Administração | Última verificação (inclusive "passou"), escopo, contagem, primeiro elo inconsistente, incidente, selo | Dispara verificação | 3 | #605 (regra #61, #62, #558) |
| A8 | Registro do servidor | Auditoria | O que o banco viu no intervalo: papel, classe, comando, objeto; nunca o texto | Consulta até 7 dias | 6 | #606 (regra #60, #500) |
| A9 | Alteração de configuração | Administração | A chave, o valor novo e o motivo (lista fechada); pendentes e quem falta aprovar; o resultado do aplicador | Submete; outra pessoa com a função aprova; o aplicador no servidor valida, grava e reinicia | 3 | #634 |

## Observações

- Rotas que já existem sem tela: configuração (A5), estado e versões (A6), verificação da custódia
  (A7) e registro do servidor (A8). A #632 restringe o estado sem sessão a vivo e apto.
- A configuração se altera pela A9: submissão e aprovação por outra pessoa; só o aplicador, no
  servidor, grava o arquivo e reinicia o núcleo. Segredos, custódia da chave, certificado, escuta
  e conexão com o banco só mudam no arquivo, no servidor (requisito 032).
- A A8 é da função de auditoria, não da administração, e só existe depois da fase 6.
- Renovar é conceder de novo; revogar não reinicia o prazo de outra concessão (requisito 028).
- A A7 mostra o resultado "passou" e não só a falha, para que a ausência de alerta não seja prova.
- Acessibilidade: #591. Teste de navegador: #590.
