# PEGA — Group Knowledge

- **Data:** 2026-11-02
- **Status:** implementação presente; a primeira prova de leitura Group está relatada como PASS em 2026-11-02 e a evolução de autoria inicial/diagnóstico foi validada manualmente em 2026-11-03 pelo usuário. O Codex não executou Unity nesta revisão.
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

## Limites e evolução futura

- Não há propagação automática entre Individual, Group e Global.
- Na etapa inicial deste relatório, a Mission ainda não tinha autoria de Group e a prova registrava fatos temporários no Play Mode. A evolução posterior descrita abaixo adicionou a configuração serializada de fatos iniciais Group e Global.
- O GOAP dispõe de leitura Global via capability composta, mas a prova não integra os fatos a Actions, Desires ou decisões do planner. Group Knowledge é validado por leitura explícita e não participa do planejamento atual.
- Não há produtor de gameplay conectado ao fato demonstrador Global nem distribuição de eventos reais do assalto.
- Não foi alterado o Immersive Framework.

## Atualização — autoria inicial e diagnóstico dos três escopos (2026-11-03)

A evolução implementada reorganiza as definições tipadas canônicas e a entrada de autoria compartilhada sem introduzir um armazenamento concorrente. O Inspector usa o catálogo para compor **Identidade → Propriedade → Valor** em Individual, Group e Global. Individual, Group e Global continuam escrevendo em seus Knowledge States e stores existentes, com as respectivas authorities, políticas e evidências. Perception continua consumindo as mesmas definições canônicas.

Uma nova propriedade fica declarada como definição tipada canônica em `Assets/PEGA/NPC/Knowledge/Definitions`, com ID, tipos de endereço/valor e política. `PEGAKnowledgeFactDefinitionCatalog` encontra automaticamente os campos estáticos de definição e rejeita IDs duplicados; a matriz genérica do Inspector a apresenta quando os payloads tipados disponíveis cobrem seus tipos. Os consumidores continuam usando a definição sem uma lista ou editor duplicado por propriedade.

Para Group, `PEGAActorGroupComposition` cria os Groups reais usando o contrato oficial do Framework. `PEGAActorGroupKnowledgeState` só aplica a autoria do perfil declarado depois de confirmar o Group vivo criado pelo owner; as leituras dos membros verificam membership vigente. O Inspector GOAP mostra autoridades e fatos por escopo, propriedade, identidade, valor, origem, timestamps e validade, e oculta fatos Group quando a membership não autoriza a consulta.

Na cena `MissionActivityContent`, um NPC também tem um fato Individual inicial `pega.actor.last-observed-character-position` endereçado pelo Actor do Player resolvido via binding e com valor `(100, 0, 100)`. A Perception publica a mesma definição e identidade quando observa o Actor, com origem própria e a política canônica. Na configuração atual da demonstração, o Second Group `pega.mission.actor-group.second` e o Global usam a mesma definição `pega.object-entry.position` e o mesmo Object Entry `pega.warehouse.storage-rack-west-north`, com valores `(-5, 1, 15.700001)` e `(200, 0, 200)`, respectivamente. Os stores permanecem independentes. O usuário reportou validação manual no Unity em 2026-11-03, incluindo autoria inicial, identidade/propriedade equivalentes entre Individual e Perception, leitura autorizada, acesso negado, isolamento entre Groups e conhecimento Global; esta atualização documental não repetiu a prova.

Permanecem pendentes: autoria/avaliação genérica de condições GOAP, sensores/fontes de Object Entry, produtores de eventos reais do assalto e propagação entre escopos. A evolução específica de integração com GOAP está registrada a seguir. Os ADRs [GROUP-KNOWLEDGE-01](../../ADRs/ADR-GROUP-KNOWLEDGE-01-conhecimento-compartilhado-e-previo.md), [GOAP-02](../../ADRs/ADR-GOAP-02-modelo-causal-e-planejamento.md) e [POI-01](../../ADRs/ADR-POI-01-identidade-manifestacao-e-conhecimento.md) registram as decisões e o estado consolidado.

## Atualização — posição de POI do Group em Target, Goal e navegação GOAP (2026-11-03)

### Implementação PEGA

