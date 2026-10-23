# PEGA #91 N04 — Local Investigation Implementation Plan

> **Status:** plano aprovado pelo usuário; implementação no workspace preparada; authoring do prefab e aceite Unity pendentes.
>
> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement task-by-task in the Native workflow already selected by the user. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Depois de perder Rick por percepção real, o larápio deve alcançar a última posição conhecida e, se necessário, escolher e executar pelo GOAP posições locais finitas que criem novas oportunidades reais de percepção, sem declarar Rick encontrado.

**Architecture:** O PEGA acrescentará um passo intermediário de perseguição ao contrato existente de seleção GOAP e uma Action de navegação que oferece candidatos locais derivados apenas da última evidência do próprio NPC e da NavMesh. `PEGANpcAutonomy` mantém a oportunidade por eventos, conserva o relógio de N05 e não satisfaz Desejos com deslocamentos intermediários. Um runner temporário controla Rick pela entrada de movimento existente e observa as consequências físicas na Mission.

**Tech Stack:** Unity 6000.5.0f1; `com.unity.ai.navigation` 2.0.16; `com.unity.inputsystem` 1.19.0; PEGA C#; `NavMeshAgent`; `CharacterController`; contratos atuais de `PlayerGameplayInputReader` e logger canônico `PEGALogging`.

**Resolved package note:** o PEGA resolve `com.immersive.framework` no hash `7baf6a336ea98aa6558d06e401791c4015e4181e`. O contrato instalado do `PlayerGameplayInputReader` expõe leitura do `InputActionReference` e `RuntimeGameplayAvailable`, ligado ao `PlayerInput` atual; o teste envia eventos pelo Input System e não altera o Framework nem seu binding.

**Spec:** [docs/superpowers/specs/2026-10-22-pega-n04-local-investigation-design.md](../specs/2026-10-22-pega-n04-local-investigation-design.md)

## Global Constraints

- O primeiro domínio de investigação é a vizinhança finita em torno da última posição conhecida na área única do armazém da Mission; NavMesh radial e cotas de rota não identificam salas semânticas.
- O timeout já pertencente à percepção/N05 é a autoridade final e não reinicia quando uma Action começa, termina ou falha; também encerra oportunidades sem destino local válido.
- Só eventos legítimos da Perception, do Knowledge State e do lifecycle das Actions podem iniciar reavaliação; não adicionar polling de decisão.
- Runtime do NPC não consulta Transform, velocidade ou posição atual de Rick; somente os fatos individuais do `PEGANpcKnowledgeState` podem originar posições de planejamento.
- Chegar à última posição ou a um ponto local é progresso físico intermediário, não readquisição, conhecimento novo ou satisfação do Desejo.
- A investigação local só fica disponível após o NPC alcançar fisicamente a última posição conhecida; iniciar ou falhar a Action de aproximação não equivale a chegar.
- `PEGANpcNavigationAction` é base abstrata de Actions GOAP irmãs. Cada Action concreta restringe duplicação de sua própria classe; a base não pode aplicar `DisallowMultipleComponent`, que impediria coexistência no Actor.
- Desativar a autonomia ou o GameObject pausa a Action física sem apagar o estado da oportunidade, inclusive quando `PEGANpcNavigationAction.OnDisable` entrega `Cancelled` antes do callback da autonomia; os bindings continuam recebendo eventos legítimos e uma avaliação única ao reativar reconcilia o estado. O timestamp de perda e o timeout de N05 permanecem os mesmos.
- O probe e o runner são temporários, limitados à Mission e removidos após a aprovação manual; sem Framework/QAFramework, cena paralela ou conhecimento sintético.
- Preservar a perda/readquisição/despistamento N04/N05 existentes e todas as alterações alheias presentes no worktree.
- Não executar Unity, Import, Play Mode, smoke ou batchmode a partir do Codex; validação funcional e de serialização final cabem ao usuário no Unity Editor.

