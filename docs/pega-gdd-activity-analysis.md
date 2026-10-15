# PEGA — Catálogo de objetivos derivados do GDD

**Status:** catálogo derivado para análise e especificação; não é roadmap nem altera fontes normativas.
**Data:** 2026-10-15
**Ponto de entrada:** [Especificações comportamentais dos objetivos](pega-objective-specifications.md).
**Fontes normativas:** [GDD](../PEGA%20GDD/GDD.md), [MVP](../PEGA%20GDD/MVP.md) e [ADR de pré-produção](../PEGA%20GDD/ADR-PREPRODUCAO.md). [TASKS](../PEGA%20GDD/TASKS.md) foi consultado apenas para entender a estrutura operacional existente, sem inferir regras de gameplay.

## Propósito e regras de leitura

Este catálogo reconstrói os resultados de jogo a partir das fontes normativas. O GDD define regras e experiência; o MVP determina os recortes de entrega; o ADR organiza decisões de produção sem substituir as duas fontes anteriores. IDs MC, VS e DEM são recortes do ADR, não IDs de objetivos de jogo.

O critério para separar objetivos é a independência do resultado pretendido e de sua condição de conclusão. Timers, estados, inputs, objetos e componentes aparecem como regras ou critérios de aceite, salvo quando as fontes lhes dão resultados independentes. Objetivos de gameplay do PEGA também não são os Goals/Desejos usados pelo GOAP dos larápios.

O GDD resume a intenção central como **“Capturar é a recompensa. Perseguir é a experiência.”** O catálogo representa localização, perseguição, interceptação, confronto e decisões autônomas dos larápios. A perda/recuperação de contato pelo Player é uma lacuna específica: o GDD define memória de alvo para larápios, mas não fixa a regra/feedback equivalente para o Player. A qualidade de uma perseguição ser divertida, legível ou caótica é uma hipótese para playtest, não uma regra funcional inventada para este documento.

## Recortes documentais

| Recorte | Significado usado aqui | Fonte |
|---|---|---|
| Microciclo inicial | Primeiro teste integrado, menor que o vertical slice. Adia fases e resultados completos para cortes posteriores do mesmo slice. | ADR §§3.1–3.2 (71–97) |
| Vertical slice | Primeiro ciclo completo jogável com um Player: Preparação, invasão, perseguição, roubo, recuperação/captura, fuga e resultado. | MVP §A (23–39) |
| Demo | Acrescenta dois Players locais, seleção de Rick/Petra, cooperação, Armazém completo, apresentação, scoring, economia e persistência. | MVP §B (45–61) |
| Posterior à Demo | Expansões declaradas pelo MVP, sem especificação adicional criada aqui. | MVP §C (71–77) |
| Opcional / playtest | Conteúdo ou medida cuja inclusão/valor permanece sujeita a decisão ou validação. | MVP §D (79–84) |

Diferenças entre microciclo, slice e Demo indicam ordem ou ampliação de escopo, não conflito de regras. O slice começa diretamente na atividade Mission, sem menu inicial/HUB; isso não elimina o fluxo da Demo. ADR adendo v2 (165–179).

## Análise preservada por perspectiva

### Ciclo do jogador

