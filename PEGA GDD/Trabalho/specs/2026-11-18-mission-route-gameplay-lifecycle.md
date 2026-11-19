# Lifecycle de gameplay da missão por Route e Activities

**Minuta de trabalho para revisão · 18 de novembro de 2026.** Direção definida para a reestruturação do PEGA. O primeiro corte descrito abaixo é uma proposta de implementação e aceite. A auditoria estática considera o PEGA em 76a5ddb com os arquivos de trabalho presentes e o pacote local com.immersive.framework 1.2.0 em c1a4954b, resolvido por Packages/manifest.json e Packages/packages-lock.json.

## 1. Unidade de missão e fronteira de autoridade

Uma ocorrência da Route representa uma execução da missão. Na Missão 01, a mesma Route contém Preparação, Assalto, Disputa pela Relíquia, Grande Fuga e Resultados. Game Over pertence a outra Route. Os assets da missão selecionam destinos por referência explícita e fornecem durações e conteúdo próprios. A finalidade de cada etapa é comum aos NPCs de todas as missões que a utilizam.

O Framework estabelece qual Activity está corrente. O PEGA confirma fatos físicos, aplica consequências, mede tempo de jogo e pede o destino cabível. Um pedido pendente pode guardar o alvo e a causa enquanto o Framework decide. A leitura da etapa vigente vem da Activity comprometida. A implementação pode associar referências de Activity às Missões principais na autoria da Route. Não precisa de FSM paralela, singleton, serviço de navegação ou interpretação do nome do asset.

### Ownership e lifetime

| Elemento | Owner e continuidade | Entrada, saída e reset |
|---|---|---|
| Armazém, saídas, cofres e objetos compartilhados | Cena primária ou adicional da Route. A instância e a posição sobrevivem às Activities. | Configurar uma vez por ocorrência da Route. Liberar na saída da missão. |
| NPCs, inventários, posse e relíquia | Actor e Object Entry com owner de Route. Inventário no Actor. O item mantém seu próprio owner mesmo quando carregado. | Ingressar no Assalto conforme a receita. Preservar ocorrências e dados na troca de etapa. Captura ou fuga requer resolução física pelo PEGA. |
| Player | Player físico admitido na Session. Cada Activity declara participação, readiness e eventual relocação. | Join inicial uma vez por admissão. Preservar posição quando a etapa dispensa relocação. Renovar acessos cuja âncora anterior foi liberada. |
| Tempo, fatos e resultado | A execução PEGA da Route conserva o prazo compartilhado, a primeira descoberta, as saídas aceitas e o resultado. | Um novo assalto começa com dados novos. Resultados lê o resultado fechado antes da navegação. Se necessário após sair da Route, entregar apenas o resultado consolidado à progressão. |
| Conhecimento | Geral no escopo da missão, Grupo no owner da gangue, Individual no Actor. | Aproveitar stores e políticas existentes. O Geral atual cria e limpa o store em callbacks de Activity e requer adaptação ao owner de Route. A composição de grupos já admite Route. |
| Conteúdo temporário | Mecânicas exclusivas, interação de Preparação, apresentação da Disputa e UI de Resultados pertencem à Activity. | Ativar na entrada apropriada, cancelar trabalho e desfazer bindings na saída. Usar conteúdo aditivo com ReleaseOnActivityChange quando for necessário remover fisicamente a cena. |
| Trabalho em curso | A mecânica que o iniciou conserva o prazo e as condições de execução. | Sobreviver fisicamente não basta para continuar uma Action ou callback. Descartar referências temporárias liberadas. Só interromper uma operação compartilhada quando sua própria regra ou o gate aplicável exigir. |

## 2. Contratos PEGA–Framework conferidos no pacote

Os caminhos abaixo são relativos à raiz do pacote resolvido e identificam as superfícies a usar e as implementações que sustentam o comportamento.

