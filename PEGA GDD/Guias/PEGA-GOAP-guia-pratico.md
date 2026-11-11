# Guia prático — GOAP do PEGA

> **Guia experimental — GOAP em desenvolvimento.** Este documento descreve as capacidades disponíveis na versão atual do PEGA. A organização, os controles e os comportamentos poderão evoluir durante a experimentação.

Este guia ajuda Game Designers e testadores a configurar NPCs com os assets que já existem no projeto. Ele descreve a configuração atual do Unity; não amplia as regras do GDD nem substitui suas decisões.

## 1. Como pensar em um NPC GOAP

Pense no NPC como alguém que combina motivação, conhecimento e capacidades. Mantenha estes conceitos separados:

- **Desire:** a motivação e seu peso relativo. Sua aplicabilidade pode depender do Knowledge atual.
- **Goal:** a condição de estado cuja satisfação representa o resultado desejado.
- **Intenção:** o Desire que venceu a seleção no contexto atual.
- **Plano:** a cadeia causal de Actions e Targets que pode satisfazer o Goal escolhido.
- **Conhecimento:** o que ele percebeu ou sabe. O NPC só pode escolher alvos que estejam disponíveis para ele.
- **Comportamentos disponíveis:** o que ele sabe fazer, como aproximar-se de um personagem conhecido ou investigar uma presença.

O GOAP compara Desires aplicáveis e planos causais viáveis com os Targets conhecidos. Uma Action pode preparar o estado necessário a outra; após resultados observados, o GOAP reavalia, continua ou reconstrói/substitui o plano conforme validade, custo restante e estabilidade. Isso não fixa uma lista de destinos. Effects projetados durante a busca são hipóteses; Effects observados são evidências e só são reutilizados enquanto suas condições de validade forem verdadeiras.

## 2. Os elementos de autoria

| Nome no Unity | Para que serve | O que pode ser editado hoje |
|---|---|---|
| **Desire** (definição) | Dá nome a uma motivação e aponta para a condição que a considera atendida. | `Desire Id`, `Display Name` e `Desired Goal Condition`. |
| **Goal Condition** | Diz que resultado conta como satisfazer o Goal daquele Desire. | Existem condições de chegada e condições tipadas para investigações/saída do Second Group. |
| **Desire Settings** | Ajusta elegibilidade, peso e satisfação do Desire no contexto. | `Desire Definition`, `Applicability Condition` opcional, `Enabled For Context`, `Base Weight`, controles de satisfação e `Completion Limit`. |
| **Desire Profile** | Reúne Desire Settings por contexto e define como comparar o peso dos Desires com o tempo estimado dos planos. | `Contexts`; em cada contexto, `Context`, `Plan Comparison`, `Plan Time Reference Seconds`, `Weight And Time Balance` e `Desire Settings`. |
| **Available Behaviors** | Lista os comportamentos que o NPC pode considerar. | Uma lista de assets de comportamento no componente **Autonomy** do NPC. |

Os nomes `Desire Definition`, `Desire Settings` e `Desire Profiles` também aparecem nos menus de criação. Uma **Goal Condition** é selecionada dentro de uma definição de Desire; não há um asset separado de Goal para cada Profile.

### Onde encontrar os assets

- Comportamentos: [`Assets/PEGA/NPC/Actions`](../../Assets/PEGA/NPC/Actions/)
- Desejos: [`Assets/PEGA/NPC/Desires`](../../Assets/PEGA/NPC/Desires/)
- Perfis de Actor Group: [`Assets/PEGA/NPC/ActorGroups/Profiles`](../../Assets/PEGA/NPC/ActorGroups/Profiles/)
- Knowledge: [`Assets/PEGA/NPC/Knowledge`](../../Assets/PEGA/NPC/Knowledge/)
- Condições: [`Assets/PEGA/NPC/Goals/Conditions`](../../Assets/PEGA/NPC/Goals/Conditions/)
- Ajustes de cada desejo: [`Assets/PEGA/NPC/Desires/Settings`](../../Assets/PEGA/NPC/Desires/Settings/)
- Combinações para NPCs: [`Assets/PEGA/NPC/Desires/Profiles`](../../Assets/PEGA/NPC/Desires/Profiles/)
- Prefab base: [`Assets/PEGA/NPC/Prefabs/PEGA_NPCActorRuntimeBase.prefab`](../../Assets/PEGA/NPC/Prefabs/PEGA_NPCActorRuntimeBase.prefab)
- Cena com quatro NPCs demonstradores e dois Actor Groups: [`Assets/PEGA/Scenes/MissionActivityContent.unity`](../../Assets/PEGA/Scenes/MissionActivityContent.unity)

