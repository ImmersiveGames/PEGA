# PEGA — Group Knowledge

> Registro cronológico de implementação. As afirmações de estado nas etapas anteriores descrevem o que havia sido verificado naquela data; a consolidação da demonstração completa está na última atualização.

- **Data:** 2026-11-02
- **Status:** implementação presente; o responsável reportou validação visual no Play Mode das três investigações, chegada à saída e satisfação dos Goals em 2026-11-03, corroboradas pelos logs do Unity. O Codex não executou Unity nem inspecionou os logs brutos.
- **Decisão arquitetural:** [ADR-GROUP-KNOWLEDGE-01](../../ADRs/ADR-GROUP-KNOWLEDGE-01-conhecimento-compartilhado-e-previo.md)
- **Etapa anterior:** [Global Knowledge](2026-11-02-global-knowledge-implementation.md)

## Implementação

`PEGAActorGroupComposition` continua usando o contrato oficial `ActorGroupAccessBinding` para criar Groups e confirmar memberships no lifecycle do owner. `PEGAActorGroupKnowledgeState` é uma capacidade PEGA opcional ao lado dessa composição. Depois que o Framework retorna e confirma um Group vivo, o state registra um `PEGAKnowledgeFactStore` genérico, com escopo Group, para o `GroupId` real. Cada Group mantém seu próprio store; nenhum fato é copiado para os Knowledge States Individuais.

`PEGAActorGroupKnowledgeReader` consulta o state explicitamente referenciado e verifica em cada leitura que o Group continua vivo e que o `ActorId` do leitor é membro vigente via `ActorGroupAccessBinding.TryIsMember`. Uma consulta negada retorna `NotMember` com fato `Unknown` vazio; a ausência de fato autorizado continua distinta (`Authorized` com `Unknown`). `Leave` revoga o acesso na consulta seguinte. O encerramento do owner PEGA limpa seus stores. A falta do componente Group Knowledge não impede criar ou usar Actor Groups.

Fatos usam as definições tipadas, evidências, validade, proveniência e políticas do núcleo PEGA, incluindo `PEGAKnowledgeFactStore`; a implementação não transfere Knowledge para o Framework. Global permanece uma authority compartilhada independente de membership, e Individual permanece separado.

Individual Knowledge é a implementação pré-existente por NPC, com histórico de execução anterior descrito no contexto do ADR. Esta entrega não repetiu uma prova comportamental Individual; as provas Global e Group confirmam apenas que seus fatos não foram copiados para o escopo Individual.

## Mission e prova manual

A Mission mantém três larápios: dois membros do primeiro Group e um membro do segundo, com `GroupProfile` distinto. Os Group readers dos três NPCs referenciam a authority PEGA da composição. A criação dos Groups e memberships continua pertencendo ao Framework; conhecimento não é requisito para a composição.

O usuário relata ter executado no Unity, em 2026-11-02, as duas provas com resultado **PASS**:

- **Global:** a janela `PEGAGlobalKnowledgeProofWindow` registrou `false`, atualizou o fato Global para `true` e comparou valor, status, escopo, timestamp e proveniência entre vistas GOAP de NPCs em Groups distintos e uma vista independente. O Knowledge Individual permaneceu separado.
- **Group:** a janela `PEGAGroupKnowledgeProofWindow` registrou fatos distintos; os dois membros do primeiro Group leram o mesmo fato e o membro do segundo leu o próprio. As leituras cruzadas retornaram `NotMember` e fato vazio, sem valor ou proveniência. A prova também verificou revogação após `Leave`, restauração após `Join`, disponibilidade Global e ausência de cópia Individual. Nenhuma Action ou decisão do planner foi executada.

Este resultado é **relatado pelo usuário**. O Codex não executou Unity nem inspecionou log/captura da execução; nenhum artefato de saída foi anexado a este registro.

## Contrato oficial usado

O `Packages/manifest.json` do PEGA resolve `com.immersive.framework` por dependência local (`source: local` no lock); o pacote resolvido declara `1.2.0-preview.4` e seu repositório local estava em `master`, commit `a4537b0339ff07becb5f0504cbb21db4f25cdd09`. O contrato de Actor Groups está marcado **Experimental**. `GroupProfile` declara a identidade explícita (`GroupId`), mas sua referência não cria um Group. A criação explícita, lookup e membership passam pelo `ActorGroupAccessBinding` ligado após o commit de um owner Session, Route ou Activity; o Framework libera os Groups criados pelo owner exato no encerramento. O Framework não impõe autorização de gameplay; o PEGA consulta a membership vigente antes de devolver Knowledge.

