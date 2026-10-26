# ADR-GOAP-03 — Seleção de intenção, personalidade e estabilidade

- **Status:** Aceito
- **Data:** 2026-10-25
- **Owner:** PEGA — decisão dos larápios

## Contexto

O GDD já define Contextos, Desejos por arquétipo, prioridades, custos de plano, satisfação individual e Contingência. A discussão conceitual refinou a semântica para evitar confundir a motivação com a condição desejada, a intenção com seu plano e as preferências com a capacidade de raciocinar ou executar. Também definiu que informação é uma oportunidade concorrente, não uma resposta automática a todo desconhecimento.

## Decisão

1. O modelo distingue **Goal** (condição desejada), **Desejo** (motivação e valor), **intenção ativa** (compromisso atual), **plano ativo** (Actions/Targets escolhidos) e **Action em execução** (operação atual). Uma troca em um nível não implica necessariamente troca nos demais.
2. Goals aplicáveis e planos candidatos são avaliados conjuntamente. Peso/prioridade do Desejo e custo do plano continuam dimensões distintas; oportunidade, qualidade estimada, informação potencial e consequências da mudança também podem participar. A arquitetura não fixa uma fórmula de agregação.
3. **Personalidade** representa aquilo que o larápio valoriza, incluindo aversões e tendência investigativa. **Competência decisória** representa como procura e compara soluções: amplitude/profundidade de busca, qualidade da avaliação, range de aceitação e estabilidade decisória. Range de aceitação avalia se uma solução é suficientemente boa; margem de troca avalia se uma decisão ativa deve ser substituída. São relacionadas, mas não idênticas. **Capacidades de gameplay** representam o que consegue fazer no mundo. São dimensões independentes, configuráveis e combináveis.
4. Oportunidades de investigação competem com alternativas normais segundo valor potencial da informação, custo e tendência investigativa. A avaliação estima potencial informacional sem presumir qual fato será descoberto. A mesma informação não recebe bônus arbitrariamente duplicado. Investigação não ganha preferência automática e não se torna obrigatória por existir desconhecimento.
5. A estabilidade opera em três níveis:
   - **Intenção:** pode permanecer enquanto se reconstrói ou troca o plano; outra intenção exige vantagem efetiva, considerados validade, satisfação, Contexto e consequências da troca.
   - **Plano:** pode continuar enquanto válido quando a alternativa não justifica sua substituição; compara-se custo restante e efeitos da mudança, não apenas tempo já investido.
   - **Action em execução:** conserva progresso se continua válida e selecionada segundo as regras de execução do GDD.

   Estabilidade não mantém uma decisão inválida nem exige uma pilha permanente de Goals suspensos. Planos abandonados podem ser reconstruídos a partir do conhecimento e dos Effects comprovados; resultados de gameplay continuam preservados.
6. Permanecem as regras do GDD para os três Contextos Globais exclusivos, satisfação individual acumulada e elegibilidade da Contingência. A Contingência continua usando o mesmo GOAP; oportunidades informativas não ganham prioridade nem exceção implícita sobre sua elegibilidade.

Uma oportunidade informativa concorre com alternativas normais quando está representada por um Desejo aplicável e por uma solução válida conforme o GDD. A elegibilidade da Contingência continua dependendo da regra existente — haver ou não um plano normal válido —, sem bloqueio ou preferência global adicional para investigação.

## Alternativas consideradas

- **Escolher primeiro o Desejo de maior peso e depois procurar uma Action:** rejeitada; pode ignorar custo, oportunidade e qualidade das soluções disponíveis.
- **Misturar personalidade, competência e capacidades num único peso:** rejeitada; torna o motivo da escolha opaco e reduz a intercambialidade.
- **Forçar investigação sempre que houver desconhecimento:** rejeitada; transforma informação em prioridade fixa e ignora outras oportunidades legítimas.
- **Preservar sempre o plano mais antigo ou criar uma fila obrigatória de Goals suspensos:** rejeitada; pode manter planos inválidos e guardar estado redundante, sem necessidade aprovada.

## Consequências

- Perfis comportamentais podem variar preferências sem duplicar capacidades ou Actions.
- Uma escolha pode privilegiar uma oportunidade concreta de roubo, investigação ou outra alternativa conforme os parâmetros e custos configurados, sem um algoritmo de preferência imposto por este ADR.
- Eventos relevantes podem reabrir a comparação; a política decide entre manter intenção/plano, reconstruir plano ou trocar de intenção.
- Os pesos da demonstração e os comportamentos específicos dos arquétipos continuam sendo configuração e gameplay do GDD, não valores definidos por este ADR.

## Não decidido

Função matemática de pontuação, ordem de desempate além do determinismo já requerido pelo GDD, valores de parâmetros, quantidade de alternativas exploradas e representação serializada dos perfis.

## Relação com decisões anteriores

Substitui a leitura em que “Desejo/Goal” era um conceito único e refina a frase do GDD que preservava resultados, mas não planos: intenção e plano ativos podem continuar enquanto válidos, sem manter uma fila de planos abandonados. Mantém os três Contextos, satisfação acumulada individual e Contingência já definidos no GDD.

## Autoridade e rastreabilidade

- [GDD — Peso, oportunidade e custo dos planos](../GDD.md#peso-oportunidade-e-custo-dos-planos)
- [GDD — Contextos e satisfação dos Desejos](../GDD.md#contextos-e-satisfação-dos-desejos)
- [GDD — Desejos e prioridades por arquétipo](../GDD.md#desejos-e-prioridades-por-arquétipo)
- [GDD — Áreas, POIs e investigação](../GDD.md#áreas-pois-e-investigação)
- [Registro conceitual — seleção, competência e investigação](../../docs/PEGA-GOAP-registro-de-decisoes-conceituais-2026-10-25.md)

