# PEGA — Análise macro de atividades do GDD

**Status:** análise de objetivos e entregáveis candidatos; não altera GDD, MVP, ADR, TASKS ou Trello.
**Data:** 2026-10-15
**Fontes principais:** [GDD](../PEGA%20GDD/GDD.md), [MVP](../PEGA%20GDD/MVP.md) e [ADR de pré-produção](../PEGA%20GDD/ADR-PREPRODUCAO.md). O [Art Book](../PEGA%20GDD/ArtBook.md) é referência visual, não fonte de regras de gameplay.

## Objetivo e recorte

Esta análise transforma os resultados pretendidos pelo jogo em atividades macro candidatas. O GDD define comportamento e experiência; o MVP diz o que entra no primeiro vertical slice e na Demo; o ADR registra decisões de produção. TASKS e Trello podem ser comparados depois, mas não criam objetivos de jogo.

Uma atividade candidata representa um resultado de jogo coerente. Regras, verbos, timers, objetos e estados são checks que sustentam esse resultado, a menos que o GDD lhes dê um objetivo independente. As duas perspectivas permanecem visíveis: o que o jogador escolhe/faz e o que o mundo produz.

**Atenção ao recorte de produção:** o microciclo inicial do ADR é um subconjunto do vertical slice. Preparação completa, invasão temporal, depósito em cofres, prisão, Fuga Final e resolução continuam no escopo do vertical slice completo; o ADR apenas os coloca depois do microciclo inicial. Isso não é remoção de conteúdo do MVP (MVP §A; ADR §§3.1–3.2).

## Ciclo pela perspectiva do jogador

| Fase/objetivo do jogador | Resultado observável | Recorte |
|---|---|---|
| Selecionar personagem e entrar em sessão local | Um Player entra controlável no slice; na Demo, os Players escolhem Rick/Petra e cada personagem selecionado pode ser controlado com apresentação correta. | Slice solo com identidade padrão, sem tela de seleção; Demo com seleção de personagem, dois Players e split-screen (MVP §§A–B; ADR, adendo v2). |
| Escolher a missão e entender o que será protegido | Na Demo, o jogador percorre o HUB, escolhe a missão e recebe apresentação/briefing do cenário e da gangue. | A Demo tem um Armazém jogável. O slice inicia diretamente na missão, sem menu/HUB (MVP §B; ADR, adendo v2). |
| Preparar a proteção | Durante a Preparação, o jogador escolhe um cofre válido para o objetivo principal; a escolha fica visível aos Players. | Slice e Demo. No slice, ao menos dois cofres válidos e um padrão; na Demo, ambos podem alterar a escolha e prevalece a última válida. A transição de fase pertence à progressão temporal da missão (GDD §Preparação; MVP §§A–B). |
| Responder ao assalto e proteger os bens | O jogador acompanha ameaças, persegue/interrompe larápios, usa o cenário, recupera itens e os protege em armazenamento válido. | Ciclo central do slice e da Demo (GDD §§Loop principal, Controles, Inventário e Interações; MVP §A). |
| Concluir capturas | Um larápio incapacitado é transportado e entregue à prisão; só a entrega conclui a captura. | Slice completo e Demo. É resultado distinto da recuperação de itens; o microciclo inicial do ADR o deixa para etapa posterior (GDD §§Transporte de larápios e Encerramento; MVP §A; ADR §3). |
| Ver o resultado e continuar | O jogador recebe os estados finais e a classificação; na Demo, acompanha saldo, carreira, repetição da missão e bancarrota/reinício. | Resultado básico no slice; carreira e fluxo financeiro na Demo (GDD §§Classificação, Carreira e Encerramento; MVP §§A–B). |

## Ciclo pela perspectiva de gameplay

