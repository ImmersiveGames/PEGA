# PEGA — Especificações comportamentais dos objetivos

**Status:** especificação derivada para revisão; não é plano de implementação.
**Data:** 2026-10-15
**Catálogo macro e cobertura:** [pega-gdd-activity-analysis.md](pega-gdd-activity-analysis.md).
**Autoridade:** GDD para regras/experiência; MVP para recortes; ADR para ordem e decisões de produção. TASKS/Trello não criam regras.

## Como ler estas especificações

Cada contrato descreve resultado de jogo, não classe, prefab, cena, package ou arquitetura. Os critérios são cenários comportamentais; “evidência” significa observação reproduzível do estado/feedback, sem prescrever instrumentação.

Os recortes são cumulativos apenas quando a fonte diz isso:

- **Microciclo:** primeiro subconjunto integrado do slice, conforme ADR §3.1.
- **Slice:** ciclo completo do MVP §A; o microciclo não o substitui.
- **Demo:** amplia o slice conforme MVP §B.
- **Pós-Demo** e **opcional/playtest:** identificados sem inventar comportamento ainda aberto.

“Goal/Desejo” com maiúscula ou no contexto de GOAP refere-se a conceito de IA dos larápios. OBJ-01–OBJ-16 são objetivos de produto/jogo.

## OBJ-01 — Participar da missão com o roster local escolhido

**Família:** Entrada e compreensão da missão. **Perspectiva:** jogador e sessão local.

**Fontes:** GDD §§Personagens (355–375), Modos (869–893), Menus (943–959); MVP §§A–B (23–39, 45–61); ADR adendo v2 (165–179).

**Propósito e resultado observável.** O jogador escolhe jogar solo ou em cooperativo local conforme o recorte e controla o personagem correspondente na missão. Na Demo, os Players locais escolhidos participam juntos com suas identidades corretas. O resultado é a presença de um ou dois Players controláveis na missão; selecionar uma identidade sem ela entrar na missão não conclui o objetivo.

**Condições iniciais.** O modo/roster permitido pelo recorte está determinado; quando houver seleção formal, cada escolha válida está disponível. A missão inicia pela entrada direta do slice ou pelo fluxo da Demo.

**Comportamento e estados.**

- Slice: deve existir um Player controlável; MVP não especifica seleção formal de personagem nem identidade padrão.
- O GDD prevê campanha solo ou cooperativa local para até dois Players; a Demo deve demonstrar dois Players e o MVP inclui verificação de fluxo com um e dois.
- Demo: há dois Players locais; Rick e Petra são selecionáveis e mecanicamente equivalentes.
- A identidade escolhida deve corresponder ao personagem que aparece e responde ao respectivo controle.
- O GDD não fixa ordem entre escolher quantidade de Players, modo local e personagem, nem regra de exclusividade Rick/Petra.
- Na Demo com split-screen, HUD, minimapa e ícones precisam ser legíveis para ambos os Players; nenhuma informação crítica pode ficar pequena demais ou apenas na metade do outro Player.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Slice inicia diretamente na missão | O modo e o roster permitidos pelo recorte estão determinados. | A rota direta do slice carrega a missão. | Um Player está presente e controlável na missão; o catálogo não exige tela de seleção nem fixa uma identidade padrão para o slice. Falha se não houver personagem controlável. | Observar entrada e controle do Player; a identidade padrão permanece em GAP-07 | MVP §A (23–39); GDD §§Personagens (355–375), Modos (869–893). |
| Demo inicia com um Player selecionado | O modo e o roster permitidos pelo recorte estão determinados. | A missão começa com a identidade selecionada. | Rick ou Petra aparece como selecionado e controlável na missão. Falha se a escolha não corresponder. | Comparar escolha e personagem materializado/controlado | MVP §B (45–61); GDD §§Personagens (355–375), Menus (943–959). |
| Demo inicia com dois Players locais | O modo e o roster permitidos pelo recorte estão determinados. | A missão começa com os dois Players locais selecionados. | Os dois personagens escolhidos estão presentes e cada Player consegue controlar o seu. Falha se só um participar ou se identidade/controle forem trocados. | Observar os dois controles e personagens em gameplay local | MVP §B (45–61); GDD §Modos (869–893); ADR adendo v2 (165–179). |
| HUD e minimapa no split-screen | O modo e o roster permitidos pelo recorte estão determinados. | A partida é exibida em split-screen durante o gameplay. | HUD, minimapa e ícones permanecem legíveis para ambos; informação crítica não fica pequena demais nem apenas na metade do outro Player. Falha se um Player não conseguir lê-la ou se ela só aparecer na metade oposta. | Inspecionar a mesma informação durante a partida em cada metade | GDD §Câmera (461–476, legibilidade de split-screen); MVP §B (45–61). |

**Fronteiras e relações.** Inclui modo local, roster e materialização percebida. Seleção de contrato é OBJ-02; briefing é OBJ-03; regra de cooperação da escolha de cofre é OBJ-04. Não implica jogo online.

**Escopo.** Slice: um Player. Demo: dois Players locais, seleção Rick/Petra e split-screen. Pós-Demo: modos adicionais conforme GDD/MVP, sem detalhe criado aqui.

**Lacunas.** GAP-03 (repetição de personagem/atribuição no coop) e GAP-07 (identidade padrão do slice). Não impedem reconhecer o objetivo, mas limitam o aceite final desses casos.

## OBJ-02 — Selecionar e iniciar um contrato disponível

**Família:** Entrada e compreensão da missão. **Perspectiva:** jogador.

**Fontes:** GDD §§Loop principal (112–143), HUB (346–353), Menus (943–959); MVP §§B–C (45–77); ADR adendo v2 (165–179).

**Propósito e resultado observável.** O jogador inicia um contrato disponível e a atividade iniciada corresponde à missão selecionada. O objetivo termina quando o contrato escolhido começa; navegar pelo menu ou destacar um contrato sem iniciá-lo não basta.

**Condições iniciais.** A entrada de Demo está ativa e existe pelo menos um contrato disponível. Para o slice, a rota começa diretamente na missão, sem exigir HUB/menu inicial.

**Comportamento e estados.**

- Demo: escritório estático com hotspots, missão disponível e retorno ao HUB conforme MVP.
- O Armazém é a missão jogável da Demo.
- Pós-Demo: MVP prevê HUB navegável, cenários/contratos e campanha expandidos; catálogo não presume ordenação ou desbloqueios não especificados.
- Uma tela/página de fluxo não define uma Unity Scene.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Slice inicia | A rota de entrada correspondente ao recorte está ativa. | A atividade prevista para o slice é iniciada. | A atividade abre diretamente na missão prevista para o slice. Falha se seleção/HUB da Demo for requisito indevido do slice ou se não iniciar. | Observar missão ativa | MVP §A (23–39); GDD §Loop principal (112–143). |
| Demo escolhe o Armazém | A rota de entrada correspondente ao recorte está ativa. | O contrato Armazém é escolhido e iniciado. | A seleção inicia o Armazém e o fluxo passa à apresentação/jogo correspondente. Falha se inicia outro conteúdo ou nenhum. | Comparar contrato escolhido e missão iniciada | MVP §§B–C (45–77); GDD §§HUB (346–353), Menus (943–959). |

**Fronteiras e relações.** OBJ-01 estabelece roster; OBJ-03 fornece contexto do contrato; OBJ-04 começa a Preparação. Não cobre carreira/unlocks nem define telas ou cenas técnicas.

**Escopo.** Slice: entrada direta. Demo: missão selecionável no escritório estático. HUB navegável e expansão: pós-Demo.

**Lacunas.** A ordem entre seleção de modo, personagem e contrato não está formalizada; isso não impede identificar o objetivo. A quantidade de Unity Scenes está fora da autoridade deste catálogo e não é uma lacuna de comportamento.

## OBJ-03 — Compreender o contexto do contrato

**Família:** Entrada e compreensão da missão. **Perspectiva:** jogador.

**Fontes:** GDD §Estrutura de uma missão (128–143), HUB (346–353), NPCs (368–375); MVP §§A–B (31, 39, 53, 63–69).

**Propósito e resultado observável.** Antes de agir, o jogador recebe o contexto previsto para reconhecer o cenário e a ameaça: cenário, entradas/saídas, itens de valor, novidades e gangue, dentro do fluxo de apresentação. Esse objetivo não obriga uma cena específica nem inventa conteúdo ausente.

**Condições iniciais.** Há contrato selecionado (OBJ-02) ou a rota direta do slice carregou sua missão.

**Comportamento e estados.**