| Momento | Resultado/decisão do jogador | Recorte e observação |
|---|---|---|
| Entrar na missão | Escolher modo local/roster permitido e controlar o personagem selecionado. | Slice: um Player, sem seleção formal definida. Demo: dois Players locais e Rick/Petra selecionáveis e mecanicamente equivalentes; GDD também permite modo solo. Fluxo exato, exclusividade de personagens e atribuição entre controles não estão definidos. GDD §§Personagens, Modos e Menus; MVP §§A–B. |
| Escolher contrato | Selecionar uma missão disponível e iniciar esse contrato. | Demo: escritório estático com hotspots e um Armazém. HUB navegável e novos contratos são pós-Demo. MVP §§B–C. |
| Entender o contrato | Reconhecer cenário, entradas, saídas, itens, novidades e gangue antes da ação. | Demo: briefing e apresentações. O conteúdo formal do briefing do slice não é especificado. GDD §Estrutura de uma missão; MVP §§A–B. |
| Preparar | Escolher um cofre válido para proteger o objetivo principal durante a janela fixa de Preparação. | Slice e Demo; na Demo, ambos podem alterar a escolha e a última escolha válida prevalece. GDD §Preparação; MVP §§A–B. |
| Ler e atravessar | Mover-se e observar ameaças/pontos relevantes para perseguir e interceptar. | O comportamento de perda/recuperação de contato pelo Player não está definido. Uso de câmeras, esquiva e salto não estão alocados pelo MVP a um recorte específico. |
| Intervir | Atacar e usar recursos do cenário disponíveis para alterar acesso, informação ou oportunidade. | Interações são descritas no GDD; o MVP não enumera quais objetos ou capacidades específicas precisam estar no slice ou na Demo. |
| Recuperar e proteger | Recuperar itens soltos e depositar os elegíveis em armazenamento para afetar o estado final e a proteção. | Resultados diferentes: um item recuperado pode continuar exposto no cenário; depósito válido produz estado de armazenamento e possível bônus. MVP slice inclui inventário e depósito em cofre. |
| Capturar | Transportar um larápio incapacitado e entregá-lo à prisão. | Incapacitação não é captura. Entrega efetiva é necessária. O microciclo inicial adia prisão para depois, ainda no mesmo slice. |
| Encerrar e avaliar | Acompanhar a Fuga Final, os estados finais de NPCs/itens e a classificação da missão. | Slice: resultado básico; Demo: scoring configurado. Métrica temporal é opcional no primeiro slice. |
| Continuar carreira | Ver créditos/saldo e retornar, repetir ou reiniciar a carreira conforme o resultado. | Economia e persistência entre missões são requisitos da Demo; não existe salvamento/retomada do assalto em andamento. |

### Ciclo de gameplay

| Resultado do mundo | Comportamento observável | Recorte |
|---|---|---|
| A missão progride | Preparação termina pelo relógio; Assalto Ativo e Fuga Final usam relógios separados. Preparação não encerra por comando de pronto. | Slice completo e Demo. |
| A gangue entra | Cada Spawn Point executa receitas independentes a partir do Assalto Ativo; Spawn Seguro e portas afetam a entrada física conforme regras do GDD. | Slice: um Spawn Point e Spawn Seguro; Demo amplia o conteúdo do Armazém. |
| Informação permanece parcial e rastreável | Percepção, investigação e propagação atualizam fatos conforme fonte, capacidade, validade e escopo Individual/Gangue/Global. O planner não consulta estado oculto. | Conhecimento parcial e replanejamento estão no slice; nem todo conteúdo de habilidade está automaticamente incluído. |
| Larápios conduzem o assalto | GOAP seleciona Desejos/Actions compatíveis com conhecimento, capacidades, custos e configuração; mudanças relevantes levam a reavaliação. | Slice: ao menos um arquétipo; Demo: gangue Trapalhões com quatro arquétipos. |
| O cenário muda oportunidades | Interações válidas alteram estados de objetos, acesso ou informação e comunicam os estados relevantes. | GDD descreve portas, câmeras, armadilhas, cofres, prisões e outros objetos; escopo por entrega não está enumerado. |
| Posse e estado de NPCs mudam | Roubo, ataque, soltura, recuperação, armazenamento, incapacitação, transporte e entrega alteram estados de itens e larápios. | Slice completo e Demo, com itens não destruídos e captura só após entrega. |
| O assalto termina | Fuga Final mantém o GOAP ativo; fim do relógio fecha o gameplay e resolve larápios/itens. | Slice completo e Demo. |
| Resultado e carreira avançam | Dados consolidados produzem classificação; na Demo, créditos/custo e persistência atualizam a carreira. | Resultado básico no slice, scoring/economia/persistência na Demo. |

