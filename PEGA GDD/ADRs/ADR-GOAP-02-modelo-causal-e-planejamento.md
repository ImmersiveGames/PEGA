# ADR-GOAP-02 — Condições, Effects e planejamento causal

- **Status:** Aceito
- **Data:** 2026-10-25
- **Owner:** PEGA — planejamento GOAP

## Contexto

O GDD já prevê Preconditions, Effects e cadeias de Actions, mas a demonstração usa seleção de um passo e associa diretamente uma Action a um Goal. A arquitetura aprovada precisa permitir que Goals expressem condições e que o planejador encontre cadeias sem Actions programadas para Goals particulares. Ela também deve separar fatos conhecidos de realidade e de projeções.

## Decisão

1. **Goal** descreve uma condição de estado a alcançar, manter ou restaurar conforme gameplay. **Preconditions** descrevem condições necessárias ao resultado previsto de uma Action. **Effects** descrevem mudanças que a Action pode produzir quando observadas. Nenhum Effect referencia um Goal específico.
2. Condições são componíveis por `AND`, `OR` e `NOT` e podem retornar **Verdadeiro, Falso ou Desconhecido**. Desconhecido não se converte em falso nem é aceito como verdadeiro; negar Desconhecido continua Desconhecido. Cada avaliação identifica sua fonte de estado.
3. O sistema mantém separadas estas fontes:
   - **World State real:** fatos confirmados pelas autoridades de gameplay;
   - **Knowledge State:** fatos legitimamente conhecidos, com escopo e proveniência;
   - **Estado hipotético:** projeção local dos Effects previstos durante a busca, sem mutar mundo ou conhecimento;
   - **Previsões externas opcionais:** estimativas incertas de acontecimentos futuros, distintas de observação e de Effects projetados.
4. O planejador parte de conhecimento legítimo e Targets conhecidos/compatíveis, projeta Effects sobre estados hipotéticos e procura cadeias que satisfaçam um Goal. Não consulta fatos ocultos nem inventa o resultado de uma investigação futura.
5. A busca não tem um comprimento máximo normativo no GDD. Limites por custo de desempenho podem ser configurados; o resultado deve distinguir “sem solução conhecida” de “busca esgotada sem conclusão”. Nenhum algoritmo, fórmula universal de score ou valor de orçamento é imposto aqui.
6. Avaliações especializadas, como navegação e confronto, podem fornecer viabilidade, custo e qualidade para alternativas, inclusive sobre estados hipotéticos. Perfis determinísticos, configuráveis e intercambiáveis podem compor esses critérios por Actor/arquétipo. Se uma avaliação não puder considerar a projeção com legitimidade, ela comunica a limitação/desconhecimento; não substitui silenciosamente o estado projetado pelo estado real atual.
7. Previsões externas continuam opcionais. Quando existirem, carregam origem, horizonte e incerteza; não se tornam fatos conhecidos nem satisfazem por si sós Preconditions determinísticas.

## Alternativas consideradas

- **Action escolhida diretamente por cada Goal:** rejeitada; duplicaria soluções e faria cada novo comportamento exigir alteração de dados ou lógica de Goals.
- **Usar apenas condições booleanas sem fonte de estado:** rejeitada; não representa desconhecimento, avaliações hipotéticas ou a origem dos fatos.
- **Planejar diretamente sobre o World State real:** rejeitada; permitiria aproveitar informação oculta e confundiria previsão com observação.
- **Simular toda a física/Unity como álgebra de planejamento:** rejeitada; excede a necessidade semântica e não foi aprovada.

## Consequências

- Uma Action nova pode oferecer outro caminho a Goals existentes, e Actions existentes podem compor-se em cadeias para Goals novos.
- Navegação, confronto e outras mecânicas preservam sua autoridade física; seus avaliadores informam a decisão sem executar a Action.
- “Desconhecido”, “sem solução conhecida” e “busca esgotada” permanecem situações diferentes nos diagnósticos.
- A regra atual do GDD para perda de alvo continua valendo: não há extrapolação automática de movimento. Previsores futuros não a alteram sem revisão explícita de gameplay.
- Este ADR não determina representação de predicados, estado hipotético, busca ou perfis em código/Unity.

## Não decidido

Estruturas de estado, gramática de predicados, algoritmo de planejamento, fórmula de comparação, orçamento numérico, interface de avaliadores e formato de previsão.

## Relação com decisões anteriores

Preserva do GDD o uso de conhecimento legítimo, capacidades, custo temporal como base do MVP e possibilidade de Actions encadeadas sem comprimento máximo normativo. Refina as condições para expressarem desconhecimento e estados hipotéticos, e formaliza a independência entre Effects e Goals. Não aprova nem exige qualquer algoritmo específico.

## Autoridade e rastreabilidade

- [GDD — Planejamento inimigo por GOAP](../GDD.md#planejamento-inimigo-por-goap)
- [GDD — Conhecimento modular, memória e validade da informação](../GDD.md#conhecimento-modular-memória-e-validade-da-informação)
- [GDD — Perda de alvo](../GDD.md#perda-de-alvo)
- [Registro conceitual — Goals, condições, Actions e planejamento](../../docs/PEGA-GOAP-registro-de-decisoes-conceituais-2026-10-25.md)