- Demo inclui briefing e apresentação da gangue; o fluxo de telas deve conduzir ao gameplay.
- O GDD descreve a informação útil ao jogador sobre cenário e novidades; MVP não estabelece roteiro fechado nem exige briefing formal no slice.
- A apresentação não revela automaticamente aos larápios informação que o GDD mantém desconhecida.
- O feedback que sustenta o entendimento deve ser coerente com a informação disponibilizada ao jogador.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Demo apresenta o contrato | O contrato foi selecionado ou a rota direta do slice carregou a missão. | A apresentação do contrato é exibida antes do gameplay. | A apresentação identifica o Armazém e a gangue correspondente e oferece o contexto de missão previsto antes do gameplay. Falha se omitir informação exigida pelo fluxo do MVP ou apresentar gangue incorreta. | Conferir conteúdo exibido e transição ao jogo | GDD §§Estrutura de missão (128–143), NPCs (368–375); MVP §B (45–61). |
| Slice entra direto na missão | O contrato foi selecionado ou a rota direta do slice carregou a missão. | A rota direta do slice inicia o gameplay. | A missão inicia sem exigir uma tela formal de briefing, conforme o recorte do slice. A especificação não presume que uma UI específica explique todo o contexto. | Observar entrada direta e ausência de dependência de briefing; a qualidade de leitura do contexto permanece em GAP-08. | GDD §Estrutura de missão (128–143); MVP §A (23–39). |
| Apresentação e conhecimento dos larápios | O contrato foi selecionado ou a rota direta do slice carregou a missão. | O briefing é apresentado ao Player. | O briefing não concede automaticamente aos larápios a localização do objetivo principal. Falha se a apresentação ao Player alterar o conhecimento dos larápios sem uma fonte de aquisição. | Comparar o conhecimento dos larápios antes/depois da apresentação e a informação apresentada ao Player. | GDD §§Objetivo principal (158–168), Conhecimento (637–689); derivado da separação entre apresentação ao Player e aquisição de conhecimento pelos larápios. |

**Fronteiras e relações.** OBJ-02 seleciona o contrato. OBJ-04 utiliza a informação para preparar. Conteúdo detalhado de fala/narrativa e layout visual não está especificado.

**Escopo.** Slice: compreensão no cenário e informação necessária ao ciclo; briefing formal não alocado. Demo: briefing, gang splash e fluxo de apresentação.

**Lacunas.** GAP-08: conteúdo fechado do briefing e critérios de legibilidade/qualidade.

## OBJ-04 — Preparar a proteção do objetivo principal

**Família:** Preparação e progressão do assalto. **Perspectiva:** Player e estado do mundo.

**Fontes:** GDD §§Preparação (146–155), Objetivo principal (158–168); MVP §§A–B (31–33, 55); ADR §3.2 VS-10 (85–97).

**Propósito e resultado observável.** Durante a Preparação, os Players estabelecem um cofre válido como proteção escolhida para o objetivo principal. O resultado é uma escolha válida única, visível aos Players e não divulgada automaticamente aos larápios.

**Condições iniciais.** Preparação está ativa; há pelo menos dois cofres válidos no slice; existe cofre padrão definido para escolha inicial.

**Comportamento e estados.**

- Preparação tem duração fixa; jogo normal não oferece “Pronto” nem término antecipado.
- Escolher cofre válido não custa recurso.
- Demo: ambos os Players podem alterar a escolha; prevalece a última escolha válida e a seleção é atualizada para ambos.
- Escolhas inválidas não substituem a última escolha válida.
- Larápios não recebem automaticamente a localização do objetivo principal.
- No início da Preparação do primeiro MVP, o objetivo principal está em um cofre válido e nunca ocupa o inventário do Player.
- O documento não infere em que instante ou de que forma física o objetivo se move para o cofre.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Início da Preparação | A missão está carregada, com cofres válidos configurados para o recorte. | A fase de Preparação começa. | Há escolha padrão entre cofres válidos. Falha se não houver escolha válida. | Observar estado inicial da seleção | GDD §Preparação (146–155); MVP §§A–B (31–33, 55). |
| Início da Preparação no primeiro MVP | A missão do primeiro MVP está carregada, com cofres válidos e o objetivo principal configurados. | A Preparação começa. | O objetivo principal está em um cofre válido e não aparece no inventário do Player. Falha se estiver no inventário ou não estiver associado a cofre válido. | Conferir estado/localização inicial | GDD §Objetivo principal (158–168); MVP §A (23–39). |
| Player escolhe cofre válido | A Preparação está ativa e existem cofres válidos conforme o recorte. | Um Player escolhe um cofre válido. | A seleção muda sem custo e é mostrada como atual. Falha se houver custo não previsto ou escolha não persistir. | Observar seleção e saldo/recursos | GDD §Preparação (146–155). |
| Demo: ambos alteram escolha | A Preparação está ativa e existem cofres válidos conforme o recorte. | Os dois Players fazem escolhas válidas em sequência. | A última escolha válida prevalece e ambos veem o mesmo valor. | Reproduzir duas escolhas em sequência e comparar estado mostrado. Caso de entradas simultâneas fica em GAP-10 | GDD §Preparação (146–155); MVP §B (45–61). |
| Player tenta escolha inválida | A Preparação está ativa e existem cofres válidos conforme o recorte. | Um Player tenta escolher um cofre inválido. | A escolha inválida não remove a última válida. Falha se o estado ficar sem escolha válida. | Observar seleção antes/depois | GDD §Preparação (146–155). |
| Larápios iniciam assalto | A Preparação está ativa e existem cofres válidos conforme o recorte. | O Assalto Ativo começa. | A localização do objetivo não é concedida automaticamente. Falha se o objetivo aparecer no conhecimento sem fonte legítima. | Verificar fatos conhecidos por NPCs | GDD §Objetivo principal (158–168); GDD §Conhecimento (637–689). |

**Condições finais/invariantes.** Uma escolha válida está registrada para os Players ao terminar a fase. Quando o objetivo efetivamente muda de posição é indeterminado; não se declara transferência física no aceite sem esclarecer GAP-02.

**Fronteiras e relações.** OBJ-05 termina a Preparação por tempo. OBJ-07/08 devem respeitar segredo da localização. A classificação de itens protegidos pertence a OBJ-12/15.

**Escopo.** Slice e Demo. Cooperação de escolha descrita para Demo. Microciclo inicial do ADR adia Preparação.

**Lacunas.** GAP-02 (momento/efeito físico da escolha), GAP-10 (empate simultâneo).

## OBJ-05 — Avançar fases e relógios da missão

**Família:** Preparação e progressão do assalto. **Perspectiva:** gameplay.

**Fontes:** GDD §§Loop principal (112–125), Estrutura de missão (128–143), Preparação (146–155), Encerramento (969–986); MVP §§A, D (23–39, 79–84); ADR §§3.1–3.2 (71–97).

**Propósito e resultado observável.** O assalto progride de Preparação para Assalto Ativo e depois Fuga Final conforme relógios distintos. O fim do Tempo de Fuga encerra imediatamente o gameplay.

**Condições iniciais.** A missão ativa tem tempos de Preparação, Assalto e Fuga configurados.

**Comportamento e estados.**

- A Preparação termina pelo relógio e inicia o Assalto Ativo; não termina antecipadamente por comando de pronto.
- Tempo de Assalto e Tempo de Fuga são contagens separadas.
- O término do Assalto inicia a Fuga Final, durante a qual os larápios continuam ativos.
- A janela de invasão é derivada dos horários de Spawn Points; não é fase global independente.
- Fuga Final encerra o gameplay imediatamente quando seu relógio termina.
- Tempos exatos são parâmetros da missão e sujeitos a balanceamento.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Timer da Preparação expira | A missão está ativa e seus relógios estão configurados. | O relógio da Preparação chega ao fim. | Fase muda para Assalto Ativo e as receitas passam a contar desde esse início. Falha se a contagem começar na Preparação ou exigir ação de pronto. | Comparar fase e referência temporal | GDD §§Loop principal (112–125), Preparação (146–155); MVP §A (23–39); ADR §§3.1–3.2 (71–97). |
| Timer do Assalto expira | A missão está ativa e seus relógios estão configurados. | O relógio do Assalto chega ao fim. | Fuga Final começa sem encerrar o gameplay. Falha se o jogo fechar nessa transição. | Observar transição e continuidade de gameplay/GOAP | GDD §§Loop principal (112–125), Encerramento (969–986); MVP §A (23–39). |
| Timer da Fuga expira | A missão está ativa e seus relógios estão configurados. | O relógio da Fuga chega ao fim. | Gameplay termina e estados são entregues a OBJ-14/OBJ-15. Falha se ações de missão continuarem após o fim. | Observar parada imediata e resolução | GDD §Encerramento (969–986); MVP §A (23–39). |

**Fronteiras e relações.** Não define horários de cada receita (OBJ-06), resultado terminal (OBJ-14), duração numérica nem pontuação (OBJ-15).

**Escopo.** Slice completo e Demo. O microciclo inicial testa partes sem representar o ciclo temporizado completo.

**Lacunas.** Durações são configuráveis, não impedimento de especificação macro. A dependência do relógio de spawn é OBJ-06.

## OBJ-06 — Executar receitas temporais de invasão

**Família:** Preparação e progressão do assalto. **Perspectiva:** gameplay.

**Fontes:** GDD §§Spawn Points e receitas (811–825), Spawn Seguro (827–839), Encerramento (969–986); MVP §A (33); ADR §§3.1–3.2 (71–97).

**Propósito e resultado observável.** Larápios são criados e entram no cenário conforme receitas independentes de Spawn Points, sem depender de uma onda global.

**Condições iniciais.** Assalto Ativo começa; missão fornece pelo menos um Spawn Point, sua receita e um Spawn Seguro.

**Comportamento e estados.**