O caminho geral é: uma definição de **Desire** aponta para uma **Goal Condition**; um asset **Desire Settings** escolhe essa definição, pode fornecer uma condição de aplicabilidade e ajusta seus valores; um **Desire Profile** referencia essas configurações; e o componente **Autonomy** do NPC recebe o Profile e sua lista de **Available Behaviors**. Aplicabilidade, satisfação do Goal e disponibilidade de plano são avaliações distintas. O Profile não contém cópias isoladas das configurações: se o mesmo asset Desire Settings for usado em mais de um Profile, editar esse asset afeta todos eles.

## 3. Os recursos incluídos no projeto

### Desejos e condições

Os assets de Desire incluem os exemplos gerais e os dois Desires da demonstração Second Group:

- [`PursueRick.asset`](../../Assets/PEGA/NPC/Desires/PursueRick.asset) — perseguir Rick reconhecido; usa `CharacterPositionReached`.
- [`MaintainSafeDistance.asset`](../../Assets/PEGA/NPC/Desires/MaintainSafeDistance.asset) — manter distância segura; usa `SafeSeparationReached`.
- [`InvestigatePresence.asset`](../../Assets/PEGA/NPC/Desires/InvestigatePresence.asset) — investigar presença percebida; usa `InvestigationPositionReached`.
- [`InvestigateSecondGroupRequiredPois.asset`](../../Assets/PEGA/NPC/Desires/InvestigateSecondGroupRequiredPois.asset) — investigar POIs obrigatórios ainda pendentes no Second Group.
- [`ReachSecondGroupActivityExit.asset`](../../Assets/PEGA/NPC/Desires/ReachSecondGroupActivityExit.asset) — alcançar a saída depois que as investigações obrigatórias forem concluídas.

As condições correspondentes estão na pasta `Goals/Conditions`. As condições de investigação e de saída consultam fatos válidos do Group autorizado e Effects observados; conhecer uma posição, por si só, não satisfaz chegada ou investigação.

### Comportamentos disponíveis

O prefab base contém estes quatro assets:

- [`ApproachKnownCharacter.asset`](../../Assets/PEGA/NPC/Actions/Definitions/ApproachKnownCharacter.asset) — aproximar-se de personagem conhecido.
- [`MoveAwayFromCharacter.asset`](../../Assets/PEGA/NPC/Actions/Definitions/MoveAwayFromCharacter.asset) — afastar-se de personagem conhecido.
- [`SearchLastKnownPosition.asset`](../../Assets/PEGA/NPC/Actions/Definitions/SearchLastKnownPosition.asset) — procurar na última posição conhecida.
- [`InvestigateDetectedPresence.asset`](../../Assets/PEGA/NPC/Actions/Definitions/InvestigateDetectedPresence.asset) — investigar presença detectada.
- [`NavigateToKnownGroupObjectEntryPosition.asset`](../../Assets/PEGA/NPC/Actions/Definitions/NavigateToKnownGroupObjectEntryPosition.asset) — navegar a uma posição tipada de POI conhecida pelo Group, usando a avaliação e execução de rota existentes.
- [`InvestigateKnownGroupPoi.asset`](../../Assets/PEGA/NPC/Actions/Definitions/InvestigateKnownGroupPoi.asset) — permanecer no POI conhecido, confirmar a investigação e publicar o fato de conclusão no Group Knowledge.
- [`PickUpKnownObjectEntry.asset`](../../Assets/PEGA/NPC/Actions/Definitions/PickUpKnownObjectEntry.asset) — planejar a operação Pick Up contra um Object Entry conhecido pelo Group.

O demonstrador usa a Action de navegação para alcançar a saída após as investigações. Isso não inicia uma transição para `ResultActivity` nem implementa, por si só, o fluxo de Fuga Final do GDD.

As Actions de aproximação e afastamento exigem dois fatos Individuais obtidos por Perception: `IsPlayer == true` e uma última posição observada válida. O `ActorId` endereça esses fatos, mas não comprova sozinho que o alvo é Player. A fonte só publica `IsPlayer` após contato visual com uma ocorrência vinculada a Player Slot; presença radial mantém o fato desconhecido e pode sustentar a investigação local. Ao perder contato, a última posição continua disponível. A continuação é aberta somente quando o plano selecionado pertence à Action de aproximação; a readquisição visual ou a reavaliação após o intervalo configurado devolve a escolha aos planos normais do GOAP.

