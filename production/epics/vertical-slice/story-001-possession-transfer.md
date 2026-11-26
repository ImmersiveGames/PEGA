# Story 001: Posse exclusiva, aquisição e soltura de itens

> **Épico:** Vertical Slice — expansão do gameplay (PP-37, etapa 5)
> **Status local:** In Progress (`in-progress`)
> **Trello:** [#92 — Posse exclusiva, aquisição e soltura de itens](https://trello.com/c/31ow9RDz)
> **Trello observado em:** 2026-11-24; lista `Em andamento`; última atividade observada no cartão: 2026-11-13

## Contexto e autoridade

- **GDD:** `PEGA GDD/GDD.md`, § “Inventário, transferência e soltura de itens” e regras de inventário físico do Actor.
- **MVP:** `PEGA GDD/MVP.md`, seção do primeiro vertical slice, tabela “Itens e armazenamento”.
- **ADR:** `PEGA GDD/ADR-PREPRODUCAO.md`, PP-37 e a diretriz de evolução por capacidades.
- **TASKS:** `PEGA GDD/TASKS.md`, etapa 5, “Itens, posse e transferência”. O backlog ainda aponta para o cartão histórico #38; o cartão operacional atual #92 declara que o substitui.
- **Escopo operacional:** Trello #92. O cartão e seus comentários ajudam a localizar o trabalho, mas não substituem as fontes normativas nem comprovam aceite por si sós.

## Critérios de aceite locais

O aceite local exige evidência rastreável do comportamento no PEGA. Os estados do Trello abaixo são registrados separadamente.

- [ ] **Autoridade de posse:** item disponível no cenário ou carregado por um Actor, com apenas um local/portador válido.
  - Trello V01: `COMPLETE`.
  - Evidência externa relatada no cartão: ação Trello `6af678535074fa1bf02cfdb4` (2026-11-12) e ação `6af72db2eb9dbf497a1899aa` (2026-11-13) relatam representação e aquisição pelo Player em Play Mode.
  - Estado local: **NOT VERIFIED independently**; não há artefato de evidência anexado a este registro.
- [ ] **Aquisição e transferência exclusivas:** impedir dois portadores do mesmo item, inclusive em tentativas concorrentes pertinentes ao GDD.
  - Trello V02: `INCOMPLETE`.
  - Estado local: **NOT VERIFIED**.
- [ ] **Soltura e retorno ao cenário:** soltar conforme o comando/regra do GDD e preservar invariantes de não duplicação ou perda.
  - Trello V03: `INCOMPLETE`.
  - Estado local: **NOT VERIFIED**.

## Evidência e próxima verificação

- Código ou assets estáticos podem demonstrar implementação presente, mas não execução integrada.
- A evidência de aceite deve ficar em `production/evidence/vertical-slice-001/` e apontar para o cenário, resultado observado e versão/commit correspondente.
- A validação de gameplay pendente exige observação manual no Unity Editor/Play Mode. Nenhuma validação Unity foi executada durante este bootstrap.

## Fora deste registro

Depósito/retirada de cofre, transporte/prisão e a consequência de queda de itens por ataque têm escopos próprios. A relação de #92 com o primeiro incremento de #93 deve preservar a autoridade de posse sem transformar a queda de um item em aceite integral de V02/V03.