- Horários da receita são absolutos desde o início do Assalto Ativo; cada Spawn Point é independente.
- A Janela de Invasão é derivada do último horário previsto.
- Grupo é criado em Spawn Seguro fora do espaço jogável e tenta entrar pelo ponto configurado.
- Entrada é permitida; porta fechada/trancada pode atrasar, mas não impedir.
- Entradas da mesma receita simultânea no ponto compartilham atraso; novas chegadas durante abertura juntam-se usando tempo restante.
- Começar Fuga Final cancela receitas ainda pendentes.
- Quantidade, gangue, arquétipo e horários são conteúdo configurado por missão.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Um horário de receita chega | O Assalto Ativo começou e a missão tem Spawn Point, receita e Spawn Seguro configurados. | Chega o horário configurado para uma receita. | O grupo previsto é criado no Spawn Seguro associado e tenta entrar pelo Spawn Point correto. Falha se ocorrer em fase/horário ou ponto incorreto. | Observar quantidade, pertencimento ao grupo e rota de entrada | GDD §§Spawn Points e receitas (811–825), Spawn Seguro (827–839); MVP §A (23–39). |
| Porta de entrada está fechada | O Assalto Ativo começou e a missão tem Spawn Point, receita e Spawn Seguro configurados. | A porta que controla a entrada permanece fechada. | Entrada física é atrasada até condição de abertura, mas não bloqueada para sempre por essa condição. Falha se a receita for impedida pela porta. | Observar estado e chegada após liberação | GDD §§Spawn Points e receitas (811–825), Cenários (478–514). |
| Várias chegadas simultâneas ou durante abertura | O Assalto Ativo começou e a missão tem Spawn Point, receita e Spawn Seguro configurados. | Mais de uma chegada ocorre ao mesmo tempo ou durante a abertura. | A regra de atraso compartilhado e tempo restante é aplicada. Falha se cada chegada reiniciar atraso sem base na regra. | Comparar horários/entradas observáveis | GDD §Spawn Points e receitas (811–825). |
| Fuga Final inicia antes de receita pendente | O Assalto Ativo começou e a missão tem Spawn Point, receita e Spawn Seguro configurados. | A Fuga Final começa enquanto uma receita ainda está pendente. | Receita futura é cancelada. | Verificar que grupo não surge depois da transição | GDD §§Spawn Points e receitas (811–825), Encerramento (969–986). |

**Fronteiras e relações.** OBJ-05 fornece relógios; OBJ-07/08 começam após criação. Não define IA depois da entrada.

**Escopo.** Slice: um Spawn Point e Spawn Seguro bastam para a fixture. Demo: conteúdo do Armazém. Repertório inteiro do GDD não é requisito automático.

**Lacunas.** Valores e composição das receitas são conteúdo por missão/balanceamento, não falta de regra.

## OBJ-07 — Manter conhecimento limitado e válido dos larápios

**Família:** Operação dos larápios. **Perspectiva:** gameplay/NPC.

**Fontes:** [GDD](../PEGA%20GDD/GDD.md) — “Objetivo principal e valores secundários”, “Áreas, POIs e investigação”, “Percepção determinística”, “Armadilhas: detecção e desarme”, “Conhecimento modular, memória e validade da informação”, “Perda de alvo” e “Conhecimento da gangue e informação global”; [MVP](../PEGA%20GDD/MVP.md), seção A; [ADR de pré-produção](../PEGA%20GDD/ADR-PREPRODUCAO.md), MC-02; [ADR-GOAP-02](../PEGA%20GDD/ADRs/ADR-GOAP-02-modelo-causal-e-planejamento.md) e [ADR-GOAP-04](../PEGA%20GDD/ADRs/ADR-GOAP-04-interacoes-informativas.md).

**Propósito e resultado observável.** Larápios agem com fatos obtidos por fontes permitidas, com validade e escopo definidos. Presença percebida não força reação; dado desconhecido não equivale a ausência confirmada; estado oculto não é consultado pelo planner.

**Condições iniciais.** A fixture declara quais fatos são conhecidos, sua origem, escopo e validade; habilidades e capacidades do larápio estão configuradas.

**Comportamento e estados.**

- Percepção usa áreas radiais e cone de visão determinísticos; obstáculos bloqueiam visão.
- A presença radial pode habilitar investigação sem conceder identidade ou posição do alvo; a Visão acrescenta confirmação e fatos que permitem ao GOAP reconsiderar alternativas.
- Visão pode adquirir fatos compatíveis durante deslocamento. Investigação ativa de Área tem duração e compartilha fatos compatíveis conforme política.
- Fatos são Individuais, de Gangue ou Globais. Conhecimento distingue desconhecido de ausência confirmada, mantém fonte/tempo/revisão e segue validade declarada.
- Ao ser criado no Spawn Seguro, um larápio passa a pertencer à gangue e recebe o Contexto global atual e o conhecimento compartilhado que sua própria gangue já possui; isso ocorre na criação, antes de cruzar o Spawn Point. Gangues distintas não compartilham tudo por padrão.
- Ao perder um alvo, o larápio tenta readquiri-lo na última posição conhecida; após intervalo/reavaliação sem sucesso volta ao planejamento normal, sem extrapolar trajetória.
- Detectar e desarmar armadilha são capacidades/Actions diferentes; nenhum conhecimento é concedido por simples presença do objeto.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Fato oculto sem fonte de aquisição | Fatos, fontes, escopo, validade e capacidades do NPC estão definidos no cenário. | O planner consulta um fato sem fonte de aquisição. | Fato permanece desconhecido e não aparece como ausência conhecida; plano não usa o valor oculto. Falha se planner usar estado oculto ou tratar desconhecido como ausência. | Comparar fato real e conhecimento do NPC | GDD: “Conhecimento modular, memória e validade da informação”; ADR-GOAP-02. |
| Investigação ativa termina | Fatos, fontes, escopo, validade e capacidades do NPC estão definidos no cenário. | Uma investigação ativa termina. | Apenas fatos detectáveis e compartilháveis pela Área/capacidade são atualizados e disseminados ao escopo correto. | Conferir fato, origem, escopo e estado anterior/posterior | GDD: “Percepção determinística”, “Áreas, POIs e investigação” e “Conhecimento modular”; ADR-GOAP-04. |
| Alvo sai da visão | Fatos, fontes, escopo, validade e capacidades do NPC estão definidos no cenário. | O alvo sai do campo de visão do larápio. | NPC tenta a última posição conhecida e volta ao planejamento normal após a condição temporal configurada. Falha se extrapolar localização ou manter alvo atual sem fonte. | Observar sequência de comportamento e fato de memória | GDD: “Perda de alvo” e “Conhecimento modular, memória e validade da informação”; ADR-GOAP-02. |
| Larápio é criado no Spawn Seguro após mudanças de Contexto e compartilhamento da gangue | Fatos, fontes, escopo, validade e capacidades do NPC estão definidos no cenário. | Um novo larápio é criado no Spawn Seguro. | Na criação, recebe o Contexto global atual e o conhecimento compartilhado que sua política de escopo lhe disponibiliza, antes de entrar fisicamente no cenário. Falha se a atualização ocorrer apenas depois da entrada ou se o larápio receber fato individual/de gangue sem propagação autorizada. Fatos globais distribuídos explicitamente à gangue podem ser recebidos. | Comparar Contexto e fatos autorizados imediatamente antes/depois da criação; verificar o escopo e a política de propagação de cada fato. | GDD: “Contextos e satisfação dos Desejos”, “Spawn Seguro” e “Conhecimento da gangue e informação global”. |
| Armadilha é percebida | Fatos, fontes, escopo, validade e capacidades do NPC estão definidos no cenário. | O larápio percebe uma armadilha. | Percepção comum não equivale a saber desarmar; detectar/desarmar requerem capacidades distintas quando usados. Falha se capacidade for presumida. | Observar conhecimento e ação permitida | GDD: “Armadilhas: detecção e desarme” e “Capacidades de interação”. |

**Condições finais/invariantes.** Todo fato usado para planejar tem origem e escopo compatíveis; nenhum caminho concede onisciência.

**Fronteiras e relações.** OBJ-08 consome conhecimento para decidir e agir; ações de OBJ-08/10 podem produzir eventos que atualizam OBJ-07. Esse retorno é ciclo de gameplay, não definição circular. Não descreve uma arquitetura de blackboard ou memória.

**Escopo.** Slice exige percepção, conhecimento parcial e replanejamento por eventos; Demo amplia a gangue/arquétipos. Todo o catálogo de habilidades não é obrigatório.

**Lacunas.** GAP-01: “item inicialmente desconhecido” do ADR MC-02 não identifica com precisão se desconhecido é localização/identidade/existência. Nenhum conflito GDD/ADR está confirmado sem essa distinção.

## OBJ-08 — Produzir operação autônoma dos larápios por GOAP

**Família:** Operação dos larápios. **Perspectiva:** gameplay/NPC.