## Review Focus

- NPC já dentro do raio de chegada na perda: não contar como progresso físico; a execução de validação deve registrar `UNAVAILABLE` se a pré-condição de deslocamento não for atendida.
- Projeção em outra altura/superfície ou máscara incompatível: rejeitar candidato sem sair do agent type e area mask atuais.
- Caminho completo cujos cantos saem da vizinhança: rejeitar, ainda que o destino final esteja próximo; as cotas não provam separação semântica de salas futuras.
- Chegada local ou à última posição sem visão de Rick: não atualizar Knowledge State nem satisfação; só a Perception real pode readquirir.
- Nenhum candidato, falha de rota ou candidatos esgotados: não repetir tentativas nem iniciar loop; manter a oportunidade sem decisão até a readquisição ou deadline original de N05.

---

## Mapa de arquivos e responsabilidades

- `Assets/PEGA/NPC/PEGANpcPlanSelectionContracts.cs` e `PEGANpcPlanSelector.cs`: identidade tipada do ponto local, continuação de perseguição e seleção de um passo intermediário sem reescrever o planejador geral.
- `Assets/PEGA/NPC/PEGANpcNavigationAction.cs`: novo tipo de efeito físico intermediário, sem associá-lo a predicados de satisfação.
- `Assets/PEGA/NPC/PEGANpcInvestigateLastKnownRickPositionAction.cs` e `.meta`: geração determinística/bounded de candidatos e execução pela base de navegação compartilhada.
- `Assets/PEGA/NPC/Editor/PEGANpcInvestigateLastKnownRickPositionGizmos.cs`: visualização Editor-only do volume configurado e, em Play Mode, da âncora de conhecimento e Target ativos quando disponíveis.
- `Assets/PEGA/NPC/PEGANpcAutonomy.cs` e `PEGANpcApproachKnownRickAction.cs`: continuidade event-driven, lifecycle das Actions e encerramento por readquisição/despistamento. Estas fontes já têm alterações N04 locais; continuar nelas sem substituir trabalho existente.
- `Assets/PEGA/NPC/PEGA_NPCActorRuntimeBase.prefab`: composição permanente do novo componente; as duas instâncias da Mission referenciam esse prefab. Não editar YAML manualmente nem alterar a cena se o prefab-base for suficiente.
- `Assets/PEGA/NPC/Temporary/PEGANpcAutonomyN04ObservationProbe.cs`: observação das evidências e resultados do NPC, sem escrita ou comando de Action.
- `Assets/PEGA/NPC/Temporary/PEGANpcAutonomyN04MissionValidationRunner.cs` e `.meta`: controle automatizado de Rick pelo input real e máquina de fases exclusivamente do fixture.
- `Assets/PEGA/NPC/Editor/PEGANpcAutonomyN04ObservationAuthoring.cs`: um botão no Inspector do NPC selecionado para iniciar a validação; adiciona fixture apenas a objetos da Mission em Play Mode e não salva a cena.

## Task 1: Contrato GOAP para passos da continuação

**Arquivos:** `PEGANpcPlanSelectionContracts.cs`, `PEGANpcPlanSelector.cs`, `PEGANpcNavigationAction.cs`.

**Interfaces:**

- Criar `PEGANpcPursuitContinuation` como valor imutável com `DesireId`, `SubjectActorId`, `ContactLossObservationTimeSeconds`, `LastKnownPosition`, `PositionEvidenceTimeSeconds`, tentativa da aproximação e chegada física ao last-known. Não transportar um deadline próprio; N05 continua sendo a fonte do timeout.
- Acrescentar `PEGANpcPlanPurpose` com `DesireGoal` e `PursuitContinuationStep`, e expor `Purpose` em `PEGANpcPlanSnapshot`.
- Acrescentar os contratos tipados `LocalObservationPosition`, `DerivedFromKnowledge`, `InvestigateLastKnownRickPosition` e `ReachLocalObservationPosition`; `DerivedFromKnowledge` significa geometria calculada a partir da evidência de Rick e da NavMesh, não um fato escrito no Knowledge State. A identidade do target deve ser `(Rick ActorId, contact-loss timestamp, sample ordinal)`, sem parsing de strings ou mistura com identidade de Actor.
- Estender `PEGANpcPlanningInput` com continuação opcional. `PEGANpcPlanSelector.Select(input, activeContext, satisfaction)` permanece a entrada pública existente e lê a continuação do input.