| Fronteira | Contrato e aplicação |
|---|---|
| Composição e Scene Lifecycle | RouteAsset e RouteContentProfileAsset definem cenas da Route. ActivityAsset e ActivityContentProfileAsset definem conteúdo da Activity. Colocar o conteúdo persistente no escopo materializado da Route e associar RouteContentContribution à Route correta. SceneLifecycleEvents.Available/Releasing expressam disponibilidade da cena. A troca de Activity não reentra a cena persistente. Runtime/ActivityFlow/ActivitySceneCompositionRuntime.cs seleciona os registros da Activity anterior na ocorrência da Route. KeepOnActivityChange retém a cena física, mas não mantém a Activity anterior corrente. |
| Solicitação | ActivityRequestTrigger.RequestActivity() e SubscribeRequestEvents(...) oferecem solicitação e resultado público. Colocar o trigger e a observação pendente num escopo que sobreviva à troca. Runtime/GameFlow/ActivityRequestTrigger.cs agrupa vários motivos internos em Ignored ou Failed. Um pedido submetido não confirma entrada. |
| Commit | IRouteActivityTransitionObserver.OnActivityTransitionCommitted(context) informa PreviousActivity e CurrentActivity, inclusive clear com destino nulo. O dispatcher procura observadores sob contribuições que correspondem à Route. A associação explícita evita descoberta global. Runtime/RouteLifecycle/IRouteActivityTransitionObserver.cs e RouteActivityTransitionObserverDispatcher.cs. Superfície experimental na versão inspecionada. |
| Readiness e permissões | ActivityReadinessEvents.LastSnapshot expõe Activity, Occurrence, Revision e IsReady. Occurrence distingue entradas, inclusive no mesmo asset. Revision ordena notificações daquela projeção. Runtime/ActivityFlow/ActivityReadinessSnapshot.cs e Documentation~/Guides/Activity-Readiness.md. O destino pode estar comprometido e preparando. O Framework controla gate/readiness. O PEGA só habilita sua mecânica quando as condições correspondentes estiverem satisfeitas. |
| Referências físicas | Usar as consultas de ocorrência de Actor e Object Entry com âncora válida e owner explícito quando houver ambiguidade. Uma referência à Activity liberada deixa de resolver mesmo se o consumidor for persistente. Identidade lógica não comprova igualdade de ocorrência física. Documentation~/Guides/Actor-Occurrence-References.md e Object-Entry-Occurrence-Queries.md. |
| Reset e Pause | ActivityRestartTrigger orquestra reset seletivo, clear e reentrada. Resettable e ResetMembership selecionam o estado sobrevivente. Pause pertence ao Framework. Consultar Documentation~/Guides/Reset-Usage.md e Pause-Usage.md. Os runtimes internos servem de evidência, sem dependência direta do consumidor. |

## 3. Fluxo, relógios e consistência

| Activity | Missão ou participação | Marco aceito e efeito obrigatório |
|---|---|---|
| Preparação | Somente o Player participa. | Com Player, controle e etapa prontos, iniciar a duração configurada. Ao vencer, encerrar permissões exclusivas e solicitar Assalto. |
| Assalto | NPCs ingressam. Encontrar a relíquia. | Na primeira liberação para jogo, estabelecer origem e prazo de Assalto. A primeira identificação confirmada publica a descoberta e solicita Disputa. Vencimento sem descoberta solicita Grande Fuga diretamente. |
| Disputa pela Relíquia | Cada grupo procura fazer a relíquia sair com um de seus integrantes. | Ler a origem e o prazo já estabelecidos. Descoberta, troca de portador, queda do item e entrada da Activity não prorrogam o Assalto. Vencimento solicita Grande Fuga. |
| Assalto ou Disputa | Vale o período temporal de Assalto. | Saída válida de qualquer NPC com a relíquia confirma a perda e a falha do Player imediatamente, resolve o NPC e antecipa Grande Fuga. Não há pausa para registrar a falha. |
| Grande Fuga | Todos os NPCs restantes procuram escapar sem captura. | Abrir uma única janela de duração própria. Saída da relíquia confirma a falha, preservando o prazo existente. A simulação segue até vencer a Fuga, mesmo que nenhum NPC reste. |
| Resultados | Apresentar a resolução consolidada. | Somente o vencimento da Grande Fuga fecha o gameplay, consolida resultados uma vez e autoriza solicitar Resultados. Falha de navegação pode atrasar a apresentação, jamais prolongar o período jogável. |

**Regras dos períodos e de Pause.**

**Hipótese sobre bloqueios de transição.** Descontar um intervalo de loading ou readiness do tempo da missão continua em aberto. O IF-ADR-036 separa readiness estrutural de disponibilidade transitória. `ActivityReadinessEvents.LastSnapshot` informa a condição da Activity; `PlayerGameplayInputAvailability` descreve disponibilidade do input do Player. Nenhuma dessas observações, isoladamente, demonstra que toda a simulação da missão ficou impedida de avançar. Antes de adotar uma suspensão, verificar no consumidor qual contrato público permite observar início, fim, escopo e domínio do bloqueio relevante e decidir quais impedimentos justificam descontar tempo. No Corte 1, usar a mesma base temporal do Assalto nos dois lados da troca e registrar o tempo observado. Esta prova dispensa compensação de gates, alteração de timeScale ou novo serviço de relógio.