O ciclo de NPCs deste catálogo é o dos larápios, que têm comportamento de gameplay. O GDD trata civis/figurantes, clientes e P.G.A. como presença narrativa/apresentação, não como NPCs autônomos do assalto; nenhum ciclo de gameplay foi inventado para eles (GDD §NPCs, 368–375).

## Hierarquia sintetizada

As famílias organizam resultados próximos para navegação. Não determinam arquitetura, ordem técnica nem número de cenas.

| Família | IDs | Resultado agrupador |
|---|---|---|
| Entrada e compreensão da missão | OBJ-01–OBJ-03 | Jogar com o roster local escolhido, selecionar contrato e entender sua apresentação. |
| Preparação e progressão do assalto | OBJ-04–OBJ-06 | Proteger o objetivo principal, avançar relógios e materializar as receitas de invasão. |
| Operação dos larápios | OBJ-07–OBJ-08 | Manter informação parcial e produzir comportamento autônomo por GOAP. |
| Perseguição, cenário e bens | OBJ-09–OBJ-13 | Perseguir, alterar oportunidades do cenário, recuperar bens, protegê-los em armazenamento e concluir captura. |
| Encerramento e continuidade | OBJ-14–OBJ-16 | Resolver estados finais, classificar/apresentar resultados e manter a carreira. |

### Catálogo de objetivos

| ID | Objetivo | Fronteira do resultado |
|---|---|---|
| OBJ-01 | Participar da missão com o roster local escolhido | O(s) Player(s) selecionado(s) entra(m) controlável(is) na atividade com o personagem correspondente. |
| OBJ-02 | Selecionar e iniciar um contrato disponível | Uma missão disponível é escolhida e iniciada. |
| OBJ-03 | Compreender o contexto do contrato | Apresentação informa cenário e ameaça antes do assalto, conforme o fluxo de Demo. |
| OBJ-04 | Preparar a proteção do objetivo principal | Uma escolha válida de cofre é estabelecida durante Preparação e comunicada aos Players. |
| OBJ-05 | Avançar fases e relógios da missão | Preparação, Assalto Ativo e Fuga Final obedecem a seus relógios distintos. |
| OBJ-06 | Executar receitas temporais de invasão | Spawns independentes são criados e entram no cenário segundo Spawn Points e Spawn Seguro. |
| OBJ-07 | Manter conhecimento limitado e válido dos larápios | Percepção/investigação gera fatos com origem, escopo e validade; desconhecido não vira ausência conhecida. |
| OBJ-08 | Produzir operação autônoma dos larápios por GOAP | Larápios escolhem e executam ações compatíveis com Desejos, conhecimento, capacidades e situação. |
| OBJ-09 | Perseguir, interceptar e confrontar larápios | O Player localiza/segue alvos e obtém os efeitos de ataque definidos; qualidade da perseguição é validada por playtest. |
| OBJ-10 | Alterar oportunidades por interações do cenário | Interações válidas mudam acesso, informação ou condição de objetos, nos recursos incluídos pela missão. |
| OBJ-11 | Recuperar bens e alterar sua posse | Itens roubados, soltos e recuperados mudam de portador sem destruição ou duplicação. |
| OBJ-12 | Proteger bens elegíveis em armazenamento | Depósito transfere itens elegíveis para armazenamento compatível; proteção/bônus é resolvido sem empilhar transferências. |
| OBJ-13 | Concluir captura por entrega à prisão | Um larápio só fica Capturado após incapacitação, transporte e entrega efetiva. |
| OBJ-14 | Resolver a Fuga Final e estados terminais | Fim do relógio fecha o assalto e determina estados finais de larápios e itens. |
| OBJ-15 | Classificar e apresentar o resultado da missão | Critérios configurados convertem dados do assalto em resultado compreensível. |
| OBJ-16 | Continuar a carreira e persistir progresso | Créditos, custo, saldo, bancarrota/reinício e persistência entre missões seguem as regras de carreira. |

## Relações entre objetivos

Estas relações indicam dependência ou troca de resultado no gameplay; não são sequência de implementação.