- [x] **Step 1: Adicionar continuação e identidade tipada** em `PEGANpcPlanSelectionContracts.cs`; validar ActorId, perda/observação finitos, target local derivado e unicidade por oportunidade.
- [x] **Step 2: Restringir candidatos do seletor** em `PEGANpcPlanSelector.cs`; sem continuação, preservar a seleção normal. Com continuação, aceitar o Desejo proprietário, permitir Approach somente até a tentativa/chegada e permitir investigação local somente após chegada física ao last-known, comparando pelo mesmo score/custo e ordem determinística atuais.
- [x] **Step 3: Representar o efeito intermediário** em `PEGANpcNavigationAction.cs`; manter o objetivo final do Desejo separado do efeito observado de alcançar a posição local.
- [x] **Step 4: Revisar estaticamente contratos e branches**; verificar todos os `switch`/validações de Action, Effect, Target e Plan Purpose. O probe da Task 4 exercitará plano válido, ausência de solução e satisfação invariável.

## Task 2: Candidatos locais e Action física

**Arquivos:** criar `PEGANpcInvestigateLastKnownRickPositionAction.cs` e `.meta`; modificar `PEGA_NPCActorRuntimeBase.prefab` e, se necessário, `PEGANpcAutonomy.cs` para exigir/resolver o componente.

**Interfaces:**

- `TryCreatePlanningCandidates(PEGANpcPursuitContinuation continuation, IReadOnlyCollection<PEGANpcPlanningTargetId> attemptedTargets, out IReadOnlyList<PEGANpcPlanningTarget> targets, out IReadOnlyList<PEGANpcPlanningActionCandidate> candidates, out string diagnostic)` revalida a perda e a posição em `PEGANpcKnowledgeState`, oferece apenas targets ainda não tentados e não escolhe nem inicia Actions.
- `TryStart(PEGANpcPlanSnapshot selectedPlan, out string diagnostic)` inicia somente o target local aprovado pelo seletor, através de `TryStartNavigation` da base.

- [x] **Step 1: Implementar amostragem local determinística** ao redor de `LastKnownPosition`, com quantidade finita, ordem estável, projeção curta na NavMesh do Agent existente, teste de distância vertical e eliminação de destinos duplicados.
- [x] **Step 2: Aplicar limites radiais e de rota**; exigir rota completa com o agent type/area mask atuais, comprimento máximo e todos os cantos dentro da vizinhança configurada da última posição. Guardar no plano/log a maior distância dos cantos à âncora para observação do probe. Documentar no Inspector que isso delimita a Mission atual, não fronteiras de sala futuras.
- [x] **Step 3: Validar configuração e oportunidade**; expor unidades e faixas para raio, projeção, variação vertical, amostras, espaçamento e comprimento de rota; rejeitar evidência que não corresponda à oportunidade ativa e não consultar o Player vivo.
- [x] **Step 4: Visualizar a região espacial no Editor**; desenhar prévia configurada na Scene View e, durante perda legítima em Play Mode, ancorar o volume na última posição conhecida; mostrar o Target físico ativo sem alterar o runtime.
- [ ] **Step 5: Compor no prefab-base** pelo Unity Prefab Mode/`PrefabUtility`, preservando referências existentes; confirmar que ambas as instâncias de Mission recebem exatamente um componente e valores válidos.