**Fontes:** [GDD](../PEGA%20GDD/GDD.md) — “Planejamento inimigo por GOAP”, “Peso, oportunidade e custo dos planos”, “Contrato de Actions”, “Contextos e satisfação dos Desejos”, “Replanejamento, tempo e execução”, “Desejos e prioridades por arquétipo”, “Posse, desejo e inversão da perseguição” e “Fuga e escolha de saída”; [MVP](../PEGA%20GDD/MVP.md), seções A–B; [ADR de pré-produção](../PEGA%20GDD/ADR-PREPRODUCAO.md), MC-02/MC-04; [ADR-GOAP-01](../PEGA%20GDD/ADRs/ADR-GOAP-01-composicao-e-fronteiras.md), [ADR-GOAP-02](../PEGA%20GDD/ADRs/ADR-GOAP-02-modelo-causal-e-planejamento.md), [ADR-GOAP-03](../PEGA%20GDD/ADRs/ADR-GOAP-03-selecao-e-estabilidade.md) e [ADR-GOAP-04](../PEGA%20GDD/ADRs/ADR-GOAP-04-interacoes-informativas.md).

**Propósito e resultado observável.** Larápios perseguem objetivos declarados por sua configuração e escolhem planos a partir de conhecimento e Actions possíveis; o assalto pode avançar sem controle direto do jogador. Somente larápios usam GOAP; Rick/Petra não são IA/GOAP.

**Condições iniciais.** Há gangue, arquétipo, Contexto, Desejos, capacidades, Actions e fatos conhecidos configurados. A fixture de aceite define alternativas e valores suficientes para um resultado determinístico quando exige uma escolha específica.

**Comportamento e estados.**

- Contexto global progride de Normal para Item Principal em Jogo e depois Fuga Final. Descobrir o objetivo principal altera o contexto; perder/trocar a posse não reverte descoberta global.
- Arquétipos configuram repertórios e capacidades; não criam física/mecânica invisível à regra geral.
- Goals aplicáveis e seus planos candidatos são comparados conjuntamente. Peso, custo temporal, oportunidade, qualidade estimada e valor informacional podem participar; não há fórmula universal fixada.
- Goals expressam condições e Actions descrevem Preconditions/Effects independentemente delas; cadeias podem usar Effects projetados sem tratar estado hipotético como fato observado. Condições desconhecidas não são presumidas verdadeiras.
- Personalidade influencia preferências, inclusive tendência investigativa; competência decisória influencia busca, estimativa, aceitação e estabilidade; capacidades determinam o que o ator consegue executar.
- Intenção, plano e Action em execução têm continuidade própria. Reavaliar pode manter uma intenção com outro plano, manter um plano ainda válido ou preservar o progresso da Action válida e selecionada.
- Desejos de Contingência são avaliados individualmente apenas quando nenhum Desejo normal tem plano válido; deixam de ser elegíveis quando qualquer Desejo normal volta a ter plano. Se nenhum plano normal nem contingente estiver disponível, Idle é apenas proteção técnica, não intenção de gameplay.
- Targets usados são conhecidos e Actions requerem capacidades e Preconditions de sucesso compatíveis; tentativas informativas só ocorrem quando a mecânica as autoriza.
- Satisfação acumulada é individual por Desejo, persiste entre mudanças de Contexto durante o assalto e aumenta somente quando uma Action concluída satisfaz aquele Desejo; tentativas, interrupções e Actions incompletas não contam. Configuração determina ganho, pesos e limites de execuções concluídas (uma, limitadas ou ilimitadas); perder depois o estado não reduz a satisfação acumulada.
- Desempates devem ser determinísticos; não se exige um alvo/Action exato se configuração não produzir vencedor único.
- Action preserva progresso enquanto continuar válida e selecionada. Interrupção, invalidação ou abandono cancela progresso; Effects finais só são aplicados ao concluir com sucesso.
- Mudança relevante dispara reavaliação; não há polling obrigatório. Tempo restante, sozinho, não invalida plano automaticamente.
- Reservas protegem recursos de operações incompatíveis: são adquiridas no início da Action, normalmente no escopo da gangue, têm dono de ator/instância de Action e são liberadas quando a Action conclui, cancela, fica inválida ou é interrompida. Mudança no World State ainda pode invalidar a precondição de uma Action concorrente.
- Larápio pode roubar e tentar escapar antes da Fuga Final se seus Desejos/configuração levarem a isso. Tentativa não equivale a Escape terminal.
- Se Desejo de Fuga não tem plano válido, ele deixa de participar; investigar não passa a acontecer automaticamente. Oportunidades de investigação competem conforme valor potencial e tendência do larápio, sem presumir o resultado.
- A Demo usa os quatro arquétipos dos Trapalhões; o slice não requer catálogo completo.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Uma alternativa conhecida/configurada vence | Gangue, Contexto, Desejos, capacidades e Actions estão configurados; quando uma escolha específica é verificada, a fixture é determinística. | O larápio compara alternativas conhecidas e configuradas. | O larápio seleciona plano compatível segundo prioridade, custo e demais fatores configurados e executa Actions possíveis. Falha se usar Target oculto, ignorar capacidade ou escolher alternativa incompatível. | Registrar candidatos/resultado observável | GDD: “Planejamento inimigo por GOAP”, “Peso, oportunidade e custo dos planos” e “Conhecimento modular”; ADR-GOAP-02/03; MVP §A. |
| Action conclui com sucesso | Gangue, Contexto, Desejos, capacidades e Actions estão configurados; quando uma escolha específica é verificada, a fixture é determinística. | A Action termina com sucesso. | Effects declarados pela Action aplicam-se quando ela termina com sucesso. Falha se os Effects declarados não forem aplicados ou forem aplicados antes da conclusão. | Observar estado antes/depois e conclusão | GDD: “Contrato de Actions”; ADR-GOAP-02/04. |
| Action perde validade ou é interrompida | Gangue, Contexto, Desejos, capacidades e Actions estão configurados; quando uma escolha específica é verificada, a fixture é determinística. | A Action perde validade ou é interrompida antes de concluir. | Progresso da Action cancelada é perdido; Effects de sucesso não são aplicados. Falha se efeito terminal ocorrer incompleta. | Observar estado e tentativa seguinte | GDD: “Contrato de Actions” e “Replanejamento, tempo e execução”; ADR-GOAP-02/03. |
| Fato relevante muda | Gangue, Contexto, Desejos, capacidades e Actions estão configurados; quando uma escolha específica é verificada, a fixture é determinística. | Um fato relevante para o plano muda. | Planos afetados são reavaliados; uma intenção ou plano ainda válido pode continuar, e a Action preserva progresso se continuar válida e selecionada. Falha se manter decisão inválida ou perder progresso válido sem causa. | Observar mudança, validade e comportamento | GDD: “Replanejamento, tempo e execução” e “Contrato de Actions”; ADR-GOAP-03. |
| Não há plano de fuga elegível | Gangue, Contexto, Desejos, capacidades e Actions estão configurados; quando uma escolha específica é verificada, a fixture é determinística. | Nenhum plano válido atende ao Desejo de fuga. | Desejo de fuga não é executado; outro Desejo só roda se ele próprio for elegível e escolhido. Falha se investigação for acionada por fallback não definido. | Observar plano escolhido | GDD: “Contextos e satisfação dos Desejos” e “Áreas, POIs e investigação”; ADR-GOAP-03. |
| Fuga Final começa | Gangue, Contexto, Desejos, capacidades e Actions estão configurados; quando uma escolha específica é verificada, a fixture é determinística. | O Contexto global entra em Fuga Final. | Contexto global muda para Fuga Final e GOAP continua até o encerramento do relógio. Falha se IA for desligada no início da fase. | Observar contexto/continuidade | GDD: “Contextos e satisfação dos Desejos”, “Fuga e escolha de saída” e “Encerramento”. |
| Nenhum Desejo normal tem plano válido; um Desejo de Contingência tem plano | Gangue, Contexto, Desejos, capacidades e Actions estão configurados; quando uma escolha específica é verificada, a fixture é determinística. | Nenhum Desejo normal tem plano; existe um Desejo de Contingência elegível. | O Desejo de Contingência é elegível somente enquanto nenhum Desejo normal tiver plano válido; quando qualquer Desejo normal voltar a ter plano, a Contingência deixa de ser elegível. Falha se contingência competir com plano normal válido ou permanecer elegível depois dele voltar. | Observar elegibilidade e seleção antes/depois da mudança | GDD: “Contextos e satisfação dos Desejos”; ADR-GOAP-03. |
| Action concluída satisfaz um Desejo; outra tentativa não conclui | Gangue, Contexto, Desejos, capacidades e Actions estão configurados; quando uma escolha específica é verificada, a fixture é determinística. | Uma Action concluída satisfaz um Desejo; outra tentativa é interrompida. | Só uma conclusão observada que satisfaça o Goal aumenta a satisfação correspondente; o ganho acumulado permanece entre Contextos e respeita ganho/peso/limite configurados. Falha se uma tentativa recusada ou incompleta contar ou satisfação acumulada for removida pela mudança de Contexto. | Comparar configuração, estado e contagem antes/depois das duas Actions e da mudança de Contexto | GDD: “Contextos e satisfação dos Desejos” e “Contrato de Actions”; ADR-GOAP-03/04. |
| Dois larápios tentam operações incompatíveis sobre recurso reservado | Gangue, Contexto, Desejos, capacidades e Actions estão configurados; quando uma escolha específica é verificada, a fixture é determinística. | Dois larápios iniciam operações incompatíveis sobre um recurso reservado. | Apenas a Action que adquiriu a reserva mantém a exclusividade; a reserva é liberada ao concluir, cancelar, invalidar ou interromper. Falha se operações incompatíveis forem simultaneamente autorizadas ou reserva órfã bloquear após liberação. | Observar aquisição, dono, concorrente e liberação em cada término | GDD: “Reservas como dados de coordenação” e “Contrato de Actions”. |
| Microciclo: informação legítima elimina oportunidade e o larápio replaneja até roubar e tentar fugir | A fixture estabelece uma Action atual válida segundo o conhecimento disponível, com progresso temporizado positivo; há uma oportunidade cuja disponibilidade ainda é desconhecida, uma fonte legítima que revelará que ela deixou de existir e uma única alternativa restante que permite roubo e tentativa de fuga. | A investigação fornece a informação válida de que a oportunidade da Action atual deixou de existir. | A Action atual é invalidada e perde o progresso acumulado; a mudança de conhecimento aciona reavaliação; o larápio segue a alternativa elegível, conclui o roubo e tenta fugir se houver plano válido. Falha se usar estado oculto, preservar Action inválida, não descartar progresso ou não replanejar. | Registrar progresso maior que zero antes da informação e sua perda após a invalidação; comparar a fonte/fato conhecido, precondição, plano escolhido, transferência do item após sucesso e tentativa de fuga. O vencedor único é uma configuração da fixture; o significado do “item inicialmente desconhecido” no MC-02 permanece em GAP-01. | GDD: “Conhecimento modular”, “Contrato de Actions”, “Replanejamento”, “Posse, desejo e inversão da perseguição” e “Fuga e escolha de saída”; MVP §A; ADR-GOAP-02/03/04. |

