# PEGA GOAP Desires and Plan Selection Implementation Plan

> **Para agentes implementadores:** subskill obrigatória: `superpowers:executing-plans` (execução nativa, recomendada) ou `superpowers:subagent-driven-development`. Execute as tarefas em ordem e mantenha os passos verificáveis.

**Goal:** Implementar no PEGA a configuração e avaliação de Desejos, seleção determinística de planos de uma Action para Targets conhecidos, registro individual de satisfação e uma validação temporária separada entre entrada isolada e integração com conhecimento real.

**Architecture:** Os contratos de Desejo, memória de satisfação e seleção pertencem a `Assets/PEGA/NPC`; a seleção recebe contexto e dados explícitos, sem iniciar Actions nem atualizar Knowledge State. Actions existentes expõem candidatos derivados do conhecimento real e estimativas NavMesh sem efeito colateral; um probe temporário na Mission demonstra o algoritmo isolado e, separadamente, a integração disponível.

**Tech Stack:** Unity 6000.5.0f1, C#, `ScriptableObject`, `MonoBehaviour`, `NavMeshAgent.CalculatePath` ([API Unity](https://docs.unity.cn/ScriptReference/AI.NavMeshAgent.CalculatePath.html)), Actions/Knowledge State do PEGA e o logger canônico via `PEGALogging`.

**Spec:** [docs/superpowers/specs/2026-10-21-pega-goap-desires-plan-selection-design.md](../specs/2026-10-21-pega-goap-desires-plan-selection-design.md)

## Global Constraints

- Todo novo contrato de gameplay fica em `Assets/PEGA/NPC`; nenhum pacote Immersive recebe alteração.
- `PEGANpcDesireProfile` é dado de jogo do PEGA e permanece distinto de Immersive `ActorProfile`.
- O Contexto é entrada explícita; não se cria autoridade global de Contexto nesta etapa.
- Em modo `LiveKnowledge`, Targets vêm exclusivamente de fatos aceitos no Knowledge State do NPC; a cena não transforma um objeto em conhecimento.
- `IsolatedFixture` usa valores destacados do NPC e informa esse modo nos logs; não escreve nem simula evidência no Knowledge State.
- #64 não adiciona Spawn Points, fatos de saída, propagação de conhecimento de Gangue, autonomia #91, replanejamento automático, FSM roteirizada, Actions de gameplay ou conteúdo definitivo de arquétipo.
- Aproximação de Rick e a configuração de escolha são apenas demonstração; não são Desejos aprovados para uma missão/arquetipo.
- Satisfação só muda após uma observação real de Action concluída que satisfaça o predicado; uma conclusão pode atualizar mais de um Desejo.
- Usar `PEGALogging`; não emitir logs por frame. Codex não executa Unity build/import, testes, Play Mode, smoke ou batchmode.
- Preservar os resultados anteriores do #63 e o Knowledge State/percepção #68/#69.

## Review Focus

1. Desejo continua aplicável quando seu Target ou pré-condição de plano não é conhecido; testar `applicable=true` junto a `NoPlan`.
2. Empate e seleção não dependem de ordem de enumeração/chegada; repetir a entrada com candidatos invertidos precisa gerar o mesmo resultado.
3. Rota parcial/inválida e custo/velocidade inválidos não geram candidato; a consulta live de rota não altera o pedido de navegação ativo.
4. `Outcome == Success` sem observação física de chegada, efeito ou Target correspondente não aumenta satisfação; uma chegada observada avalia todos os Desejos do Contexto.
5. E01 na cena não vira Target conhecido; o resultado live deve reportar a indisponibilidade de alternativa de fuga e não alegar que a seleção real entre alternativas foi aceita.

---

## Inventário de arquivos