**Gate de autoria:** não há superfície Immersive de authoring de `MonoBehaviour` no prefab consumidor. O usuário autorizou um utilitário temporário Native com `PrefabUtility`, que acrescenta o componente idempotentemente ao prefab-base, salva pelo Unity API e verifica a cardinalidade após reabrir. Nenhum YAML é editado. A ação de authoring permanece pendente para o usuário executar no Unity Editor.

## Task 3: Lifecycle event-driven, timeout e ausência de ciclos

**Arquivos:** `PEGANpcAutonomy.cs`, `PEGANpcApproachKnownRickAction.cs`, `PEGANpcPlanSelectionContracts.cs`.

- [x] **Step 1: Abrir continuação na perda real** usando o intent atual e oportunidade `(ActorId, loss timestamp, last-position evidence time)`; manter o approach físico à última posição como primeiro passo, sem extrapolação.
- [x] **Step 2: Avançar por resultado terminal**; durante a continuação, tratar approach/posição local como progresso, sem chamar `RecordCompletedAction`. Registrar cada target como tentado antes de iniciar a Action para que falha síncrona, rota inválida ou repetição do mesmo evento não reinicie aquele target.
- [x] **Step 3: Reavaliar uma vez por evento** depois do resultado terminal e deixar o GOAP selecionar o próximo target viável. Se não houver candidato novo, parar de emitir solicitações e aguardar eventos legítimos; não criar polling nem re-enfileirar o mesmo terminal.
- [x] **Step 4: Encerrar a oportunidade em readquisição ou despistamento**; readquisição usa apenas os fatos já consolidados pela Perception. `RickConsideredEvaded` fecha a continuação e permite seleção Normal. Nenhuma Action, tentativa ou falha altera o relógio original de N05.
- [x] **Step 5: Conferir isolamento por componente e regressões N01–N05**; verificar que os conjuntos de tentativas, plano ativo e eventos continuam no root de cada NPC e que ausência/exaustão de candidatos não impede a entrega do evento de evasion.
- [x] **Step 6: Preservar o lifecycle e o timeout**; ao desativar a autonomia ou o GameObject, cancelar somente a Action ativa sem aplicar Effect/satisfação, conservar bindings e eventos pendentes e reconciliar uma vez ao reativar. O cancelamento recebido enquanto a autonomia está inativa é tratado como pausa mesmo se a ordem dos callbacks `OnDisable` variar. A investigação local só é selecionável após a chegada física à última posição; falha/rota inválida encerra novas tentativas e aguarda os eventos ou o deadline original de N05.

## Task 4: Validação temporária automatizada na Mission

**Arquivos:** estender `PEGANpcAutonomyN04ObservationProbe.cs`; criar `PEGANpcAutonomyN04MissionValidationRunner.cs` e `.meta`; substituir as opções de arm/sumário separadas em `PEGANpcAutonomyN04ObservationAuthoring.cs` por um botão Inspector único.

- [x] **Step 1: Preparar preflight idempotente** pelo Inspector do `PEGANpcAutonomy` selecionado; exigir Play Mode, Mission carregada, Player atual ligado pelo `PlayerGameplayInputReader` e duas ocorrências NPC da Mission. Não criar/salvar cena durante o smoke.
- [x] **Step 2: Controlar Rick pelo input normal** `Input System -> PlayerInput -> PlayerGameplayInputReader -> PEGAPlayerMovementIntentSource -> PEGAActorMovementMechanic -> CharacterController`; automatizar uma rota física frontal-para-trás de ocultação e outra de readquisição. Não escrever transform, Knowledge State ou resultado de Action. Se o input gate/controle pareado não permitir condução segura, registrar `UNAVAILABLE` e terminar limpo.
- [x] **Step 3: Observar perda e progresso**; registrar posição conhecida no instante da perda, deslocamento físico posterior do NPC e do Player, Action selecionada, destino/cantos da rota e satisfação antes/depois. Estar inicialmente dentro do raio de chegada gera `UNAVAILABLE` para a asserção de progresso.
- [x] **Step 4: Verificar readquisição, esgotamento e deadline**; readquisição só passa com nova evidência visual real e solicitação única. Para o ramo sem destino, o runner reduz temporariamente e de forma válida o limite de comprimento da rota abaixo do raio de chegada da Action, espera o próprio GOAP concluir sua aproximação a Rick readquirido e então cria a segunda perda real. A configuração é restaurada no cleanup. O cenário deve mostrar targets distintos/limitados, nenhum update oculto de conhecimento, nenhuma repetição ou ciclo e despistamento no timeout original.
- [x] **Step 5: Consolidar e restaurar**; emitir cada check e resumo único `PASS/FAIL/UNAVAILABLE` por `PEGALogging`; cessar input, restaurar o estado original do teclado/gamepad e o limite de rota, e remover componentes/runtime fixture. A posição dos Actors só é restaurada ao sair do Play Mode.