## Limites observados na primeira etapa (2026-11-02)

- Não há propagação automática entre Individual, Group e Global.
- Na etapa inicial, a Mission ainda não tinha autoria de Group e a prova registrava fatos temporários no Play Mode. A evolução posterior descrita abaixo adicionou a configuração serializada de fatos iniciais Group e Global.
- Na prova inicial, o GOAP não integrava fatos Group a Actions, Desires ou decisões do planner. As atualizações posteriores deste relatório descrevem a integração causal e os limites atuais de autoria declarativa.
- Não há produtor de gameplay conectado ao fato demonstrador Global nem distribuição de eventos reais do assalto.
- Não foi alterado o Immersive Framework.

## Atualização — autoria inicial e diagnóstico dos três escopos (2026-11-03)

A evolução implementada reorganiza as definições tipadas canônicas e a entrada de autoria compartilhada sem introduzir um armazenamento concorrente. O Inspector usa o catálogo para compor **Identidade → Propriedade → Valor** em Individual, Group e Global. Individual, Group e Global continuam escrevendo em seus Knowledge States e stores existentes, com as respectivas authorities, políticas e evidências. Perception continua consumindo as mesmas definições canônicas.

Uma nova propriedade fica declarada como definição tipada canônica em `Assets/PEGA/NPC/Knowledge/Definitions`, com ID, tipos de endereço/valor e política. `PEGAKnowledgeFactDefinitionCatalog` encontra automaticamente os campos estáticos de definição e rejeita IDs duplicados; a matriz genérica do Inspector a apresenta quando os payloads tipados disponíveis cobrem seus tipos. Os consumidores continuam usando a definição sem uma lista ou editor duplicado por propriedade.

Para Group, `PEGAActorGroupComposition` cria os Groups reais usando o contrato oficial do Framework. `PEGAActorGroupKnowledgeState` só aplica a autoria do perfil declarado depois de confirmar o Group vivo criado pelo owner; as leituras dos membros verificam membership vigente. O Inspector GOAP mostra autoridades e fatos por escopo, propriedade, identidade, valor, origem, timestamps e validade, e oculta fatos Group quando a membership não autoriza a consulta.

Na cena `MissionActivityContent`, um NPC também tem um fato Individual inicial `pega.actor.last-observed-character-position` endereçado pelo Actor do Player resolvido via binding e com valor `(100, 0, 100)`. A Perception publica a mesma definição e identidade quando observa o Actor, com origem própria e a política canônica. Na etapa anterior de autoria/diagnóstico, o Second Group `pega.mission.actor-group.second` e o Global usavam a definição `pega.object-entry.position` para `pega.warehouse.storage-rack-west-north`, com valores `(-5, 1, 15.700001)` e `(200, 0, 200)`, respectivamente. A configuração final substituiu o fato Group desse rack pelas posições e papéis dos três POIs e da saída; o fato Global antigo do rack permanece isolado e não é composto no `Second Group Demo`. Os stores continuam independentes. O usuário reportou validação manual no Unity em 2026-11-03, incluindo autoria inicial, identidade/propriedade equivalentes entre Individual e Perception, leitura autorizada, acesso negado, isolamento entre Groups e conhecimento Global; esta atualização documental não repetiu a prova.

Continuam futuras a autoria genérica de qualquer predicado GOAP por assets, sensores/fontes de descoberta ou atualização de Object Entry, produtores de eventos reais do assalto e propagação automática entre escopos. A demonstração descrita ao final implementa condições tipadas de aplicabilidade e Goal para esta atividade, sem declarar uma superfície genérica para qualquer semântica.

## Atualização — posição de POI do Group em Target, Goal e navegação GOAP (2026-11-03)

### Implementação PEGA

A capacidade usa a Action declarativa `NavigateToKnownGroupObjectEntryPosition`, configurada no Actor somente por composição/override. `PEGAActorGroupKnowledgeReader` enumera fatos tipados `pega.object-entry.position` nas authorities Group explicitamente ligadas e consulta apenas Groups retornados pela membership vigente do Framework; cada snapshot mantém seu `GroupId`. A Action emite um Target cujo ID lógico combina `ObjectEntryId` e `GroupId`, com posição e instante de evidência do fato. O Target não é fabricado por descoberta da Transform nem por resolver a ocorrência física.

O GoalCondition relê o mesmo fato Group, confirma Group/membership/validade e exige o Effect de chegada correspondente. A Action causal declara um Effect hipotético `TargetReached` para planejamento; o planner verifica Preconditions/candidatura, a disponibilidade de rota real pelo provider existente e os Goals aplicáveis antes de escolher o plano pela política normal. Esse Effect projetado não grava World State ou Knowledge. Durante execução, a navegação usa o provider existente, o sucesso depende da distância configurada de chegada e o Effect só é observado quando a Action informa sucesso e o conhecimento Group continua autorizado. Não foi criado Target espacial paralelo nem fornecido destino direto ao provider fora do GOAP.