### Pick Up: decisão e execução compartilhada

Para o NPC, `PickUpKnownObjectEntry.asset` é o comportamento de planejamento: o GOAP escolhe o Desire, a Action e um Target disponível no Group Knowledge autorizado. No Player, a intenção genérica de interagir é resolvida pela seleção e autoridade de descoberta do Player. `PegaPlayerObjectInteractionMechanic` e `PegaNpcObjectInteractionMechanic` expõem essas entradas distintas; `PegaActorObjectInteractionMechanic` mantém a validação e o encaminhamento de execução compartilhados.

O Inspector de cada Object Entry compõe a oferta pelo componente **Interaction Aggregator**, associando a Action no mesmo objeto e os participantes permitidos. O componente **Pick Up** contém `Interaction Range Meters` e `Execution Duration Seconds`. Os objetos de prova da cena usam ambos os participantes, **1,35 m** e **1 s**; a configuração é por objeto. Para a decisão do NPC, o Group Knowledge correspondente mantém os fatos tipados de posição, disponibilidade para Pick Up e parâmetros de planejamento daquela identidade.

O componente **Physical Inventory** expõe `Capacity`. O prefab do Player e o prefab base dos NPCs estão configurados para **2 itens**. Essa capacidade é conferida antes de iniciar e durante a execução da operação.

## 4. Tutorial: configurar outro NPC para teste

Este procedimento cria uma cópia de teste na cena de missão existente, preservando as ligações de Player e Actor Group já configuradas. Confira a membership do Group no componente do NPC duplicado antes de salvar.

1. Na janela **Project**, abra `Assets/PEGA/Scenes/MissionActivityContent.unity`.
2. Na janela **Hierarchy**, selecione `Larapio NPC - Pursuit Demo` ou `Larapio NPC - Self-Preservation Demo` e use **Ctrl+D** para duplicá-lo.
3. Renomeie a cópia na **Hierarchy**. No componente **Actor Declaration**, defina um `Display Name` e um `Actor Id` exclusivo. Não reutilize o identificador de outro Actor.
4. No componente **Autonomy**, escolha o asset desejado em `Desire Profile`.
5. Confira a lista `Available Behaviors`. Para começar, mantenha os quatro comportamentos que vieram com a cópia. Se remover um deles, o NPC deixa de poder usá-lo.
6. Confira se a cópia está sobre uma área navegável e afastada o suficiente dos outros NPCs para observar sua reação.
7. Salve a cena se quiser guardar a configuração. Para um teste temporário, descarte as alterações da cena ao sair.

Ao duplicar um NPC já presente na cena, mantenha seus outros componentes e referências. Eles fornecem dados de percepção, movimento e conhecimento usados pelo NPC; este guia não pede que sejam reconfigurados.

### Criar um Profile próprio para experimentar

Para não mudar um dos exemplos originais, duplique um Profile existente na janela **Project** com **Ctrl+D** e dê outro nome ao arquivo. Selecione a cópia e edite seu contexto `Normal` e a lista `Desire Settings` no Inspector.

Se quiser alterar o peso de um Desire sem afetar outros Profiles, duplique também o asset correspondente em `Desire Settings`. No asset duplicado, escolha a mesma `Desire Definition`, ajuste `Base Weight` e coloque a cópia na lista `Desire Settings` do seu Profile. Cada Desire deve aparecer uma vez no mesmo Profile.

Se quiser mudar qual condição define um Desire, faça cópias dos assets de `Desire Definition` e `Goal Condition` que pretende alterar. Na cópia da definição, escolha a condição copiada em `Desired Goal Condition`; no asset Desire Settings duplicado, escolha a definição copiada em `Desire Definition` e substitua o Setting original no Profile pela cópia. Isso mantém os exemplos originais intactos e continua usando o tipo de condição existente.

Também é possível criar assets pelos menus da janela **Project**: botão direito > **Create** > **PEGA** > **NPC**. As opções incluem **Desire Definition**, **Desire Settings**, **Desire Profiles** e **GOAP** > **Target Reached Goal Condition**. A última opção cria outra configuração da condição que já existe; não cria um tipo novo de Goal.

## 5. Os quatro larápios demonstrativos

