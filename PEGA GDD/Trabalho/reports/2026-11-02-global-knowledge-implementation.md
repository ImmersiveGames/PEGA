# PEGA — primeira etapa de Global Knowledge

- **Data:** 2026-11-02
- **Status:** implementação registrada; prova manual no Unity com resultado PASS, conforme relato do usuário em 2026-11-02. O Codex não executou Unity.
- **Autoridade arquitetural:** [ADR-GROUP-KNOWLEDGE-01](../../ADRs/ADR-GROUP-KNOWLEDGE-01-conhecimento-compartilhado-e-previo.md)

## Escopo entregue

A Mission possui uma autoridade PEGA de Knowledge no escopo Global, pertencente à contribuição da Activity. A autoridade reutiliza `PEGAKnowledgeFactStore`, definições tipadas canônicas, evidência, validade e proveniência. A definição Global demonstradora é `MainObjectiveIdentified`; ainda não há produtor de gameplay ligado a ela.

O contrato `IPEGAKnowledgeFactReader` expõe leitura tipada como capability GOAP. `PEGAKnowledgeGoapStateView` permite compor essa capability em qualquer vista de Knowledge GOAP sem fazer o GOAP genérico conhecer tipos do PEGA. As três autonomias da Mission recebem referência explícita à mesma autoridade. O Knowledge Individual permanece em seu armazenamento e escopo próprios.

A autoridade é ativada e limpa pelo lifecycle da contribuição Activity. Leituras após a saída retornam `Unknown`; escritas exigem que a contribuição esteja ativa. Isso limita a autoridade à Activity que possui os fatos, sem singleton ou autoridade paralela.

## Limites preservados

Esta primeira etapa de Global Knowledge não incluiu Group Knowledge, propagação entre escopos, compartilhamento por membership ou consulta baseada em Actor Group. Group Knowledge foi implementado depois e está registrado em [sua própria etapa](2026-11-02-group-knowledge-implementation.md).
- Não liga fatos Globais a eventos reais do assalto nem implementa a distribuição de eventos prevista no GDD.
- Não altera Actions, Desires, comportamento ou decisões de planejamento dos NPCs.
- Não altera o Immersive Framework nem as memberships dos Groups existentes.

## Evidências

- Estático: a cena referencia a mesma autoridade Global nas três autonomias e declara uma contribuição Activity independente com ID local único.
- Estático: IDs de objeto da cena e GUIDs `.meta` novos são únicos; as referências locais ao componente Global resolvem para um objeto existente no YAML.
- Estático: o assembly GOAP permanece sem referências a tipos PEGA; a revisão arquitetural independente pós-implementação foi `CLEAR`.
- Unity manual — **PASS, relatado pelo usuário em 2026-11-02:** `PEGAGlobalKnowledgeProofWindow` confirma memberships e Groups vivos pela API oficial, registra `false`, atualiza para `true` e compara valor, status, escopo, timestamp e proveniência em vistas GOAP de NPCs de Groups distintos e numa vista GOAP independente. Também verifica que o fato não foi copiado para o armazenamento Individual. O resultado foi executado pelo usuário, não pelo Codex; nenhum log ou captura foi anexado nesta revisão.


## Escopo não coberto por esta prova

A prova não cria produtores de gameplay para `MainObjectiveIdentified`, não integra fatos Globais a Actions ou decisões do planner e não propaga fatos entre escopos. A execução manual PASS foi relatada pelo usuário; o Codex não reexecutou nem inspecionou um log do Unity.