### Configuração da demonstração

`MissionRoute` declara o POI `pega.warehouse.second-group-demo-poi` com `ObjectEntryDeclaration` do Immersive Framework e `PEGAPointOfInterest`. O fato inicial `pega.object-entry.position` desse ID, valor `(0, 0, 10)`, estava configurado explicitamente na authority inicial do Second Group em `MissionActivityContent`; não havia fato Individual nem Global para esse POI. O override então compunha a Action e o antigo Profile de navegação a um único POI, depois substituído pelo Profile de investigação e saída. O Desire de chegar à posição usava peso base 24 e a política multiplicativa normal, sem prioridade absoluta.

Naquela configuração anterior à investigação, o fato Group de posição do rack `pega.warehouse.storage-rack-west-north` estava separado do fato Global antigo para o mesmo endereço (valores `(-5, 1, 15.700001)` e `(200, 0, 200)`, respectivamente). A configuração final da cena substitui o fato Group pelos POIs da atividade; o Global antigo do rack permanece independente, não é composto no demonstrador e não contém os Targets da atividade. A Action/condição consomem o Group Reader e não consultam o binding/occurrence query para completar uma posição conhecida.

A primeira tentativa usava a posição do rack dentro de seu volume físico; a Action exige rota NavMesh completa até a posição conhecida e o planejador descarta a candidatura indisponível antes de projetar seu Effect. Os logs fornecidos não incluem o enum específico da rota rejeitada; distingui-lo exige observação no Unity. A correção manteve o fato e a identidade lógica do rack e apontou o Goal para o POI demonstrativo separado no corredor.

### Evidência e limites

- **Implementação observada estaticamente:** a Action consulta Group Knowledge por membership efetiva; Target/Effect/GoalCondition mantêm a identidade tipada e exigem conhecimento autorizado; a avaliação e execução usam a rota/navegação existentes e confirmação de chegada. `git diff --check` e verificações estáticas de referências serializadas foram executados nesta consolidação.
- **Revisão arquitetural:** o parecer independente anterior foi `CLEAR` para o corte funcional, estaticamente. Isso não certifica importação, compilação ou execução Unity.
- **Execução relatada:** o responsável confirmou visualmente que o NPC iniciou o deslocamento em Play Mode após a correção. Nenhuma captura/log do novo Play Mode foi anexada ou inspecionada pelo Codex.
- **Não comprovado naquela etapa de navegação a um POI:** conclusão da navegação/chegada, Effect observado e satisfação final do Goal. A validação posterior do ciclo de três investigações e saída está registrada abaixo.

### Contratos e maturidade

O `Packages/manifest.json` do consumidor PEGA resolve localmente `com.immersive.framework` `1.2.0-preview.4`. Actor Groups e a superfície de identidade/autoria `ObjectEntryId`/`ObjectEntryDeclaration` estão classificadas **Experimental** na API resolvida. O POI usa `ObjectEntryDeclaration`; resolução física por occurrence query não participa desta feature. Nenhum arquivo/código do Framework foi alterado. As decisões de autoridade, separação de Knowledge e uso de ObjectEntryId já aprovadas permanecem as dos ADRs vinculados acima; esta integração é uma capacidade PEGA composta sobre elas.

## Atualização final — investigar três POIs e alcançar a saída (2026-11-03)

### Decisões arquiteturais preservadas

A implementação aplica as decisões já aprovadas em [ADR-GOAP-01](../../ADRs/ADR-GOAP-01-composicao-e-fronteiras.md), [ADR-GOAP-02](../../ADRs/ADR-GOAP-02-modelo-causal-e-planejamento.md), [ADR-GOAP-03](../../ADRs/ADR-GOAP-03-selecao-e-estabilidade.md), [ADR-GROUP-KNOWLEDGE-01](../../ADRs/ADR-GROUP-KNOWLEDGE-01-conhecimento-compartilhado-e-previo.md) e [ADR-POI-01](../../ADRs/ADR-POI-01-identidade-manifestacao-e-conhecimento.md). Não foi criada uma nova decisão de gameplay: Group continua proprietário dos fatos coletivos; o Framework continua autoridade de Group, membership e Object Entry; World State, Knowledge State e projeções GOAP mantêm fontes distintas. A posição do POI permanece fato prévio endereçado por `ObjectEntryId`, não derivado de ocorrência física. A autoridade Global da Activity não é composta na vista GOAP do demonstrador Second Group.