## Task 5: Gate de validação, limpeza e entrega N04

- [x] **Step 1: Executar checagens estáticas permitidas**: `git diff --check`, revisão dos contratos/caminhos de runtime, busca por consulta ao Rick atual fora do runner e confirmação de que não há loop de decisão em `Update`/coroutine. A revisão também confere pausa/reativação da autonomia, cancelamento sem Effect inclusive com variação da ordem de `OnDisable`, e continuidade do relógio original de N05. As buscas confirmaram que leitura de posição viva do Player ocorre somente no fixture e que o runtime decide por eventos/resultados.
- [ ] **Step 2: Usuário executa a validação** na Mission: no Edit Mode clicar `Prepare #91 N04 Action on NPC Prefab`; depois selecionar um NPC da Mission em Play Mode e clicar uma vez em `Run #91 N04 Controlled Mission Validation`; revisar o resumo canônico, as métricas físicas e qualquer `UNAVAILABLE` antes do aceite.
- [ ] **Step 3: Após o aceite funcional, remover** somente `Assets/PEGA/NPC/Temporary/PEGANpcAutonomyN04ObservationProbe.cs`, o runner e seus `.meta`, e a extensão temporária de Editor com seu `.meta`; manter Action, contratos, autonomia e prefab definitivos.
- [x] **Step 4: Conferir o diff final**; preservar trabalho alheio, confirmar que a cena não recebeu referências ao fixture e registrar os critérios que continuam pendentes. O diff não altera a Mission; o authoring Native e smoke funcional no Unity continuam pendentes. N04 fica aberto e não avança N05.

## Critérios Unity a reportar

1. `lastKnownPhysicalApproach`: perda legítima, posição preservada e deslocamento físico do NPC até a última posição; a posição não é tratada como Rick encontrado.
2. `localGoapStep`: seleção pelo GOAP de destino novo derivado da evidência, rota completa dentro das cotas e chegada física pelo `CharacterController`.
3. `intermediateDoesNotSatisfy`: chegada local não modifica conhecimento ou satisfação.
4. `realReadquisition`: nova evidência visual após reposicionamento cancela a continuação e produz uma única reavaliação legítima.
5. `boundedNoProgress`: candidatos são finitos/únicos; nenhum caminho ultrapassa a cota; sem destinos não há ciclo e N05 despista no deadline original.
6. `perNpcIsolation`: oportunidades, targets tentados, Actions, mudanças de conhecimento e notificações de cada NPC não se misturam.
7. `missionRoomLimit`: o comportamento passa na Mission atual. O resumo não declara garantia semântica de sala em futuras cenas conectadas.

**Estado de aceite:** implementação e sonda estão preparadas no workspace, mas o Unity Editor não foi executado nesta sessão. O usuário ainda precisa preparar o prefab pelo botão Native, compilar/importar e executar o smoke na Mission. N04 permanece aberto; a validação temporária será removida somente após a confirmação funcional.