| Relação | Justificativa |
|---|---|
| OBJ-01 → OBJ-02/OBJ-03 → OBJ-04 | A atividade precisa de roster jogável; a Demo escolhe/apresenta contrato antes da Preparação. O slice entra diretamente na missão e não exige a seleção/apresentação formal da Demo. |
| OBJ-04 → OBJ-05 | A Preparação temporizada termina e inicia o Assalto Ativo; a proteção escolhida é estabelecida nessa fase. |
| OBJ-05 → OBJ-06 | Receitas contam tempos absolutos a partir do Assalto Ativo; Fuga Final cancela receitas ainda pendentes. |
| OBJ-06 → OBJ-07/OBJ-08 | Larápios criados entram no mundo, percebem fatos e iniciam decisões autônomas. |
| OBJ-07 ↔ OBJ-08 | Conhecimento válido orienta GOAP; mudanças e efeitos de Actions podem produzir novos fatos. É um ciclo de gameplay, não dependência circular de definição. |
| OBJ-08 ↔ OBJ-09/OBJ-10 | Ações dos larápios criam alvos e oportunidades; Player e cenário alteram perseguição, acesso e informação. |
| OBJ-09 → OBJ-11/OBJ-13 | Ataques podem soltar itens e incapacitar conforme regra; recuperar o item e capturar o larápio têm condições finais próprias. |
| OBJ-11 → OBJ-12 → OBJ-14/OBJ-15 | Posse e depósito afetam o estado final dos bens e podem afetar proteção/classificação. Permanecer no cenário ao fim não equivale a estar protegido em cofre. |
| OBJ-13 → OBJ-14/OBJ-15 | Entrega à prisão afeta o estado final do larápio e os critérios de resultado. |
| OBJ-05/OBJ-08/OBJ-11/OBJ-12/OBJ-13 → OBJ-14 | Tempo, comportamento, posse, armazenamento e captura alimentam a resolução terminal. |
| OBJ-14 → OBJ-15 → OBJ-16 | Estados terminais sustentam classificação; na Demo o resultado alimenta créditos e carreira. |

## Requisitos transversais

São condições de suporte a múltiplos objetivos, não objetivos independentes apenas por aparecerem no GDD.

| Requisito transversal | Objetivos atendidos | Estado de especificação |
|---|---|---|
| Controles: mover, interagir/atacar, soltar, confirmar e pausar; esquiva/salto | OBJ-01, OBJ-04, OBJ-09–OBJ-13 | Ações de controle estão no GDD; bindings de várias ações e escopo MVP de esquiva/salto não estão alocados. |
| Feedback/HUD: relógios, inventário, ameaças, itens, capturas, estado do Player e minimapa | OBJ-01, OBJ-04–OBJ-15 | GDD indica conteúdo; MVP exige UI suficiente para observar o ciclo. Layout e alocação de cada indicador permanecem a detalhar. |
| Câmera e leitura simultânea | OBJ-01, OBJ-09–OBJ-13 | GDD prevê câmera oblíqua/split horizontal e dá critério verificável: HUD, minimapa e ícones legíveis para ambos, sem conteúdo crítico pequeno demais ou apenas na metade do outro Player. |
| Estado legível de objetos interativos | OBJ-07, OBJ-08, OBJ-10–OBJ-13 | GDD exige comunicar estado relevante; a lista de objetos presente em cada entrega não está fechada. |
| Apresentação audiovisual e humor | OBJ-01–OBJ-03, OBJ-09, OBJ-15 | GDD/MVP exigem apresentação de Demo. Qualidade narrativa/visual precisa de avaliação, não ganha critério funcional arbitrário aqui. |
| Menus, navegação e Pause | OBJ-01–OBJ-03, OBJ-15–OBJ-16 | Fluxo da Demo cobre início/solo-coop, opções/saída, HUB estático, missão/resultados, Pause e bancarrota. Pause oferece continuar, opções e saída confirmada conforme GDD. São partes do fluxo, não um objetivo por tela. Semântica de Pause com dois Players ainda precisa de aceite. |
| Integridade de fatos, posse e resultado | OBJ-07–OBJ-08, OBJ-11–OBJ-15 | Invariantes comportamentais especificados nos contratos; aceitação de implementação ainda será planejada depois desta revisão. |

