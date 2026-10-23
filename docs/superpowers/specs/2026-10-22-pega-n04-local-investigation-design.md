# PEGA #91 N04 — investigação local após perda de contato

- **Status:** desenho e plano aprovados; implementação preparada no workspace; authoring do prefab e smoke funcional no Unity pendentes.
- **Data:** 2026-10-22
- **Escopo:** complementar N04 com reposicionamento local, orientado pelo GOAP, depois de alcançar a última posição conhecida de Rick.
- **Card:** [#91 — Autonomia GOAP para perseguir, fugir e perder contato](https://trello.com/c/UQxoJAWb/91-npc-autonomia-goap-para-perseguir-fugir-e-perder-contato)

## 1. Objetivo

Após perder contato visual real, o larápio continua até a última posição de Rick que seu próprio `PEGANpcKnowledgeState` registrou. Se chegar ali sem readquirir Rick, poderá escolher por GOAP uma quantidade finita de posições de observação próximas, usando navegação e movimento físico reais. A percepção normal continua ativa durante e depois desses deslocamentos.

O reposicionamento cria oportunidades espaciais reais para a percepção. Chegar a uma posição de observação não significa encontrar Rick. Só uma nova observação visual válida pode atualizar a identidade/posição conhecida e publicar `RickReacquired`. Se nenhuma readquisição acontecer, a janela original de despistamento de N05 encerra a oportunidade e devolve o NPC à seleção normal de Desejos.

## 2. Autoridade e evidências do workspace

- O GDD, em “Perda de alvo”, determina que o NPC guarda a última posição conhecida, segue até ela tentando readquirir o alvo pela percepção normal e não extrapola trajetória, segue trilhas, procura salas vizinhas ou simula audição.
- `PEGANpcKnowledgeState` mantém evidência individual; `PEGANpcApproachKnownRickAction` só resolve a posição de Rick desse conhecimento. Nenhuma destas responsabilidades pode passar a consultar o `Transform` atual de Rick.
- `PEGANpcPerceptionSource` aplica o cone de visão e oclusão existentes. A perda visual é um resultado legítimo da percepção, não uma ordem para aumentar o cone ou girar automaticamente.
- `PEGANpcNavMeshMovementIntentSource` calcula rotas completas pelo `NavMeshAgent`, reconcilia a simulação com a posição física e entrega o deslocamento ao `PEGAActorMovementMechanic`/`CharacterController`. É a rota compartilhada de execução física a preservar.
- A autonomia atual escolhe um plano GOAP e inicia a Action escolhida; N03 consome solicitações e resultados terminais de modo event-driven. Não existe uma Action local de observação nem uma fronteira de “sala” semântica no código atual.
- A Mission contém uma área de armazém com paredes externas e obstáculos de estantes. `ProjectSettings/NavMeshAreas.asset` só define `Walkable`, `Not Walkable` e `Jump`; não há regiões NavMesh de sala/investigação. O cenário atual não fornece IDs ou volumes de sala.
- A seleção atual associa a Action disponível ao objetivo previsto do Desejo. Portanto, uma Action que apenas chega a outro ponto não pode declarar falsamente que Rick foi alcançado, que foi readquirido ou que o Desejo final foi satisfeito.

### Evidência anterior e seu limite

As execuções aceitas anteriormente comprovaram perda visual real, preservação da última posição, readquisição real e retorno normal após o timeout. Elas não comprovam o reposicionamento local. Nas perdas observadas, o larápio já estava a aproximadamente 0,80–1,15 m da última posição, dentro do raio declarado de chegada de 1,25 m. O probe aceitava estar dentro desse raio como continuidade; portanto, o `PASS` não prova deslocamento físico após a perda. A orientação do NPC para longe de Rick foi observada, mas isso não prova defeito no cone de visão.

## 3. Desenho proposto

### 3.1 Oportunidade de investigação

Uma oportunidade começa somente com a mudança legítima de contato visual `true → false` publicada pela percepção e validada por N01. Sua identidade inclui o ActorId de Rick, o horário da perda e a evidência de posição conhecida usada naquela perda. O estado é local à autonomia daquele NPC; não é um novo fato conhecido do mundo e não é compartilhado entre larápios.

A oportunidade conserva a mesma hora limite de despistamento estabelecida por N05. Alcançar a última posição, concluir deslocamentos locais ou receber novos resultados de Action não reinicia o relógio. Uma readquisição visual real cancela a oportunidade; uma nova perda depois dela cria uma nova oportunidade. Notificações repetidas da mesma perda não reiniciam os destinos tentados.

### 3.2 Área local e candidatos de posição

No primeiro aceite, “local” significa um conjunto finito de posições navegáveis em torno da última posição conhecida, dentro da área única do armazém da Mission. Não significa seguir Rick, percorrer corredores em busca dele ou escolher salas/áreas adjacentes.

Cada posição candidata deve ser derivada somente de:

1. última posição conhecida e timestamp da evidência de Perception do próprio NPC;
2. posição física atual do larápio;
3. configuração do NavMeshAgent deste larápio e geometria estática da Mission.

A seleção espacial proposta é determinística e limitada:

1. Gerar uma lista pequena e finita de amostras ao redor da última posição, em direções/ordem estáveis e repetíveis. Cada posição tem um identificador estável dentro da oportunidade. Número de amostras, raio local e afastamento entre amostras são calibração de gameplay; não são sorteados e não usam a posição atual/oculta de Rick.
2. Projetar cada amostra na NavMesh com o agent type e a máscara de área do NPC, usando distância de projeção curta. Rejeitar amostras cujo desnível exceda o limite configurado, fora do raio local, duplicadas ou sem posição física válida.
3. Calcular uma rota completa do NPC físico para cada candidata usando a configuração do agente já resolvida. Rejeitar rotas cuja distância ou cujos pontos/cantos ultrapassem o limite local configurado. A execução continua pela Action GOAP e pelo movimento físico compartilhado; uma estimativa de NavMesh não é tratada como chegada.
4. Usar a última posição conhecida como referência para classificar o destino e para a evidência do plano. Uma consulta geométrica opcional de visada pode usar somente essa posição conhecida e a mesma máscara de oclusão da percepção; nunca consulta Rick presente.
5. Quando uma candidata for selecionada para execução, registrá-la como consumida nesta oportunidade, inclusive se a Action falhar ao iniciar. Após seu resultado terminal, N03 reavalia por evento. O seletor pode escolher outra candidata viável ainda não consumida; não seleciona a mesma posição novamente por causa de uma reavaliação duplicada.

Se não houver destino local viável ou restarem somente destinos tentados, não se cria um destino fictício. O NPC aguarda uma mudança legítima (readquisição, resultado, ou evento de despistamento) sem polling de decisão. Quando N05 fecha a oportunidade, qualquer Action local ainda ativa é encerrada/substituída pela reavaliação normal conforme a validade do plano.

Os valores do raio local, comprimento máximo de rota, limite vertical e quantidade/espaçamento de amostras devem ser configuráveis para calibração e validados para valores finitos positivos. O N04 não fixa números de balanceamento sem medição no Unity; o fixture temporário deve usar valores explícitos e restaurá-los ao terminar.

### 3.3 Limite do que a NavMesh consegue garantir

`NavMesh.SamplePosition` encontra a posição navegável mais próxima e não considera obstruções; a documentação também alerta que pode selecionar outra superfície/andar. Assim, amostra curta, verificação vertical e rota completa são necessárias.

`NavMeshAgent.CalculatePath` respeita agent type, area mask e custos e permite medir os cantos de uma rota completa. Um raio em torno da última posição mais um limite no caminho impede exploração distante e caminhos que saem do raio. Esses dados não identificam uma “sala”: na Mission atual todas as áreas usam a mesma área caminhável e não existem limites semânticos de sala. Um vão aberto que conecte duas salas dentro do raio poderia continuar navegável.

Para o aceite deste incremento, a área local permanece confinada ao armazém único já autorado na Mission, com o raio/caminho finitos e os limites físicos presentes. A especificação não afirma uma garantia genérica para cenas futuras com salas conectadas. Se a Mission receber salas adjacentes dentro dessa vizinhança, uma fronteira de região autorada será uma dependência separada; N04 deverá falhar fechado para esses destinos, sem entrar nas salas. Não se cria agora camada, sistema de salas ou conteúdo de Level Design.

Referências oficiais Unity 6.5 (6000.5), versão do projeto:

- [NavMesh.SamplePosition](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/AI.NavMesh.SamplePosition.html) — projeção próxima, máscara e limites de oclusão/andar.
- [NavMeshAgent](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/AI.NavMeshAgent.html) e [NavMesh.CalculatePath](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/AI.NavMesh.CalculatePath.html) — APIs de navegação e cálculo de rota com máscara/filtro.
- [NavMeshPath](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/AI.NavMeshPath.html) — status e cantos da rota.

### 3.4 GOAP, Action e satisfação

O seletor e a autonomia continuam sendo a autoridade para escolher e iniciar cada Action. A percepção emite evidência/eventos; não escolhe um destino local nem dispara Action. A sonda temporária apenas observa.

A chegada a uma posição local é um efeito intermediário verdadeiro (“NPC alcançou esta posição de observação”), e não `RickReacquired`, `RickReached` ou `ApproachKnownRickPosition`. Esse sucesso local não atualiza o Knowledge State e não aumenta satisfação do Desejo de perseguição. O evento terminal de Action já integrado em N03 provoca a reavaliação event-driven que pode escolher uma próxima posição ainda não consumida.

O contrato atual do seletor compara o efeito/meta da Action ao objetivo do Desejo e não representa progresso intermediário sem declarar satisfação. Para manter esta capacidade no GOAP sem falsear satisfação ou criar uma FSM, a seleção durante a perda deve transportar uma **continuação de perseguição** vinculada ao Desejo/plano que abriu a oportunidade. Essa continuação tem condição terminal somente por readquisição visual real ou pelo evento de despistamento de N05; alcançar o ponto conhecido ou um destino local é progresso físico intermediário.

Durante essa continuação, o GOAP avalia candidatos de Action com o objetivo intermediário “alcançar posição local não consumida”. A Action reporta apenas o efeito físico observado de chegada. N03 recebe o resultado terminal e solicita uma nova seleção; o GOAP avalia outro ponto se a oportunidade ainda estiver válida. O Desejo final continua pendente; o `PEGANpcDesireState` não ganha satisfação pela chegada local. Readquisição e despistamento encerram a continuação por eventos legítimos e devolvem a seleção ao GOAP.

Essa é uma extensão mínima e PEGA-específica ao contrato entre plan selection, resultado de Action e continuidade do objetivo. Ela não é uma busca genérica de cadeias de Actions, não altera contratos Immersive, não adiciona polling e não faz a Action assumir a decisão do GOAP. O identificador do Desejo/plano e a condição terminal permanecem no registro da continuação; o id da posição local e o resultado físico são dados do passo atual.

O próximo ponto deve ser selecionado pelo GOAP depois do resultado anterior e somente entre destinos ainda viáveis/tentáveis. Se Rick for readquirido durante a navegação, a observação consolidada da Perception cancela a oportunidade e o GOAP decide se preserva/substitui a execução. Se Rick for despistado, N05 fecha a oportunidade e o GOAP retorna à seleção normal. O conhecimento conserva a última posição enquanto estiver válida.

## 4. Fora de escopo

- Consultar transform, velocidade ou localização oculta de Rick para planejar, gerar pontos ou validar chegada.
- Escrever/injetar evidência no Knowledge State, aumentar cone/alcance, simular audição, extrapolar trajetória ou fabricar readquisição.
- Rotação, varredura, busca inteligente por salas vizinhas, rastreamento por trilhas ou nova simulação FSM.
- Alterar o timeout de N05, sua origem, ou tratar cada deslocamento como uma nova perda.
- Selecionar, substituir ou executar uma Action diretamente pela Perception/probe.
- Alterar movimento do Player, física, Action/GOAP de outros cards, arquitetura Immersive, criar regiões genéricas de sala, ou avançar N05.
- Marcar N04 como concluído antes da execução e aceitação manual no Unity.

## 5. Validação reproduzível proposta na Mission

Preparar um probe temporário na Mission existente, executável por um único comando no Inspector e removível depois da aprovação. Ele não cria cena paralela, não escreve conhecimento, não inicia/cancela Actions do NPC e não modifica Transform/CharacterController do larápio.

Para controlar Rick, o fixture envia entrada temporária à ação `Move` já usada por `PEGAPlayerMovementIntentSource`/`PlayerGameplayInputReader`. O movimento de Rick percorre, portanto, o Input System, o intent source, `PEGAActorMovementMechanic` e seu `CharacterController`. O fixture pode iniciar e parar a entrada em resposta a eventos observados ou a marcos temporais internos; não altera o resultado da percepção. Posição real de Rick pode ser lida pelo observador para confirmar a execução da rota do fixture, mas nunca é passada ao NPC ou ao seletor.

### Cenários

1. **Perda, chegada e reposicionamento físico:** começar fora do raio de chegada; obter identidade/posição por percepção visual; deslocar Rick pela entrada legítima para trás de uma estante/oclusor; confirmar perda real e imóvel a posição conhecida. Confirmar deslocamento físico mensurável do larápio até a última posição e, só após chegar, seleção GOAP de um destino local novo. Medir movimento do `CharacterController`, chegada real e bounds do destino/rota. Estar dentro do raio antes da perda não pode contar como progresso.
2. **Readquisição real após reposicionamento:** manter Rick numa rota de Player que o exponha à percepção normal somente depois de uma oportunidade física de visada. Confirmar timestamp/posição nova de Perception, uma única solicitação `RickReacquired`, cancelamento da oportunidade local e decisão/execução coerente pelo GOAP. Alcançar o destino local sozinho não pode passar esta verificação.
3. **Sem readquisição e sem destinos repetidos:** manter Rick oculto por movimento físico real. Confirmar que a última posição e timestamp não mudam; as candidatas do NPC são distintas e finitas; cada rota continua dentro dos limites de N04; nenhuma candidata é executada duas vezes na mesma oportunidade; e N05 publica uma única despistagem dentro do timeout originalmente configurado. O timeout não se reinicia após Action local.
4. **Isolamento:** o observador registra as saídas por NPC. Estado da oportunidade, posições tentadas, Action e resultado de um larápio não podem causar evento, movimento ou atualização de conhecimento no outro.

Cada verificação gera evento estruturado por `PEGALogging`; o resumo final contém `result=PASS|FAIL`, contagens por estado, oportunidade, NPC, ação, target, distâncias física/rota e timestamps das evidências. Pré-condições ausentes, rota controlada inviável ou janela insuficiente produzem `UNAVAILABLE` com causa; não são convertidos em PASS.

### Evidências esperadas

- `lossObservedByPerception` e `lastKnownPreservedWhileHidden`.
- `physicalApproachProgressAfterLoss`, medido a partir da distância física registrada no instante da perda; exige deslocamento acima do limiar calibrado ou chegada real quando inicia fora do raio. Se inicia dentro do raio, marca indisponível para essa execução de progresso, não PASS.
- `localPlanSelectedByGoap`, `localTargetDerivedFromLastKnownEvidence`, `localTargetWithinConfiguredBounds`, `routeCompleteAndWithinLimit`, `physicalRepositionCompleted`.
- `noKnowledgeChangeFromLocalArrival`, `noSyntheticOrHiddenRickTarget`.
- `realPerceptionReacquisitionAfterReposition` e `singleReevaluation`, somente se a percepção realmente reconhecer Rick.
- `localTargetsUniqueWithinOpportunity`, `evadedAtOriginalDeadline`, `normalDesireSelectionAfterEvaded` e `noCrossNpcNotificationOrMovement`.

## 6. Critérios de aceite N04 após esta revisão

- O GOAP, sem ordens da percepção, inicia deslocamento físico à última posição observada após perda visual legítima.
- Ao chegar sem readquirir Rick, o GOAP pode escolher posições de observação finitas, dentro da vizinhança local configurada e alcançáveis pela rota física do NPC.
- Chegada local é distinguível de readquisição: não altera conhecimento/satisfação; só Perception real produz nova posição/contato.
- Readquisição cancela a oportunidade perdida; o mesmo evento não repete destinos; uma perda nova após readquisição pode criar nova oportunidade.
- A janela e o timeout N05 são preservados; despistamento encerra o comportamento local sem duplicação e retorna ao Desejo normal.
- O tester comprova deslocamento do NPC e do Player por suas cadeias reais e fornece resultado consolidado canônico, sem conhecimento sintético ou comandos de Action.
- A Scene View do Editor mostra a prévia configurada da vizinhança; quando há perda e evidência legítima disponível em Play Mode, ancora a área na última posição conhecida e destaca o Target GOAP em execução. Essa visualização é Editor-only e não alimenta percepção, conhecimento ou decisão.
- O movimento local fica restrito ao armazém da Mission e às cotas espaciais configuradas. A falta de semântica de sala fica explicitada; não se aceita alegação de suporte geral a salas conectadas.

## 7. Riscos e revisão necessária

1. **Fronteira semântica:** o NavMesh da Mission não marca salas. O usuário aprovou a vizinhança radial e de rota para este primeiro corte na área única; isso não garante separação semântica em futuras cenas conectadas. Uma fronteira de sala autorada permanece fora deste N04.
2. **Contrato GOAP:** o usuário aprovou a extensão limitada da seção 3.4 para distinguir progresso físico intermediário da satisfação final. A implementação deve preservar a seleção pelo GOAP e evitar declarar Rick encontrado após simples movimentação.
3. **Tempo de execução:** a rota física precisa caber na janela N05. Se a configuração atual não permitir completar a aproximação/reposicionamento antes do prazo, a validação deve reportar a limitação e não alterar silenciosamente o timeout. O smoke também espera a conclusão física da aproximação a Rick readquirido antes de iniciar sua segunda perda controlada, e limita temporariamente o comprimento máximo de rota a um valor positivo abaixo do raio de chegada para provar a janela sem candidato.
4. **Aceite anterior:** logs PASS de perda/readquisição/despistamento continuam válidos para os eventos que observaram, mas não cobrem a nova capacidade de reposicionamento nem provam progresso físico quando o NPC já estava no raio de chegada.

## 8. Decisões confirmadas

1. O usuário aprovou a vizinhança finita por proximidade e rota para a Mission atual e confirmou que esses limites não garantem fronteiras semânticas entre salas futuras.
2. O usuário aprovou o progresso intermediário do GOAP sem satisfação do Desejo e determinou que o timeout de N05 siga limitando a investigação, inclusive sem destino local válido, sem ciclos de replanejamento sem progresso.
3. O usuário aprovou a validação temporária automatizada na Mission, com movimento real de Rick pela cadeia normal do Player e autonomia do NPC preservada.

O plano de implementação foi preparado em `docs/superpowers/plans/2026-10-22-pega-n91-n04-local-investigation.md` e aprovado pelo usuário. A implementação foi preparada no workspace; o card N04 permanece aberto até authoring do prefab e aceitação funcional manual no Unity.