| Resultado do mundo | Regra observável | Recorte |
|---|---|---|
| A missão avança por fases temporizadas | A Preparação termina pelo próprio timer; inicia o Assalto Ativo. O Tempo de Assalto e o Tempo de Fuga são separados. As receitas de Spawn Point começam no Assalto Ativo, não durante a Preparação. | Slice completo e Demo. Valores são configurados/balanceados por missão (GDD §§Estrutura da missão, Preparação e Encerramento; MVP §§A, D). |
| Larápios invadem e conduzem um assalto autônomo | Spawn Points executam receitas independentes; os larápios entram pelo Spawn Seguro. Eles têm conhecimento parcial, investigam e decidem via GOAP, podem roubar e tentar escapar; mudanças relevantes provocam replanejamento. | Slice com ao menos um grupo/arquetipo e Demo com a gangue e quatro arquétipos definidos (GDD §§GOAP, Investigação, Spawn Points e Fuga; MVP §§A–B). |
| Cenário e objetos respondem às interações | Portas, armadilhas, câmeras, armazenamentos e outros objetos podem alterar acesso, informação ou oportunidade; seus estados devem ser comunicáveis. | Slice: leitura de mapa e interações necessárias ao ciclo. Demo: Armazém completo com objetos e condições legíveis. A lista de objetos necessária em cada corte não está enumerada individualmente (GDD §§Interações e Sala de controle; MVP §§A–B). |
| Posse de itens e captura mudam o estado da missão | Itens não são destruídos: sua posse muda, ataques válidos podem derrubá-los, e armazenamento pode protegê-los. Captura só se conclui por entrega efetiva à prisão. | Slice completo e Demo; recuperação, depósito e prisão são resultados ligados, mas cada um tem critério próprio (GDD §§Inventário, Interações e Estado final dos itens; MVP §A). |
| Fuga e encerramento resolvem NPCs e itens | O fim do Tempo de Assalto inicia a Fuga Final; GOAP continua ativo. Ao fim do Tempo de Fuga, os larápios são Capturado, Escapou ou Não escapou; itens que saíram com fugitivos são Roubados e os que permaneceram no cenário são Recuperados. | Slice completo e Demo; a classificação temporal é opcional no primeiro corte (GDD §§Fuga Final, Encerramento e Classificação; MVP §§A, D). |
| Power-ups alteram temporariamente propriedades do jogador | Efeito e duração devem ser legíveis quando forem incluídos. | Fora do primeiro MVP; inclusão/efeitos futuros dependem de escopo e playtest. Não é atividade do slice atual (GDD §Power-ups; MVP §D). |

O ciclo de NPCs coberto aqui é o dos larápios. Civis/figurantes dão vida aos cenários; clientes apoiam briefing narrativo; P.G.A. aparece na narrativa/transições, mas não atua como NPC durante o assalto (GDD §NPCs). Não se inventou um ciclo de simulação para essas categorias.

## Atividades macro candidatas

Os IDs abaixo são locais a esta análise; não são IDs de cartões. As linhas descrevem objetivos, não cenas, componentes ou ordem de implementação.

