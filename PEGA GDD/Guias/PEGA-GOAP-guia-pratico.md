# Guia prático — GOAP do PEGA

> **Guia experimental — GOAP em desenvolvimento.** Este documento descreve as capacidades disponíveis na versão atual do PEGA. A organização, os controles e os comportamentos poderão evoluir durante a experimentação.

Este guia ajuda Game Designers e testadores a configurar NPCs com os assets que já existem no projeto. Ele descreve a configuração atual do Unity; não amplia as regras do GDD nem substitui suas decisões.

## 1. Como pensar em um NPC GOAP

Pense no NPC como alguém que combina três coisas:

- **Desejos:** o que ele tenta alcançar, por exemplo manter distância de um personagem.
- **Conhecimento:** o que ele percebeu ou sabe. O NPC só pode escolher alvos que estejam disponíveis para ele.
- **Comportamentos disponíveis:** o que ele sabe fazer, como aproximar-se de um personagem conhecido ou investigar uma presença.

O GOAP procura um comportamento que possa aproximar o NPC de um desejo, usando o conhecimento disponível. Marcar um comportamento em **Available Behaviors** não obriga o NPC a executá-lo; o desejo, o conhecimento e a viabilidade do comportamento também precisam combinar.

## 2. Os elementos de autoria

| Nome no Unity | Para que serve | O que pode ser editado hoje |
|---|---|---|
| **Desire** (definição) | Dá nome a uma motivação e aponta para a condição que a considera atendida. | `Desire Id`, `Display Name` e `Desired Goal Condition`. |
| **Goal Condition** | Diz que resultado conta como atingir aquele desejo. | Os assets atuais verificam a chegada a um tipo conhecido de destino. |
| **Desire Settings** | Ajusta como um Desire participa do Profile e do contexto. | `Desire Definition`, `Enabled For Context`, `Base Weight`, controles de satisfação e `Completion Limit`. |
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
- Cena com três larápios e dois Actor Groups: [`Assets/PEGA/Scenes/MissionActivityContent.unity`](../../Assets/PEGA/Scenes/MissionActivityContent.unity)

O caminho geral é: uma definição de **Desire** aponta para uma **Goal Condition**; um asset **Desire Settings** escolhe essa definição e ajusta seus valores; um **Desire Profile** referencia essas configurações; e o componente **Autonomy** do NPC recebe o Profile e sua lista de **Available Behaviors**. O Profile não contém cópias isoladas das configurações: se o mesmo asset Desire Settings for usado em mais de um Profile, editar esse asset afeta todos eles.

## 3. Os recursos incluídos no projeto

### Desejos e condições

Os três assets de Desire disponíveis são:

- [`PursueRick.asset`](../../Assets/PEGA/NPC/Desires/PursueRick.asset) — perseguir Rick reconhecido; usa `CharacterPositionReached`.
- [`MaintainSafeDistance.asset`](../../Assets/PEGA/NPC/Desires/MaintainSafeDistance.asset) — manter distância segura; usa `SafeSeparationReached`.
- [`InvestigatePresence.asset`](../../Assets/PEGA/NPC/Desires/InvestigatePresence.asset) — investigar presença percebida; usa `InvestigationPositionReached`.

As três condições correspondentes estão na pasta `Goals/Conditions`. Elas são variações da condição atual **Target Reached Goal Condition**, configuradas para diferentes tipos de destino. O asset `CharacterPositionReached.asset` também exige contato visual atual quando o resultado é observado.

### Comportamentos disponíveis

O prefab base contém estes quatro assets:

- [`ApproachKnownCharacter.asset`](../../Assets/PEGA/NPC/Actions/Definitions/ApproachKnownCharacter.asset) — aproximar-se de personagem conhecido.
- [`MoveAwayFromCharacter.asset`](../../Assets/PEGA/NPC/Actions/Definitions/MoveAwayFromCharacter.asset) — afastar-se de personagem conhecido.
- [`SearchLastKnownPosition.asset`](../../Assets/PEGA/NPC/Actions/Definitions/SearchLastKnownPosition.asset) — procurar na última posição conhecida.
- [`InvestigateDetectedPresence.asset`](../../Assets/PEGA/NPC/Actions/Definitions/InvestigateDetectedPresence.asset) — investigar presença detectada.

O comportamento de fuga por uma saída conhecida foi removido junto com a integração de conhecimento específica de Spawn Points. Essa capacidade permanece pendente de reconstrução sobre os contratos aprovados de Knowledge.

As Actions de aproximação e afastamento exigem dois fatos Individuais obtidos por Perception: `IsPlayer == true` e uma última posição observada válida. O `ActorId` endereça esses fatos, mas não comprova sozinho que o alvo é Player. A fonte só publica `IsPlayer` após contato visual com uma ocorrência vinculada a Player Slot; presença radial mantém o fato desconhecido e pode sustentar a investigação local. Ao perder contato, a última posição continua disponível. A continuação é aberta somente quando o plano selecionado pertence à Action de aproximação; a readquisição visual ou a reavaliação após o intervalo configurado devolve a escolha aos planos normais do GOAP.

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

## 5. Os três larápios demonstrativos

Os três objetos na cena são instâncias do mesmo prefab base e usam a mesma lista de comportamentos. Os dois larápios originais permanecem no primeiro Actor Group e conservam seus Profiles distintos. O terceiro tem identidade própria, usa o Profile `Demo_PursuitFocus` como o primeiro demonstrador e pertence a um segundo Actor Group com `GroupProfile` próprio.

### `Larapio NPC - Pursuit Demo` — foco em perseguição

Usa `Demo_PursuitFocus`. No contexto `Normal`, seus Desire Settings dão peso-base **4** a `PursueRick`, **4** a `InvestigatePresence` e **0,5** a `MaintainSafeDistance`.

### `Larapio NPC - Self-Preservation Demo` — foco em autopreservação

Usa `Demo_SelfPreservationFocus`. No mesmo contexto, dá peso-base **4** a `MaintainSafeDistance` e **0,5** a `PursueRick` e `InvestigatePresence`.

Os assets Desire Settings estão em `Desires/Settings`; os dois Profiles estão em `Desires/Profiles`. Ambos usam `Normal` e têm `Plan Time Reference Seconds` igual a **10 segundos**. O terceiro larápio reutiliza o Profile de perseguição do primeiro, sem alterar os comportamentos dos dois originais.

Esses pesos favorecem uma motivação, mas não são uma prioridade absoluta. O GOAP também considera o tempo estimado dos planos e quais comportamentos e alvos são viáveis com o conhecimento atual. A ordem das linhas em `Desire Settings` não força a primeira motivação a vencer. Um plano bem mais curto pode vencer outro cujo Desire tenha peso maior.

Nos Profiles demonstrativos, `Weight Reduction From Satisfaction` é **0** e `Completion Limit` é **-1** (sem limite). O histórico de satisfação, portanto, não reduz os pesos nem desativa esses Desires nesses exemplos.

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

Os círculos de percepção podem aparecer mesmo em NPCs não selecionados, conforme **Draw Perception Gizmos** e **Draw Gizmos When Unselected** no componente **Perception Source**. As prévias das duas investigações são desenhadas quando o **Autonomy** do NPC está selecionado. Para reduzir a sobreposição na Scene, selecione outro objeto ou desative **Gizmos** na janela Scene; isso não altera o comportamento do NPC.

## 8. Limitações atuais