- Criar `Assets/PEGA/NPC/PEGANpcDesireProfile.cs`: dados serializáveis PEGA para Contextos e definições de Desejos, política peso/custo e validação de IDs/valores.
- Criar `Assets/PEGA/NPC/PEGANpcDesireState.cs`: ledger transitório de satisfação individual no root de cada NPC; nenhuma partilha entre Actors.
- Criar `Assets/PEGA/NPC/PEGANpcPlanSelectionContracts.cs`: tipos imutáveis de entrada, fatos/Targets conhecidos, candidato de Action, custo, plano e resultado de seleção; inclui marca explícita de modo de validação/proveniência.
- Criar `Assets/PEGA/NPC/PEGANpcPlanSelector.cs`: construção/seleção determinística de planos válidos de um passo a partir dos candidatos compatíveis disponíveis.
- Modificar `Assets/PEGA/NPC/PEGANpcNavMeshMovementIntentSource.cs`: consulta de rota/custo sem alterar a simulação, pedido ativo ou movimento físico.
- Modificar `Assets/PEGA/NPC/PEGANpcApproachKnownRickAction.cs` e `Assets/PEGA/NPC/PEGANpcTravelToEscapeAction.cs`: adaptação read-only das Actions para candidatos de planejamento; o primeiro deriva Rick da memória real, o segundo só aceita Target explicitamente conhecido.
- Modificar `Assets/PEGA/NPC/PEGANpcNavigationAction.cs`: expor uma observação física tipada de conclusão sem redefinir `EffectObserved` como predicado de Desejo.
- Criar `Assets/PEGA/NPC/Temporary/PEGANpcPlanSelectionValidationProbe.cs`: ferramenta descartável para Mission existente, com comandos manuais e resumos canônicos separados para `IsolatedFixture` e `LiveKnowledge`.
- O Unity cria os `.meta` na importação. Não editar YAML da Mission/prefabs nem adicionar a ferramenta automaticamente. O profile de demonstração e o probe são temporários; `PEGANpcDesireState` é um componente definitivo, anexado manualmente ao root do NPC no Inspector e preservado após a validação.

## Tarefa 1: Contrato de Desejos e estado individual

**Arquivos:**
- Criar `Assets/PEGA/NPC/PEGANpcDesireProfile.cs`.
- Criar `Assets/PEGA/NPC/PEGANpcDesireState.cs`.

**Interfaces:**
- `PEGANpcDesireProfile.TryGetContext(PEGANpcDesireContext context, out PEGANpcDesireContextDefinition definition)` retorna a configuração declarada ou falha com diagnóstico; não aplica default silencioso.
- Cada `PEGANpcDesireDefinition` declara ID estável, Contexto, peso base, regra de aplicabilidade, efeito/predicado de objetivo, ganho, modificador de satisfação e limite de execuções.
- Limite usa valor explícito para repetição ilimitada; IDs duplicados, pesos/ganhos não finitos e parâmetros inválidos causam erro de validação.
- `PEGANpcDesireState` expõe snapshots imutáveis por ID do Desejo e fica no root individual do NPC. Ele não é anexado por código e não recebe Profile por descoberta implícita.

- [ ] Definir no profile os tipos PEGA serializáveis necessários para o Contexto `Normal` deste corte e os dois predicados ligados às Actions existentes; manter Rick/escape apenas como dados de demonstração temporária.
- [ ] Implementar validação explícita de perfil: IDs estáveis únicos, um registro por Contexto, valores finitos e faixa documentada; registrar erro sem escolher outra configuração como fallback.
- [ ] Implementar o ledger transitório em `PEGANpcDesireState`, isolado por instância e com consultas que não retornem coleção mutável interna.
- [ ] Fazer inspeção estática do fluxo: sem persistência de World State como satisfação e sem compartilhar ledger entre NPCs.
- [ ] Commitar a unidade após revisão estática: `feat(pega): add NPC desire profile and state`.

## Tarefa 2: Contratos de planejamento e seletor

**Arquivos:**
- Criar `Assets/PEGA/NPC/PEGANpcPlanSelectionContracts.cs`.
- Criar `Assets/PEGA/NPC/PEGANpcPlanSelector.cs`.

**Interfaces:**
- `PEGANpcPlanningInput` declara `ValidationMode` (`LiveKnowledge` ou `IsolatedFixture`), Contexto, fatos conhecidos, Targets e candidatos disponíveis; é imutável durante uma avaliação.
- `PEGANpcPlanningTarget` tem identidade PEGA tipada, posição conhecida, proveniência e momento da evidência. Em modo live, só aceita proveniência de conhecimento admitida; a Fixture isolada é rejeitada como fonte live.
- `PEGANpcPlanningActionCandidate` informa ID/efeito da Action, Target, pré-condições verificadas, rota estimada, regime de velocidade e duração intrínseca resolvida. Nenhum campo representa efeito já observado.
- `PEGANpcPlanSelector.Select(PEGANpcPlanningInput input, PEGANpcDesireContextDefinition context, PEGANpcDesireStateSnapshot satisfaction)` retorna todas as avaliações aplicáveis e candidatas, o estado `NoPlan` quando não há solução e a escolha determinística quando há.
- Este primeiro seletor suporta plano de um passo, sem busca de cadeias; score = `effectiveWeight × 1/(1 + estimatedSeconds/referenceSeconds)`. Empates: ID estável do Desejo, Action, Target; nenhuma ordem de enumeração, InstanceID ou aleatoriedade.