**Quatro marcos independentes.** (1) A mecânica confirma um acontecimento com identidades e instante. A Route aceita suas consequências irreversíveis. (2) O PEGA solicita o destino pelo trigger. (3) O observador da Route recebe o commit e a nova Missão passa a corresponder à Activity vigente. (4) A readiness do destino e as permissões aplicáveis liberam as operações daquela etapa. A descoberta pode ser conhecida enquanto Assalto ainda está corrente. Um destino comprometido pode continuar indisponível para executar mecânicas.

**Concorrência e recuperação.**

- Validar a execução da Route, as ocorrências físicas relevantes e o instante do fato. Guardar na própria execução se a primeira descoberta, a saída daquele NPC, a falha e o fechamento já foram aceitos. Reentregas não multiplicam efeitos ou solicitações. Rejeitar trabalho remanescente de uma Route encerrada.
- Ao final do passo de simulação, registrar separadamente os acontecimentos físicos válidos e só então escolher o destino. Se um NPC descobrir a relíquia e escapar com ela no mesmo passo, conservar descoberta, saída, posse no cruzamento e falha do Player. Solicitar Grande Fuga diretamente. Tanto a saída válida com a relíquia quanto o vencimento do Assalto têm precedência sobre Disputa. Se ambas as causas ocorrerem juntas, iniciar um único período de Fuga.
- Para fatos atrasados, distinguir instante físico de instante de entrega. Processar fatos confirmados do último passo de simulação antes de fechar seu resultado. Como convenção proposta, o fim da Fuga exclui cruzamentos exatamente no prazo. Uma saída anterior, entregue no mesmo passo de fechamento, deve contar. Notificação antiga não reabre resultado publicado.
- Manter no máximo um pedido em voo. Se a intenção de ir a Disputa ainda não foi submetida, substituí-la por Grande Fuga quando ocorrer saída válida ou vencimento. Se o pedido já foi submetido, conservar a necessidade de Fuga, observar o desfecho e reconciliar com a Activity comprometida assim que o Framework admitir novo pedido. Disputa pode chegar a ser comprometida nesse intervalo; sua readiness não é pré-condição de gameplay para priorizar Fuga. Um resultado genérico Failed ou Ignored, por si só, não determina a Activity corrente.
- Se a origem continuar vigente, conservar o fato e permitir nova solicitação do destino ainda aplicável quando a causa impeditiva cessar. Se o destino já estiver comprometido mas não pronto, diagnosticar a ocorrência e manter bloqueadas suas operações dependentes. Não emitir retry por frame. Um clear, unload ou substituição da Route invalida seus pedidos, bindings e callbacks; abandono não gera resultado de conclusão.

## 4. Contrato de leitura para conhecimento e autonomia

| Consumidor precisa saber | Fonte e garantia |
|---|---|
| Qual Activity e Missão valem agora | Activity comprometida pelo Framework e associação de autoria PEGA. Um NPC tardio lê a situação vigente na admissão. |
| Quem descobriu, onde e quando | Primeira identificação confirmada nesta missão, com identidade da relíquia, Actor descobridor, local da descoberta e instante no tempo de jogo. Publicar no Conhecimento Geral, acessível a todos, inclusive o descobridor. Conservar o fato após a troca de Activity. |
| Quais acontecimentos são globais | Somente os fatos designados pelo gameplay, como descoberta pública e início de Fuga. Uma observação histórica não revela posição ou portador atuais. Grupo mantém informação compartilhada por sua gangue. Individual preserva evidências próprias. |
| Quanto tempo rege a operação | A execução da Route fornece período e restante aos consumidores autorizados. Assalto e Disputa compartilham o período. Cálculos locais de UI ou IA não determinam vencimento. |

**Consulta após a entrada.** Qualquer consumidor admitido na execução da Route pode consultar a Activity comprometida, a Missão principal correspondente, a descoberta pública e o prazo vigente, mesmo se foi inicializado depois da entrada. Uma leitura durante Preparing deve distinguir etapa já comprometida de disponibilidade para agir. No clear, a leitura representa a ausência de Activity corrente. O leitor usa a situação atual da mesma execução, sem depender de eventos passados nem adquirir autoridade para modificá-la.

O fato booleano atual MainObjectiveIdentified não comporta os campos da descoberta. Reaproveitar o mecanismo tipado de conhecimento e separar o registro histórico de qualquer observação atualizável. O resultado e a Activity vigente nunca são inferidos do store. A integração termina na disponibilidade dos dados; seleção de desejos, planos e Actions está fora deste desenho.

## 5. Restart e possibilidade de restauração