**Condições finais/invariantes.** Larápios atuam por conhecimento, capacidade, configuração e estado atual. Ações físicas/comportamentais e efeitos declarados concordam.

**Fronteiras e relações.** OBJ-07 fornece fatos; OBJ-06 cria atores; OBJ-09/10 confrontam ou alteram oportunidades; OBJ-11/12 alteram posse; OBJ-14 declara o estado terminal de fuga. Goals/Desejos internos são dados de IA, não IDs deste catálogo.

**Escopo.** Slice: um grupo/arquétipo e GOAP com conhecimento limitado. Demo: uma gangue e quatro arquétipos. Microciclo MC-02/04 cobre parte do objetivo; VS-10–16 completam o slice.

**Lacunas.** GAP-01, GAP-08 e GAP-15. O GDD permite configurar peso e custo, mas não nomeia o vencedor para uma missão concreta; a seleção de um plano específico só pode ser aceita quando a configuração da missão/fixture produzir um vencedor inequívoco. Isso não bloqueia verificar validade, replanejamento e execução do GOAP.

## OBJ-09 — Perseguir, interceptar e confrontar larápios

**Família:** Perseguição, cenário e bens. **Perspectiva:** jogador.

**Fontes:** GDD §§Intenção central (28–43), Verbos (86–98), Controles/Interação (377–432), Câmera (461–476), Propriedades de gameplay (522–539), Posse (691–702); MVP §§A–B (25, 36, 53).

**Propósito e resultado observável.** O jogador localiza e persegue larápios, podendo interceptá-los/confrontá-los. Perseguir é parte central da experiência; capturar é resultado distinto (OBJ-13). A regra de perda e recuperação de contato do Player permanece em GAP-12.

**Condições iniciais.** Player controlável e larápio no cenário; capacidades/resistência do confronto estão configuradas e legíveis antes de exigir o resultado.

**Comportamento e estados.**

- Movimento, orientação, visão do cenário e controles necessários permitem acompanhar alvos.
- A intenção central é perseguir/interceptar. O comportamento exato quando o Player perde contato e tenta reencontrar o larápio não está definido; não se copia a regra de memória de alvo dos NPCs.
- Ataque válido contra larápio produz efeitos segundo capacidade/resistência: se capacidade alcança resistência, incapacita; abaixo, não incapacita e não acumula dano.
- Ataque válido derruba todos os itens carregados, independente de incapacitação (OBJ-11).
- Durante transporte do larápio, movimento continua possível, mas ataque, salto, esquiva e interação comum ficam restritos conforme GDD.
- “Interessante”, “engraçado”, “caótico” e “legível” são qualidades de playtest, não condição funcional presumida.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Player persegue alvo visível ou localizado | Há Player controlável e larápio no cenário; as capacidades relevantes estão configuradas. | O Player usa movimento e leitura disponíveis para perseguir o alvo. | Player consegue se mover até o alvo e responder a mudanças de posição/contato compatíveis com os meios de leitura do recorte. | Observar trajetória, percepção e feedback. Avaliar diversão/legibilidade em playtest separado | GDD §§Intenção central (28–43), Controles/Interação (377–432); MVP §A (23–39). |
| Ataque com capacidade suficiente | Há Player controlável e larápio no cenário; as capacidades relevantes estão configuradas. | O Player acerta um ataque válido com capacidade suficiente. | Larápio é incapacitado sem acumular dano progressivo; itens caem. | Comparar capacidade/resistência, estado e posse | GDD §§Propriedades (522–539), Estados (541–557), Inventário (434–449). |
| Ataque com capacidade insuficiente | Há Player controlável e larápio no cenário; as capacidades relevantes estão configuradas. | O Player acerta um ataque válido com capacidade insuficiente. | Larápio não é incapacitado; não há dano acumulado; itens ainda caem se ataque for válido. Falha se o limiar for ignorado. | Observar estado do NPC e itens | GDD §§Propriedades (522–539), Estados (541–557), Inventário (434–449). |
| Player carrega larápio | Há Player controlável e larápio no cenário; as capacidades relevantes estão configuradas. | O Player transporta um larápio incapacitado e tenta as ações listadas. | Ações listadas como restringidas não são executadas; movimento/soltar/entrega continuam conforme GDD. | Tentar ações e observar resultado | GDD §Transporte de NPC (451–459); GDD §Interação (423–432). |

**Fronteiras e relações.** OBJ-07 define percepção dos larápios, não a interface do Player. OBJ-10 cobre mudanças por objetos. OBJ-11 cobre recuperação/posse; OBJ-13 cobre captura. Não exige esquiva/salto sem alocação do MVP.

**Escopo.** Perseguição, confronto e incapacitação estão no slice e Demo; habilidades individuais de controle podem estar não alocadas.

**Lacunas.** GAP-04 (critérios de ataque válido/timer durante transporte) e GAP-05 (incapacitação do Player por habilidade inimiga). GAP-07: esquiva/salto não têm recorte MVP definido.

## OBJ-10 — Alterar oportunidades por interações do cenário

**Família:** Perseguição, cenário e bens. **Perspectiva:** jogador e gameplay.

**Fontes:** GDD §§Interação (423–432), Cenários (478–514), Propriedades de gameplay (520–557), Armadilhas (625–635), Capacidades (726–738), Interações de cenário (789–809), Sala de controle (841–855); MVP §§A–B (31, 39, 53).

**Propósito e resultado observável.** Interações disponíveis no cenário mudam um estado relevante de acesso, informação ou oportunidade, de acordo com capacidade e regra daquele objeto, e comunicam o estado resultante. Não é uma exigência de implementar todo tipo de objeto do GDD em cada recorte.

**Condições iniciais.** A missão determina os objetos/interações que fazem parte do conteúdo; cada interação exige alvo, condição/capacidade e resultado definidos pela regra aplicável.

**Comportamento e estados.**

- Alvos válidos comunicam estado atual; perceber um objeto não dá automaticamente capacidade de utilizá-lo.
- Interação do Player prioriza alvo válido mais próximo e apresenta indicação; sem alvo válido, o comando funciona como ataque conforme GDD.
- Ação exige condições/capacidade/nível; nível pode alterar duração segundo configuração.
- Exemplos normativos incluem portas, armadilhas, câmeras, cofres, prisões e itens de cenário. Detecção e desarme de armadilha são capacidades distintas.
- A interação pode mudar acesso ou informação, mas não concede conhecimento a NPC sem fonte legítima (OBJ-07).

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Player interage com alvo válido do conteúdo | O cenário contém alvo/interação incluído no recorte, com regra, condição e capacidade definidas. | O Player aciona a interação sobre um alvo válido. | Alvo indicado/priorizado recebe a interação permitida e o estado muda conforme a regra do objeto. Falha se ação ocorre em alvo não prioritário sem regra ou ignora capacidade. | Observar alvo, pré-condições e estado antes/depois | GDD §§Interação (423–432), Interações de cenário (789–809); MVP §§A–B (23–61). |
| Não há alvo de interação válido | O cenário contém alvo/interação incluído no recorte, com regra, condição e capacidade definidas. | O Player aciona interação quando não há alvo válido. | A regra de fallback para ataque é aplicada. | Observar o efeito de ataque definido por OBJ-09 | GDD §Interação (423–432); derivado de OBJ-09 para o efeito de fallback de ataque. |
| Larápio não tem capacidade necessária | O cenário contém alvo/interação incluído no recorte, com regra, condição e capacidade definidas. | O larápio tenta uma ação sem a capacidade necessária. | A ação indisponível não é executada como se fosse válida. | Observar capacidade, ação e estado | GDD §§Capacidades (726–738), Interações de cenário (789–809). |
| Objeto altera informação/acesso | O cenário contém alvo/interação incluído no recorte, com regra, condição e capacidade definidas. | Uma interação de cenário altera informação ou acesso. | Apenas o fato/condição permitido passa a valer para Player/NPCs que tenham fonte de conhecimento. Falha se informação oculta é propagada implicitamente. | Comparar mundo e conhecimento | GDD §§Interações de cenário (789–809), Conhecimento (637–689). |