A capacidade usa a Action declarativa `NavigateToKnownGroupObjectEntryPosition`, configurada no Actor somente por composição/override. `PEGAActorGroupKnowledgeReader` enumera fatos tipados `pega.object-entry.position` nas authorities Group explicitamente ligadas e consulta apenas Groups retornados pela membership vigente do Framework; cada snapshot mantém seu `GroupId`. A Action emite um Target cujo ID lógico combina `ObjectEntryId` e `GroupId`, com posição e instante de evidência do fato. O Target não é fabricado por descoberta da Transform nem por resolver a ocorrência física.

O GoalCondition relê o mesmo fato Group, confirma Group/membership/validade e exige o Effect de chegada correspondente. A Action causal declara um Effect hipotético `TargetReached` para planejamento; o planner verifica Preconditions/candidatura, a disponibilidade de rota real pelo provider existente e os Goals aplicáveis antes de escolher o plano pela política normal. Esse Effect projetado não grava World State ou Knowledge. Durante execução, a navegação usa o provider existente, o sucesso depende da distância configurada de chegada e o Effect só é observado quando a Action informa sucesso e o conhecimento Group continua autorizado. Não foi criado Target espacial paralelo nem fornecido destino direto ao provider fora do GOAP.

### Configuração da demonstração

`MissionRoute` declara o POI `pega.warehouse.second-group-demo-poi` com `ObjectEntryDeclaration` do Immersive Framework e `PEGAPointOfInterest`. O fato inicial `pega.object-entry.position` desse ID, valor `(0, 0, 10)`, está configurado explicitamente na authority inicial do Second Group em `MissionActivityContent`; não há fato Individual nem Global para esse POI. O override de `Larapio NPC - Second Group Demo` compõe a Action e o perfil `Demo_SecondGroupKnownPoi`. O Desire de chegar a essa posição usa peso base 24, preservando os demais Desires e a política normal multiplicativa de seleção; não há prioridade absoluta.

O fato Group anterior de posição do rack `pega.warehouse.storage-rack-west-north` continua separado do fato Global antigo para o mesmo endereço (valores `(-5, 1, 15.700001)` e `(200, 0, 200)`, respectivamente). Isso demonstra stores e authorities independentes. A Action/condição desta feature só consultam o Group Reader; a posição Global não entra no Target. A Action também não consulta o binding/occurrence query para completar a posição conhecida.

A primeira tentativa usava a posição do rack dentro de seu volume físico; a Action exige rota NavMesh completa até a posição conhecida e o planejador descarta a candidatura indisponível antes de projetar seu Effect. Os logs fornecidos não incluem o enum específico da rota rejeitada; distingui-lo exige observação no Unity. A correção manteve o fato e a identidade lógica do rack e apontou o Goal para o POI demonstrativo separado no corredor.

### Evidência e limites

- **Implementação observada estaticamente:** a Action consulta Group Knowledge por membership efetiva; Target/Effect/GoalCondition mantêm a identidade tipada e exigem conhecimento autorizado; a avaliação e execução usam a rota/navegação existentes e confirmação de chegada. `git diff --check` e verificações estáticas de referências serializadas foram executados nesta consolidação.
- **Revisão arquitetural:** o parecer independente anterior foi `CLEAR` para o corte funcional, estaticamente. Isso não certifica importação, compilação ou execução Unity.
- **Execução relatada:** o responsável confirmou visualmente que o NPC iniciou o deslocamento em Play Mode após a correção. Nenhuma captura/log do novo Play Mode foi anexada ou inspecionada pelo Codex.
- **Não comprovado neste registro:** conclusão da navegação/chegada, Effect observado, satisfação final do Goal, enum de status da rota em execução e execução independente pelo Codex.

### Contratos e maturidade

O `Packages/manifest.json` do consumidor PEGA resolve localmente `com.immersive.framework` `1.2.0-preview.4`. Actor Groups e a superfície de identidade/autoria `ObjectEntryId`/`ObjectEntryDeclaration` estão classificadas **Experimental** na API resolvida. O POI usa `ObjectEntryDeclaration`; resolução física por occurrence query não participa desta feature. Nenhum arquivo/código do Framework foi alterado. As decisões de autoridade, separação de Knowledge e uso de ObjectEntryId já aprovadas permanecem as dos ADRs vinculados acima; esta integração é uma capacidade PEGA composta sobre elas.