- O contexto disponível é somente `Normal`. Um Profile não adiciona contextos novos apenas por ser editado no Inspector.
- A autoria de condições está limitada às quatro configurações existentes de **Target Reached Goal Condition**. É possível criar outra instância desse tipo, mas não definir por assets uma condição com significado diferente.
- As motivações configuráveis são os quatro Desires listados neste guia. Um novo tipo de condição ou comportamento exige trabalho técnico; não basta criar outro Profile.
- Os cinco comportamentos são opções, não comandos. Se o NPC não tem o conhecimento necessário, se não há um alvo válido ou se nenhuma combinação atende ao Desire, ele pode não iniciar um deles.
- A integração anterior de fuga por saída conhecida foi removida e ainda não foi reconstruída. A Fuga Final descrita no GDD também não deve ser considerada disponível só porque seus requisitos estão documentados.
- Desire Definitions, Desire Settings, Profiles, condições e comportamentos são assets compartilháveis. O Profile apenas referencia os Desire Settings; se um mesmo asset for usado por mais de um Profile, editá-lo altera a configuração em todos eles. Uma Desire Definition compartilhada dá significado à motivação e aponta para sua condição; uma mudança nela afeta todos os Settings que a referenciam. Os Assets de comportamento também são compartilhados, enquanto `Available Behaviors` define quais deles aquele NPC pode usar. Para um experimento isolado, faça cópias do Profile e dos Settings que pretende mudar; atribua a cópia ao NPC de teste.
- O histórico de satisfação dos NPCs é runtime e não é salvo como uma configuração do Profile. Reiniciar o Play Mode inicia outro teste.

Este guia descreve a superfície atual do Unity. Para regras de jogo, o GDD permanece a referência normativa: [PEGA GDD](../GDD.md). A existência de uma regra no GDD não comprova que já exista uma opção de autoria ou implementação para ela.







## 9. Knowledge: escopos, acesso e validação

O [ADR-GROUP-KNOWLEDGE-01](../ADRs/ADR-GROUP-KNOWLEDGE-01-conhecimento-compartilhado-e-previo.md) preserva os escopos **Individual**, **Group** e **Global**. O núcleo PEGA reutiliza definições tipadas, políticas, evidências, proveniência e [`PEGAKnowledgeFactStore`](../../Assets/PEGA/NPC/Knowledge/Core/PEGAKnowledgeFactStore.cs). Identidades técnicas endereçam fatos, mas não provam que um NPC conhece uma entidade; consumidores não completam dados ausentes consultando Player Session, declarações de Actor ou World State.

Para adicionar uma propriedade canônica, o programador declara sua definição tipada em [`Knowledge/Definitions`](../../Assets/PEGA/NPC/Knowledge/Definitions/) com ID estável, tipos de endereço/valor e política. O catálogo localiza automaticamente as definições estáticas; a autoria genérica no Inspector as oferece quando ambas as tipagens têm payload compatível, sem editores específicos por propriedade.

**Individual Knowledge** permanece próprio de cada NPC. Perception registra fatos Individuais de identidade, contato visual, última posição e presença radial. Approach e Avoid usam fatos com evidência de Perception; o GOAP continua usando os Profiles de Desejos e capacidades já configurados.

Na configuração de demonstração da `Mission`, o primeiro NPC também começa com `pega.actor.last-observed-character-position` para o Actor do Player resolvido pelo `ActorOccurrenceReferenceBinding`, com valor `(100, 0, 100)`. Perception escreve a mesma definição para o mesmo `ActorId` quando observa esse Actor; a origem `DesignerAuthoredInitial` permanece distinta da origem `Perception` e ambas passam pela política canônica. O usuário reportou essa equivalência de identidade/propriedade validada no Unity.

**Global Knowledge** tem uma autoridade PEGA compartilhada no contexto da Activity da Mission. A mesma capability de leitura tipada é composta explicitamente nas vistas de Knowledge GOAP dos três NPCs e fica disponível sem membership. A disponibilidade do fato não obriga o planner a escolhê-lo nem executa Actions; ainda não há produtor de gameplay para o fato demonstrador `MainObjectiveIdentified`. Fatos Globais não são cópias nos stores Individuais.

