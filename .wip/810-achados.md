# 810 — achados da auditoria (rascunho de trabalho, sai antes do PR)

Medição: lizard 1.17.13 (`uv run --with lizard==1.17.13 python -m lizard`, `-Eduplicate`).
Ressalva: construtor primário e record posicional contam como uma função do tamanho da classe;
.razor não é lido.

- Duplicação core/src 4,84% (38 blocos, 119 ocorrências; Host 80, Persistence 34).
- Duplicação core/tests 25,11% (681 blocos, 2.875 ocorrências). Topo: EmptyingRoutesTests 107,
  ManualSealHostTests 60, PersonRegistryDatabaseTests 56, SystemStateScreenTests 46,
  SealDeclarationRefusalTests 44, SubmissionRoutesTests 42.
- Complexidade (métodos/NLOC/CCN médio/máx/>10/>5): Boundary 32/341/2,62/6/0/2;
  Contracts 9/48/1,11/2/0/0; Host 425/4930/2,85/10/0/50; Ingestion 93/841/2,29/9/0/6;
  Persistence 541/5507/2,06/26*/4*/17; Platform 146/1552/2,16/15*/1*/9.
  (*) classe inteira: AuditSettings, ProtectedContentReader, ContentWriter, RecoveryReceipt,
  SessionStore. Método real máximo 10 (SignIn, ConfigurationChangeScreensEndpoint.Unavailable,
  OperatorEmailEndpoint.CorrectAsync, HostEntry.ConfiguredAsync). Testes: CCN médio 1,02–1,61.

## Desvios (custo; ganho)
1. Acesso por função: ScreenGate.cs:20 deixa passar rota não-página sem ScreenAccess, contra
   ScreenAccess.cs:5-6. SealEndpoint (POST confirmação do selo) e AuditEndpoint abertos a
   qualquer sessão; SealScreensEndpoint exige Administration. DEFEITO: issue própria.
   ~8 arquivos, 25 linhas.
2. Falha esperada de 3 jeitos: Result (11 tipos), exceção (ContentWriter:100-111, 3 exceções que
   ninguém captura; ReplicationStore), null/bool (SessionStore.Resolve, CloseListed,
   BatchReviews.ReadAsync). ~2 arquivos, +40.
3. Armazenamento decide domínio: BatchReviews:77-114, DomainLabelRotation:54-68,
   ProtectedContentReader:81-93, SignInAttempts:37-41/82-95, IngestionBatches:182 (estado como
   texto). Modelo bom: LabelState.Refusal(). ~4 arquivos, 120 linhas movidas; teste sem banco.
4. StrongBox em 7 arquivos porque CustodyTransaction.RunAsync só devolve CustodyResult?.
   RunAsync<T>: 1+7 arquivos, -60.
5. Nova tentativa de 5 jeitos (BatchRetry, SignInAttempts, ContentWriter, Custody/Guarantee
   Verifier, DomainLabelRevocation). 1+4 arquivos, -30.
6. Tela indisponível: 503 cru em ~10 telas (ex. DomainScreensEndpoint:23-26) contra
   ScreenResult.Unavailable em 5. ~10 arquivos, 30 linhas.
7. Form e sessão à mão: ternário HasFormContentType em 8 lugares; ConfigurationChange
   ScreensEndpoint:45,79 sem guarda; SessionStore obtido de 3 jeitos; sem antiforgery, defesa é
   SameSite=Strict (SessionCookie.cs:8), não documentada. FormOf/SessionOf: ~12 arquivos, -40.
8. Estado de falha da tela em 5 formatos; status da recusa 200/400/409; aviso por cookie ou
   query; WaitNotice e class="wait". ~20 arquivos, 150 linhas.
9. Endpoint com responsabilidade misturada: OperatorRegistration (duas escritas não atômicas
   :32,38), AuditEndpoint, SealVerificationEndpoint, ConfigurationChange, EmptyingAct. 5 arq, 120.
10. Testes: cenário remontado por teste (EmptyingRoutes/Submission/ManualSeal), banco de 5 jeitos
   (37 arquivos migram no teste; 106 new NewPerson, 8 NewPersonAsync, 11 INSERT crus), host de
   5 jeitos (System/Navigation/EntryScreenHost fora de ScreenHostBase, -400), login clonado
   (SystemSession), Task.Delay (EntryScreenTests:89/177, ResponseWithoutRegionTests:44).
   Scenario.StartAsync + PageOfAsync/PostFormAsync tiram ~210 dos 681 blocos.
Extras: configurações com helpers duplicados (8 arq, -50); nomes de armazenamento e SQL embutido
(29 arquivos contra 10 *Statements): documentar, não renomear.

## Exemplares do guia
armazenamento SignInAttempts+SignInStatements; serviço DestinationPolicy (+SubmissionGate);
rota ChainVerificationEndpoint e SealScreensEndpoint; tela PasswordScreen.razor; teste com banco
PersonChangeScopeTests; navegador NotAppliedChangesScreenTests.

## Falta
Escrever docs/arquitetura/padroes/ (README + 001..006), link em docs/arquitetura/README.md,
verify-docs, PR com Closes #810, relatório completo na #810, apagar .wip/.