## Matriz de cobertura GDD/MVP

Esta checagem parte novamente das áreas do GDD/MVP. Uma mesma linha pode ser transversal e coberta por mais de um objetivo.

| Área de cobertura | Objetivo(s)/aceite | Recorte | Estado / lacuna |
|---|---|---|---|
| Identidade e participação de Players | OBJ-01; roster selecionado fica controlável no contrato | Slice: um Player; Demo: dois Players locais, Rick/Petra | Seleção formal no slice, exclusividade/atribuição de personagens não alocadas. |
| Controles e interação do Player | OBJ-01, OBJ-04, OBJ-09–OBJ-13; feedback e controles transversais | Slice/Demo conforme ciclo | Esquiva, salto, mapeamentos e algumas regras de incapacitação do Player não alocados/especificados. |
| Preparação e proteção do objetivo principal | OBJ-04; critérios de cofre válido e cooperação | Slice e Demo | Momento exato da mudança de localização no mundo não especificado. |
| Relógios e progressão do assalto | OBJ-05 | Slice completo e Demo | Valores são configuração/balanceamento, não lacuna de objetivo. |
| Spawn Points, Spawn Seguro e receitas | OBJ-06 | Slice: ao menos um ponto e zona; Demo conforme Armazém | Regras comportamentais no GDD; horários/quantidades são conteúdo por missão. |
| GOAP, conhecimento e arquétipos | OBJ-07–OBJ-08 | Slice: um grupo e ao menos um arquétipo; Demo: quatro arquétipos Trapalhões | Interpretação de “item inicialmente desconhecido” no MC-02 exige esclarecer objeto da informação (GAP-01); plano vencedor específico exige configuração inequívoca (GAP-15). |
| Interações de cenário | OBJ-10; itens, prisão e ataque ficam nos objetivos próprios | Slice: necessárias ao loop; Demo: Armazém completo | MVP não lista cada interação/objeto obrigatório por corte. |
| Posse, inventário e transferência de itens | OBJ-11–OBJ-12 | Microciclo começa com posse/queda/recuperação; slice acrescenta armazenamento completo; Demo pontua proteção | Ordem parcial inicial documentada como estável e revisável por playtest. |
| Recuperação e armazenamento | OBJ-11 e OBJ-12 | Slice e Demo | Objetivos separados: recuperação não implica proteção em armazenamento. |
| Confronto, incapacitação, transporte e captura | OBJ-09 e OBJ-13 | Slice completo/Demo; prisão é posterior ao microciclo | Critério completo de ataque válido e continuidade do timer durante transporte em aberto. |
| Fuga Final e resolução terminal | OBJ-05, OBJ-08, OBJ-14 | Slice completo e Demo | Invariantes definidos; duração configurável. |
| Classificação e resultado | OBJ-14–OBJ-15 | Slice: resultado básico; Demo: scoring configurado | Fórmula/valores e critérios efetivamente selecionados para o Armazém precisam de configuração da missão. |
| Cooperação local | OBJ-01, OBJ-04 e transversal de câmera/HUD | Demo | Regras específicas de escolha cooperativa definidas; divisão geral de tarefas/legibilidade exige playtest. |
| HUB e fluxo de apresentação | OBJ-02–OBJ-03, OBJ-15–OBJ-16 | Demo: escritório estático/hotspots; pós-Demo: HUB navegável | Não se infere quantidade de Unity Scenes a partir de páginas/etapas de experiência. |
| Modo de jogo local | OBJ-01; escolha de solo/cooperativo e roster | GDD permite um ou dois Players; Demo exige coop local para dois e verifica fluxo de um/dois | Sem multiplayer online. A semântica completa de pause compartilhado está em GAP-14. |
| Dificuldade por cenário | Sem objetivo detalhado; mantém rastreabilidade no horizonte | GDD descreve três níveis por cenário; MVP não aloca a seleção/configuração a um recorte | GAP-13: não inferir que dificuldade seja selecionável antes de haver decisão de fluxo. |
| Carreira e persistência | OBJ-16 | Demo | Valores iniciais de custo/limite são provisórios de playtest; sem save/resume de assalto. |
| Feedback/HUD | Requisito transversal aplicado aos objetivos acima | Slice: observar o ciclo; Demo: HUD/minimapa conforme MVP/GDD | Contrato de conteúdo existe, layout e critérios de legibilidade ainda não foram detalhados. |
| Posterior à Demo | horizonte abaixo | Pós-Demo | Identificado e rastreável; sem especificação especulativa. |
| Opcional/playtest | horizonte abaixo | Opcional / conforme playtest | Não antecipado ao primeiro corte. |