O restart nativo seleciona o estado persistente com membership de Activity, executa seus participantes de reset e faz clear/reentrada. Falha de reset obrigatório impede o avanço ao clear. Conteúdo local segue sua política de release. O mesmo ActivityAsset pode receber uma nova ocorrência de readiness. Descartar callbacks da entrada antiga e recompor os bindings locais.

Como política inicial, um restart local recompõe a etapa sobre o mundo existente. Prazo do Assalto, descoberta irreversível, posse e falha permanecem com a Route, fora do reset da Activity. Reentrar em Disputa não concede um Assalto novo. Um participante que deva restaurar explicitamente uma mecânica local usa Resettable e a membership aplicável. Recomeçar a missão inteira exige uma nova execução ou o reset de todo o estado relevante. Solicitar a mesma Route já ativa pode ser ignorado. Abandonar a missão libera a execução sem apurar vitória ou derrota adicional.

Um futuro checkpoint deverá distinguir identidade lógica dos objetos de suas ocorrências Unity e preservar, no mínimo, a coerência entre etapa, prazo e estado físico restaurado. O contrato atual de ProgressionSaveRuntime oferece pedidos de armazenamento; a captura e a reconstituição do mundo pertencem ao jogo. Save/Load e retorno ao instante exato de entrada ficam para uma decisão de produto futura.

## 6. Plano incremental

### Corte 1 — Assalto → Disputa pela Relíquia

**Entrega verificável.** Uma Route, duas Activities, um NPC, uma relíquia de Route, um prazo de Assalto e um elemento exclusivo dessa Activity. A prova começa em Assalto e usa confirmação controlada da descoberta com identidade, local e instante. Seu alcance é a integração do lifecycle. Preparação, ingresso completo, percepção definitiva, Grande Fuga, Resultados, Save/Load e GOAP ficam fora do corte.

| Trabalho e dono | Arquivos ou locais previstos para a futura implementação | Dependência |
|---|---|---|
| Compor as duas etapas e o cenário de prova | Referências de composição existentes em Assets/PEGA/GameFlow/MissionRoute.asset, MissionActivity.asset, Mission_ActivityContent.asset e Assets/PEGA/Scenes/MissionRoute.unity e MissionActivityContent.unity. Prever os assets de Disputa em GameFlow e conteúdo de cena próprio apenas quando precisar de materialização. Isolar a prova das demais demonstrações atuais. | Route primária e perfis válidos; contribuição explícita de Route; elemento exclusivo de Assalto em cena configurada para release. |
| Preservar NPC, relíquia e posse | Composição da cena de Route. Consumir Assets/PEGA/Interactions/Runtime/PEGAActorPhysicalInventory.cs e ObjectEntry/PEGAObjectEntryPhysicalItem.cs. Revisar vínculos em Assets/PEGA/Player/PEGAMissionPlayerObjectEntryRecognitionAccess.cs apenas se usados no caminho da prova. | Ocorrências resolvidas sob owner de Route, item identificável e posse confirmada pelo inventário. Fixar o NPC parado para comparar a posição. |
| Conectar fato, prazo e pedido | Gameplay de missão em Assets/PEGA/GameFlow. Compor comportamento PEGA na contribuição de Route para prazo e reação à confirmação, com referência ao ActivityRequestTrigger de Disputa. Em Assets/PEGA/NPC/Knowledge/Global/PEGAGlobalKnowledgeState.cs e Knowledge/Definitions/PEGAGlobalKnowledgeFacts.cs, considerar apenas o necessário para projetar a descoberta de Route. | Uma origem explícita de confirmação com descobridor/local/instante. Confirmar a ocorrência por entrada controlada e registrar a origem da prova. |
| Verificar entrada e disponibilidade | Composição do destino com ActivityReadinessEvents. Usar o observador de Route para commit. Usar o logger canônico PEGA ou Inspector existente para evidência nos marcos, sem polling de logs. | Participante obrigatório controlável e Wait Visible para distinguir Preparing de Ready na prova; validar binding, occurrence e revision. |

**Aceite manual no Unity para o futuro corte**