- [ ] Implementar a validação de proveniência por modo; recusar `DetachedFixture` em `LiveKnowledge` e reportar diagnóstico identificável.
- [ ] Implementar aplicabilidade/contexto/limite independentemente da existência de plano; candidatos inexistentes preservam aplicabilidade e retornam `NoPlan`.
- [ ] Construir um plano de um passo para cada combinação compatível de Action/Target cujo efeito satisfaz o predicado do Desejo e cujas pré-condições estão satisfeitas; descartar `PathPartial`/`PathInvalid`.
- [ ] Calcular segundos como rota/speed resolvida + duração intrínseca; excluir valores não finitos/negativos e Targets sem rota completa.
- [ ] Aplicar a política configurada mantendo peso e tempo disponíveis separadamente no snapshot; estabilizar a escolha por IDs antes de retornar.
- [ ] Conferir manualmente o algoritmo com entradas destacadas: 0 candidatos, 2 planos válidos, alteração isolada de fatos, peso e custo, empate com listas em ordem oposta, `PathPartial` e custo inválido. Nenhum desses dados é atribuído ao NPC.
- [ ] Commitar a unidade após `git diff --check`: `feat(pega): select deterministic NPC plans`.

## Tarefa 3: Estimativa física e candidatos das Actions existentes

**Arquivos:**
- Modificar `Assets/PEGA/NPC/PEGANpcNavMeshMovementIntentSource.cs`.
- Modificar `Assets/PEGA/NPC/PEGANpcApproachKnownRickAction.cs`.
- Modificar `Assets/PEGA/NPC/PEGANpcTravelToEscapeAction.cs`.

**Interfaces:**
- `PEGANpcNavMeshMovementIntentSource.TryEstimateRoute(Vector3 targetPosition, PEGAActorMovementSpeedRegime speedRegime, float arrivalRadiusMeters, out PEGANpcNavigationRouteEstimate estimate, out string diagnostic)` usa `NavMeshAgent.CalculatePath` com a configuração ativa do agente (tipo, máscara e custos de área), além de `PEGAActorMovementMechanic.TryGetMaximumSpeed`; devolve comprimento, velocidade e segundos estimados.
- `PEGANpcApproachKnownRickAction.TryCreatePlanningCandidate(out PEGANpcPlanningActionCandidate candidate, out string diagnostic)` só publica o último ponto de Rick após validar referência/identidade/fato conhecido deste NPC; não lê o Transform atual de Rick e não chama `TryStart`.
- `PEGANpcTravelToEscapeAction.TryCreatePlanningCandidate(PEGANpcPlanningTarget target, PEGAActorMovementSpeedRegime speedRegime, out PEGANpcPlanningActionCandidate candidate, out string diagnostic)` requer Target com proveniência de conhecimento. Nenhum provider live de saída existe no escopo, então o método não fabrica E01 nem a torna candidata.

- [ ] Implementar `TryEstimateRoute` com `NavMeshAgent.CalculatePath(targetPosition, path)`, verificando `NavMeshPathStatus.PathComplete`, e o mesmo regime de velocidade/raio da execução.
- [ ] Executar a consulta apenas numa avaliação explícita do seletor, não por frame: `CalculatePath` é síncrono e o custo cresce com o número de consultas.
- [ ] Confirmar por inspeção que a consulta não altera `NavMeshAgent.path`, `speed`, `stoppingDistance`, `isStopped`, `_currentRequestId`, Knowledge State ou Actor Transform.
- [ ] Adaptar a Action de Rick para converter apenas fatos de identidade e última posição aceitos no Knowledge State individual em um Target de proveniência legítima.
- [ ] Adaptar a Action de fuga para aceitar uma entrada Target já legitimamente conhecida, sem resolver `PEGANpcEscapeDestination` na cena e sem adicionar produtor de fatos.
- [ ] Usar o regime já exigido pelo método de execução de cada Action; não adicionar enum de velocidade nem redefinir os regimes do #63.
- [ ] Commitar a unidade após revisão de ausência de efeitos colaterais: `feat(pega): estimate NPC action routes for planning`.