**Condições finais/invariantes.** Cada interação aceita tem pré-condição, efeito e estado legível definidos por seu próprio conteúdo; este objetivo não cria uma lista universal de soluções.

**Fronteiras e relações.** Ataque é OBJ-09; itens e armazenamento são OBJ-11/12; entrega na prisão é OBJ-13; invasão é OBJ-06. OBJ-10 cobre outras mudanças táticas do ambiente.

**Escopo.** Slice: interações necessárias ao ciclo, lista não enumerada. Demo: Armazém completo com objetos/condições legíveis, mas lista individual também não fechada.

**Lacunas.** GAP-06: conteúdo de interações por entrega e critérios de aceite por objeto.

## OBJ-11 — Recuperar bens e alterar sua posse

**Família:** Perseguição, cenário e bens. **Perspectiva:** jogador e gameplay.

**Fontes:** GDD §§Interação (423–432), Inventário (434–449), Posse (691–702), Estado final (909–913); MVP §A (35–39); ADR §§3.1–3.2 (71–97).

**Propósito e resultado observável.** Bens transitam entre cenário, Players e larápios sem destruição ou duplicação; o Player pode recuperar item solto e reconhecê-lo como item sob sua posse. Recuperar não significa depositar nem protegê-lo.

**Condições iniciais.** Itens têm identidade/estado de posse definidos; os atores têm capacidade configurada; o alvo está em alcance/condição para interação.

**Comportamento e estados.**

- Cada item ocupa uma unidade de capacidade; item não é dividido.
- Player pode pegar item elegível do chão ou armazenamento; limites de capacidade são aplicados.
- Soltar voluntariamente libera todos os itens carregados.
- Ataque válido derruba todos os itens carregados mesmo quando não incapacita o portador.
- Roubo concluído transfere posse ao larápio; ação interrompida não aplica o efeito de sucesso.
- Item nunca é destruído. Limite de capacidade e feedback quando indisponível devem ser coerentes; feedback de negativo não detalhado nas fontes.
- Posse final fora de armazenamento é resolvida em OBJ-14/15.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Player recolhe item válido no chão | O item e o portador estão no cenário, com elegibilidade e capacidades de inventário definidas. | O Player recolhe um item elegível do chão. | Posse muda para Player e capacidade ocupa uma unidade. Falha se houver cópia ou destruição. | Comparar dono/localização e capacidade | GDD §Inventário (434–449). |
| Player não tem espaço | O item e o portador estão no cenário, com elegibilidade e capacidades de inventário definidas. | O Player tenta recolher um item sem espaço disponível. | Item não é transferido para Player; a posse/localização anterior se mantém. | Observar estado de posse. Feedback de UX para capacidade insuficiente não foi especificado pelas fontes | GDD §Inventário (434–449). |
| Portador recebe ataque válido | O item e o portador estão no cenário, com elegibilidade e capacidades de inventário definidas. | O portador recebe um ataque válido. | Todos os itens carregados caem, ainda que capacidade de ataque seja inferior à resistência. | Comparar itens antes/depois e estado de incapacitação | GDD §§Inventário (434–449), Propriedades/Estados do Player (520–557). |
| Portador solta voluntariamente | O item e o portador estão no cenário, com elegibilidade e capacidades de inventário definidas. | O portador solta voluntariamente os itens. | Todos os itens carregados tornam-se recuperáveis no cenário; capacidade é liberada. | Observar posse/localização e capacidade | GDD §Inventário (434–449). |
| Item roubado por larápio | O item e o portador estão no cenário, com elegibilidade e capacidades de inventário definidas. | A Action de roubo termina ou é interrompida. | Posse muda apenas quando a solução de roubo termina com sucesso; item continua único. Falha se tentativa incompleta alterar posse ou item duplicar. | Observar conclusão e posse | GDD §§Contrato de Actions (236–250), Posse/roubo (691–702). |

**Condições finais/invariantes.** Todo item continua existindo e tem um estado/portador coerente; recuperação pode deixar o item ainda exposto.

**Fronteiras e relações.** OBJ-09 causa queda por ataque; OBJ-12 guarda itens e estabelece proteção; OBJ-14 classifica item ao fim; OBJ-15 calcula critérios. OBJ-11 não inclui cofre/armário como condição de proteção.

**Escopo.** Microciclo: posse, roubo, queda e recuperação. Slice: inventário, depósito em cofre e fluxo completo. Demo: scoring/proteção da missão.

**Lacunas.** Critério completo de ataque válido em GAP-04. Feedback de inventário cheio não tem conteúdo detalhado no GDD; ele pode ser especificado ao detalhar UX, mas não é inventado como regra de posse neste contrato.

## OBJ-12 — Proteger bens elegíveis em armazenamento

**Família:** Perseguição, cenário e bens. **Perspectiva:** jogador e gameplay.

**Fontes:** GDD §§Inventário (434–449), Interações de cenário (789–809), Classificação/Estado final de itens (895–913); MVP §§A–B (37–39, 59); ADR §3.2 VS-13 (85–97).

**Propósito e resultado observável.** O jogador transfere bens elegíveis para armazenamento compatível; ao final, depósito válido pode gerar estado/bônus de proteção configurado. Esse objetivo é independente de recolher o item do chão.

**Condições iniciais.** Há item elegível, armazenamento acessível e capacidades definidas para item/ator/armazenamento. O MVP do slice requer ao menos cofres válidos; outros armários podem ficar fora do primeiro corte.

**Comportamento e estados.**

- Depósito transfere itens elegíveis; não duplica nem destrói.
- Se houver capacidade insuficiente, transferência é parcial e determinística. A ordem inicial é estável pela ordem de inventário, mas o GDD permite revisão em playtest.
- Cofres e armários possuem funções distintas. Armário não concede bônus de proteção por padrão.
- Proteção/bônus depende do estado final do item e é calculado uma vez; transferências sucessivas não acumulam bônus; item removido/roubado não recebe bônus.
- A escolha do cofre do objetivo principal em OBJ-04 não deve ser confundida com depósito de itens comuns.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Item elegível é depositado em cofre compatível | Há item elegível, armazenamento compatível e capacidades configuradas. | Um item elegível é depositado em um cofre compatível. | Item sai do inventário/cenário e passa a pertencer ao armazenamento. Falha se houver cópia/perda. | Observar estado e capacidade antes/depois | GDD §§Inventário (434–449), Interações de cenário (789–809). |
| Capacidade do armazenamento não comporta tudo | Há item elegível, armazenamento compatível e capacidades configuradas. | O armazenamento não tem capacidade para todos os itens elegíveis. | Itens transferidos parcialmente em ordem determinística; restantes continuam com seu estado anterior. | Repetir cenário e comparar seleção/ordem | GDD §Inventário (434–449). |
| Item permanece em armazenamento protegido no fim | Há item elegível, armazenamento compatível e capacidades configuradas. | Um item permanece em armazenamento protegido até o fim da missão. | Critério/bônus configurado é aplicado uma única vez conforme estado final. | Comparar classificação com estado final | GDD §§Classificação (895–907), Estado final dos itens (909–913). |
| Item é retirado ou roubado depois | Há item elegível, armazenamento compatível e capacidades configuradas. | Um item é removido ou roubado depois de armazenado. | Bônus aplicável não é mantido como se ainda estivesse protegido. | Observar remoção/roubo e cálculo final | GDD §§Classificação (895–907), Estado final dos itens (909–913). |
| Item é guardado em armário | Há item elegível, armazenamento compatível e capacidades configuradas. | Um item é colocado em armário. | Não recebe automaticamente bônus próprio de cofre. | Comparar tipo de armazenamento e classificação | GDD §§Interações de cenário (789–809), Classificação (895–907). |

**Condições finais/invariantes.** Cada item tem um só portador/localização; resultado de proteção depende do estado final e não da quantidade de depósitos.

**Fronteiras e relações.** OBJ-11 recupera/transporta bens; OBJ-04 protege o objetivo principal escolhendo cofre; OBJ-14 determina estados finais; OBJ-15 consome critérios configurados. Não define armazenamento técnico.

**Escopo.** O microciclo inicial adia armazenamento completo; slice inclui depósito em cofre, parcial e capacidade insuficiente; Demo inclui classificação configurada.

**Lacunas.** A ordem parcial está sujeita a playtest; conteúdo de armazenamentos específico por cenário entra no GAP-06.

## OBJ-13 — Concluir captura por entrega à prisão

**Família:** Perseguição, cenário e bens. **Perspectiva:** jogador e gameplay.

**Fontes:** GDD §§Propriedades (522–539), Estados (541–557), Inventário/Transporte (451–459), Interação (423–432), Encerramento (969–986); MVP §A (36); ADR §§3.1–3.2, VS-12 (71–97).

**Propósito e resultado observável.** Um larápio só termina como Capturado quando incapacitado, transportado e entregue efetivamente à prisão. Incapacitação temporária ou carregar o larápio não basta.