## Horizonte além do primeiro slice/Demo

Estes itens permanecem rastreáveis às fontes, sem receber contrato inventado onde o GDD ainda está aberto.

| Item | Classificação | Fonte/limite |
|---|---|---|
| HUB navegável, novos cenários/contratos e campanha expandida | Pós-Demo | MVP §C; conteúdo e ordenação ainda não detalhados. |
| Mais gangues, habilidades e desbloqueios | Pós-Demo | MVP §C; GDD lista possibilidades, não aloca todo o catálogo à Demo. |
| Endless | Pós-Demo | MVP §C e GDD §Modos. |
| Power-ups | Fora do primeiro MVP; inclusão depende de escopo/playtest | MVP §D; GDD §Power-ups deixa efeitos futuros abertos. |
| Três níveis de dificuldade por cenário | GDD descreve a variação; recorte/fluxo de escolha não alocado pelo MVP | GDD §Dificuldade; não classificar automaticamente como Demo ou pós-Demo. |
| Classificação temporal no primeiro slice | Opcional | MVP §D; não é removida da possibilidade de scoring/configuração posterior. |
| Valores de relógios, capacidades, pesos e economia | Configuração/balanceamento sujeito a playtest | GDD; MVP §D. |

## Lacunas e decisões pendentes

Uma lacuna não apaga a identidade do objetivo. Ela limita comportamento, aceite ou integração, conforme indicado.