**Group Knowledge** é opcional e usa stores independentes por `GroupId` do Framework. O componente `PEGAActorGroupKnowledgeState` acompanha o owner PEGA que compõe os Groups; sua ausência não impede criar ou usar Actor Groups. `PEGAActorGroupKnowledgeReader` só devolve conteúdo quando o Framework confirma que o Group está vivo e o Actor possui membership vigente. Uma consulta negada retorna `NotMember` separadamente de um fato ausente e não expõe valor ou proveniência. Sair do Group revoga a leitura; o encerramento do owner limpa os stores. Os três leitores da Mission apontam para a autoridade, mas cada leitura continua limitada à membership do Group solicitado.

Na cena `Mission`, o componente `PEGAActorGroupKnowledgeState` da composição autoriza um fato inicial para o `PEGAGangGroupProfile` (`pega.gang.larapios`), depois da criação real do Group pelo owner. O componente Global da Activity autoriza outro fato para a mesma propriedade e identidade. Ambos usam `pega.object-entry.position` e `pega.warehouse.storage-rack-west-north`; o valor Group é `(-5, 1, 15.700001)` e o Global é `(200, 0, 200)`. A leitura Group continua limitada à membership vigente, e nenhum dos valores é copiado para o Knowledge Individual ou para o outro store.

Para observar os dados durante a execução, abra **PEGA > Diagnostics > GOAP Runtime Inspector**, selecione um NPC da `Mission` e abra a aba **Conhecimento**. A seção Individual mostra os fatos do NPC; Global mostra a autoridade da Activity quando composta; Group lista as authorities pertinentes, seu `GroupId`, membership confirmada ou negada e fatos somente quando a leitura está autorizada. Compare um dos dois NPCs do Group `pega.gang.larapios` com o NPC do `pega.mission.actor-group.second`: o membro autorizado pode ler o fato da Gangue, enquanto o membro do outro Group não recebe seu valor ou proveniência. Ambos podem ler o fato Global `(200, 0, 200)`. Ative **Exibir fonte e payload de cada evidência** para inspecionar origem e payload.

O Inspector é somente leitura: ausência de fato, fato desconhecido/inválido, autoridade indisponível e acesso negado permanecem estados distintos; a aba não consulta World State nem cria fatos. A autoria inicial e a leitura dos três escopos foram validadas manualmente no Unity pelo usuário em 2026-11-03. Isso não integra esses fatos a Actions, Desires ou decisões do planner. Permanecem pendentes condições GOAP declarativas, sensores de Object Entry, produtores de eventos reais e uso de posições conhecidas para Targets/navegação.

### Evidências e validação

- **Global Knowledge — Unity manual: PASS, relatado pelo usuário.** `PEGAGlobalKnowledgeProofWindow` registra e atualiza um fato Global, compara valor, status, escopo, timestamp e proveniência nas vistas GOAP de NPCs em Groups distintos e numa vista independente; também confirma que o Knowledge Individual permanece separado.
- **Group Knowledge — Unity manual: PASS, relatado pelo usuário.** `PEGAGroupKnowledgeProofWindow` verifica os dois membros do primeiro Group, o membro do segundo, fatos separados, leituras cruzadas negadas e redigidas, revogação/restauração via Leave/Join, acesso Global preservado e independência Individual. Não executa Actions nem decisões do planner.
- **Autoria inicial e diagnóstico — Unity manual: validado pelo usuário em 2026-11-03.** A cena configura o mesmo fato Object Entry nos escopos Group e Global com valores diferentes; o Runtime Inspector mostra identidade, propriedade, valor, evidência e controle de acesso conforme a membership.
- **Individual Knowledge:** implementação e execução anterior são registradas no contexto deste ADR. Esta etapa não repetiu uma prova comportamental Individual; as provas Global/Group atuais verificam que esse estado continua independente.
- O Codex não executou as provas. O PASS foi informado pelo usuário; não há log ou captura de Unity anexado a este registro.