**Condições iniciais.** Alvo é larápio; a regra de ataque configurada permite incapacitação; existe prisão válida na missão; Player está apto a transportar.

**Comportamento e estados.**

- Capacidade de ataque igual/superior à resistência incapacita sem dano acumulado; abaixo disso, não incapacita.
- Larápio incapacitado inicia recuperação temporizada e pode ser carregado.
- Transportar restringe o Player: não pode atacar, saltar, esquivar ou usar interação comum; pode mover, soltar ou concluir entrega conforme GDD.
- Entrega efetiva na prisão produz captura. Se o larápio voltar a atuar antes de entrega, não está capturado.
- Itens carregados são independentes do transporte de NPC; as regras de bloqueio de manipulação durante transporte seguem GDD.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Ataque incapacitante válido | Há larápio, regra de ataque configurada, prisão válida e Player apto a transportar. | Um ataque válido alcança o limiar de incapacitação. | Larápio entra em estado Incapacitado e inicia condição temporizada, sem captura ainda. Falha se marcar Capturado antes de entrega. | Observar estado e timer | GDD §§Propriedades (522–539), Estados (541–557). |
| Player carrega o incapacitado | Há larápio, regra de ataque configurada, prisão válida e Player apto a transportar. | O Player carrega um NPC incapacitado. | Restrições de transporte são aplicadas; NPC não conta como Capturado. | Testar verbos permitidos/restritos e estado do NPC | GDD §Transporte de NPC (451–459). |
| Player entrega na prisão | Há larápio, regra de ataque configurada, prisão válida e Player apto a transportar. | O Player completa a entrega do NPC na prisão. | Estado passa a Capturado e conta para resultado. Falha se simples proximidade ou carregamento contar como entrega sem regra. | Observar estado terminal/contagem | GDD §§Estados (541–557), Encerramento (969–986); MVP §A (23–39). |
| NPC se recupera antes de entrega | Há larápio, regra de ataque configurada, prisão válida e Player apto a transportar. | O NPC termina a recuperação antes da entrega. | Continua não capturado e pode voltar à operação conforme regra configurada. | Observar transição. Continuidade do timer durante transporte é GAP-04 | GDD §§Estados (541–557), Transporte de NPC (451–459). |

**Condições finais/invariantes.** Só entrega válida produz estado Capturado; captura não equivale a incapacitação.

**Fronteiras e relações.** OBJ-09 inclui perseguição/ataque; OBJ-11 recebe itens derrubados; OBJ-14/15 resolvem estados e pontuação. Não inclui captura por eliminação — o GDD não define eliminação acumulativa.

**Escopo.** Slice completo e Demo. Microciclo inicial testa confronto/queda/recuperação antes da entrega à prisão.

**Lacunas.** GAP-04: acerto, timer durante transporte e evento de entrega.

## OBJ-14 — Resolver a Fuga Final e estados terminais

**Família:** Encerramento e continuidade. **Perspectiva:** gameplay.

**Fontes:** GDD §§Contextos (252–278), Posse (691–702), Fuga/saídas (704–724), Spawn Points (811–825), Estado final de itens (909–913), Encerramento (969–986); MVP §§A, D (23–39, 82); ADR §3.2 VS-14–VS-16 (85–97).

**Propósito e resultado observável.** O fim do Tempo de Fuga fecha o gameplay e atribui estado final coerente a cada larápio e item, conforme os eventos e posições válidas da missão.

**Condições iniciais.** Fuga Final está ativa; posições, posse, capturas, saídas válidas e tempos estão atualizados.

**Comportamento e estados.**

- Larápios podem escapar antes da Fuga Final se sua operação e configuração levarem à saída.
- Na Fuga Final, GOAP continua; larápios usam saídas conhecidas/descobertas conforme OBJ-07/08.
- Ao expirar o Tempo de Fuga, gameplay encerra.
- Larápio entregue à prisão é Capturado; quem atravessou saída válida antes do prazo é Escapou; demais presentes são Não escapou.
- Item que cruzou saída com larápio que escapou é Roubado; item que permaneceu no cenário é Recuperado. Item não é destruído.
- Receita de Spawn Point pendente é cancelada no início da Fuga Final.
- Resultado não é necessariamente vitória/derrota binária.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Larápio cruza saída válida antes do encerramento | A Fuga Final está ativa e posição, posse e capturas refletem os eventos ocorridos. | O larápio atravessa uma saída válida antes do fim da missão. | Estado terminal Escapou. Falha se tentativa for contada sem cruzamento. | Observar posição/saída e classificação | GDD §§Fuga/saídas (704–724), Encerramento (969–986). |
| Timer termina com larápio já entregue | A Fuga Final está ativa e posição, posse e capturas refletem os eventos ocorridos. | O relógio termina depois de o larápio ser entregue à prisão. | Estado Capturado preservado. | Comparar entrega e estado final | GDD §§Estados (541–557), Encerramento (969–986). |
| Timer termina com larápio no cenário e não capturado | A Fuga Final está ativa e posição, posse e capturas refletem os eventos ocorridos. | O relógio termina enquanto o larápio está no cenário e não foi capturado. | Estado Não escapou. | Observar estado final | GDD §Encerramento (969–986). |
| Item sai com fugitivo | A Fuga Final está ativa e posição, posse e capturas refletem os eventos ocorridos. | Um item atravessa a saída com um larápio que escapa. | Item Roubado. | Comparar posse/saída | GDD §§Posse (691–702), Estado final dos itens (909–913). |
| Item permanece no cenário no fim | A Fuga Final está ativa e posição, posse e capturas refletem os eventos ocorridos. | Um item continua no cenário quando o relógio termina. | Item Recuperado, esteja ou não em cofre; proteção/bônus é outra dimensão. | Comparar localização e classificação | GDD §Estado final dos itens (909–913). |
| Fuga Final inicia com receita pendente | O Assalto Ativo está em curso e há receita de spawn ainda pendente. | A missão entra em Fuga Final. | A receita pendente é cancelada e não cria novos larápios após a transição. | Observar cancelamento da receita e ausência de spawn tardio. | GDD §§Spawn Points e receitas (811–825), Encerramento (969–986). |

**Condições finais/invariantes.** Cada larápio e item recebe um estado final compatível com eventos e localização; as ações de gameplay cessam quando o Tempo de Fuga termina.

**Fronteiras e relações.** OBJ-05 governa relógios; OBJ-08 continua a operação; OBJ-11/12 atualizam posse/armazenamento; OBJ-13 produz captura; OBJ-15 calcula resultado. Recuperado não significa protegido.

**Escopo.** Slice completo e Demo. Métrica temporal de scoring não é necessária no primeiro slice; não afeta estado terminal.

**Lacunas.** Regras gerais estão especificadas. Valores de relógio são configuração; GAP-09 cobre seleção concreta de scoring.

## OBJ-15 — Classificar e apresentar o resultado da missão

**Família:** Encerramento e continuidade. **Perspectiva:** gameplay e jogador.

**Fontes:** GDD §§Critérios de classificação (895–907), Estado final dos itens (909–913), HUD/Menus (915–959), Encerramento (969–986); MVP §§A–B, D (38–39, 45–61, 82); ADR §§3.2–3.3, 10 (85–111, 261–286).

**Propósito e resultado observável.** Depois do encerramento, dados consolidados da missão produzem uma classificação legível para o jogador. A classificação usa critérios/pesos configurados para a missão; não há dominância universal obrigatória entre captura, proteção de bens e tempo.

**Condições iniciais.** OBJ-14 consolidou estados terminais; a missão fornece critérios e valores configurados para o recorte.

**Comportamento e estados.**

- Critérios possíveis incluem larápios capturados, bens recuperados/protegidos e eficiência temporal, conforme configuração da missão.
- Proteção/bônus é calculada uma vez do estado final do item; depósitos não empilham bônus; armários não têm bônus de cofre por padrão.
- Slice apresenta resultado/classificação básica; métricas temporais podem ficar de fora.
- Demo exige scoring configurado e telas de resultado/resumo financeiro no fluxo.
- O jogador consegue compreender o resultado e continuar/voltar conforme o fluxo; layout/copy detalhados dependem de especificação visual.
- Resultado é consumido pela carreira na Demo (OBJ-16).

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Slice conclui missão | A missão terminou e seus estados finais e critérios aplicáveis estão consolidados. | O resultado do slice é consolidado. | É apresentado resultado básico baseado em estados consolidados, sem exigir métrica temporal. Falha se contradizer estado de NPC/item. | Conferir dados finais e resumo | GDD §§Critérios de classificação (895–907), Estado final (909–913); MVP §A (23–39). |
| Demo conclui Armazém | A missão terminou e seus estados finais e critérios aplicáveis estão consolidados. | O resultado da missão Armazém da Demo é consolidado. | Critérios e pesos configurados geram resultado do assalto e informação suficiente ao jogador para entender o resultado. | Comparar configuração e apresentação. Valores/configuração não podem ser presumidos pelo catálogo | GDD §Critérios de classificação (895–907); MVP §B (45–61). |
| Item protegido e depois removido/roubado | A missão terminou e seus estados finais e critérios aplicáveis estão consolidados. | Um item protegido é removido ou roubado antes do fim. | Classificação considera apenas estado final aplicável; sem bônus acumulado. | Comparar sequência e classificação | GDD §§Inventário (434–449), Estado final/classificação (895–913). |
| Resultado consumido pela carreira | A missão terminou e seus estados finais e critérios aplicáveis estão consolidados. | O resultado consolidado é aplicado à carreira. | Valor usado por OBJ-16 corresponde à classificação consolidada. | Comparar classificação, créditos e saldo | GDD §§Carreira (961–967), Encerramento (969–986); MVP §B (45–61); derivado da entrega de OBJ-15 a OBJ-16. |