## Tarefa 4: Observação real e satisfação de todos os Desejos

**Arquivos:**
- Modificar `Assets/PEGA/NPC/PEGANpcNavigationAction.cs`.
- Modificar `Assets/PEGA/NPC/PEGANpcDesireState.cs`.

**Interfaces:**
- `PEGANpcNavigationAction.TryGetObservedEffect(out PEGANpcObservedActionEffect effect)` produz observação somente quando há snapshot de navegação, conclusão `Success`, Target de efeito resolvido e distância física medida dentro do raio de chegada declarado.
- `PEGANpcDesireState.RecordCompletedAction(PEGANpcObservedActionEffect observedEffect, PEGANpcDesireContextDefinition activeContext)` avalia todas as definições do Contexto e incrementa separadamente cada ledger cujo predicado corresponde ao efeito observado; retorna IDs/ganhos aplicados para log/teste.
- A atualização não depende do Desejo que originou o plano. `CurrentExecution.EffectObserved` não é usado isoladamente, pois no contrato atual equivale apenas a `Outcome == Success`.

- [ ] Criar o tipo imutável de efeito observado a partir do snapshot físico existente, preservando a declaração prevista como dado distinto.
- [ ] Implementar a avaliação das definições ativas por `PEGANpcActionEffectKind`/Target observado; não aceitar Desired Goal previsto como evidência.
- [ ] Atualizar todas as definições satisfeitas, com ganhos e limites independentes por ID, sem gravar fatos no Knowledge State.
- [ ] Cobrir em revisão de código: sucesso sem chegada não incrementa; resultado inválido/cancelado não incrementa; uma chegada efetiva satisfaz mais de um Desejo que declare o mesmo predicado; origem do plano não filtra.
- [ ] Commitar a unidade após `git diff --check`: `feat(pega): record observed NPC desire satisfaction`.

## Tarefa 5: Probe temporário e execução manual no Unity

**Arquivos:**
- Criar `Assets/PEGA/NPC/Temporary/PEGANpcPlanSelectionValidationProbe.cs`.
- Usar a Mission existente `Assets/PEGA/Scenes/MissionActivityContent.unity`; não editar a cena/prefabs em texto.

**Interfaces:**
- Métodos explícitos de menu de contexto no probe: `RunIsolatedSelectorChecks()`, `RunLiveKnowledgeCheck()`, `BeginObservedMovementCheck()` e `ReportNpcLedgerIsolation()`.
- Cada método retorna um relatório local com checks contados e escreve um único resumo final `result=PASS|FAIL`, `checksPassed`, `checksFailed` via `PEGALogging`; logs separados por `validationMode`, nunca por frame.
- O probe pode iniciar a Action física somente após comando manual explícito; seleção sozinha nunca chama `TryStart`.

- [ ] No Inspector do projeto, criar um `PEGANpcDesireProfile` temporário em `Assets/PEGA/NPC/Temporary`, com duas definições claramente rotuladas como demo; uma segunda definição com o mesmo predicado de satisfação comprova o ledger múltiplo. Não criar asset de arquétipo de gameplay.
- [ ] Anexar manualmente `PEGANpcDesireState` e o probe às instâncias existentes necessárias para a checagem; atribuir apenas referências/Actor Profiles existentes e o profile temporário.
- [ ] Fazer `RunIsolatedSelectorChecks()` verificar NoPlan, dois planos destacados, mudança da escolha após mudar somente os fatos destacados, alteração independente de peso/custo, desempate com ordem invertida e ausência de efeito em Requests/Knowledge State dos NPCs.
- [ ] Fazer `RunLiveKnowledgeCheck()` consultar fatos produzidos pela percepção #69, selecionar o candidato de Rick a partir da última posição conhecida, verificar que a consulta de rota preservou o pedido ativo e declarar explicitamente que E01 não é Target conhecido e não há alternativa live de fuga.
- [ ] Fazer `BeginObservedMovementCheck()` iniciar manualmente a Action #63 pelo Target conhecido; após conclusão física, conferir previsão versus resultado e incremento das duas definições correspondentes. Confirmar que outro NPC mantém ledger independente e que resultado não concluído não concede satisfação.
- [ ] Executar apenas checagens estáticas permitidas (`git diff --check`, revisão de fontes e referências). Deixar import/compile, bake, Play Mode e logs executados para o usuário no Editor.
- [ ] Commitar o recurso descartável separadamente: `test(pega): add temporary NPC plan selection probe`.