Os quatro NPCs da cena são instâncias do mesmo prefab base. Os dois larápios originais permanecem no primeiro Actor Group e conservam seus Profiles distintos. `Larapio NPC - Second Group Demo` e `Larapio NPC - Pick Up Demo` têm identidades próprias, pertencem ao Group de demonstração e usam Profiles separados.

### `Larapio NPC - Pursuit Demo` — foco em perseguição

Usa `Demo_PursuitFocus`. No contexto `Normal`, seus Desire Settings dão peso-base **4** a `PursueRick`, **4** a `InvestigatePresence` e **0,5** a `MaintainSafeDistance`.

### `Larapio NPC - Self-Preservation Demo` — foco em autopreservação

Usa `Demo_SelfPreservationFocus`. No mesmo contexto, dá peso-base **4** a `MaintainSafeDistance` e **0,5** a `PursueRick` e `InvestigatePresence`.

Os Profiles estão em `Desires/Profiles`; seus Desire Settings estão em `Desires/Settings`. Os dois Profiles anteriores usam `Normal` e `Plan Time Reference Seconds` igual a **10 segundos**. A demonstração Second Group também usa `Normal`, a mesma referência de **10 segundos** e uma política multiplicativa normal.

Esses pesos favorecem uma motivação, mas não são uma prioridade absoluta. O GOAP também considera o tempo estimado dos planos e quais comportamentos e alvos são viáveis com o conhecimento atual. A ordem das linhas em `Desire Settings` não força a primeira motivação a vencer. Um plano bem mais curto pode vencer outro cujo Desire tenha peso maior.

Nos Profiles de perseguição e autopreservação, `Weight Reduction From Satisfaction` é **0** e `Completion Limit` é **-1** (sem limite). No Profile Second Group, a redução por satisfação também é **0**; investigar pode repetir enquanto houver POI pendente, e o Desire de saída limita a conclusão a uma execução. O histórico de satisfação não altera os pesos destes Profiles.

### `Larapio NPC - Second Group Demo` — investigar POIs e alcançar a saída

Na cena `MissionActivityContent`, este NPC é membro do Group `pega.mission.actor-group.second` (Group Profile [`PEGAMissionSecondActorGroupProfile.asset`](../../Assets/PEGA/NPC/ActorGroups/Profiles/PEGAMissionSecondActorGroupProfile.asset)). O Profile de Desire contém `InvestigateSecondGroupRequiredPois` e `ReachSecondGroupActivityExit`, ambos com peso-base **24**. O primeiro só é aplicável enquanto existir investigação obrigatória pendente; o segundo só fica aplicável quando todas as investigações obrigatórias têm conclusão conhecida. O Goal de saída exige também a chegada observada à saída.

Sua lista `Available Behaviors` conserva os quatro comportamentos-base e acrescenta `NavigateToKnownGroupObjectEntryPosition` e `InvestigateKnownGroupPoi`. O Group inicia com fatos tipados de posição, investigação obrigatória e conclusão pendente para três `ObjectEntryId`s; a saída tem posição e o fato de papel `pega.poi.activity-exit`. O override de `Global Knowledge State` do NPC está vazio; a authority Global da Activity usada pelos demonstradores anteriores não é composta nesta vista GOAP.

A Action de investigação tem pausa configurada de **2 segundos** e raio de chegada de **1,25 m**. A posição chega ao planner pelo Group Knowledge e a navegação reutiliza o provider normal de rota. Para cada POI pendente, o GOAP avalia a cadeia causal navegação → chegada válida → investigação; a comparação usa a rota real estimada mais a duração da investigação. Com os pesos iguais, os custos disponíveis influenciam a escolha. Depois de cada investigação confirmada, a conclusão é lida de volta no Group Knowledge e o GOAP reavalia a partir da posição atual. Não há ordem de waypoints configurada. Quando não resta POI obrigatório pendente, o Desire de saída pode competir normalmente e seu Goal só se satisfaz depois da chegada confirmada.

**Evidência de Play Mode:** o responsável reportou que os três POIs foram investigados, a saída foi alcançada e os Goals correspondentes foram satisfeitos; os logs do Unity corroboraram o resultado. Encontros ocasionais com o Player e as reavaliações associadas foram aceitos para este escopo. O Codex não executou Unity nem inspecionou os arquivos brutos dos logs, que não estão anexados ao repositório.

### `Larapio NPC - Pick Up Demo` — Pick Up de um Object Entry conhecido