**Condições finais/invariantes.** Não recalcula/duplica bônus por transferência; valores exibidos concordam com estado final e critérios configurados.

**Fronteiras e relações.** OBJ-14 produz estados finais; OBJ-16 processa economia/persistência. Não inventa UI como objetivos separados nem define fórmula de pontuação.

**Escopo.** Slice: resultado básico, sem exigir score temporal. Demo: scoring configurado, resumo financeiro e fluxo de retorno. Configuração da missão deve estar definida para aceite.

**Lacunas.** GAP-09: critérios efetivos, valores e apresentação configurados para a Demo. A regra é identificável; o resultado específico da missão não pode ser aceito antes dessa configuração.

## OBJ-16 — Continuar a carreira e persistir progresso

**Família:** Encerramento e continuidade. **Perspectiva:** jogador/progressão.

**Fontes:** GDD §§Modos/Campanha/Créditos (869–893), Carreira e persistência (961–967), Menus (943–959); MVP §§B–C (45–77); ADR §3.3 DEM-06–DEM-09 (99–111).

**Propósito e resultado observável.** Na Demo, o resultado econômico do assalto atualiza a carreira e persiste entre missões; o jogador pode continuar ou repetir conforme fluxo. Bancarrota requer saldo abaixo do limite configurado; uma missão ruim isolada não encerra a carreira.

**Condições iniciais.** Uma classificação foi produzida (OBJ-15), custo operacional e limite da carreira estão configurados, e carreira está inicializada.

**Comportamento e estados.**

- Créditos líquidos são recompensa da classificação menos custo operacional.
- Resultado/saldo persiste entre missões; não existe salvar ou retomar assalto em andamento.
- Saldo abaixo do limite de bancarrota termina a carreira e permite reinício explícito com saldo inicial zerado.
- Um resultado ruim sem cruzar o limite não encerra a carreira.
- GDD indica custo inicial de 150 e limite de -1000 como valores provisórios de playtest; estes não são valores imutáveis de produto.
- A Demo permite repetir missão e retornar pelo fluxo de carreira. Expansão de campanha, contratos/unlocks e Endless são posteriores à Demo.

**Critérios de aceite.**

| Cenário | Given | When | Then / pass boundary | Evidence | Sources |
|---|---|---|---|---|---|
| Resultado gera créditos e custo | A carreira está inicializada e custo/limite aplicáveis estão configurados. | A recompensa e o custo operacional são aplicados ao saldo. | Saldo novo corresponde ao saldo anterior + recompensa configurada − custo operacional configurado. Falha se não corresponder às entradas. | Conferir cálculo apresentado e persistido | GDD §§Créditos/carreira (869–893), Carreira (961–967); MVP §B (45–61). |
| Saldo continua acima do limite | A carreira está inicializada e custo/limite aplicáveis estão configurados. | A carreira recebe um resultado ruim, mas o saldo permanece acima do limite. | Carreira permanece ativa mesmo após assalto ruim; jogador pode seguir fluxo permitido. | Observar estado e retorno | GDD §Carreira (961–967); MVP §B (45–61). |
| Saldo cruza abaixo do limite | A carreira está inicializada e custo/limite aplicáveis estão configurados. | O saldo cai abaixo do limite de bancarrota. | Tela/estado de bancarrota encerra continuidade atual e oferece reinício explícito. | Observar estado e saldo | GDD §§Carreira (961–967), Menus (943–959); MVP §B (45–61). |
| Carreira é reiniciada | A carreira está inicializada e custo/limite aplicáveis estão configurados. | O jogador confirma o reinício da carreira. | Saldo/carreira volta ao estado inicial definido. | Comparar antes/depois | GDD §Carreira (961–967); MVP §B (45–61). |
| Assalto é interrompido e jogo reaberto | A carreira está inicializada e custo/limite aplicáveis estão configurados. | O jogador fecha/reabre o jogo após interromper um assalto. | Não existe retomada de progresso do assalto em andamento; carreira entre missões continua conforme persistência definida. | Observar ausência de resume do assalto. Backend/semântica de salvamento ficam fora deste contrato | GDD §Carreira e persistência (961–967); MVP §B (45–61). |

**Condições finais/invariantes.** Resultado econômico de uma missão é aplicado uma vez; bancarrota depende do saldo cruzar estritamente o limite; falha de uma missão não encerra carreira automaticamente.

**Fronteiras e relações.** OBJ-15 fornece classificação. OBJ-02/HUB suportam continuar para contratos conforme disponibilidade. Não especifica backend técnico nem campanha pós-Demo.

**Escopo.** Demo: créditos, carreira e persistência entre missões. Pós-Demo: novos contratos/campanha/unlocks/Endless sem detalhe adicional. Slice não exige economia completa.

**Lacunas.** Valores iniciais são provisórios; configuração/valores exatos para aceitar a Demo devem ser fixados (GDD §Carreira). Conteúdo pós-Demo permanece aberto e não é bloqueio do contrato da Demo.

## Prontidão para planejamento posterior

| Eixo | Prontidão | Limite conhecido |
|---|---|---|
| Identificação dos objetivos | Pronto para revisão: os 16 objetivos têm resultado e fronteira documentados, sem conflito efetivo confirmado entre GDD e MVP. | A aprovação da decomposição ainda cabe à revisão do catálogo. |
| Especificação comportamental | Especificada até onde as fontes permitem; objetivos continuam identificáveis mesmo quando um caso fica aberto. | GAP-02, GAP-04, GAP-05 e GAP-12 deixam transições ou consequências sem regra suficiente para os casos afetados. |
| Critérios de aceite | Todos os contratos têm cenários Given/When/Then, evidência e fonte por linha. | Aceite final de conteúdo/interações, briefing/UI, scoring, dificuldade, empates cooperativos, Pause e vencedor GOAP específico ainda depende de GAP-03/06/08/09/10/13/14/15; ambiguidades do microciclo estão em GAP-01. |
| Integração entre objetivos | Relações e handoffs de gameplay estão mapeados, sem dependência circular de definição identificada. | Movimentação física do objetivo principal, efeitos de incapacitação do Player, scoring configurado e semântica cooperativa de Pause precisam das decisões dos GAPs correspondentes antes do aceite integrado. |

Esta prontidão descreve a base documental para revisão e planejamento futuro; não aprova um roadmap, tarefa técnica ou implementação.

## Revisão de consistência dos contratos

| Verificação | Resultado |
|---|---|
| Resultado observável e fronteira por objetivo | Definidos nos 16 contratos; recuperação (OBJ-11) e proteção em armazenamento (OBJ-12) têm estados finais distintos. |
| Regras novas ou decisões presumidas | Não foram adicionadas. Campos sem fonte foram registrados como GAPs. |
| GOAP versus objetivos de produto | OBJ-07/08 descrevem resultado do jogo; Desejos GOAP não foram numerados como objetivos do catálogo. |
| Microciclo, slice e Demo | Microciclo tratado como subconjunto de produção. Preparação, spawn completo, armazenamento, prisão, Fuga Final e classificação permanecem no slice/Demo conforme MVP. |
| Captura versus incapacitação | Separadas: só entrega efetiva em OBJ-13 conclui captura. |
| Encerramento versus classificação | Separados: OBJ-14 resolve estados; OBJ-15 classifica/apresenta. |
| Dependências circulares | Nenhuma dependência circular de definição. OBJ-07/08 formam feedback de gameplay explicitamente descrito como tal. |
| Resultado sem fonte normativa | Não identificado. Questões sem detalhe estão rastreadas em GAPs. |

## Lacunas herdadas do catálogo

O registro consolidado está em [pega-gdd-activity-analysis.md — Lacunas e decisões pendentes](pega-gdd-activity-analysis.md#lacunas-e-decisoes-pendentes). As de maior efeito sobre especificação/aceite são:

- GAP-01: significado de “item inicialmente desconhecido” no ADR MC-02.
- GAP-02: quando a escolha de cofre altera localização/estado físico do objetivo principal.
- GAP-04: critério de ataque válido, timer durante transporte e evento de entrega na prisão.
- GAP-05: efeito de incapacitação do Player por habilidades inimigas.
- GAP-06: objetos/interações incluídos em cada recorte de missão.
- GAP-09: configuração concreta de scoring da Demo.
- GAP-12: regra de perda e recuperação de contato do Player durante perseguição.
- GAP-13: escopo e momento de escolha dos três níveis de dificuldade descritos para cada cenário.
- GAP-14: comportamento de Pause com dois Players locais.
- GAP-15: configuração necessária para aceitar um plano GOAP vencedor específico.

Estas lacunas não foram transformadas em decisões, tarefas ou arquitetura. A conclusão do catálogo não implica aceite de implementação.
