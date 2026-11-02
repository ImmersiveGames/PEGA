# PEGA — primeira etapa de Global Knowledge

- **Data:** 2026-11-02
- **Status:** implementação no workspace; importação, compilação e prova no Unity pendentes
- **Autoridade arquitetural:** [ADR-GROUP-KNOWLEDGE-01](../../ADRs/ADR-GROUP-KNOWLEDGE-01-conhecimento-compartilhado-e-previo.md)

## Escopo entregue

A Mission possui uma autoridade PEGA de Knowledge no escopo Global, pertencente à contribuição da Activity. A autoridade reutiliza `PEGAKnowledgeFactStore`, definições tipadas canônicas, evidência, validade e proveniência. A definição Global demonstradora é `MainObjectiveIdentified`; ainda não há produtor de gameplay ligado a ela.

O contrato `IPEGAKnowledgeFactReader` expõe leitura tipada como capability GOAP. `PEGAKnowledgeGoapStateView` permite compor essa capability em qualquer vista de Knowledge GOAP sem fazer o GOAP genérico conhecer tipos do PEGA. As três autonomias da Mission recebem referência explícita à mesma autoridade. O Knowledge Individual permanece em seu armazenamento e escopo próprios.

A autoridade é ativada e limpa pelo lifecycle da contribuição Activity. Leituras após a saída retornam `Unknown`; escritas exigem que a contribuição esteja ativa. Isso limita a autoridade à Activity que possui os fatos, sem singleton ou autoridade paralela.

## Limites preservados

- Não implementa Group Knowledge, propagação entre escopos, compartilhamento por membership ou consulta baseada em Actor Group.
- Não liga fatos Globais a eventos reais do assalto nem implementa a distribuição de eventos prevista no GDD.
- Não altera Actions, Desires, comportamento ou decisões de planejamento dos NPCs.
- Não altera o Immersive Framework nem as memberships dos Groups existentes.

## Evidências

- Estático: a cena referencia a mesma autoridade Global nas três autonomias e declara uma contribuição Activity independente com ID local único.
- Estático: IDs de objeto da cena e GUIDs `.meta` novos são únicos; as referências locais ao componente Global resolvem para um objeto existente no YAML.
- Estático: o assembly GOAP permanece sem referências a tipos PEGA; a revisão arquitetural independente pós-implementação foi `CLEAR`.
- Unity: `PEGAGlobalKnowledgeProofWindow` oferece um procedimento no Play Mode. Ele confirma memberships e Groups vivos pela API oficial, registra `false`, atualiza para `true` e compara valor, status, escopo e proveniência em duas vistas de NPCs de Groups distintos e numa vista GOAP independente. Também verifica que o fato não foi copiado para o armazenamento Individual. **A janela ainda não foi executada no Unity.**

## Validação manual pendente

Importar e compilar no Unity, abrir a Mission, entrar no Play Mode após a Activity e as memberships estarem ativas, abrir **PEGA → Diagnostics → Global Knowledge Proof**, detectar a Mission e executar a prova. Confirmar também que a autoridade fica indisponível após a saída da Activity e inicia vazia na reentrada.