| ID | Objetivo e resultado observável | Perspectiva | Escopo e relações |
|---|---|---|---|
| A1 | **Participar da missão como Player local:** o Player (ou os dois Players na Demo) controla o personagem associado à sessão e recebe a apresentação correta. | Jogador | Slice: um Player controlável. Demo: Rick/Petra selecionáveis e dois Players em split-screen. A seleção de personagem e a de missão têm objetivos próprios; a materialização comprova o resultado da seleção de personagem (GDD §§Personagens e Modos; MVP §§A–B). |
| A2 | **Escolher e entender a missão:** o jogador inicia o contrato escolhido com informação sobre cenário e ameaça. | Jogador | Demo: HUB, seleção, briefing e uma missão jogável, o Armazém. Slice: entrada direta na missão. A apresentação detalhada é suporte deste objetivo, não contagem de Unity Scenes (GDD §§Loop principal e HUB; MVP §B; ADR, adendo v2). |
| A3 | **Preparar a proteção do objetivo principal:** o objetivo fica associado ao cofre válido escolhido e a escolha é conhecida pelos Players. | Jogador + mundo | Slice e Demo. Checks: cofre padrão, escolha gratuita; na Demo, última escolha válida prevalece. A localização não é revelada automaticamente aos larápios. Liga-se a A4 e A5 (GDD §Preparação; MVP §§A–B). |
| A4 | **Avançar a missão por fases e relógios:** a Preparação, o Assalto Ativo e a Fuga Final começam e terminam conforme seus tempos; receitas de invasão ocorrem na janela prevista. | Mundo | Slice completo e Demo. Preparação, Tempo de Assalto, Tempo de Fuga e receitas de Spawn Point seguem regras temporais próprias; seus valores podem ser configurados por missão. Habilita A5 e leva a A9 (GDD §§Estrutura da missão, Preparação e Encerramento; MVP §§A, D). |
| A5 | **Produzir a invasão e o assalto autônomo dos larápios:** a gangue entra e executa objetivos a partir de informação parcial, podendo roubar e tentar escapar. | Mundo | Slice: entradas/Spawn Seguro e GOAP com pelo menos um arquétipo. Demo: Armazém completo e quatro arquétipos. Checks: conhecimento, investigação, decisões e replanejamento; não são atividades individuais. Depende da progressão A4 (GDD §§GOAP e Spawn Points; MVP §§A–B). |
| A6 | **Usar o cenário para afetar informação e acesso:** interações do ambiente mudam as oportunidades disponíveis e comunicam o estado relevante. | Jogador + mundo | Slice: apenas interações necessárias ao ciclo; Demo: cenário completo. Câmeras/sala de controle e a seleção exata de portas, armadilhas e armazenamentos ainda precisam ser alocadas ao conteúdo da missão (GDD §§Interações e Sala de controle; MVP §§A–B). |
| A7 | **Recuperar e proteger os bens:** itens mudam de portador, podem ser derrubados/recuperados e, quando elegíveis, depositados em armazenamento válido. | Jogador + mundo | Slice completo e Demo. Não depende de capturar o portador; relaciona-se a A5 e A9. A9 resolve os estados finais dos itens (GDD §§Inventário e Estado final dos itens; MVP §A; ADR §3.2). |
| A8 | **Concluir a captura de um larápio:** o resultado Capturado só ocorre após incapacitação, transporte e entrega à prisão. | Jogador + mundo | Slice completo e Demo. Independente de recuperar itens, ainda que os dois resultados influenciem o desempenho do assalto. Liga-se a A5 e A9 (GDD §§Transporte e Encerramento; MVP §A; ADR §3.2). |
| A9 | **Encerrar o assalto e produzir seu resultado:** o fim da Fuga resolve estados de larápios/itens e produz uma classificação compreensível. | Mundo + jogador | Slice: resultado básico sem exigir pontuação temporal. Demo: critérios/pesos da missão comunicados e resumo do turno. Depende de A4, A5, A7 e A8 (GDD §§Encerramento e Classificação; MVP §§A–B). |
| A10 | **Continuar a carreira após a missão:** o jogador consulta saldo e resultados, repete a missão ou encerra/reinicia diante da bancarrota. | Jogador + progressão | Demo; créditos e persistência entre assaltos. Campanha expandida, contratos adicionais e Endless são posteriores à Demo (GDD §§Carreira e Modos; MVP §§B–C). |

## Relações entre os ciclos e as atividades

| Encadeamento | Experiência do jogador | Resultado de gameplay relacionado |
|---|---|---|
| A1 + A2 → início da missão | Na Demo, o jogador tem um personagem e uma missão selecionados; no slice, a missão inicia diretamente com a identidade padrão. | A sessão começa com personagem controlável e contexto de missão definidos. A seleção do personagem e a seleção da missão continuam objetivos distintos. |
| A2 → A3 → A4 | O jogador entende o objetivo principal e prepara sua proteção. | A missão selecionada define o objetivo; a fase de Preparação termina pelo relógio e habilita o Assalto Ativo. |
| A4 → A5 | O jogador passa a responder à invasão. | Receitas geram as entradas durante o Assalto Ativo; os larápios investigam, decidem e replanejam sem receber fatos ocultos. |
| A5 ↔ A6 | O jogador observa e altera oportunidades usando o cenário. | Larápios e jogadores interagem com objetos; o ambiente pode alterar acesso ou informação disponível. A seleção exata de objetos permanece alocada ao conteúdo da missão. |
| A5 → A7; A5 → A8; A4, A5, A7 e A8 → A9 | O jogador responde ao assalto, recupera bens e tenta concluir capturas. | Posse de itens e condição dos larápios evoluem; ao terminar a Fuga, o jogo resolve os estados finais e calcula a classificação. Recuperar bens e capturar um larápio continuam resultados distintos. |
| A9 → A10 | O jogador consulta o resultado e decide continuar a carreira na Demo. | Saldo e persistência da carreira afetam a repetição, continuidade ou bancarrota. |

As setas indicam dependência de sequência; `↔` indica influência durante o mesmo assalto. São relações entre objetivos do jogo, não uma ordem de implementação.

## Cobertura e lacunas

### Objetivos macro bloqueados por decisão

**Nenhum identificado.** As fontes permitem reconhecer as atividades candidatas e separar o que pertence ao slice, à Demo, ao pós-Demo ou ao opcional.