| ID | Objetivo(s) | Tipo | Informação ausente / impacto | Quando resolver | Fontes consultadas |
|---|---|---|---|---|---|
| GAP-01 | OBJ-07–OBJ-08 | Especificação/aceite | ADR MC-02 chama algo de “item inicialmente desconhecido”; o GDD diz que identidade do objetivo principal é conhecida e localização não é revelada automaticamente. Não está claro se MC-02 se refere à localização, existência ou identidade. Não é conflito confirmado. | Antes de fixar estado inicial do microciclo/fixture de aceite. | GDD §Objetivo principal; ADR §3.1 MC-02; MVP §A. |
| GAP-02 | OBJ-04 | Comportamento/aceite | O GDD define escolha do cofre e última escolha válida, mas não explicita quando o objetivo muda sua posição física/estado de armazenamento após a escolha. | Antes de aceitar a materialização física de OBJ-04; seleção e visibilidade podem ser especificadas desde já. | GDD §§Preparação e Objetivo principal; MVP §§A–B. |
| GAP-03 | OBJ-01 | Aceite | Não está fixado se Rick e Petra podem ser escolhidos simultaneamente pelos dois Players nem como escolhas/controles são atribuídos quando coincidem. | Antes de fechar critérios de seleção cooperativa da Demo. | GDD §§Personagens/Menus; MVP §B. |
| GAP-04 | OBJ-09, OBJ-13 | Comportamento/aceite | “Ataque válido” não tem todos os critérios de acerto descritos; o GDD não esclarece se o timer de recuperação continua durante transporte. Também precisa ficar observável o evento que conta como entrega à prisão. | Antes de aceite verificável do combate/transporte/captura. | GDD §§Interação, Propriedades, Estados e Transporte; MVP §A; ADR §§3.1–3.2. |
| GAP-05 | OBJ-09 | Comportamento/integração | Habilidades de larápios podem incapacitar Players, mas a consequência para Player, assalto e retomada de controle não está definida. | Antes de exigir esse efeito em uma missão/arquétipo. | GDD §Habilidades especiais; MVP não aloca consequência. |
| GAP-06 | OBJ-10 | Escopo/aceite | “Armazém completo” e interações necessárias ao slice não vêm acompanhados de uma lista fechada de portas, armadilhas, câmeras, controles, objetos carregáveis ou capacidades incluídos em cada corte. | Ao definir conteúdo da missão e seus critérios de aceite, antes de implementá-los. | GDD §§Cenários, Interações de cenário, Sala de controle; MVP §§A–B. |
| GAP-07 | OBJ-01, OBJ-09 | Escopo | O GDD descreve esquiva e salto, mas MVP não aloca esses verbos a uma entrega. Identidade padrão/formal do Player do slice também não foi fixada. | Ao especificar controles e roster do slice, sem bloquear o objetivo macro. | GDD §§Controles/Movimento/Esquiva/Salto; MVP §A. |
| GAP-08 | OBJ-03, OBJ-10, transversais | Aceite/qualidade | O conteúdo do briefing e o layout exato de HUD ainda não estão fechados; a legibilidade básica de split-screen, porém, tem critério normativo explícito e não é lacuna. Qualidade subjetiva continua dependente de playtest. | Antes do aceite de apresentação/UI da Demo; não bloqueia identificar os objetivos. | GDD §§Estrutura da missão, Câmera, HUD, Sala de controle; MVP §§A–B. |
| GAP-09 | OBJ-15 | Integração/aceite | GDD define critérios possíveis e pontuação por missão; MVP exige scoring configurado na Demo, mas não nomeia a configuração concreta do Armazém nem valores. | Antes de aceitar o resultado configurado da Demo; slice pode ter resultado básico. | GDD §Critérios de classificação; MVP §§A–B, D. |
| GAP-10 | OBJ-04 | Aceite | A regra de “última escolha válida” não define desempate se dois comandos forem recebidos como simultâneos no mesmo instante lógico. | Se o modo de entrada permitir empate simultâneo; resolver na especificação de cooperação. | GDD §Preparação; MVP §B. |
| GAP-11 | OBJ-01–OBJ-16 | Qualidade/playtest | “Perseguição interessante”, humor, caos legível e domínio gradual são intenções qualitativas, não limiares funcionais. Não devem ser convertidos em números sem pesquisa/playtest. | Em validações de experiência, depois de existir ciclo jogável. | GDD §§Intenção, Pilares e Experiência alvo; MVP §D. |
| GAP-12 | OBJ-09 | Comportamento/aceite | A intenção central pede perseguição, mas a regra de contato/última posição/recuperação da percepção do Player não está especificada. O GDD define memória de alvo para larápios; essa regra não deve ser copiada automaticamente para o Player. | Antes de exigir casos de perda e retomada de contato no aceite de perseguição. | GDD §§Intenção central, HUD/Minimapa e Perda de alvo; MVP §A. |
| GAP-13 | OBJ-02–OBJ-03 | Escopo/aceite | GDD define três níveis de dificuldade por cenário; MVP não fixa se a Demo/slice oferece escolha de dificuldade nem em que momento ela acontece. | Antes de exigir seleção de dificuldade ou pontuar um nível específico na missão. | GDD §Dificuldade (885–887); MVP §§A–C. |
| GAP-14 | Requisito transversal de menu/Pause | Aceite de cooperação | GDD/MVP exigem Pause no fluxo da Demo, mas não definem se pausar por um Player pausa o jogo para ambos, nem as regras de opções/saída durante coop local. | Antes do aceite manual de Pause com dois Players. | GDD §Menus (943–959); MVP §B (56, 61). |
| GAP-15 | OBJ-08 | Aceite/configuração | Pesos e custos são configuráveis, mas nenhuma missão/fixture da fonte define candidatos e um vencedor inequívoco para um aceite de seleção de plano específico. Sem essa configuração não se deve presumir uma fórmula ou vencedor. | Antes de aceitar escolhas específicas de plano; não bloqueia os critérios de validade, replanejamento e execução. | GDD §§GOAP e Peso/custo (170–234); MVP §§A–B (34–35, 54). |

