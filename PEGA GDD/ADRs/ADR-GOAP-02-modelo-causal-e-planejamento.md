# ADR-GOAP-02 — Condições, Effects e planejamento causal

- **Status:** Aceito
- **Data:** 2026-10-25
- **Revisado:** 2026-11-03 — autoria de fatos iniciais, diagnóstico, aplicabilidade de Desires e ciclo causal de investigação de POIs implementados no PEGA
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

8. **Autoria declarativa reutiliza definições canônicas do Knowledge.** Um fato é composto no Inspector como **Identificador/endereço tipado → Propriedade → Valor**; uma condição, como **Identificador/endereço tipado → Propriedade → Operador → Valor**. O identificador é o endereço exigido pela definição, não necessariamente uma entidade: conforme o fato, pode endereçar entidade, relação ou estado. Nos exemplos, POIs e Actors são endereçados respectivamente por **ObjectEntryId** e **ActorId**; quando o fato trata de uma entidade, usa sua identidade lógica, não um profile reutilizável nem uma ocorrência física. A definição tipada canônica determina os tipos de endereço e valor, os operadores compatíveis e a política do fato. O designer escolhe definições disponíveis e fornece valores compatíveis; adicionar propriedades, tipos ou políticas é responsabilidade de programação. Fatos iniciais, adquiridos por sensores ou compartilhados continuam nos contratos existentes de Knowledge Store, evidência, temporalidade, validade e escopo. Goals e Desires compõem condições sobre as mesmas propriedades; Preconditions consultam essas condições. Effects e Actions referem-se às mesmas definições tipadas em seus contratos próprios, preservando a projeção de Effects em estado hipotético sem escrever World State ou Knowledge durante a busca. Por exemplo, um Group pode conhecer previamente a Posição de um POI endereçado por ObjectEntryId, enquanto um sensor publica propriedades observadas de um Actor endereçado por ActorId. Quando o PEGA disponibilizar a definição tipada de Facção observada, o fato válido Policiais torna a condição Facção = Larapios falsa; sem fato válido, a condição é Desconhecida. Membership de Actor Group do Framework não constitui uma afirmação de facção do PEGA. Esta decisão fixa a semântica de autoria, sem determinar estruturas internas, layout do Inspector, classes, assets, drawers ou algoritmos.

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

## Implementação PEGA — autoria de fatos e diagnóstico (2026-11-03)

O PEGA passou a oferecer no Inspector autoria de fatos iniciais como **Identidade → Propriedade canônica → Valor** nos escopos Individual, Group e Global. As definições tipadas continuam compartilhadas pelo catálogo e pelas fontes de percepção; cada autoridade grava no seu próprio Knowledge State com evidência, validade e política da definição. A aba Conhecimento do GOAP Runtime Inspector enumera os fatos realmente legíveis e apresenta identidade, propriedade, valor, escopo, proveniência, timestamps e validade. A configuração e o fluxo foram validados manualmente no Unity pelo usuário; esta revisão documental não repetiu a execução. A [implementação e a prova reportada](../Trabalho/reports/2026-11-02-group-knowledge-implementation.md) registram o Object Entry e os valores usados na Mission.

### Integração de posição conhecida de Group ao Target e planejamento (2026-11-03)

O PEGA agora oferece uma Action de navegação causal a partir de `pega.object-entry.position` lida exclusivamente pelo Group Knowledge Reader autorizado por membership. O Target retém `ObjectEntryId`, `GroupId`, posição e evidência; a Action propõe o Effect hipotético de chegada, e a avaliação causal exige rota disponível antes de expandir o plano. A execução navega pelo provider existente, revalida o conhecimento/membership e só publica Effect observado após sucesso e chegada física. O Goal/Desire é configurado em conteúdo e usa a seleção GOAP normal. Esta integração não altera a semântica de planejamento, não consulta World State/Global para completar o Target e não transforma posição conhecida em chegada. Ver configuração, limites e evidência reportada no [relatório Group Knowledge](../Trabalho/reports/2026-11-02-group-knowledge-implementation.md).

### Execução causal, condições de aplicabilidade e investigação de POIs (2026-11-03)

O executor pode reavaliar e reconstruir um plano após cada resultado observado sem tornar as Actions remanescentes uma sequência obrigatória. Checkpoints de Effects observados são preservados apenas enquanto um validador confirma suas condições atuais; eles permanecem distintos do Knowledge e dos Effects projetados. `Desire Settings` pode consultar uma condição de aplicabilidade sobre Knowledge State, separada da satisfação do Goal e da existência de plano.

Na demonstração `Larapio NPC - Second Group Demo`, o plano normal encadeia navegação a um Target de POI conhecido pelo Group, Effect de chegada, investigação temporizada e Effect de conclusão após escrita autorizada aceita e lida de volta. A pausa configurada é contínua e as Preconditions são revalidadas; interrupção, perda de proximidade ou escrita rejeitada não concluem a investigação. A reavaliação após a conclusão compara novamente POIs pendentes pelo custo total estimado de rota e Action, e a condição dinâmica libera o Desire de saída quando todas as investigações obrigatórias estão concluídas. O Goal da saída requer chegada observada.

O responsável reportou visualmente no Play Mode as três investigações, chegada à saída e satisfação dos Goals, corroboradas pelos logs do Unity. Encontros ocasionais com o Player e reavaliações foram aceitos no escopo. O Codex não executou Unity nem inspecionou logs brutos. Configuração e limites estão no [relatório de implementação](../Trabalho/reports/2026-11-02-group-knowledge-implementation.md) e no [guia prático do GOAP](../Guias/PEGA-GOAP-guia-pratico.md).

## Não decidido

A autoria e a leitura de fatos armazenados têm uma superfície no Inspector; isso não constitui autoria genérica de qualquer predicado ou condição GOAP. A navegação, investigação e saída por fatos Group/ObjectEntry desta demonstração são condições e Actions PEGA compostas sobre os contratos existentes. Permanecem em aberto operadores e semânticas genéricos configuráveis para novas condições, a ligação de endereços dinâmicos a Actors ou Targets percebidos e integrações de outras propriedades/fontes. Também permanecem em aberto o algoritmo de planejamento além do contrato causal existente, a fórmula completa de comparação, o orçamento numérico, a interface geral de avaliadores e o formato de previsão.

## Relação com decisões anteriores

Preserva do GDD o uso de conhecimento legítimo, capacidades, custo temporal como base do MVP e possibilidade de Actions encadeadas sem comprimento máximo normativo. Refina as condições para expressarem desconhecimento e estados hipotéticos, e formaliza a independência entre Effects e Goals. Não aprova nem exige qualquer algoritmo específico.

## Autoridade e rastreabilidade

- [GDD — Planejamento inimigo por GOAP](../GDD.md#planejamento-inimigo-por-goap)
- [GDD — Conhecimento modular, memória e validade da informação](../GDD.md#conhecimento-modular-memória-e-validade-da-informação)
- [GDD — Perda de alvo](../GDD.md#perda-de-alvo)
- [Registro conceitual — Goals, condições, Actions e planejamento](Historico/PEGA-GOAP-registro-de-decisoes-conceituais-2026-10-25.md)

