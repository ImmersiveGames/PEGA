# PEGA — Group Knowledge

- **Data:** 2026-11-02
- **Status:** implementação presente; prova manual no Unity com resultado PASS, conforme relato do usuário. O Codex não executou Unity.
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
- A Mission não autoriza conhecimento inicial de Group; a prova registra fatos temporários durante o Play Mode.
- O GOAP dispõe de leitura Global via capability composta, mas a prova não integra os fatos a Actions, Desires ou decisões do planner. Group Knowledge é validado por leitura explícita e não participa do planejamento atual.
- Não há produtor de gameplay conectado ao fato demonstrador Global nem distribuição de eventos reais do assalto.
- Não foi alterado o Immersive Framework.