### Conflitos reais entre fontes

**Nenhum identificado.** Diferenças aparentes correspondem a recortes explícitos:

- O loop completo do GDD passa pelo HUB e seleção de missão; o slice do ADR entra diretamente na Mission Route. A seleção e apresentação pertencem ao fluxo de Demo, não ao slice.
- O primeiro microciclo do ADR adia preparação, invasão temporal, prisão, armazenamento, Fuga Final e classificação dentro do mesmo vertical slice; o MVP ainda exige o ciclo completo para concluir o slice.
- O jogo completo prevê conteúdo maior que a Demo; MVP §C identifica campanha expandida, outros cenários/contratos, Endless e conteúdo adicional como posterior.

### Detalhes que não bloqueiam definir as atividades macro

Essas lacunas não impedem reconhecer os objetivos, mas podem virar decisões ou checks de aceitação quando uma atividade for preparada para execução.

| Lacuna | Por que não bloqueia o objetivo macro |
|---|---|
| Se dois Players podem escolher o mesmo personagem; identidade padrão do slice | A seleção/controle está definida; a regra de exclusividade e a identidade do slice são critérios detalhados ainda não declarados (GDD §Personagens; MVP §§A–B). |
| Posição/regra de entrada do Player no cenário | “Player controlável/entrada correta” é um resultado definido para a Demo, mas o ponto exato é detalhe de composição/conteúdo (MVP §B). |
| Valores de Preparação, Assalto, Fuga, receitas de Spawn Points e balanceamento por arquétipo | O GDD/MVP definem que tempos e receitas existem; valores e combinações são conteúdo configurável (MVP §D). |
| Conteúdo exato de objetos interativos, sala de controle, câmeras e armadilhas por entrega | O objetivo de ler/interagir com o cenário está claro; a MVP pede somente interações necessárias no slice e cenário completo na Demo, sem inventário fechado por objeto (GDD §§Interações e Sala de controle; MVP §§A–B). |
| Fórmula/pesos finais da classificação, detalhes de apresentação do resultado e conteúdo da carreira após a Demo | O MVP define resultado básico no slice e carreira na Demo; pesos são por missão/playtest e o pós-Demo ainda é aberto (GDD §§Classificação e Carreira; MVP §§B–D). |
| Mapeamento final de esquiva, salto e soltura de itens; política de Pause cooperativo | Ações e necessidade de Pause estão descritas, mas parte do input e suporte para dois Players ainda precisa de definição (GDD §Controles; MVP §§B e decisões pendentes). |
| Lista e efeitos dos power-ups | Explicitamente fora do primeiro MVP e sujeitos a escopo/playtest posterior (MVP §D). |

### Entregas transversais para detalhamento posterior

- **Apresentação e navegação:** o fluxo da Demo inclui splash/logo, menu, opções, HUB, seleção de Players/personagens e missão, briefing, gangue, gameplay/Pause, classificação, resumo financeiro e retorno/bancarrota. São momentos de experiência; não implicam uma Unity Scene cada.
- **Feedback/HUD:** temporizadores, inventário, itens recuperados/protegidos, capturas, estado do personagem e minimapa sustentam as atividades A3–A9; a orientação do minimapa permanece opcional.
- **Players e controles:** o GDD prevê um ou dois Players locais; a Demo exige dois, sem multiplayer online. Movimento, interagir/atacar, confirmar e pausar têm mapeamentos descritos; esquiva, salto e soltura ainda precisam de binding.
- **Conteúdo visual/audiovisual:** a Demo exige assets, animações e identidade visual necessários para Players, gangue, Armazém e apresentação. Esta análise não verifica arquivos existentes nem fecha a lista de produção; o Art Book serve como referência visual.

Esses elementos podem gerar atividades próprias quando houver objetivos de produto independentes. Nesta análise, telas, controles, feedback e assets permanecem requisitos transversais das atividades acima; não foram transformados automaticamente em cartões por tela ou arquivo.

## Integridade da análise

O GDD/MVP/ADR foram usados somente como fontes de análise; não foram editados. TASKS e Trello não foram alterados. Nenhum código, prefab, asset ou configuração de gameplay foi implementado. Os resultados podem agora ser usados para comparar os cartões existentes com os objetivos do jogo, sem presumir que cada linha desta análise corresponda a um cartão.