Este NPC usa `Demo_SecondGroupPickUpOnly`, pertence ao mesmo Group de demonstração e disponibiliza os quatro comportamentos-base, navegação para Object Entry e `PickUpKnownObjectEntry`. Seu Desire de Pick Up pode ser planejado quando o Group Knowledge autoriza posição e disponibilidade do item. O fluxo GOAP determina Action e Target; a execução física usa a mesma oferta e o mesmo componente **Pick Up** disponíveis ao Player.

Na cena, `Object Entry - Pick Up (Player Proof)` e `Object Entry - Pick Up (NPC Proof)` conservam identidades e posições próprias. Ambos oferecem Pick Up a Player e NPC com alcance **1,35 m** e duração **1 s**. Seus IDs técnicos continuam `pega.mission.player.rob-item` e `pega.mission.second-group.rob-item`; a palavra `rob` nesses valores pertence aos identificadores estáveis, enquanto a operação e os labels atuais são **Pick Up**. A capacidade configurada do inventário físico é **2** para Player e NPC.

## 6. Experimentar combinações

Faça uma cópia do Profile antes de experimentar. Depois:

1. Na lista `Desire Settings`, remova uma configuração para tirar aquele Desire do Profile, ou adicione outro asset existente.
2. Para favorecer uma motivação, aumente seu `Base Weight`; para desfavorecê-la, reduza-o. Compare os resultados sem assumir que isso cria uma ordem fixa.
3. Em uma cópia de `Desire Settings`, `Enabled For Context` permite incluir ou retirar o Desire do contexto sem apagar sua definição.
4. Em `Available Behaviors`, remova ou adicione assets existentes para limitar ou ampliar as capacidades consideradas pelo NPC. A lista é compartilhada por todos os desejos daquele NPC; não associa um comportamento exclusivamente a um Desire.
5. Atribua o Profile experimental somente ao NPC de teste e mude um valor por vez.

Os controles de histórico são opcionais para a configuração atual: `Satisfaction Gain` define quanto aumenta a satisfação após um resultado confirmado; `Maximum Satisfaction` limita esse valor; `Weight Reduction From Satisfaction` regula quanto a satisfação reduz o peso; `Completion Limit` limita quantas vezes o Desire pode ser satisfeito (`-1` significa sem limite). Essa satisfação pertence ao histórico runtime de cada NPC, enquanto os valores do asset são compartilhados.

## 7. Observar o resultado no Play Mode

O caminho completo da missão usa a inicialização do Immersive Framework. No Unity, confira **Edit > Project Settings > Immersive Framework > Editor Play Mode > Startup**. O projeto está configurado como `FrameworkStartup`; mantenha essa opção para iniciar a rota da missão. `CurrentSceneOnly` executa a cena aberta sem a inicialização da rota do Framework e não é o caminho recomendado para testar esta composição.

1. Deixe a cena `MissionActivityContent` aberta para editar e observar os NPCs, mas inicie Play Mode pelo botão **Play** do Editor com `FrameworkStartup`.
2. Observe o resultado na janela **Game**. Os NPCs só podem considerar comportamentos cujos alvos e informações estejam disponíveis para eles.
3. Para comparar perseguição e autopreservação, provoque contato visual e confira a seleção segundo os dois `Desire Profiles`, pesos e custos. Para testar investigação radial, provoque presença antes do contato visual. Para testar a continuação, adquira Player e posição visualmente, perca contato e observe a busca pela última posição; se o plano selecionado não for Approach, a continuação não deve impor perseguição.
4. Use a janela **Scene** com **Gizmos** ligados para conferir o alcance e a direção da percepção. Os dois larápios demonstrativos estão configurados para mostrar seus Gizmos mesmo quando não estão selecionados.
5. Se o resultado não aparecer, pare o Play Mode e confira primeiro `Desire Profile`, `Available Behaviors`, conhecimento/percepção e se existe um alvo válido. A janela **Window > General > Console** pode mostrar erros ou avisos. A ausência de mensagens na Console não comprova, sozinha, que o NPC executou o comportamento esperado.
6. Ajuste uma configuração por vez, salve e repita o mesmo cenário para comparar.

### Entender os Gizmos de percepção e investigação

Na janela **Scene**, os desenhos são auxiliares de desenvolvimento, não objetos físicos nem áreas que o GOAP necessariamente esteja usando. As cores e os rótulos ajudam a distinguir **alcance dos sensores**, **regiões configuradas para busca** e **evidências de execução**:

| O que aparece | Significado para o designer | Quando/onde aparece |
|---|---|---|
| **Círculo azul grande** centrado no NPC | Alcance da **percepção radial**, que detecta presença sem identificar automaticamente o personagem. | Geometria do sensor; acompanha o NPC. |
| **Cone amarelo grande** saindo do NPC | **Campo de visão**: alcance, direção e ângulo do sensor visual. | Geometria do sensor; acompanha a orientação do NPC. |
| **Cilindro azul-claro** com o rótulo de área local | Limite da **investigação da última posição conhecida** de um personagem. Não é um segundo sensor nem um marcador fixo do Player. | Se há evidência válida de uma posição anteriormente observada e o contato visual foi perdido, fica ancorado nessa posição; caso contrário, mostra uma **prévia** ancorada no NPC. |
| **Cilindro amarelo/alaranjado menor** com o rótulo de busca radial | Limite da **investigação de presença radial não identificada**, dentro do qual podem ser propostos pontos de observação. Não revela a posição real do alvo. | Quando há presença radial conhecida, usa a posição do NPC registrada no momento da observação; sem essa evidência, mostra uma **prévia** ancorada no NPC. |

Os **pequenos círculos amarelos** ao redor da região de busca radial representam amostras possíveis de investigação. Os **círculos perto dos pés do NPC**, rotulados como *Deslocamento físico mínimo* ou *Distância mínima do destino local*, representam limites usados para evitar destinos próximos demais, combinando o raio de chegada com o deslocamento mínimo configurado. Quando uma dessas Actions de investigação está efetivamente em execução, um **pequeno círculo verde no destino** identifica o alvo escolhido pelo GOAP. Um círculo alaranjado adicional pode representar a distância de separação segura em torno da última posição conhecida de um personagem.

**Como interpretar:** os volumes de investigação aparecem ao selecionar um NPC com o comportamento correspondente disponível, inclusive como **prévia** antes de ele investigar qualquer coisa. Sua presença **não comprova** que a Action tenha sido selecionada ou iniciada. Já o destino verde exige uma Action de investigação em execução. A linha que liga o NPC ao alvo, acompanhada de um rótulo como *Target: contato visual* ou *Target: fora da percepção*, representa a observação mais recente do sensor, não um destino do GOAP.

Os círculos de percepção podem aparecer mesmo em NPCs não selecionados, conforme **Draw Perception Gizmos** e **Draw Gizmos When Unselected** no componente **Perception Source**. As prévias das investigações são desenhadas quando o **Autonomy** do NPC está selecionado. Para reduzir a sobreposição na Scene, selecione outro objeto ou desative **Gizmos** na janela Scene; isso não altera o comportamento do NPC.

## 8. Limitações atuais

- O contexto disponível é somente `Normal`. Um Profile não adiciona contextos novos apenas por ser editado no Inspector.
- A autoria de condições ainda depende de tipos de condição implementados em código. O demonstrador acrescenta condições tipadas para atividade de investigação, conclusão coletiva e chegada à saída; criar uma semântica arbitrária só com assets continua fora da superfície atual.
- Desires e Actions são combináveis por Profile e `Available Behaviors`, mas um novo tipo de condição ou Action exige trabalho técnico. O GOAP decide entre candidaturas conhecidas e viáveis; esses campos não comandam uma sequência.
- A navegação conhecida do Second Group inclui o POI de saída nesta demonstração. Ela não transiciona a Activity para `ResultActivity` nem demonstra a Fuga Final completa do GDD.
- Desire Definitions, Desire Settings, Profiles, condições e comportamentos são assets compartilháveis. O Profile apenas referencia os Desire Settings; se um mesmo asset for usado por mais de um Profile, editá-lo altera a configuração em todos eles. Uma Desire Definition compartilhada dá significado à motivação e aponta para sua condição; uma mudança nela afeta todos os Settings que a referenciam. Os Assets de comportamento também são compartilhados, enquanto `Available Behaviors` define quais deles aquele NPC pode usar. Para um experimento isolado, faça cópias do Profile e dos Settings que pretende mudar; atribua a cópia ao NPC de teste.
- O histórico de satisfação dos NPCs é runtime e não é salvo como uma configuração do Profile. Reiniciar o Play Mode inicia outro teste.

Este guia descreve a superfície atual do Unity. Para regras de jogo, o GDD permanece a referência normativa: [PEGA GDD](../GDD.md). A existência de uma regra no GDD não comprova que já exista uma opção de autoria ou implementação para ela.







## 9. Knowledge: escopos, acesso e validação

