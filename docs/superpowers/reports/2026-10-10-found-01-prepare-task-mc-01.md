# Prepare Task — MC-01 / Trello #75

Data: 2026-10-10  
Estado: preparação concluída; nenhuma implementação executada.

## 1. Cartão e estado operacional

Cartão: [#75 — Integrar Player/Actor do Framework na missão](https://trello.com/c/EfPEV4Ip) (`ari:cloud:trello::card/workspace/5b2188c22a562119a0557fff/6acaa9cd4b150b7504b6aceb`). No momento da leitura: aberto, lista “A fazer”, estimativa v2 de 4 h, realizado 0 h, prazo 16/10/2026. Descrição operacional atual prescreve Manager-Provisioned, entrada P1 direta e Actor default sem UI de seleção; câmera final (#29), movimento/colisão (#34), P2, split-screen e seleção de personagem ficam fora do cartão.

Checklist especializado `trelloReadChecklist/list_by_card`: 2 checklists, 6 itens, todos incompletos. Preconditions: (1) confirmar contratos do Framework instalado; (2) confirmar a composição da missão antes de #34/#29. Execution/Acceptance: (3) FOUND-01 concluído; (4) Player Session, Slot, ActorProfile padrão e Manager-Provisioned com Join P1 explícito, Host/Actor na Mission Activity sem UI de seleção; (5) Input Gate relacionado a `PlayerGameplayInputReader`, câmera #29 e movimento #34; (6) repetir Play Mode sem duplicação de Actor. Não foi consultada/escrita nenhuma ação de Trello além das leituras.

## 2. Rastreabilidade PEGA

- ADR: `PEGA GDD/ADR-PREPRODUCAO.md`, adendo v2 de 10/10/2026 define Mission Route e Activities; a referência operacional de #75 dizia Scene-Provided. O texto foi reconciliado pontualmente para Manager-Provisioned e seleção default sem UI. O registro de Scene-Provided como protótipo/histórico foi preservado.
- TASKS: `PEGA GDD/TASKS.md` mantém FOUND-01 como pré-requisito de #75 e não declara estratégia conflitante; nenhuma alteração necessária.
- MVP: `PEGA GDD/MVP.md` distingue o teste Scene-Provided da feature de produção, mas não determina o modelo de provisioning. Sem alteração.
- GDD: não foi localizada decisão normativa de gameplay que determine Scene-Provided ou Manager-Provisioned; não é necessário mudar GDD para preparar este cartão.
- Foundational prerequisite: FOUND-01 foi declarado tecnicamente aprovado nas instruções e seus registros locais/documentação tratam do fluxo próprio Mission/Result. Esta preparação não reabre seu aceite.

## 3. Resolução e contratos Immersive

Consumidor PEGA: Unity `6000.5.0f1` (`88b47c5e7076`), `Packages/manifest.json` e `packages-lock.json` resolvem `com.immersive.framework` `1.1.0-preview.6`, registry; cache `Library/PackageCache/com.immersive.framework@ed40a507d908`, package revision identificada nos registros de FOUND-01 como `fca1dd130bbd9a953526c6b7b21fedead4ab1e07`. Evidência de API principal: código e docs instalados, em especial `Documentation~/README.md`, `Guides/Player-Usage.md`, `Guides/Activity-Readiness.md`, `Guides/Game-Flow.md`, `Guides/Camera-Usage.md`, `API/Public-API.md`, `Runtime/PlayerParticipation` e validadores Editor. Não inferir comportamento runtime sem a validação manual.

| Superfície | Contrato observado em preview.6 | Maturidade/limite |
|---|---|---|
| `GameApplicationAsset` | Player Session é opt-in, exige `DefaultPlayerSessionProfile` quando habilitado; a política `PlayerActorSelectionDuplicatePolicy` precisa ser definida mesmo com Session desabilitada. Camera Session é sempre validada. | Aplicação Stable; composição geral do Player é mista. |
| `CameraSessionConfiguration.TryValidate` | Exige 1..N prefabs de Output explícitos, sem duplicatas, cada um com um `CameraOutputAuthoring`, Unity Camera e CinemachineBrain no mesmo GameObject, fallback rig válido e pertencente ao prefab. | Experimental; neste consumidor já foi resolvida por FOUND-01 com Camera Output técnico PEGA. Continua requisito independente de Player/câmera final. |
| `PlayerSessionProfile` | Lista de Slots suportados sem duplicatas/IDs inválidos, `HostProvisioning` e `ActorResolutionPolicy` definidos. Defaults no tipo: Manager-Provisioned e ResolveConfiguredDefault, mas autoria explícita e validadores permanecem necessários. | Experimental. |
| `PlayerSlotProfile` | ID estável obrigatório; pode referenciar ActorProfile default. | Stable para uso Scene-Provided; Manager-Provisioned/Session persistente Experimental. |
| `ActorProfile` | ID estável e tipo/role definidos; apresentação visual opcional, subordinada ao Actor físico. | Experimental. Default e descrição visual não constituem gameplay funcional. |
| `LocalPlayerProvisioningAuthoring` | Referencia explicitamente `PlayerInputManager` autorizado e prefab Local Player Host; Manager prefab precisa corresponder, e Join é solicitado explicitamente. Não provisiona em callback de lifecycle por si só. | Experimental; `TryMaterializeManagerPrefab` pode escrever `PlayerInputManager.playerPrefab` em execução, requer verificação de efeitos e repetição. |
| Actor Runtime Host | `PlayerActorRuntimeHost` requer `PlayerActorDeclaration` canônico no próprio GameObject e não permite `PlayerInput` ali; Input pertence ao Local Player Host. Visual mount é opcional. | Experimental. A autoria real e validação do prefab PEGA ainda são trabalho de #75. |
| Activity Player projection/readiness | Activity pode explicitar Slots, regra para zero participantes e nível de requisito; `GameplayReady` significa projeção contextual de gameplay pronta, sem provar locomotion, câmera ou consumidor de gameplay. | Participação/readiness Experimental para a integração. Ver `ActivityParticipationProjectionAuthoringValidator`. |
| Join/default selection/Input | `PlayerSessionJoinCommandTrigger` pede Join Manager-Provisioned para Slot tipado; `PlayerSessionDefaultActorSelectionCommandTrigger` aplica o default. `PlayerGameplayInputReader` e gate do Host são domínios distintos; Input Gate não substitui readiness. | Triggers e contratos Session experimentais; o bloco/gate Stable é limitado ao fluxo documentado e não estabiliza toda a API Session. |
| Camera Actor Subject | Manager-Provisioned converge nos mesmos semânticos de Actor/Subject; a doc exige Subject explícito quando composição de câmera o requer. #75 não cria assignment final nem Camera Subject para #29 sem comprovar necessidade do validador do Actor no caso sem câmera de gameplay. | Actor Subject/assignment Experimental; necessidade específica sem câmera final não provada em execução. |

### Gatilhos de validação encontrados (inspeção estática)

- `Editor/Validation/FrameworkAuthoringValidator.ValidateGameApplication`: startup Route, Camera Session, assignments, Persistent Content, identidade inicial e frame rate; Camera Session não é opcional por haver Player Session desabilitada.
- `Editor/PlayerParticipation/PlayerParticipationAuthoringValidator.ValidateGameApplication`: política de duplicata inválida gera erro antes do early-return de Session desabilitada; com Session ativa exige Profile e valida Slots/configuração.
- `PlayerSessionProfile.TryValidate` e `PlayerSessionConfigurationResolver.Resolve`: rejects nulos/IDs duplicados/ inválidos e enum não definido; resolução sem mutação do runtime.
- `LocalPlayerProvisioningAuthoring`: requer manager e Host explícitos, verifica divergência e materializa prefab no `PlayerInputManager` durante operação específica. Editor inspector de provisioning valida Session ativa e Profile.
- `ActivityParticipationProjectionAuthoringValidator`: verifica projeção, Slots explícitos, duplicatas, zero-participant policy e requisitos de readiness.

A tabela é inspeção da instalação, não execução dos validadores no PEGA. O GameApplication final já usa `UniqueAcrossJoinedSlots`, configuração específica PEGA compatível com o enum e o objetivo futuro de seleção local P1/P2; não é default recomendável para todos os consumidores.

## 4. FirstGame — exemplos consultados e compatibilidade

Consulta somente leitura do repositório `ImmersiveGames/planet-devourer` (árvore main observada em `98a0c23ea33f11e721afe80161fe0aba8cb7acc7`):

- `Assets/_Sample/PlayerSamples/ManagerProvisioned/README.md` e `EXPECTED-ASSETS.md`: perfil de Session com Slot explícito, `Initial Joining Open`, Manager-Provisioned e default Actor; `Local_Player_Provisioning` usa `PlayerInputManager` em manual Join (máximo 1) e `LocalPlayerProvisioningAuthoring`; Host técnico e Actor canônico são separados; atividade exige `GameplayReady` e relocation anchor; o exemplo inclui movement, CharacterController e câmera Third Person, que não devem ser copiados para #75.
- `CharacterSelectionMultiplayerSplitScreen/README.md`: é caminho P2/split-screen com duas Slots/Outputs e seleção explícita Farmer/Cow; confirma uma extensão futura, mas não é arquitetura a importar em #75.
- `CharacterSelection/README.md`: continua Manager-Provisioned e começa com `LeaveUnresolved`/seleção explícita para a feature de seleção; diferencia esse fluxo futuro da política de default sem UI pedida agora.
- Manifest/lock do FirstGame resolvem Framework por `file:C:/Projetos/ImmersivePackages/com.immersive.framework`; o package.json acessível informa `1.1.0-preview.7`. O SHA/revisão Git do path local não pôde ser lido: acesso ao repositório irmão foi negado. Assim, revisão exata fica `unknown`; exemplo é precedente de composição, não contrato compatível provado com o consumidor `.6`.
- A documentação instalada `.6` classifica Session profile, provisioning, ActorProfile, scoped access/commands e Actor runtime host como Experimental. FirstGame não eleva maturidade e usa `.7` sem SHA imutável confirmado.

## 5. Composição mínima proposta para #75

1. Reutilizar PEGA GameApplication, Mission Route, Mission Activity, Persistent Content e Camera Output técnico existentes. Habilitar Session explicitamente; apontar para um novo `PlayerSessionProfile` PEGA.
2. Criar um `PlayerSlotProfile` P1 com ID estável e um `ActorProfile` default classificado Player/Protagonist. Para manter a extensão P2 possível, definir já a política PEGA de seleção sem duplicatas `UniqueAcrossJoinedSlots`; não criar Slot P2 ou UI agora.
3. No Session Profile, configurar apenas Slot P1, Manager-Provisioned, resolução do default e política de Join que permita solicitação explícita no momento de teste. Escolher e provar se Join já começa aberto ou se abrir/fechar pertence a comandos da missão; não embutir decisão em callbacks ad hoc.
4. Criar o prefab técnico Local Player Host separado do Actor. Configurar `PlayerInput`, `LocalPlayerHostAuthoring`, gate adapter e `ActorMount` conforme composição/validador `.6`; Actor prefab separado com `PlayerActorRuntimeHost` e declaração canônica. Sem movement, colisão, camera rig, assignment ou Presentation de gameplay. Verificar se Subject é exigido pelo validador de Actor mesmo sem Assignment; se sim, usar um transform técnico explícito no Actor (não câmera funcional), sem adiantar #29.
5. Colocar `LocalPlayerProvisioningAuthoring` em Persistent Content com referências tipadas e um `PlayerInputManager` explícito, garantindo correspondência do prefab de Host. Fazer pedido de Join por API/trigger pública no escopo correto; não confiar em auto-Join nem em `PlayerInputManager.instance`.
6. Tornar Mission Activity explícita quanto ao Slot projetado, zero participantes (permitido antes do Join para preservar a entrada sem Player do FOUND-01, se contrato e requisito confirmarem) e nível mínimo. Validar ordem para evitar ciclo: a Activity precisa poder ficar Ready sem P1; Join ocorre a partir de conteúdo já ativo; depois observar admissão/preparação e `GameplayReady`. Acessos Session com escopo Activity devem ficar em conteúdo que o scope descobre, não em Persistent Content arbitrário.
7. Exercitar P1 join → Host/Actor materializados → Activity readiness → Input Gate/readiness; depois Leave/Rejoin e repetição de Play Mode conferindo um Host/Actor por ocorrência, teardown sem duplicatas e sem atribuição da câmera definitiva.

Composição acima é proposta, condicionada aos validadores `.6` e às APIs efetivamente compiláveis no Editor. Não criar PlayerInputManager ou trigger paralelos à autoridade Framework; não usar Scene-Provided.

## 6. Riscos, dependências e esforço

**Pré-requisito:** FOUND-01 considerado concluído/aprovado pelo solicitante. Dependências funcionais: #29 (câmera final) e #34 (movimento/colisão) continuam posteriores; #75 precisa demonstrar Actor sem essas funções. A falta de Subject/locomoção pode limitar `GameplayReady` sem desabilitar o objetivo de provar o ciclo de materialização — o critério deve focar evidências suportadas pelo contrato e registrar essa limitação.

**Risco alto:** APIs usadas são majoritariamente Experimental e o exemplo FirstGame é `.7`, não `.6` fixado no PEGA. Há gates escondidos em validadores em cascata; o precedente não substitui inspeção dos assets `.6`.

**Risco alto:** sequência Activity Ready → Join versus Join antes/durante readiness pode formar dependência circular. Confirmar a regra de zero participants e readiness no Inspector/runtime antes de fechar os assets.

**Risco médio:** Unity `PlayerInputManager` behavior/event timing, `PlayerInput` gate, materialização de `playerPrefab` e teardown/Rejoin podem criar duplicação ou ordem de entrada divergente. Verificar Side Effects do Inspector e logs de lifecycle.

**Risco médio:** duas Slots futuras exigem IDs distintos, contagem adequada e resolução de Actor compatível com `UniqueAcrossJoinedSlots`; não implementar capacidade P2 nesta tarefa.

Estimativa Trello de 4 h parece agressiva para construir e validar a primeira composição Manager-Provisioned experimental, incluindo readiness, Input Gate, rejoin e Play Mode repetido. Recomendação de planejamento: estimar 6–8 h incluindo autoria e validação manual, mantendo margem adicional se o Actor sem gameplay não satisfizer os gates `.6`. Isso é análise, não atualização da estimativa/Trello.

## 7. Plano de execução sugerido

1. Confirmar no Unity Package Manager/PackageCache que `.6` segue resolvido; registrar hash/revisão do pacote e abrir os validadores GameApplication, Player Participation, Activity e Host.
2. Verificar os gates incondicionais e condicionais de aplicação e os validadores de Session, Slot, Actor, Local Player Host/Provisioning, prefab, Activity projection/readiness e scopes; salvar matriz de validadores e side effects.
3. Autorizar/confirmar a decisão de Join explícito dentro da Mission (ação/input) mantendo a entrada FOUND-01 sem Player e confirmar nível de readiness que pode funcionar sem locomotion/câmera.
4. Criar somente os Profiles e prefabs técnicos PEGA mínimos; configurar aplicação/Session/Host/Actor e conteúdo mission-owned, sem alterar FrameworkValidation.
5. Rodar validadores de autoria no Editor; reparar composição via APIs públicas; registrar cada resultado e efeitos de escrita.
6. Play Mode: P1 Join, Host e Actor únicos, default aplicado sem UI, Mission readiness, Input gate, Leave/Rejoin, transições Mission/Result/retorno se aplicável e repetição sem duplicação. Capturar Editor.log/runtime snapshots.
7. Revisar diff, integridade de referências/GUIDs, `git diff --check`, atualizar evidência e solicitar revisão do plano antes de implementação. Não alterar estimate ou Trello sem autorização.

## 8. Critérios de aceite executáveis

1. PEGA GameApplication ativa permanece válida com Player Session habilitada, Profile explícito e duplicata definida.
2. Session/Profile/Slot P1 passam validadores; configuração explícita Manager-Provisioned e default Actor resolvido.
3. P1 entra por Join explícito; o Framework provisiona exatamente um Local Player Host ligado ao PlayerInputManager autorizado.
4. Default Actor é selecionado sem menu/UI; ocorrência Actor tem declaration/runtime host válidos e nenhuma cópia Scene-Provided.
5. Mission Activity aceita o estado inicial sem Player (se aprovado pelos validadores) e projeta P1 após Join; readiness observada no nível planejado sem declarar movimento/câmera prontos.
6. Input Gate bloqueia e restaura o input pelo Host/Gate oficial, sem segundo escritor de PlayerInput.
7. Leave/Rejoin gera teardown e nova ocorrência corretamente; reentrada/repetição Play Mode não acumula Player/Actor/Host.
8. Nenhuma dependência de FrameworkValidation, nenhuma mudança de pacote/framework, nenhum Player P2/split-screen/seleção UI/câmera ou movimento finais.
9. Evidência traz versão/revisão do consumer e do Framework, validadores, logs, sequência runtime, limitation e resultado de cada critério; não confunde Player/Actor lifecycle com gameplay funcional.

## 9. Validação desta preparação

- Leitura de arquivos/resolução/cache e inspeção C# / Markdown: executada, estática.
- Exemplos FirstGame: documentação e manifest/ProjectVersion consultados read-only. SHA Git do package local FirstGame: `unknown` por permissão negada no caminho local.
- Unity Editor/compile/import/validadores/Play Mode: não executados, conforme AGENTS.md.
- Trello: card e checklist somente leitura; nenhuma escrita.
- Nenhuma implementação executada.

## 10. Fontes principais

- PEGA: `PEGA GDD/TASKS.md`, `PEGA GDD/ADR-PREPRODUCAO.md` §5, adendo v2 e §10; `PEGA GDD/MVP.md`.
- Instalado PEGA: `Library/PackageCache/com.immersive.framework@ed40a507d908/Documentation~/README.md`, `Guides/Player-Usage.md`, `Guides/Activity-Readiness.md`, `Guides/Game-Flow.md`, `Guides/Camera-Usage.md`, `API/Public-API.md`; `Runtime/Authoring/GameApplicationAsset.cs`; `Runtime/CameraAuthoring/CameraSessionConfiguration.cs`; `Runtime/PlayerParticipation/Authoring/PlayerSessionProfile.cs`, `PlayerSlotProfile.cs`, `LocalPlayerProvisioningAuthoring.cs`; `Runtime/Actors/ActorProfile.cs`; `Runtime/PlayerParticipation/Authoring/PlayerActorRuntimeHost.cs`; `Editor/Validation/FrameworkAuthoringValidator.cs`; `Editor/PlayerParticipation/PlayerParticipationAuthoringValidator.cs`, `ActivityParticipationProjectionAuthoringValidator.cs`.
- FirstGame: `ImmersiveGames/planet-devourer` main at observed tree `98a0c23ea33f11e721afe80161fe0aba8cb7acc7`, `Assets/_Sample/PlayerSamples/ManagerProvisioned/{README.md,EXPECTED-ASSETS.md}`, `CharacterSelectionMultiplayerSplitScreen/README.md`, `CharacterSelection/README.md`, `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`.