### Funcionalidades implementadas

- A execução pode reavaliar e reconstruir o plano causal após o resultado observado de cada Action. Ela preserva checkpoints observados quando o validador específico confirma que ainda são válidos; esses checkpoints não mantêm uma cadeia/rota obrigatória. O estado hipotético mantém separados Effects observados e projetados, e a condição de investigação só aceita a chegada correspondente mais recente e ainda compatível com posição e evidência Group atuais.
- `Desire Settings` aceita uma condição opcional de aplicabilidade avaliada contra Knowledge State. Isso decide elegibilidade sem confundir aplicabilidade com satisfação do Goal, peso/intenção ou existência de plano. No Profile do demonstrador, investigar fica aplicável enquanto há investigação obrigatória pendente; a saída, após todas estarem concluídas.
- A Route declara três POIs de investigação e um POI de saída por `ObjectEntryDeclaration` e `PEGAPointOfInterest`, cada um com `ObjectEntryId` distinto. A authority inicial do Second Group possui as posições, `pega.poi.investigation-required = true` e `pega.poi.investigation-completed = false` para os três POIs, além da posição e `pega.poi.activity-exit = true` da saída. Os fatos estão no Group, não no Individual nem no Global.
- A Action `NavigateToKnownGroupObjectEntryPosition` cria Targets a partir dos fatos tipados do Group Reader somente quando a membership efetiva autoriza a leitura. O Target mantém `ObjectEntryId` + `GroupId`; a execução usa a avaliação e o provider existentes de navegação e confirma chegada real.
- `InvestigateKnownGroupPoi` é uma Action separada. Seu plano exige chegada válida ao POI; durante execução, o Actor precisa permanecer no raio configurado pelo tempo de pausa (**2 s** nesta demonstração) e as Preconditions e membership continuam revalidadas. Só então publica o fato tipado de conclusão no Group, com evidência associada ao Actor, POI, Group e execução. Uma escrita recusada ou não lida de volta não confirma a Action nem o Effect.
- As alternativas para POIs pendentes são reconstruídas a partir da posição atual e comparadas pelo score GOAP normal, incluindo o custo de rota estimado e a duração da investigação. Não há sequência fixa por ordem de configuração. Quando todos os POIs obrigatórios estão concluídos, a aplicabilidade do Desire de saída é liberada; o Goal correspondente só é satisfeito com chegada observada à saída. Não há transição de Activity ou para `ResultActivity`.

### Autoria para reproduzir ou adaptar

Na cena `MissionActivityContent`, `Larapio NPC - Second Group Demo` pertence ao Group `pega.mission.actor-group.second`, usa `PEGAMissionSecondActorGroupProfile`, o Desire Profile `Demo_SecondGroupInvestigationAndExit` e as Actions de navegação e investigação conhecidas do Group. Os dois Desires do Profile têm peso-base **24**, usam o comparador multiplicativo padrão e referência de **10 s** para o plano. `InvestigateKnownGroupPoi` configura pausa de **2 s** e tolerância de chegada de **1,25 m**.

Para outro conjunto, mantenha uma `ObjectEntryDeclaration`/identidade única por POI, autorize explicitamente no Group os fatos canônicos de posição e papel (`investigation-required`, `investigation-completed`, `activity-exit`), e associe a autoridade e o Group Profile adequados nas condições e Actions. As posições devem ter rota NavMesh viável. Ajustar o conjunto de fatos muda as alternativas disponíveis; não requer uma lista ordenada de destinos. O Game Designer pode calibrar pausa, raio, pesos, composição do Profile e fatos; novas semânticas de condições ainda exigem código.

### Evidência de execução e limites

O responsável reportou validação visual no Play Mode: o NPC investigou os três POIs, alcançou a saída e satisfez os Goals correspondentes. Também informou que os logs do Unity corroboraram esses resultados. Encontros ocasionais com o Player e as reavaliações decorrentes foram considerados aceitáveis para o escopo atual e não motivaram ampliação da implementação. Nenhum log/captura bruto foi anexado ao repositório ou inspecionado pelo Codex; o Codex não executou Unity nesta consolidação.

A inspeção estática confirma a composição da cena, os quatro endereços lógicos, os fatos iniciais Group, o campo Global vazio no NPC e os caminhos declarados de Action/condição. Isso não substitui a evidência runtime reportada nem constitui compilação/importação Unity independente. Nenhuma decisão normativa foi alterada; as atualizações de ADR registram somente estado de implementação e rastreabilidade.