O [ADR-GROUP-KNOWLEDGE-01](../ADRs/ADR-GROUP-KNOWLEDGE-01-conhecimento-compartilhado-e-previo.md) preserva os escopos **Individual**, **Group** e **Global**. O núcleo PEGA reutiliza definições tipadas, políticas, evidências, proveniência e [`PEGAKnowledgeFactStore`](../../Assets/PEGA/NPC/Knowledge/Core/PEGAKnowledgeFactStore.cs). Identidades técnicas endereçam fatos, mas não provam que um NPC conhece uma entidade; consumidores não completam dados ausentes consultando Player Session, declarações de Actor ou World State.

Para adicionar uma propriedade canônica, o programador declara sua definição tipada em [`Knowledge/Definitions`](../../Assets/PEGA/NPC/Knowledge/Definitions/) com ID estável, tipos de endereço/valor e política. O catálogo localiza automaticamente as definições estáticas; a autoria genérica no Inspector as oferece quando ambas as tipagens têm payload compatível, sem editores específicos por propriedade.

**Individual Knowledge** permanece próprio de cada NPC. Perception registra fatos Individuais de identidade, contato visual, última posição e presença radial. Approach e Avoid usam fatos com evidência de Perception; o GOAP continua usando os Profiles de Desejos e capacidades já configurados.

Na configuração de demonstração da `Mission`, o primeiro NPC também começa com `pega.actor.last-observed-character-position` para o Actor do Player resolvido pelo `ActorOccurrenceReferenceBinding`, com valor `(100, 0, 100)`. Perception escreve a mesma definição para o mesmo `ActorId` quando observa esse Actor; a origem `DesignerAuthoredInitial` permanece distinta da origem `Perception` e ambas passam pela política canônica. O usuário reportou essa equivalência de identidade/propriedade validada no Unity.

**Global Knowledge** tem uma autoridade PEGA compartilhada no contexto da Activity da Mission. A capability é composta nos dois NPCs demonstradores originais; `Larapio NPC - Second Group Demo` deixa `Global Knowledge State` sem referência. A disponibilidade de fatos Global nos outros NPCs não obriga o planner a escolhê-los nem executa Actions; ainda não há produtor de gameplay para o fato demonstrador `MainObjectiveIdentified`. Fatos Globais não são cópias nos stores Individuais.

**Group Knowledge** é opcional e usa stores independentes por `GroupId` do Framework. O componente `PEGAActorGroupKnowledgeState` acompanha o owner PEGA que compõe os Groups; sua ausência não impede criar ou usar Actor Groups. `PEGAActorGroupKnowledgeReader` só devolve conteúdo quando o Framework confirma que o Group está vivo e o Actor possui membership vigente. Uma consulta negada retorna `NotMember` separadamente de um fato ausente e não expõe valor ou proveniência. Sair do Group revoga a leitura; o encerramento do owner limpa os stores. Os três leitores da Mission apontam para a autoridade, mas cada leitura continua limitada à membership do Group solicitado.

Em uma etapa anterior de prova de autoria, `pega.object-entry.position` para `pega.warehouse.storage-rack-west-north` foi configurado no Group e no Global com valores distintos (`(-5, 1, 15.700001)` e `(200, 0, 200)`). A configuração final da demonstração substituiu o fato inicial do Second Group por posições dos três POIs de investigação e da saída; o fato Global antigo do rack permanece em sua authority. O `Second Group Demo` não compõe essa authority Global, e nenhum fato Global contém as posições dos POIs demonstrativos.

Para observar os dados durante a execução, abra **PEGA > Diagnostics > GOAP Runtime Inspector**, selecione um NPC da `Mission` e abra a aba **Conhecimento**. A seção Individual mostra os fatos do NPC; Global mostra a authority da Activity apenas quando composta; Group lista as authorities pertinentes, seu `GroupId`, membership confirmada ou negada e fatos somente quando a leitura está autorizada. Compare os membros de Groups distintos: o membro autorizado pode ler seu fato Group, enquanto outro Group não recebe seu valor ou proveniência. Os demonstradores originais também podem ler o fato Global `(200, 0, 200)`; a vista do `Second Group Demo` não compõe essa authority. Ative **Exibir fonte e payload de cada evidência** para inspecionar origem e payload.