## Tarefa 6: Aceite condicional e limpeza

**Arquivos:**
- Remover após validação aprovada: `Assets/PEGA/NPC/Temporary/PEGANpcPlanSelectionValidationProbe.cs` e somente o profile/dados temporários criados para a demonstração.
- Preservar os contratos definitivos de Desire, seleção, estado individual, observação e estimativa de rota.

- [ ] O usuário confirma no Unity a importação/compilação e os resumos PASS/FAIL separados para fixture e conhecimento live; registrar no relatório os casos não disponíveis.
- [ ] Não fechar o aceite de alternativas reais nem o #64 enquanto a Mission não fornecer ao menos dois Targets legitimamente conhecidos compatíveis com o conteúdo de Desejo aprovado; o fixture isolado não substitui esse critério.
- [ ] Depois da confirmação do usuário, remover somente probe e profile/dados temporários em `NPC/Temporary`; manter o componente `PEGANpcDesireState` no root dos NPCs que usam a capacidade. Conferir `git status` para preservar alterações alheias e executar `git diff --check`.
- [ ] Registrar as evidências no card somente após a validação correspondente; não alterar Trello durante a implementação antes do resultado manual.
- [ ] Commitar a limpeza isolada: `test(pega): remove temporary NPC plan selection probe`.

## Sequência de validação manual

1. Abrir a Mission existente e confirmar no Console a compilação/importação sem erros.
2. Invocar `RunIsolatedSelectorChecks`: exigir os casos NoPlan, duas alternativas, peso, custo e desempate invariável à ordem como `PASS`; o resumo precisa indicar `validationMode=IsolatedFixture`.
3. Invocar `RunLiveKnowledgeCheck` após percepção real registrar Rick; exigir Target igual à última posição conhecida e nenhum pedido de movimento iniciado pela seleção. O resumo deve indicar `validationMode=LiveKnowledge` e marcar alternativa live de fuga como indisponível, não como aprovada. Quando #69 observar Rick em posição mais recente, a reavaliação live deve usar esse novo Target; isso não conta como prova de escolha de um plano alternativo.
4. Invocar `BeginObservedMovementCheck`; observar deslocamento físico pelo sistema #63. Ao fim, exigir que as definições cujo predicado corresponde à chegada física incrementem, que outro NPC permaneça sem alteração e que conhecimento não seja escrito pelo planner.
5. Repetir a seleção após #69 produzir uma posição mais recente; confirmar novo Target/custo na próxima chamada explícita, sem decisão automática. O caso de plano diferente em resposta a fatos alterados é comprovado pela fixture isolada e continua sem evidência live até existirem alternativas legítimas.
6. Registrar PASS/FAIL, checks, Actor root, Target/fonte da evidência e modos de validação. A validação Unity permanece pendente até execução do usuário; somente depois remover o probe/profile temporário.

## Limitações e gate do card

- O workspace fornece um Target real de perseguição (última posição conhecida de Rick) via #69 e as duas Actions físicas #63. Ele não fornece fato de portador do Item Principal nem Target de saída conhecido.
- GDD sustenta perseguição de portador conhecido e fuga configurável, mas não define Rick como alvo padrão do ladrão. Portanto, nenhuma configuração permanente de `Approach Rick` ou peso de missão é introduzida por este plano.
- A fixture destacada comprova a capacidade matemática de escolher duas alternativas; não comprova a integração live de fuga nem satisfaz o aceite do card que exige escolha entre Actions/Targets reais e conhecidos.
- Este corte só cria planos de um passo. Cadeias, escolha autônoma de plano, interrupções, execução integrada e replanejamento ficam nas etapas correspondentes, incluindo #91 e o trabalho posterior de reavaliação.
- Unity Editor/Play Mode é validação manual do usuário. Codex fará apenas inspeção estática e `git diff --check`; não será reportado PASS de execução Unity sem os logs do Editor.