1. Em Assalto pronto, registrar Route, Activity, ocorrência do NPC, posição, identidade e ocorrência física da relíquia, posse no inventário, origem, prazo e restante já menor que a duração total. Confirmar a presença do elemento exclusivo de Assalto.
2. Confirmar a descoberta uma vez. Comparar descobridor, local e instante no acontecimento e na leitura Geral. Observar um único pedido para Disputa. Reentregar a mesma confirmação e verificar que não cria uma segunda descoberta ou transição.
3. Manter o participante obrigatório do destino em preparação. Observar o commit Assalto → Disputa e o snapshot Preparing dessa Activity. Liberar o participante e observar Ready na mesma Occurrence. A liberação não altera a descoberta ou o prazo.
4. Confirmar a mesma Route e as mesmas ocorrências físicas do NPC e da relíquia, mesma posição do NPC parado dentro da precisão da leitura, mesma relação de posse e uma unidade do item. Confirmar saída/liberação do elemento exclusivo de Assalto.
5. Comparar origem e prazo antes e depois por igualdade, usando a mesma base de tempo. Conferir que o restante já consumido não foi reposto e que a diferença entre as leituras corresponde ao avanço observado nessa base. Registrar o intervalo em Preparing como evidência para decidir a política futura de bloqueios. Uma suspensão específica por gate não é critério de aceite.

6. Após Ready, inicializar um leitor de prova que esteve ausente durante a descoberta e o commit. Na primeira consulta à execução corrente, obter Disputa, sua Missão principal, o acontecimento de descoberta e o prazo original de Assalto com restante atualizado. A leitura deve funcionar sem replay, sem nova transição e sem criar outro NPC ou integrar GOAP.

**Evidência mínima da prova.** Capturar os valores antes da descoberta controlada, no commit, no Ready e na primeira consulta tardia. Registrar separadamente a submissão e a conclusão do pedido. O aceite em execução comprova a continuidade do mundo e do prazo durante a troca de etapa; a percepção real tem aceite próprio.

### Cortes posteriores e dependências

1. **Preparação → Assalto.** Após a prova de continuidade, integrar admissão e controle do Player, duração da Preparação e primeira janela de entrada dos NPCs. Aceite: sem autonomia de Assalto na Preparação e apenas uma origem temporal na liberação do Assalto.
2. **Grande Fuga por vencimento.** A partir do prazo já validado, admitir tanto Assalto → Fuga sem descoberta quanto Disputa → Fuga. Aceite: um prazo final próprio, inclusive se o vencimento ocorrer durante um pedido pendente.
3. **Saída física com relíquia.** Integrar saída válida, posse e participação do Actor. Aceite: falha imediata sem Pause, Fuga antecipada durante o Assalto e prazo final intacto durante Fuga.
4. **Resultados.** Após haver período final e resolução física, fechar no vencimento, classificar e apresentar o mesmo resultado mesmo se a navegação exigir recuperação. Integrar Game Over em outra Route somente quando a regra de progressão o exigir.
5. **Restart seletivo e interrupções integradas.** Exercitar reentrada de Activity, abandono e substituição da Route com callbacks pendentes. Aceite: nenhuma duplicação de fato, prazo ou resultado; trabalho da execução antiga não afeta a nova.

## 7. Decisões para revisão e limites de evidência

- **Corte 1.** A confirmação controlada é suficiente para iniciar a implementação após autorização. A identificação por percepção real e o critério definitivo de local divulgado pertencem ao futuro corte de descoberta.
- **Antes da integração temporal completa.** Verificar o sinal público e o alcance dos bloqueios, decidir quais suspendem o tempo da missão e ratificar o limite exclusivo da Fuga. Para empates físicos entre perda de posse, captura e cruzamento, definir qual efeito físico foi concluído primeiro no passo de simulação.
- **Antes de oferecer “reiniciar etapa” ao jogador:** decidir se a opção recompõe o lifecycle sobre o mundo atual ou promete voltar a um checkpoint. Só a segunda promessa demanda restauração de dados. A prova de duas Activities independe dessa escolha.
- **Sequência de entrega.** A arquitetura tem aprovação conceitual. Esta revisão incorpora os ajustes de contrato. Seguem-se autorização do Corte 1, implementação, validação manual no Unity pelos seis critérios acima e expansão incremental após evidência de continuidade.

### Referências de trabalho

- [ADR anterior, composição da missão](../../ADR-PREPRODUCAO.md) e [GDD, regras de preparação, conhecimento e resolução](../../GDD.md).
- [Configuração do consumidor](../../../Packages/manifest.json), [Route](../../../Assets/PEGA/GameFlow/MissionRoute.asset) e [perfil de conteúdo atual](../../../Assets/PEGA/GameFlow/Mission_ActivityContent.asset).
- No pacote resolvido, consultar também Runtime/ActivityFlow/ActivityFlowRuntime.Transaction.cs (ordenação e compensação), Runtime/GameFlow/GameFlowRuntime.cs (pedidos repetidos), Runtime/ActivityRestart/ActivityRestartTrigger.cs e Runtime/ProgressionSave/ProgressionSaveRuntime.cs.