O Inspector é somente leitura: ausência de fato, fato desconhecido/inválido, authority indisponível e acesso negado permanecem estados distintos; a aba não consulta World State nem cria fatos. No Second Group Demo, Targets, aplicabilidade, Preconditions e condições dos Goals consultam Group Knowledge autorizado; os fatos de posição são autoria prévia do Group, não leitura da Transform nem resolução da ocorrência física. A Action de investigação registra `pega.poi.investigation-completed` no mesmo Group somente após a pausa contínua, validação local, escrita aceita e leitura de volta da evidência.

### Evidências e validação

- **Global Knowledge — Unity manual: PASS, relatado pelo usuário.** `PEGAGlobalKnowledgeProofWindow` registra e atualiza um fato Global, compara valor, status, escopo, timestamp e proveniência nas vistas GOAP de NPCs em Groups distintos e numa vista independente; também confirma que o Knowledge Individual permanece separado.
- **Group Knowledge — Unity manual: PASS, relatado pelo usuário.** `PEGAGroupKnowledgeProofWindow` verifica os dois membros do primeiro Group, o membro do segundo, fatos separados, leituras cruzadas negadas e redigidas, revogação/restauração via Leave/Join, acesso Global preservado e independência Individual. Não executa Actions nem decisões do planner.
- **Autoria inicial e diagnóstico — Unity manual: validado pelo usuário em 2026-11-03.** A cena configura o mesmo fato Object Entry nos escopos Group e Global com valores diferentes; o Runtime Inspector mostra identidade, propriedade, valor, evidência e controle de acesso conforme a membership.
- **Investigar POIs e sair — Play Mode: validado visualmente pelo responsável e corroborado pelos logs do Unity.** Foram reportadas a investigação dos três POIs, chegada à saída e satisfação dos respectivos Goals. Encontros ocasionais com o Player e as reavaliações decorrentes foram aceitos no escopo atual. O Codex não executou Unity nem inspecionou os logs brutos; nenhum artefato da execução foi anexado a este guia.
- **Individual Knowledge:** implementação e execução anterior são registradas no contexto deste ADR. Esta etapa não repetiu uma prova comportamental Individual; as provas Global/Group atuais verificam que esse estado continua independente.
- As provas de Knowledge e o resultado completo do demonstrador são relatos do responsável; este guia não declara validação independente do Codex em Unity.

## 10. Configurar ou adaptar conjuntos de POIs

Para reproduzir a demonstração, abra `MissionActivityContent` com o startup de rota `FrameworkStartup` e mantenha, no `Larapio NPC - Second Group Demo`, o Group Profile `PEGAMissionSecondActorGroupProfile`, o Desire Profile `Demo_SecondGroupInvestigationAndExit` e as seis Actions disponíveis. Confirme que sua membership efetiva pertence ao `pega.mission.actor-group.second` e que o campo `Global Knowledge State` está vazio.

Na Route, cada POI precisa de uma identidade `ObjectEntryId` própria declarada por `ObjectEntryDeclaration` do Immersive Framework. Na autoria inicial do Knowledge do Second Group, configure por identidade os fatos tipados `pega.object-entry.position`, `pega.poi.investigation-required = true` e `pega.poi.investigation-completed = false` para cada POI de investigação. Para a saída, configure `pega.object-entry.position` e `pega.poi.activity-exit = true`. A configuração existente usa os POIs `pega.warehouse.second-group-demo-poi`, `pega.warehouse.second-group-investigation-poi-west`, `pega.warehouse.second-group-investigation-poi-south` e `pega.warehouse.activity-exit-poi`.

Para adaptar o conjunto, altere as declarações e fatos Group e mantenha as Actions/Goal Conditions vinculadas ao mesmo Group Profile. Marque como investigação obrigatória apenas os POIs que participam do critério coletivo; cada posição deve ser caminhável pela NavMesh. A Action `InvestigateKnownGroupPoi` expõe a duração configurável em segundos e raio de chegada; ela só publica conclusão após cumprir a pausa sem interrupção, revalidar as Preconditions, obter escrita Group aceita sob membership vigente e ler de volta o fato com a evidência da execução. Os Desires devem manter condições de aplicabilidade apropriadas: investigações pendentes para o Desire de investigação e todas as obrigatórias concluídas para o Desire de saída.

A ordem não é configurada. A Action de navegação estima rota e custo em cada posição; a Action de investigação acrescenta sua duração. Após o resultado observado e a atualização de Knowledge, o GOAP recalcula alternativas a partir do estado e posição atuais. Não ordene os fatos por preferência de visita e não injete destinos diretamente no sistema de movimento.