### Conflitos entre fontes

**Nenhum conflito efetivo entre GDD e MVP foi identificado nesta reconstrução.** O microciclo do ADR adia partes que o MVP exige no slice completo; isso é uma sequência de produção expressa pelo próprio ADR. GAP-01 é ambiguidade na nomenclatura de um recorte do ADR, não evidência suficiente de regra conflitante.

**Limite de escopo, não lacuna de gameplay:** GDD/MVP definem momentos e telas da Demo, mas não determinam quantidade de Unity Scenes. Essa decisão não pode ser derivada deste catálogo e pertence ao planejamento técnico posterior.

## Correspondência com a análise anterior A1–A10

| ID anterior | Tratamento | Novo destino | Motivo |
|---|---|---|---|
| A1 — participar como Player local | Dividido/refinado | OBJ-01; cooperação transversal; detalhes de entrada e seleção no GAP-03/07 | Preserva participação e materialização, separando-a de missão/briefing. |
| A2 — escolher e entender missão | Dividido | OBJ-02 e OBJ-03 | Selecionar contrato e receber contexto têm resultados verificáveis distintos. |
| A3 — preparar proteção | Preservado/refinado | OBJ-04 | Mantém escolha de cofre, temporização e regras cooperativas; registra lacuna de atualização física. |
| A4 — avançar fases e relógios | Dividido | OBJ-05 e OBJ-06 | Progressão de fases e execução de receitas têm condições de conclusão independentes. |
| A5 — assalto autônomo | Dividido/refinado | OBJ-07 e OBJ-08; escapa em OBJ-14 | Conhecimento parcial é resultado verificável que alimenta a operação GOAP; término de escape pertence à resolução terminal. |
| A6 — usar cenário | Reclassificado/refinado | OBJ-10 e requisitos transversais | Interação válida pode mudar acesso/informação; o conteúdo por entrega permanece sem alocação. Interações de itens, combate e prisão pertencem aos seus próprios objetivos. |
| A7 — recuperar e proteger bens | Dividido | OBJ-11 e OBJ-12 | Recuperar item no cenário e armazená-lo protegido têm resultados/aceites independentes. |
| A8 — capturar larápio | Preservado/refinado | OBJ-13; confronto em OBJ-09 | A captura exige entrega efetiva; perseguição/ataque são etapas de jogador, não sinônimos de captura. |
| A9 — encerrar e classificar | Dividido | OBJ-14 e OBJ-15 | Resolução de estados terminais e cálculo/apresentação da classificação são resultados distintos. |
| A10 — continuar carreira | Preservado/refinado | OBJ-16 | Mantém economia, continuidade e persistência da Demo; modos futuros ficam no horizonte pós-Demo. |

Não houve objetivo retirado por duplicidade. A família e os IDs de GOAP foram mantidos como conceitos de IA, não transformados em Goals do catálogo. Requisitos visuais, de controle, HUD e telas não foram convertidos em objetivos apenas por corresponderem a assets, inputs ou cenas.

## Integridade e uso futuro

- Os contratos detalhados estão em [pega-objective-specifications.md](pega-objective-specifications.md); este arquivo é a visão macro e matriz de cobertura.
- O catálogo não cria regras novas nem resolve GAPs por inferência.
- OBJ-01–OBJ-16 não são cartões Trello nem ordem de implementação.
- As telas e momentos de fluxo não determinam a quantidade de cenas.
- O próximo trabalho é revisar este catálogo. Dependências de produção e roadmap ficam para depois da revisão.
- GDD, MVP, ADR, TASKS, Trello, código e assets não foram alterados por esta intervenção.
