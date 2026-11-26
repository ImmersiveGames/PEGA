# Story 002: Primeiro incremento do confronto — Atacar e identificar impacto

> **Épico:** Vertical Slice — expansão do gameplay (PP-37, etapa 5)
> **Status local:** In Progress (`in-progress`)
> **Trello:** [#93 — Confronto mínimo](https://trello.com/c/BbLpynTw)
> **Trello observado em:** 2026-11-24; lista `Em andamento`; última atividade observada no cartão: 2026-11-17
> **Fronteira local:** acompanha somente o primeiro incremento aprovado: comando Atacar e identificação de impacto. Queda de itens, nocaute, captura automática e recuperação são incrementos posteriores do cartão.

## Contexto e autoridade

- **GDD:** `PEGA GDD/GDD.md`, “Sistemas do jogador” (comando independente Atacar e impacto direcional), regras de confronto/incapacitação e inventário físico.
- **MVP:** `PEGA GDD/MVP.md`, tabela “Perseguição e captura” e tabela “Itens e armazenamento”. O contrato completo de captura continua exigindo entrega efetiva na prisão.
- **ADR:** `PEGA GDD/ADR-PREPRODUCAO.md`, PP-37 e §14 “Alinhamento de controles do Player”. O primeiro incremento comprova Atacar e um impacto legítimo; consequências são posteriores.
- **TASKS:** `PEGA GDD/TASKS.md`, etapa 5, “Confronto (ordem do próximo incremento)”.
- **Escopo operacional:** Trello #93. O cartão informa a sequência e o estado de seus checklists; esse estado não substitui evidência local.

## Critérios de aceite locais

- [ ] **Comando próprio:** Player aciona Atacar independentemente de Interagir (`B`); não reintroduzir salto livre. O mapeamento físico final ainda está pendente no GDD.
  - Trello V01: `INCOMPLETE`.
  - Evidência estática disponível: commits `8bc0845` (“implement attack V01 and focused logging”) e `76a5ddb` (“receive attack impacts and incapacitate actors”) alteraram arquivos de ataque/impacto sob `Assets/PEGA`.
  - Estado local: **NOT VERIFIED in Unity**; os commits comprovam alterações registradas, não comportamento em execução.
- [ ] **Impacto direcional legítimo:** ataque físico identifica alvo elegível na região frontal e alcance válidos, sem depender do foco/seleção de interação.
  - Trello V01: `INCOMPLETE`.
  - Estado local: **NOT VERIFIED**.
- [ ] **Execução de NPC agressivo:** a execução física pode ser reutilizada por NPCs agressivos; a decisão de agir permanece sob GOAP.
  - Trello V01: `INCOMPLETE`.
  - Estado local: **NOT VERIFIED**.

## Evidência e próxima verificação

- Commits registrados: `8bc0845`, `76a5ddb`. Os diffs são evidência estática da existência de alterações; não comprovam aceite.
- A evidência local deve ser guardada em `production/evidence/vertical-slice-002/`, com passos, resultado observado, cena/configuração e commit correspondente.
- A confirmação exige observação manual no Unity Editor/Play Mode. Nenhuma validação Unity foi executada durante este bootstrap.

## Fora deste incremento

Queda e recuperação de itens, nocaute/captura automática de NPC, nocaute temporário e recuperação do Player são critérios posteriores no cartão #93. Transporte e entrega na prisão continuam necessários para a captura final do MVP; a captura automática intermediária não os substitui.
