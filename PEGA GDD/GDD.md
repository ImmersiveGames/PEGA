# PEGA - Game Design Document

**Projeto:** `PEGA`
**Tipo de documento:** Game Design Document
**Status:** Fonte de verdade ativa para as regras e arquitetura de gameplay
**Versão:** 0.8.0
**Última atualização:** 2026-10-10

---

## Visão

**PEGA** é um jogo de ação top-down para PC, com foco em perseguição, captura e recuperação de itens roubados. O jogo acompanha Rick Ronda e Petra Patrol, dois jovens que tentam entrar para a Polícia e Guarda Avançada de Larópolis, a **P.G.A. Larópolis**, mas não são aceitos por falta de experiência. Sem desistir da carreira de segurança, eles fundam a própria empresa: **Proteção e Estratégia na Guarda de Ativos em Larópolis**, ou **P.E.G.A. Larópolis**.

O jogo coloca um ou dois jogadores em missões de segurança por diferentes pontos da cidade. Cada missão apresenta um local invadido por larápios, itens valiosos em risco, rotas de fuga, armadilhas, câmeras, power-ups e inimigos com comportamentos próprios. O objetivo central é impedir que os criminosos escapem com os itens, capturar o maior número possível de inimigos e proteger os bens do cliente antes do fim do assalto.

> [!NOTE] Tratamento da fonte
> Este documento é a fonte normativa das regras, contratos e arquitetura de gameplay do PEGA. O MVP recorta o que será construído em cada entrega; o Art Book documenta direção visual. A presença de uma regra aqui não significa que ela já esteja implementada.

:::objective
**Objetivo do documento:** consolidar a visão, os sistemas e as regras principais de PEGA em uma versão de GDD mais legível, navegável e pronta para evolução.

**Critério de sucesso:** uma pessoa de design, programação, arte ou produção deve conseguir entender a fantasia do jogo, o loop principal, os modos, os inimigos, as interações de cenário, a progressão e os pontos que ainda precisam de definição.

**Estado:** fonte canônica
:::

### Intenção central de design

:::decision
**Palavra-chave:** `PERSEGUIR`

**Princípio:** PEGA é sobre perseguir.

Queremos criar a experiência de identificar um alvo, persegui-lo através de um ambiente dinâmico, perder e recuperar contato, antecipar sua rota, superar obstáculos e finalmente interceptá-lo.

Os sistemas de movimento, inimigos, cenários, objetos, armadilhas, informação e cooperação devem contribuir para criar perseguições interessantes, variadas e legíveis.

**Mantra de design:** **Capturar é a recompensa. Perseguir é a experiência.**

**Critério de decisão:** quando houver dúvida sobre adicionar, remover ou alterar uma mecânica, a primeira pergunta deve ser: **“Isso torna a perseguição mais interessante?”** Se a resposta for não, a mecânica precisa justificar sua existência por outro papel essencial no jogo.

**Consequência:** perseguição não deve ser tratada apenas como uma mecânica ou como um pilar entre outros. Ela é a experiência central que orienta o gameplay moment-to-moment; os pilares de design abaixo devem sustentar diferentes aspectos dessa experiência.
:::

### Pilares de design

| Pilar | Descrição | Consequência prática |
|---|---|---|
| Perseguição cartunesca | A graça principal está em correr atrás de ladrões por cenários cheios de obstáculos. | Os mapas precisam ter rotas alternativas, atalhos, esconderijos, objetos interativos e momentos de quase captura. |
| Segurança improvisada | Rick e Petra não são policiais perfeitos; eles resolvem problemas com ferramentas disponíveis no local. | Carrinhos, armadilhas, portas, câmeras, cofres e objetos seguráveis devem fazer parte do combate e da estratégia. |
| Caos legível | O jogo pode ser frenético, mas o jogador precisa entender o que está acontecendo. | HUD, minimapa, feedback de estado, ícones de item e leitura visual dos inimigos são críticos. |
| Cooperação local | A Demo completa exige dois jogadores locais. | O primeiro vertical slice pode ser solo; a Demo deve funcionar em tela dividida horizontalmente. |
| Escalada de contratos | A agência PEGA cresce conforme assume locais mais perigosos. | Novos cenários, gangues, dificuldades e modos devem aparecer como progressão de carreira. |

### Experiência alvo

| Campo | Definição |
|---|---|
| Gênero | Ação top-down, perseguição, captura, recuperação de itens e controle de cenário. |
| Plataforma | PC. |
| Jogadores | Um jogador ou dois jogadores em multiplayer local cooperativo. |
| Público | A partir de 12 anos. |
| Idiomas planejados | Português brasileiro e inglês. |
| Tom | Cartunesco, cômico, dinâmico e levemente caótico. |
| Lançamento desejado no GDD original | Steam, Epic Games e Microsoft Game Pass. |

:::risk
**Risco:** informações de plataforma, público, idiomas e lançamento aparecem no GDD original, mas podem estar desatualizadas em relação ao planejamento atual.

**Impacto:** médio.

**Mitigação:** confirmar escopo comercial antes de estimar localização, certificação, requisitos de loja e suporte a controles.
:::

## Resumo do jogo

### Conceito principal

Rick Ronda e Petra Patrol protegem negócios de Larópolis contra gangues de larápios. Em cada fase, criminosos entram por pontos específicos do cenário, procuram itens de desejo, sabotam defesas, tentam fugir e reagem à presença dos jogadores. Os jogadores devem explorar o ambiente, usar informações do minimapa e da sala de controle, ativar armadilhas, perseguir inimigos, recuperar itens e levar criminosos capturados até a prisão do cenário.

### Fantasia do jogador

O jogador deve sentir que está comandando uma dupla de seguranças determinados, atrapalhados na medida certa e obrigados a resolver situações maiores do que sua experiência. A fantasia não é de simulação policial realista. Ela é de ação física, decisões rápidas, humor visual e domínio gradual de cenários cada vez mais complexos.

### Verbos principais

- Mover-se pelo cenário.
- Localizar entradas, saídas, itens valiosos e pontos de interesse.
- Perseguir larápios.
- Atacar, interromper ou derrubar inimigos.
- Carregar inimigos e objetos.
- Depositar inimigos na prisão.
- Depositar itens recuperados no cofre.
- Ativar armadilhas e dispositivos de cenário.
- Usar power-ups.
- Consultar minimapa e câmeras de segurança.

### Pressão de vitória e derrota

O desempenho do jogador é medido pelo quanto ele consegue proteger durante o assalto. A missão não depende apenas de vencer ou perder, mas de classificação:

- Quantos inimigos foram capturados.
- Quantos itens de desejo foram salvos.
- Quanto tempo restou ou quanto tempo foi gasto.
- Quantos inimigos escaparam.
- Quantos itens foram roubados.

> [!IMPORTANT]
> O jogo deve evitar uma leitura binária simples de sucesso ou fracasso. A classificação por letras cria espaço para replay, domínio de rotas, otimização e progressão.

## Loop principal

:::flow
1. O jogador escolhe uma missão no mapa/HUB de Larópolis.
2. O jogo apresenta o cenário, pontos de entrada, saídas, itens valiosos, armadilhas e novidades da fase.
3. Começa a **Preparação**: por uma duração fixa configurada pela missão, os Players escolhem onde proteger o objetivo principal entre os cofres válidos. O tempo representa a espera pela chegada dos larápios.
4. Os larápios invadem com conhecimento incompleto: sabem qual é o objetivo principal e possuem informações aproximadas sobre possíveis locais e pontos relevantes, mas não conhecem as decisões tomadas pelo jogador.
5. Os inimigos investigam o cenário, descobrem proteções e oportunidades, procuram o objetivo principal e podem roubar valores secundários durante o caminho.
6. O jogador manipula informação, protege bens, persegue, recupera itens, captura larápios e usa ferramentas do ambiente.
7. Quando o objetivo principal é localizado, essa descoberta torna-se informação global. Cada arquétipo reavalia seus Desejos conforme sua configuração: alguns podem disputar ou retirar o objetivo, enquanto outros continuam buscando valores secundários.
8. Quando o **Tempo de Assalto** termina, começa o **Tempo de Fuga** e o contexto global muda para **Fuga Final**. Os larápios reavaliam seus desejos e passam a operar com as prioridades configuradas para esse contexto.
9. Durante o Tempo de Fuga, o jogador recebe a última oportunidade de perseguir, recuperar itens, incapacitar e deter larápios antes que escapem.
10. Quando o Tempo de Fuga termina, o gameplay é encerrado. Larápios ainda presentes recebem a resolução **Não escapou**; itens que ainda permanecem no cenário são considerados recuperados.
11. O jogo muda de atividade para a tela de resultados, onde calcula classificação, créditos e progresso.
:::

### Estrutura de uma missão

| Etapa | Função |
|---|---|
| Apresentação do cenário | Câmera mostra partes importantes: início, entradas, saídas, power-ups, itens de valor e armadilhas. |
| Preparação | Durante uma duração fixa da missão, os Players escolhem o cofre que protegerá o objetivo principal; o assalto começa quando o tempo acaba. |
| Entrada dos larápios | Splash screen apresenta as gangues inimigas, representantes e indicação de dificuldade. |
| Invasão por Spawn Points | Cada Spawn Point executa sua própria receita temporal, definindo quando, quantos e quais arquétipos de uma gangue entram no cenário. |
| Assalto ativo | Usa um **Tempo de Assalto** configurado por partida. Captura, roubo, investigação e recuperação acontecem normalmente; a Janela de Invasão ocupa seu período inicial. |
| Fuga final | Ao terminar o Tempo de Assalto, inicia um **Tempo de Fuga** também configurado por partida. O contexto global muda para Fuga Final e os larápios reavaliam seus desejos. |
| Encerramento | Ao terminar o Tempo de Fuga, o gameplay é encerrado, os estados restantes são resolvidos e o jogo muda de atividade para a tela de resultados. |

:::decision
**Decisão:** `Assalto` é a unidade principal de gameplay; `Missão` é o contrato escolhido no mapa.

**Consequência:** documentação técnica, UI e narrativa usam os termos com esses significados.
:::

## Preparação do assalto

:::decision
**Decisão:** a Preparação tem duração fixa configurável por missão e termina quando o tempo acaba. Ela representa narrativamente o tempo até a chegada dos larápios. No gameplay normal, não há encerramento antecipado nem confirmação de `Pronto`; antecipar o fim é permitido apenas por ferramenta de debug.

A ação central da Preparação inicial é escolher em qual cofre válido proteger o objetivo principal. No primeiro MVP há pelo menos dois cofres válidos, um deles selecionado como padrão. A escolha não tem custo; orçamento, investimentos e outras preparações ficam para etapas posteriores.

Em cooperativo, ambos os Players podem alterar a seleção. A última escolha válida prevalece e o estado selecionado é comunicado aos dois Players, sem revelar automaticamente a localização aos larápios. A missão pode definir alternativas adicionais no jogo completo, inclusive iniciar o objetivo em posse do Player.

A Preparação e as receitas dos Spawn Points são sistemas temporais distintos. O tempo das receitas começa no início do Assalto Ativo, não durante a Preparação.
:::

### Objetivo principal e valores secundários

:::decision
**Decisão:** o assalto distingue um **objetivo principal** de **valores secundários roubáveis**.

Os larápios sabem qual é o objetivo principal da vez, mas não conhecem automaticamente sua localização. No primeiro MVP, o objetivo começa em um de pelo menos dois cofres válidos, nunca no inventário do Player; um cofre padrão válido já está selecionado ao iniciar a Preparação. Os larápios podem conhecer os cofres candidatos e precisam investigar para reduzir a incerteza. Missões futuras podem definir outras posições iniciais, inclusive a posse do Player.

O jogador pode usar essa incerteza estrategicamente. O objetivo pode estar protegido em um cofre válido ou, quando a missão permitir, permanecer em posse do jogador. Cofres vazios, valores secundários e defesas preparadas podem funcionar como distração ou blefe.

Abrir todos os cofres não é uma regra obrigatória. Investigar cofres é apenas uma das maneiras possíveis de obter informação. Capacidades próprias e soluções do cenário — como terminais, energia, ferramentas ou outras interações — podem permitir descobrir ou superar proteções por caminhos diferentes.
:::

### Planejamento inimigo por GOAP

:::decision
**Decisão:** somente os larápios usam **GOAP — Goal-Oriented Action Planning**, desde o primeiro corte do MVP. Rick e Petra são controlados pelos Players; não recebem uma IA nem uma máquina de estados substituta do GOAP.

O sistema segue um princípio de **modularidade por padrão, especificidade quando necessária**. Desejos, Actions, arquétipos, capacidades, modificadores, reservas, interrupções, conhecimento e parâmetros associados devem ser dados independentes e componíveis sempre que isso ampliar reutilização, balanceamento e variedade sem prejudicar legibilidade, determinismo, desempenho ou manutenção. Planejamento e execução são fases separadas. O GDD define contratos de design, não obriga uma estrutura técnica específica de Unity.

A composição de gameplay respeita a separação entre **Actor**, suas **mecânicas** e o **arquétipo**. O Actor é a entidade/ocorrência sobre a qual o gameplay opera; sua composição concreta determina quais mecânicas existem. O arquétipo não materializa, adiciona nem remove componentes ou mecânicas da ocorrência. Ele configura e especializa as possibilidades oferecidas pelas mecânicas já presentes, habilitando, restringindo ou modificando capacidades, Actions, Desejos, parâmetros e modificadores.

Essa fronteira preserva a diretriz arquitetural do Immersive Framework: identidade, ocorrência, composição física e lifecycle do Actor não são consequência de configuração de gameplay. Em PEGA, o arquétipo é inicialmente uma abstração do jogo; contratos só devem ser promovidos ao framework quando demonstrarem utilidade independente do GOAP e das necessidades específicas de PEGA.

A relação conceitual é:

**Actor → composição de mecânicas → configuração de arquétipo → capacidades e decisões efetivas**

Exemplos: possuir `Hack N3` não cria a mecânica de interação; habilita/especializa soluções que usam uma interação já existente. Da mesma forma, possuir Desejos não cria o planner GOAP; um agente que utiliza GOAP já possui essa mecânica/sistema decisório, e o arquétipo fornece sua configuração.

A cadeia causal do planejamento é:

**Contexto global → composição mecânica do Actor + configuração do arquétipo → conhecimento atual → Desejos aplicáveis → Actions e Targets conhecidos compatíveis → planos válidos → peso efetivo + custo temporal → escolha → execução → Effects/conhecimento → reavaliação quando algo relevante muda**

As responsabilidades são separadas:

- **Desejo (Goal):** declara intenção, aplicabilidade e predicados de conclusão; não prescreve uma sequência fixa.
- **Contexto de Desejos:** seleciona a configuração de Desejos disponível ao arquétipo em uma etapa macro do assalto.
- **Conhecimento:** representa fatos conhecidos no escopo Individual, Gangue ou Global; o planner nunca consulta estado real oculto para obter vantagem. Percepção individual não se torna automaticamente conhecimento compartilhado.
- **Mecânicas do Actor:** são sistemas presentes na composição concreta da ocorrência — como movimento, percepção, interação, confronto ou decisão — e fornecem a infraestrutura pela qual o gameplay é executado; nem todo Actor precisa possuir todas as mecânicas.
- **Arquétipo:** é a composição/configuração mecânica e decisória aplicada sobre essas mecânicas existentes; não é identidade, ocorrência, lifecycle nem autoridade de materialização do Actor.
- **Capacidades:** especializam o repertório disponível ao agente, desbloqueando quais soluções/Actions suas mecânicas existentes conseguem executar e podendo modificar seus parâmetros.
- **Actions:** transformam estados através de precondições e Effects e podem ser encadeadas para satisfazer qualquer Desejo compatível.
- **Targets:** são resolvidos em runtime entre candidatos conhecidos e compatíveis; não são escolhidos por uma heurística separada do plano.
- **Modificadores:** alteram valores por estágios globais de resolução, a partir de arquétipo, Action, alvo, estado, Contexto ou efeitos especiais. A composição é determinística; modificadores da mesma categoria são independentes da ordem quando a operação permitir. A fórmula definitiva permanece configurável e não é fixada nesta versão.

Métodos diferentes para obter o mesmo resultado são **Actions diferentes**. 'Usar Chave', 'Lockpick', 'Hackear' e 'Arrombar', por exemplo, podem produzir o mesmo estado 'Porta Aberta' com capacidades, precondições, duração, reserva e interrupções próprias. Adicionar uma nova Action pode criar novas soluções para Desejos existentes sem alterar esses Desejos.

Precondições representam estados que o plano pode tentar produzir; capacidades ausentes tornam a Action indisponível para aquele agente. Capacidades podem possuir níveis. A composição entre **dificuldade/requisito do alvo + nível/capacidade do agente + modificadores aplicáveis** resolve se a Action está disponível e qual sua duração. Níveis superiores podem tanto desbloquear interações mais difíceis quanto executá-las mais rapidamente quando configurado.

Desejos e condições podem usar **predicados componíveis** (por exemplo AND, OR e NOT) sobre a fonte de dados apropriada. Todo predicado precisa deixar claro se consulta conhecimento, World State real, capacidade ou outro domínio. O planner normalmente trabalha sobre o **Knowledge State**; a execução física valida o **World State real**.

O planner pode construir cadeias com quantas Actions forem necessárias ao design. Não existe limite fixo de tamanho de plano no GDD. A implementação pode usar um orçamento de busca configurável por performance e deve distinguir em debug **sem solução conhecida** de **busca esgotada antes de encontrar solução**.
:::

### Resolução de modificadores

Valores sujeitos a modificadores passam por estágios globais de resolução, em ordem definida pelo contrato da propriedade. Dentro de uma mesma categoria, a composição deve ser independente da ordem quando matematicamente possível. A configuração identifica categoria, escopo e valor de cada modificador. Nenhuma fórmula global de empilhamento, limite ou precedência entre categorias é imposta nesta versão; esses parâmetros devem ser explícitos e determinísticos quando uma mecânica os exigir.

### Peso, oportunidade e custo dos planos

:::decision
**Decisão:** **peso do Desejo e custo do plano são dimensões independentes que participam conjuntamente da escolha**. Um Desejo de peso menor pode vencer quando possuir uma oportunidade muito mais barata, permitindo comportamentos oportunistas como roubar rapidamente um valor secundário acessível.

No MVP, a unidade-base de custo é **tempo estimado em segundos**:

**Custo do plano = tempo estimado de deslocamento + soma das durações resolvidas das Actions**

A 'Duration' é custo intrínseco/resolvido da Action; deslocamento é contextual e calculado pelo planejamento/navegação. A mesma Action sobre o mesmo Target pode portanto ter custo diferente conforme posição, rota, capacidade e modificadores do agente.

O GDD **não fixa uma fórmula matemática** entre peso e custo. A relação deve permanecer configurável para balanceamento de implementação. A arquitetura-alvo também aceita risco, perigo e probabilidade como dimensões futuras, sem incorporá-las ao primeiro corte do MVP. RNG não é desempate padrão; só participa quando uma mecânica probabilística estiver explicitamente configurada. Empates do MVP são determinísticos.

O tempo restante do Assalto/Fuga **não invalida automaticamente** um plano apenas porque seu custo estimado ultrapassa a janela disponível. A pressão temporal emerge do custo, dos Contextos e dos pesos configurados. Quando uma mudança relevante de conhecimento ou estado altera uma rota, requisito ou oportunidade, os planos afetados são reconstruídos/reavaliados e seus custos recalculados.

Cada combinação válida de **Action + Target conhecido** pode originar alternativas de plano. O Target escolhido é consequência da avaliação do plano completo, não de uma regra isolada como “sempre escolher o mais próximo”.

Necessidades intermediárias normalmente são Actions/estados dentro da cadeia, não novos Desejos de alto nível. O sistema preserva **resultados de Actions no estado**, não planos anteriores: ao replanejar, Effects já concluídos podem ser naturalmente aproveitados por qualquer novo plano.
:::

### Contrato de Actions

:::decision
**Decisão:** uma Action é uma unidade modular de solução. Conforme sua necessidade, declara **precondições, Effects de conclusão, Target compatível, Duration, capacidades requeridas, reserva, condições de interrupção e modificadores**. Planejamento seleciona a Action e seus Targets; execução é um lifecycle separado, com início, manutenção, conclusão ou término explícito.

- Preconditions necessárias devem continuar válidas durante a execução. Se deixarem de ser válidas, a Action torna-se 'Invalid', é cancelada e perde o progresso.
- Effects declarativos do GOAP são aplicados **somente na conclusão bem-sucedida**. Progresso parcial não produz o Effect final.
- Comportamentos que realmente produzem efeitos durante execução devem declarar isso explicitamente, sem confundi-los com o Effect de conclusão usado pelo planner.
- Uma Action determinística não possui falha aleatória genérica. Seus resultados básicos são **Success**, **Invalid**, **Interrupted** ou **Cancelled**. Probabilidade só existe quando configurada explicitamente por uma mecânica.
- `Success` aplica Effects; `Invalid` indica que realidade, precondições ou Target já não permitem a Action; `Interrupted` indica que um evento configurado interrompeu uma Action válida; `Cancelled` indica que o plano ou proprietário encerrou explicitamente a execução.
- Invalidar, interromper ou cancelar encerra a execução e descarta seu progresso. Qualquer reserva adquirida pela Action é liberada em todo caminho de término. Efeitos durante execução só ocorrem quando declarados como tais; Effects de conclusão continuam exclusivos de `Success`.
- Planejamento usa conhecimento; execução valida realidade. Um plano pode estar correto para o conhecimento do larápio e errado diante do mundo real. Ao descobrir a divergência, o conhecimento é atualizado e os sistemas afetados reavaliam.

Effects devem ser modulares quando houver benefício de reutilização/composição, mas lógica específica é válida quando generalizá-la não traz vantagem. **Modularidade é ferramenta, não obrigação de transformar todo comportamento em um sistema genérico.**
:::

### Contextos e satisfação dos Desejos

:::decision
**Decisão:** existe exatamente **um Contexto de Desejos global ativo por vez**. Para a arquitetura atual, os contextos são exclusivos e seguem uma progressão simples:

**Normal → Item Principal em Jogo → Fuga Final**

A descoberta do objetivo principal é um evento global e irreversível naquele assalto: todos os IAs passam para **Item Principal em Jogo**. Perder o item, trocar seu portador ou perder sua posição não retorna o contexto para Normal. O início do Tempo de Fuga é o segundo evento global especial e muda todos para **Fuga Final**.

O Contexto é global, mas os desejos são configurados por **arquétipo**. O mesmo contexto pode fazer um arquétipo priorizar recuperação/confronto enquanto outro continua valorizando itens secundários. A Fuga Final não apaga o GOAP nem os demais desejos: ela troca o conjunto/pesos configurados para esse contexto, e fugir continua exigindo planejamento, rotas e solução de obstáculos.

Larápios criados posteriormente recebem imediatamente o Contexto global vigente e o conhecimento atual compartilhado de sua gangue; não reproduzem transições anteriores.

Cada Desejo declara, no mínimo, **identificador, Contexto, peso base, condições de aplicabilidade e condição de satisfação**. O Desejo não contém uma sequência fixa de Actions.

Desejos podem ter predicados de conclusão compostos; qualquer cadeia de Actions que produza o estado exigido pode satisfazê-los. O Desejo não precisa conhecer qual caminho foi usado para chegar à conclusão.

Existe ainda uma **camada individual de Contingência**, sem criar um novo Contexto global. Se nenhum Desejo normal do Contexto vigente possuir plano válido, tornam-se elegíveis Desejos de Contingência configurados para o arquétipo, como 'Vagar', 'Esconder-se', 'Manter Distância', 'Reagrupar' ou 'Esperar'. Eles usam exatamente o mesmo sistema de peso, custo, satisfação, predicados e Actions. Assim que qualquer Desejo normal volta a possuir plano válido, a contingência deixa de ser elegível. Se nem mesmo um Desejo de Contingência possuir plano, a IA entra em **Idle técnico** como proteção operacional, não como intenção de gameplay.

Um Desejo pode ser aplicável e ainda assim não participar da escolha se nenhum plano válido puder ser construído com o conhecimento e as Actions disponíveis. Novas informações ou mudanças de estado podem torná-lo planejável novamente.

Desejos podem ser reexecutáveis. Cada larápio mantém uma **Satisfação individual acumulada** por Desejo durante todo o assalto, inclusive através de mudanças de Contexto. A satisfação só aumenta quando uma Action concluída realmente produz um estado que satisfaz o Desejo; tentativas, planos abandonados e Actions interrompidas não contam.

O ganho de satisfação é configurável e não precisa ser sempre +1. Uma mesma Action concluída pode satisfazer vários Desejos se seu resultado cumprir as condições de todos eles. Perder posteriormente a condição que produziu a satisfação não reduz o histórico acumulado.

Cada Desejo pode configurar como a satisfação modifica seu peso e quantas **execuções concluídas** são permitidas: execução única, quantidade limitada ou repetição sem limite. Não existe necessidade de uma satisfação máxima global. Por padrão a satisfação é individual; satisfação compartilhada por gangue fica reservada para avaliação futura.
:::

### Replanejamento, tempo e execução

:::decision
**Decisão:** o GOAP é **event-driven**. Não existe polling periódico de decisão como regra de gameplay.

Um larápio reavalia Desejos e plano somente quando ocorre uma mudança determinística relevante em conhecimento, World State conhecido, capacidades, reservas, Contexto ou outro dado que possa alterar sua decisão. Se as entradas relevantes não mudaram, não há motivo de gameplay para recalcular o mesmo problema.

Passagem de tempo também não exige polling. Marcos temporais são **eventos agendados determinísticos**: conclusão de Action, fim de espera, recuperação de incapacitação, spawn absoluto, fim do Tempo de Assalto, início/fim do Tempo de Fuga e equivalentes. Quando o marco é atingido, o evento altera dados e provoca reavaliação apenas onde for relevante.

Há dois eventos globais especiais já definidos que alcançam todos os IAs: **descoberta do objetivo principal** e **início da Fuga Final**.

**Reavaliar não significa resetar.** Quando uma mudança relevante ocorre:

- se a mesma Action temporizada continua válida e permanece selecionada, mantém o progresso acumulado;
- se o plano/Desejo muda e a Action é abandonada, seu progresso é perdido;
- se uma precondição necessária ou Target deixa de ser válido, a Action torna-se 'Invalid', cancela e perde progresso;
- se um evento listado em sua configuração de interrupção ocorrer, torna-se 'Interrupted', cancela e perde progresso;
- se a mesma Action voltar a ser necessária posteriormente, começa novamente do zero;
- ao concluir com 'Success', aplica seus Effects, atualiza os dados afetados, atualiza satisfação quando cabível e dispara a reavaliação decorrente dessas mudanças.

Interrupções são configuráveis por Action/composição. **Invalidação lógica por perda de precondição permanece distinta de interrupção por evento.**

O sistema deve permitir rastrear em debug a cadeia **evento → dado alterado → decisão afetada → reavaliação → manutenção ou troca de plano**.
:::

### Reservas como dados de coordenação

:::decision
**Decisão:** reserva também segue o princípio modular. Uma Action pode declarar se necessita reserva e qual **recurso/chave lógica** precisa reservar; a reserva não é apenas um booleano associado à identidade da Action.

Actions diferentes podem disputar o mesmo recurso quando suas execuções forem incompatíveis. 'Lockpick Cofre' e 'Hackear Cofre', por exemplo, podem declarar exclusividade sobre o mesmo 'Cofre X'. Operações compatíveis não adquirem reserva exclusiva automaticamente.

A reserva é adquirida somente quando o larápio está em condição de **iniciar** a Action reservável, nunca apenas por ter escolhido um plano. Vários membros podem navegar para a mesma oportunidade; quem inicia primeiro adquire a reserva e a mudança de estado faz os demais reavaliarem.

Uma reserva declara recurso, política de exclusividade e escopo; o padrão de escopo para coordenação entre larápios é a **Gangue**. A reserva pertence à instância da Action em execução e ao larápio que a iniciou. Gangues rivais mantêm reservas independentes e podem agir simultaneamente sobre o mesmo recurso. Se uma concluir primeiro e alterar o World State, a Action rival é invalidada quando suas precondições deixarem de valer.

Ao concluir, cancelar, invalidar ou interromper a Action, seu proprietário libera a reserva. Progresso é individual, não é transferido e múltiplos larápios não somam progresso. A arquitetura pode aceitar outras políticas de reserva quando houver necessidade de design.
:::

### Áreas, POIs e investigação

:::decision
**Decisão:** o cenário é organizado em **Áreas** que funcionam como unidades de exploração e em **POIs (Points of Interest)** relevantes ao planejamento, como cofres, terminais, chaves e portas especiais.

Cofres candidatos são POIs conhecidos inicialmente. A invasão pode começar tentando alcançar e investigar um desses pontos. Se o objetivo não for encontrado ou uma solução necessária não for conhecida, o GOAP pode escolher outra oportunidade conhecida ou investigar uma Área ainda não explorada.

Existem duas formas principais de adquirir informação:

- **Aquisição visual passiva:** durante deslocamentos e outras Actions, a Área de Visão registra automaticamente informação visível e relevante, sem custo adicional de tempo.
- **Investigação ativa de Área:** quando o plano necessita de informação ainda desconhecida, o larápio pode navegar até uma Área e executar uma investigação temporizada. A animação pode representar procura física, mas mecanicamente basta permanecer na Área durante o tempo configurado. Ao concluir, a informação compatível é adquirida e compartilhada com a gangue.

Uma investigação comum não esgota necessariamente toda a informação existente. Conteúdo especializado só é revelado quando o arquétipo possui a capacidade apropriada. Assim, uma Área já investigada por um larápio comum pode continuar oferecendo uma oportunidade relevante para um especialista.

O especialista pode saber que existe naquela Área uma **oportunidade compatível com sua capacidade** sem conhecer antecipadamente o conteúdo oculto que será revelado pela Action.
:::

## Narrativa e mundo

### Premissa

Larópolis é uma cidade fictícia dominada por crimes cartunescos. O setor de segurança cresce porque comerciantes e instituições precisam proteger seus bens. Rick e Petra, impedidos de entrar na PGA por falta de experiência, veem nessa crise uma oportunidade de provar valor. A primeira grande missão descrita no GDD é proteger um baú de barras de ouro em um armazém.

### Larópolis

Larópolis deve funcionar como um mundo urbano colorido, exagerado e reconhecível. O tom é de desenho animado: gangues têm temas visuais fortes, comportamentos teatrais e métodos de roubo absurdos. A polícia oficial/P.G.A. participa da narrativa e das transições, mas não atua como NPC policial durante os assaltos. A entrega na prisão é parte da resolução de gameplay do PEGA.

### HUB

O HUB organiza contratos e progressão na cidade de Larópolis. A Demo usa o escritório da PEGA como uma tela temática estática com hotspots para o mapa de missões e a carreira. A forma do HUB após a Demo permanece em aberto. O HUB deve permitir:

- Selecionar fases liberadas.
- Visualizar progressão.
- Retornar ao menu inicial.
- Esconder conteúdos futuros até o jogador cumprir requisitos.

## Personagens principais

> [!INFO]
> A documentação visual dos protagonistas, gangues e conceitos de personagem fica no [Art Book](ArtBook.md#personagens-principais).

### Rick Ronda

Rick é um jovem aspirante à PGA Larópolis. Depois de ser reprovado, funda a PEGA como alternativa para ganhar experiência e provar competência. Sua imagem deve comunicar iniciativa, improviso e energia.

### Petra Patrol

Petra é parceira de Rick e cofundadora da PEGA. Ela é jogável e mecanicamente equivalente a Rick na Demo; sua identidade visual é própria. Diferenças mecânicas posteriores dependem de playtests e decisão futura.

### NPCs

| Categoria | Função |
|---|---|
| Larapolitanos | NPCs civis ou figurantes que dão vida aos cenários. |
| Larápios | Inimigos principais, organizados em gangues temáticas. |
| Polícia oficial / P.G.A. | Participação narrativa e nas transições; não é NPC durante os assaltos. |
| Clientes | Donos ou responsáveis pelos locais protegidos, úteis para briefing narrativo. |

## Controles

O GDD original usa o padrão Microsoft de controle, em que o botão sul é `A`, o botão leste é `B`, o botão oeste é `X` e `Start` pausa o jogo.

| Ação | Entrada | Descrição |
|---|---|---|
| Mover | Eixo esquerdo ou D-Pad | Movimento em todas as direções no solo. |
| Esquivar | Botão a definir no mapeamento final | Deslocamento curto e rápido com breve janela de invulnerabilidade e cooldown. |
| Saltar | Botão a definir no mapeamento final | Salto livre usado para mobilidade e para evitar ameaças explicitamente compatíveis com salto. |
| Interagir / atacar | Botão contextual (`B`) | Interage com alvo válido ou ataca quando não houver alvo interativo. |
| Soltar itens | Comando próprio a definir | Solta simultaneamente todos os itens carregados. |
| Confirmar | Botão sul (`A`) | Confirmação em menus. |
| Pausar | `Start` | Abre ou fecha menu de pausa. |

### Movimento

:::decision
**Decisão:** o jogador não possui estados separados de andar e correr. Seu movimento-base já representa a corrida de gameplay e possui velocidade definida pela mecânica de movimento do Actor, com modificadores configurados quando aplicáveis.

Larápios possuem pelo menos dois regimes de velocidade: **velocidade de movimentação**, usada durante navegação e investigação, e **velocidade de perseguição**, usada quando sua intenção exige perseguir um portador ou fugir com um objetivo. Esses valores pertencem à mecânica de movimento do Actor e podem ser especializados pela configuração de arquétipo e por modificadores. Não existe atributo universal de Agilidade.
:::

### Esquiva

:::decision
**Decisão:** a esquiva é um deslocamento curto e rápido na direção escolhida, acompanhado de uma breve janela de invulnerabilidade. Após o uso, entra em cooldown e não pode ser repetida até ficar novamente disponível.

A esquiva não altera permanentemente a velocidade-base e não depende de Vigor ou stamina.
:::

### Salto

:::decision
**Decisão:** o jogador possui salto livre. O salto é uma ferramenta de mobilidade e não concede invulnerabilidade.

Ameaças explicitamente compatíveis podem ser evitadas pelo estado aéreo ou pela posição física do personagem — por exemplo, obstáculos baixos, ataques rasteiros ou armadilhas de solo. Ataques que alcançam o personagem no ar continuam válidos.

O salto livre pode superar geometria e pequenas diferenças físicas compatíveis. Mudanças de **nível estrutural** do cenário, como atravessar uma janela para outro nível, usam transposições/interações especiais configuradas pelo cenário em vez de depender automaticamente do salto livre.
:::

:::risk
**Risco:** salto e diferenças de altura podem gerar problemas de leitura, colisão e navegação.

**Mitigação:** prototipar cedo a leitura visual do salto e separar claramente altura física local de mudanças de nível estrutural.
:::

### Interação

A interação depende do objeto diretamente à frente do personagem e dentro de sua área de ação. Se houver mais de um objeto interativo, a prioridade vai para o objeto mais próximo, indicado visualmente por linha, contorno ou efeito.

- Interagir com inimigo executa ação conforme tipo e estado do inimigo.
- Interagir com objeto segurável pega ou manipula o objeto, respeitando a capacidade disponível.
- Interagir com armazenamento compatível transfere itens conforme as regras de depósito.
- Sem objeto válido, a interação vira ataque.
- Se o personagem estiver carregando outro personagem, a interação solta o carregado.
- Cada interação tem cadência base de `1` segundo.

### Inventário, transferência e soltura de itens

:::decision
**Decisão:** itens de desejo **nunca são destruídos nem danificados**, em nenhum momento. Eles só mudam de posse ou de lugar: podem ser segurados, soltos, guardados, roubados e recuperados.

- **Capacidade:** a capacidade é configurada pela mecânica de inventário do Actor e pode variar por tipo de Actor. Não há atributo universal obrigatório. Cada item ocupa uma unidade inteira.
- **Segurar:** pegar um item o coloca no inventário se houver capacidade disponível.
- **Soltura voluntária:** um comando próprio, separado de interagir/atacar, solta simultaneamente todos os itens do inventário no chão. Não há seleção individual nem arremesso.
- **Depósito:** uma interação com armazenamento compatível transfere todos os itens elegíveis. Cofres e armários compartilham regras básicas de transferência, mas mantêm funções distintas. Armários não concedem bônus de proteção por padrão; o primeiro MVP pode implementar apenas cofres.
- **Capacidade insuficiente no armazenamento:** o depósito é parcial e determinístico. Os itens elegíveis que couberem são transferidos; os excedentes permanecem no inventário. A ordem inicial usa a ordem estável do inventário e pode ser substituída após playtests.
- **Ataque:** um ataque válido faz o personagem atingido derrubar **todos** os itens que carrega (ver seção "Posse, desejo e inversão da perseguição").

O jogador pode pegar itens do chão e do armazenamento, soltá-los no chão e depositá-los. O armazém compatível define quais itens pode receber.

**Motivo:** a única perda possível de um item é ele sair do cenário com um larápio que escapou. Isso mantém todo item sempre disputável e simplifica pontuação, feedback e comportamento da IA.
:::

### Restrições ao transportar larápios

:::decision
**Decisão:** transportar um larápio é uma dificuldade baseada em restrição de ações, não em redução de velocidade.

Enquanto carrega um larápio incapacitado, o jogador mantém sua velocidade normal. Os itens que já estavam em seu inventário permanecem lá, mas sua manipulação fica bloqueada. O larápio transportado não ocupa capacidade de inventário. O Player **não pode atacar, saltar, esquivar ou usar interações comuns**; pode continuar se movendo, soltar o larápio e concluir a detenção na área apropriada.

Essa restrição cria custo de oportunidade: durante o transporte, o jogador fica menos capaz de responder aos demais larápios, e no cooperativo o segundo jogador pode assumir proteção e interceptação.
:::

## Câmera e apresentação

A câmera usa uma visão elevada oblíqua, próxima de um top-down inclinado, fixa em distância determinada e acompanha o personagem. Em multiplayer local, a tela é dividida horizontalmente. A apresentação deve manter leitura clara de:

- Rotas e obstáculos.
- Inimigos próximos.
- Itens roubáveis.
- Armadilhas e dispositivos.
- Estado do jogador.
- Direção de perseguição.

:::requirement
**Requisito:** em tela dividida, HUD, minimapa e ícones precisam ser legíveis para os dois jogadores.

**Critério de aceitação:** nenhum elemento crítico pode depender de leitura em tamanho pequeno demais ou de informação que apareça apenas na metade da tela do outro jogador.
:::

## Cenários

> [!INFO]
> A documentação visual dos cenários, incluindo espaço para concepts e modelos futuros, fica no [Art Book](ArtBook.md#cenários).

### Função dos cenários

Cada cenário é uma arena de perseguição com identidade própria. Um bom cenário de PEGA precisa combinar:

- Itens de alto valor.
- Rotas de entrada e saída dos larápios.
- Portas, corredores, salas e gargalos.
- Armadilhas comuns e específicas.
- Cofre para itens recuperados.
- Prisão ou área de contenção.
- Sala de controle com câmeras.
- Objetos seguráveis.
- Pontos de power up.
- Elementos de mobilidade e bloqueio.
- Áreas de exploração e POIs relevantes ao GOAP.
- Spawn Points seguros com receitas independentes de invasão.

### Armazém

O armazém é o cenário inicial mais detalhado no GDD. Ele funciona como centro de distribuição com caixas, malotes, prédio administrativo, doca de carregamento e pátio de veículos.

| Elemento | Uso de gameplay |
|---|---|
| Caixas e corredores | Criam labirinto, bloqueios e oportunidades de esconderijo. |
| Baú de ouro | Item de desejo principal da primeira missão. |
| Doca de carregamento | Área natural para entrada, saída ou fuga. |
| Pátio de veículos | Espaço para carrinhos motorizados e rotas externas. |
| Prédio administrativo | Pode concentrar sala de controle, portas e câmeras. |
| Cofre | Local para depositar itens recuperados. |
| Prisão | Local para depositar inimigos capturados. |

O Armazém é o cenário inicial de referência. O recorte técnico e de conteúdo de cada entrega está definido em [MVP](MVP.md), sem tornar esta ficha uma afirmação de que o cenário já foi implementado.

### Catálogo de cenários

O GDD original sugere múltiplos locais de Larópolis. Para organização de produção, cada cenário futuro deve ser documentado com a mesma estrutura: tema, itens de desejo, gangue dominante, armadilhas específicas, gimmick principal e requisito de desbloqueio.

## Sistemas do jogador

### Propriedades de gameplay

PEGA não usa um modelo tradicional de pontos de vida e dano acumulativo como base para incapacitação. O confronto serve à perseguição: criar ou aproveitar uma oportunidade para impedir temporariamente o alvo e concluir sua captura.

| Propriedade | Função |
|---|---|
| Capacidade de ataque | Valor atual usado para determinar se um ataque pode incapacitar o alvo. Pode receber vantagens temporárias de itens, power-ups ou outras condições. |
| Resistência | Limiar atual necessário para incapacitar um ator. Deve ser legível antes do confronto e pode ser reduzido temporariamente por armadilhas, objetos ou condições do cenário. |

:::decision
**Decisão:** `Capacidade de ataque` e `Resistência` são propriedades de confronto, não barras de dano. Se a Capacidade de ataque do agressor for igual ou superior à Resistência atual do alvo, um ataque válido o incapacita. Se for inferior, o ataque não acumula dano nem reduz Resistência por si só.

A Resistência deve possuir feedback visual legível — barra, segmentos, ícones ou solução equivalente — para que o jogador possa reconhecer antecipadamente se possui capacidade para incapacitar aquele alvo.
:::

:::decision
**Decisão:** movimento não depende de uma ficha universal de atributos. Velocidade pertence à mecânica de movimento do Actor e é especializada por arquétipos e modificadores. Distância de esquiva, janela de invulnerabilidade, cooldown, parâmetros de salto e demais valores necessários são configurações diretas das respectivas mecânicas.
:::

### Estados gerais

| Estado | Descrição |
|---|---|
| Normal | Personagem livre para se mover e agir. |
| Carregando | Personagem segura item ou outro personagem. |
| Caído / Incapacitado | Larápio temporariamente incapaz de agir após ser neutralizado. Um temporizador de recuperação começa; enquanto estiver nesse estado, ele pode ser recolhido pelo jogador, mas ainda não está capturado. |
| Em transporte | Larápio incapacitado sob custódia de um jogador e sendo levado à sala de detenção. |
| Detido / Capturado | Larápio entregue à sala de detenção. Sua captura está concluída e ele deixa definitivamente o assalto. |
| Escapou | Larápio que deixou o cenário por uma rota de fuga. Ele deixa definitivamente o assalto sem ser capturado. |
| Escondido | Personagem usa objeto ou cenário para ocultação. |
| Ocultado | Personagem não aparece em câmera, minimapa ou percepção por certo tempo. |
| Confrontando | Personagem usa confronto porque isso atende a um objetivo concreto, como recuperar um item desejado ou remover um bloqueio. |
| Fugindo | Personagem tenta escapar do cenário. |
| Furtando | Personagem busca item de desejo. |
| Perseguindo | Personagem segue alvo específico. |
| Escapando | Personagem tenta sair de perigo imediato. |

## Inimigos e gangues

> [!INFO]
> As fichas visuais, conceitos e observações de direção artística das gangues ficam no [Art Book](ArtBook.md#gangues-de-larópolis).

### Larápios

Os larápios são os principais inimigos de PEGA. Eles são organizados em gangues temáticas, cada uma com identidade visual, tática e humor próprios. O GDD descreve os larápios como especialistas em furto, capazes de se esconder, fugir, sabotar, atacar, roubar itens e reagir às ações dos jogadores.

### Grupos de inimigos

| Gangue | Tema | Leitura de gameplay |
|---|---|---|
| Os Trapalhões do Crime | Comédia caótica e crime clássico cartunesco. | Boa gangue inicial, com ladrões comuns e inimigos fisicamente mais fortes. |
| Os Incríveis Larápios | Circo, disfarce, ilusão e palhaçaria criminosa. | Foco em confusão visual, distração, disfarces e engano. |
| Os Sem Leis | Faroeste, cangaço, punk e banditismo organizado. | Foco em emboscadas, ataques à distância e inimigos mais agressivos. |
| Os Cyber Piratas | Piratas tecnológicos, drones e hacking. | Foco em câmeras, portas digitais, minimapa e sabotagem de sistemas. |
| Os Fantasmas da Noite | Monstros clássicos e terror cartunesco. | Foco em invisibilidade, medo, transformação e atravessar obstáculos. |
| A Tríade da Realeza Opulenta | Elegância criminosa, realeza e sofisticação. | Foco em inimigos refinados, técnicas especiais e padrões avançados. |

### Exemplos de arquétipos

| Arquétipo | Gangue | Função |
|---|---|---|
| Ladrões Listrados | Trapalhões do Crime | Inimigos comuns, simples e legíveis. |
| Ladrões Molhados | Trapalhões do Crime | Usam artimanhas e obstáculos. |
| Capangas Atrapalhados | Trapalhões do Crime | Criam caos e momentos cômicos. |
| Marombas Ameaçadores | Trapalhões do Crime | Inimigos de força bruta. |
| Trupe Trapatapa | Incríveis Larápios | Palhaços criminosos com distrações. |
| Mimiquetes | Incríveis Larápios | Mímicos e disfarces. |
| Foras da Lei | Sem Leis | Ataques à distância e emboscadas. |
| Drone Caravela | Cyber Piratas | Hacking e mobilidade aérea. |
| Capitão Hacker | Cyber Piratas | Líder com sabotagem tecnológica. |
| Fantasma Noturno | Fantasmas da Noite | Atravessa ou ignora barreiras. |
| Vampiros Pálidos | Fantasmas da Noite | Controle, sedução ou drenagem de energia. |
| Múmia Maldita | Fantasmas da Noite | Desorientação, faixas e terror. |

> [!BEST_PRACTICE]
> Cada gangue deve ter pelo menos um inimigo comum, um inimigo de suporte, um inimigo de pressão e um líder. Isso ajuda a criar variedade sem transformar cada fase em um conjunto imprevisível demais.

## Inteligência artificial

### Desejos e prioridades por arquétipo

Cada arquétipo possui Desejos, condições e prioridades próprias. Informação compartilhada não obriga todos os membros de uma gangue a reagirem da mesma forma.

Quando o objetivo principal é identificado pela primeira vez, **a descoberta de que ele entrou em jogo** torna-se um Evento Global e muda o Contexto para **Item Principal em Jogo**. Isso não cria rastreamento mágico permanente: posição e posse atuais continuam sendo fatos dinâmicos atualizados pelas fontes de conhecimento apropriadas. A reação depende do arquétipo; alguns larápios podem tentar retirar o objetivo do cenário, alguns podem persegui-lo ou disputá-lo e outros podem continuar roubando valores secundários.

Larápios de gangues rivais **só entram em confronto entre si por causa do objetivo principal**. Valores secundários, cofres, chaves, terminais e outras oportunidades podem gerar competição, mas não justificam por si só combate entre gangues.

### Percepção determinística

:::decision
**Decisão:** percepção básica é determinística e dividida em uma **Área Percebida radial** e um **cone frontal de Visão**, ambos com parâmetros configuráveis e bloqueados por obstáculos configurados.

- **Área Percebida:** permite detectar a presença de um ator ou elemento relevante no raio configurado ao redor do larápio, inclusive fora de sua visão frontal. Essa consciência não equivale a confirmação visual e não determina automaticamente a reação.
- **Cone de Visão:** fornece confirmação visual direta dentro de seu alcance e ângulo configurados e permite respostas mais imediatas.
- **Obstrução:** paredes e outros obstáculos configurados para bloquear percepção impedem essa consciência através deles.

A IA não utiliza rolagem de audição, medidor de ruído, porcentagem de detecção, barra genérica de suspeita ou atributo universal de Presença para perceber atores próximos. A Área Percebida substitui a necessidade de simular audição convencional.

Power-ups e itens exclusivos do jogador são irrelevantes para os larápios e são ignorados por sua tomada de decisão. Um fato percebido individualmente só se propaga quando sua política de Knowledge Data autorizar o escopo Gangue ou Global.

Perceber uma interação não significa conseguir executá-la. Portas, fechaduras, cofres e terminais podem ser conhecidos, mas só produzem Actions utilizáveis quando o arquétipo possui uma capacidade compatível.
:::

### Armadilhas: detecção e desarme

:::decision
**Decisão:** armadilhas são uma exceção à percepção comum. **Detectar Armadilhas** e **Desarmar Armadilhas** são capacidades separadas.

A detecção não é passiva. Um arquétipo com capacidade de detecção precisa escolher uma Action temporizada em um momento/POI previsto por seu comportamento. Ao concluir, revela armadilhas compatíveis de nível igual ou inferior. Não existe regra automática de um aliado cair em uma armadilha fazer todos procurarem armadilhas.

Uma armadilha revelada torna-se conhecimento da gangue. Outros membros podem evitá-la mesmo sem capacidade de detecção, mas precisam de sua própria capacidade de desarme compatível para desarmá-la.

A investigação normal de uma Área não substitui a Action especializada de detectar armadilhas.
:::

### Conhecimento modular, memória e validade da informação

:::decision
**Decisão:** conhecimento é um conjunto modular de **fatos independentes** sobre entidades, relações e estados. Diferentes fontes podem produzir ou atualizar fatos distintos sobre o mesmo Target; conhecer uma entidade não implica conhecer todas as suas propriedades.

A cadeia é:

**Fonte → informação/fato → política de resolução → Knowledge State atual → consumidores (GOAP, Actions, Desires, UI/debug ou outros sistemas)**

Fontes podem incluir percepção, visão, investigação, resultado de Action, compartilhamento autorizado da Gangue e Eventos Globais. Cada Knowledge Data define escopo, validade, atualização, invalidação, conflito e propagação. Percepção individual não se torna automaticamente conhecimento compartilhado. Produção da informação e consequências dessa informação permanecem desacopladas.

O sistema distingue:

- **World State real:** o que efetivamente acontece no cenário.
- **Knowledge State atual:** melhor representação atualmente conhecida pelo agente/gangue e usada pelo planner.
- **Histórico/estado acumulado:** preservado somente quando possui função de gameplay ou rastreio, como Satisfação; não existe obrigação de manter um log completo de tudo.

Knowledge Data pode representar fatos **positivos ou negativos**. 'Não sei se o objetivo está no Cofre A' é diferente de 'Sei que o objetivo não está no Cofre A'. Ausência de conhecimento não equivale a conhecimento de ausência.

Cada tipo de informação define sua própria política de **validade, atualização, invalidação, conflito e propagação**. Não existe esquecimento genérico. Conhecimento estrutural pode persistir por todo o assalto; informação dinâmica pode ficar desatualizada até surgir evidência nova.

Para estados atuais exclusivos, o padrão é manter a informação válida mais atual **sobre o fato**, não simplesmente a mensagem recebida por último. Uma informação recebida agora pode descrever uma observação antiga e não deve sobrescrever automaticamente uma observação mais recente do mesmo fato.

Informações podem carregar metadados de rastreio como **origem, observador/fonte, momento ou revisão do fato e escopo**. Esses metadados não precisam alterar a pontuação do GOAP no MVP, mas devem permitir explicar por que o agente acredita em determinado estado.

Conhecimentos podem se combinar por regras/predicados determinísticos explícitos para permitir novas conclusões ou possibilidades de planejamento. Não existe necessidade de um sistema genérico de inferência: o comportamento inteligente deve emergir da composição dos fatos, predicados, Actions, capacidades e Desejos.

O planner só resolve Targets a partir de candidatos conhecidos e compatíveis. A existência oculta de um objeto no cenário não pode ser descoberta pelo simples fato de o GOAP procurar uma solução.
:::

### Perda de alvo

:::decision
**Decisão:** ao perder a percepção de um alvo perseguido, o larápio guarda apenas sua **última posição conhecida**.

Ele segue até essa posição enquanto tenta readquirir o alvo pela percepção normal. Não há extrapolação de direção/velocidade, trilha, busca inteligente por salas vizinhas ou simulação de audição. Se o alvo não for readquirido, após o intervalo/reavaliação configurado ele é considerado **despistado** e o larápio retorna ao planejamento normal.
:::

### Conhecimento da gangue e informação global

:::decision
**Decisão:** **Gangue** é a unidade de cooperação e conhecimento. Não existe uma entidade separada de grupo de entrada.

Cada Knowledge Data possui política de escopo e propagação configurável. Fatos de escopo **Individual** não se propagam; dados de escopo **Gangue** seguem a política configurada, que pode ser imediata; fatos **Globais** são distribuídos às gangues explicitamente definidas. Um larápio criado posteriormente recebe somente os fatos que sua política de escopo lhe disponibilizar.

| Escopo | Regra |
|---|---|
| Individual | Informação mantida pelo agente conforme a política daquele dado; percepção individual permanece aqui salvo propagação explícita. |
| Gangue | Fatos propagados aos membros da mesma gangue conforme a política configurada para aquele dado. |
| Global | Fatos explicitamente distribuídos às gangues definidas, incluindo os Eventos Globais do assalto. |

Gangues diferentes mantêm Knowledge State e reservas independentes salvo dados explicitamente globais. Compartilhar informação não significa compartilhar Desejos nem produzir a mesma reação. Informação global dinâmica também pode posteriormente ser atualizada conforme sua política de validade.
:::

### Posse, desejo e inversão da perseguição

:::decision
**Decisão:** todo ataque válido contra um ator carregando itens faz **todos** esses itens caírem, independentemente de o ataque conseguir incapacitá-lo.

- Se Capacidade de ataque >= Resistência atual, o alvo larga os itens e fica incapacitado.
- Se Capacidade de ataque < Resistência atual, o alvo larga os itens, mas permanece ativo. O ataque não causa desgaste acumulativo de Resistência.
:::

Um larápio pode perceber que o jogador carrega **algum item** sem identificar imediatamente seu conteúdo. Quando o objetivo principal é exposto e identificado, essa informação torna-se global.

Antes da Fuga Final, a posse conhecida do objetivo principal pode alterar Desejos por arquétipo: o portador pode tentar retirá-lo, rivais configurados para disputá-lo podem persegui-lo e outros larápios podem continuar procurando valores secundários. Se o objetivo for derrubado ou mudar de portador e essa mudança entrar no conhecimento relevante, os planners afetados reavaliam o novo estado.

### Fuga e escolha de saída

:::decision
**Decisão:** uma gangue conhece como saída potencial toda entrada já utilizada por qualquer um de seus membros e toda saída adicional descoberta durante exploração.

Quando um larápio possui um Desejo de escapar, as saídas conhecidas e compatíveis entram como Targets candidatos. O GOAP avalia os planos completos e seus custos temporais; por isso, uma saída próxima tende a ser atraente, mas não existe uma heurística separada de **sempre escolher a mais próxima**.

Se uma rota ou saída conhecida estiver bloqueada, a nova informação atualiza o Knowledge State e os planos afetados são recalculados. Se nenhum plano de escape puder ser construído, esse Desejo deixa de participar da escolha naquele ciclo. **Investigar não é acionado automaticamente**: só ocorrerá se o próprio Desejo `Investigar` estiver elegível e produzir o melhor plano entre os Desejos normais; se nenhum Desejo normal tiver plano, aplicam-se os Desejos de Contingência.
:::

### Fuga Final

:::decision
**Decisão:** a **Fuga Final** é um Contexto de Desejos global iniciado quando termina o Tempo de Assalto e começa o Tempo de Fuga. Ela **não substitui o GOAP por uma ordem rígida de fugir**.

Cada arquétipo usa sua configuração de Desejos e pesos para esse Contexto. 'Escapar' tende a receber grande importância, mas outros Desejos — como recuperar o objetivo principal ou confrontar o jogador — podem permanecer prioritários quando configurados dessa forma.

Durante o Tempo de Fuga, os larápios continuam planejando e replanejando normalmente para resolver rotas, portas, itens perdidos, interferência do jogador e demais mudanças conhecidas.

Quando o **Tempo de Fuga termina**, o gameplay é encerrado imediatamente, independentemente de ainda existirem larápios ativos no cenário. Os remanescentes recebem a resolução **Não escapou**; somente quem atravessou uma saída válida antes desse marco recebe **Escapou**, e somente detenções efetivamente concluídas pelo jogador contam como **Capturado**.
:::

### Capacidades de interação

:::decision
**Decisão:** Proficiência deixa de ser um atributo numérico genérico. Sua intenção é preservada por **capacidades determinísticas e modulares de interação**.

Capacidades funcionam como desbloqueios de soluções: se o agente não possui a capacidade necessária, aquela Action não faz parte de seu espaço de soluções. Preconditions, por outro lado, são estados que o planner pode tentar produzir por outras Actions.

Capacidades podem possuir níveis. Objetos/Targets podem declarar dificuldade ou requisito, e a composição entre **requisito do Target + capacidade/nível do agente + Action + modificadores** determina se a interação é possível e qual Duration é resolvida. Níveis maiores podem desbloquear requisitos superiores e/ou reduzir duração quando configurado.

Arquétipos são composições/configurações de gameplay aplicadas a Actors e podem habilitar capacidades, referenciar repertórios de Actions e Desejos e aplicar modificadores sobre definições compartilhadas sem duplicá-las. Eles **não adicionam nem removem as mecânicas/componentes da ocorrência**: a composição concreta do Actor determina quais sistemas existem, e o arquétipo especializa o que esses sistemas conseguem fazer. Dois arquétipos podem usar a mesma Action 'Hackear' com tempos diferentes, ou compartilhar o mesmo Desejo com pesos/modificadores distintos.

O princípio permanece: **consegue ou não consegue; se consegue, existe um custo de tempo legível e configurável**. Não há teste probabilístico genérico de Proficiência.
:::

## Habilidades

### Habilidades comuns

| Habilidade | Descrição |
|---|---|
| Disfarçando | Troca aparência por objeto ou NPC configurado no cenário. |
| Escondendo | Usa objetos com característica de esconderijo. |
| Tocaia | Esconde-se e ataca quando o jogador se aproxima. |
| Invisibilidade | Fica difícil de perceber por tempo limitado. |
| Ataque corpo a corpo | Impacta o alvo; derruba itens transportados e incapacita quando a Capacidade de ataque é suficiente para superar a Resistência atual. |
| Ataque de média distância | Aplica um impacto a alcance intermediário; não implica dano acumulativo. |
| Ataque de longa distância | Arremessa ou dispara objeto para produzir um efeito de impacto configurado; não implica dano acumulativo. |
| Imobilizar | Produz ou facilita incapacitação conforme a regra específica da habilidade. |
| Desativar armadilhas | Interage com armadilha para torná-la inativa. |
| Roubar outro larapio | Toma item desejado de outro inimigo. |
| Arrombar portas | Destrói ou inutiliza portas usando força. |
| Trancar/destrancar fechaduras | Manipula portas trancáveis. |
| Destrancar fechadura digital | Usa habilidade técnica ou hacker em portas digitais. |
| Apagar luzes | Escurece área e favorece esconderijos. |
| Hackear câmeras | Desativa transmissão da sala de controle. |
| Não aparecer na câmera | Oculta presença em câmera e minimapa. |
| Arrastar objetos | Move objetos seguráveis para bloquear rotas. |
| Teleportar | Move-se instantaneamente para espaço vazio. |
| Atalho | Usa interações de mobilidade para trocar de posição. |
| Andar pelas paredes | Move-se por superfícies ignorando mobilidade normal. |

### Habilidades especiais

:::decision
**Decisão:** habilidades especiais não dependem de um recurso universal `Especial SP`. Cada habilidade pertence ao repertório reutilizável do jogo; sua disponibilidade e exclusividade decorrem da configuração de arquétipos. Não há uma arquitetura paralela obrigatória para Boss Skills. A habilidade é acionada por condições explícitas de gameplay.

O contrato geral é **Condição → Ativação → Efeito**. Duração, tempo de execução e cooldown são adicionados apenas quando necessários para a habilidade específica. As condições concretas de cada especial ainda precisam ser revisadas para garantir que a habilidade crie ou altere uma situação de perseguição.
:::

| Habilidade | Efeito | Referência do GDD |
|---|---|---|
| Tufão | Cria tufões que empurram o jogador e derrubam itens. | Nômades do Deserto. |
| Rampage | Faz inimigos entrarem em estado agressivo por tempo limitado. | Rei do Crime dá ordens. |
| Apagão | Desativa câmeras e minimapa temporariamente. | Capitão Hacker. |
| Sísmico | Cria ondas de choque que empurram e podem derrubar o jogador. | Papagaio na armadura. |
| Múltiplo | Cria cópias falsas durante fuga. | Inimigo ilusionista. |
| Terror | Incapacita imediatamente o jogador. | Amon-ha assusta o jogador. |
| Transformação | Vampiro assume forma de morcego sob uma condição própria, ganhando mobilidade. | Vampiro. |
| Transpor | Atravessa portas e paredes. | Fantasma. |
| Arremessar tortas | Ataque à distância que suja a tela e reduz visibilidade. | Palhaço. |
| Parede invisível | Cria obstáculo transparente temporário. | Mímico. |
| Uppercut | Inimigo vira poça e ataca com uppercut incapacitante. | Ladrão molhado. |

## Interações de cenário

### Tipos de interação

| Tipo | Uso |
|---|---|
| Armadilhas comuns | Podem capturar, atrasar, empurrar ou derrubar inimigos. |
| Armadilhas específicas | Relacionadas ao tema do cenário. |
| Objetos seguráveis | Podem ser carregados, soltos, roubados ou usados para bloquear caminho. Nunca são destruídos. |
| Armários | Recebem itens soltos pelo personagem e permitem pegá-los de novo. São diferentes dos cofres. |
| Portas | Podem ser abertas, fechadas, trancadas, arrombadas ou hackeadas. |
| Câmeras | Informam posição e permitem ativar armadilhas em áreas monitoradas. |
| Cofres | Podem proteger bens durante a Preparação e o assalto; suas proteções podem exigir capacidades ou soluções de cenário para serem superadas. Também podem receber itens recuperados quando configurados para isso. |
| Prisões | Recebem inimigos capturados. |
| Interações de mobilidade | Atalhos, passagens, carrinhos ou objetos que mudam deslocamento. |

:::requirement
**Requisito:** todo objeto interativo precisa comunicar estado atual.

**Exemplos:** ativo, inativo, trancado, hackeado, quebrado, disponível, ocupado, carregável, escondível.
:::

### Spawn Points e receitas de invasão

:::decision
**Decisão:** a missão não utiliza um pool abstrato de larápios. A invasão é autorada por **Spawn Points**, e cada Spawn Point possui sua própria receita temporal.

Cada receita define, para cada disparo, **quando entram, quantos entram e quais arquétipos de uma gangue são criados**. Os disparos usam **tempo absoluto contado desde o início do Assalto Ativo**. Spawn Points diferentes executam suas receitas de forma independente; não existe uma onda global obrigatória.

A **Janela de Invasão** é o período inicial do Assalto Ativo em que ainda existem eventos de spawn previstos. Sua duração é uma informação de autoria/balanceamento derivada do maior timestamp entre as receitas da partida, e não uma fase de gameplay separada. Atrasos físicos de entrada não estendem essa janela: o evento já ocorreu e os larápios aguardam no Spawn Seguro.

Um Spawn Point está associado a uma gangue, mas não cria um grupo separado. Todos os membros daquela gangue, inclusive os que entram posteriormente ou por outro Spawn Point, compartilham conhecimento e reservas.

Quando um Spawn Point é utilizado, sua entrada passa a ser conhecida por toda a gangue como uma **saída potencial**. Um larápio não precisa fugir pelo mesmo ponto por onde entrou.

O Level Design deve dimensionar a duração normal do assalto e as receitas para que todas as entradas previstas ocorram antes da Fuga Final. Como fallback, se a Fuga Final começar com receitas ainda não executadas, essas entradas pendentes são canceladas e novos larápios não são gerados.
:::

### Spawn Seguro e entrada no cenário

:::decision
**Decisão:** cada Spawn Point pode possuir uma área segura fora do espaço normal de gameplay. A leva é criada nesse **Spawn Seguro** antes de atravessar a entrada física do cenário.

O jogador não bloqueia fisicamente o Spawn Point com objetos e não melhora defensivamente a entrada. Pode no máximo **fechar e trancar** a porta de entrada para atrasar a invasão.

A entrada pelo Spawn Seguro usa uma regra especial e **sempre permitida**, independente das capacidades normais dos arquétipos. Uma porta fechada/trancada adiciona um atraso configurado, mas nunca torna a invasão impossível. Ao terminar o atraso, a entrada é aberta e permanece aberta para entradas seguintes até que o jogador volte a fechá-la/trancá-la.

Quando uma receita cria vários larápios ao mesmo tempo, todos sofrem o mesmo atraso de entrada e atravessam juntos; não é necessário escolher um NPC específico para executar a abertura. Se outro evento do mesmo Spawn Point disparar enquanto a entrada já está atrasada/em abertura, os novos larápios **se juntam à entrada em andamento**, herdam o tempo restante e entram junto dos demais; o temporizador não reinicia nem recebe tempo adicional.

O larápio integra sua gangue desde o momento em que é criado no Spawn Seguro e recebe imediatamente todo o conhecimento compartilhado existente, mesmo antes de atravessar a entrada.
:::

### Sala de controle

A sala de controle contém uma interface simples com cerca de seis monitores. Cada monitor exibe uma câmera de uma área específica do cenário. O jogador pode usar a sala para:

- Identificar entradas dos larápios.
- Acompanhar trajetos.
- Localizar itens e inimigos.
- Antecipar fuga.
- Ativar armadilhas em áreas monitoradas.

:::risk
**Risco:** a sala de controle pode ser poderosa demais ou inútil demais.

**Mitigação:** equilibrar custo de tempo, distância até a sala e valor da informação obtida. No multiplayer, a sala pode criar uma função cooperativa interessante para um jogador orientar o outro.
:::

## Power-ups

Power-ups são modificadores temporários que alteram propriedades de gameplay ou concedem vantagem situacional. Eles não dependem de uma ficha universal de atributos. O HUD deve comunicar claramente quando um power-up é recebido ou perdido, seu efeito e, quando aplicável, sua duração.

### Regras de design

- Power-ups devem ser fáceis de reconhecer no cenário.
- O efeito deve ser comunicado imediatamente.
- A duração precisa ser legível.
- A coleta não deve interromper o fluxo de perseguição.
- O efeito deve criar uma vantagem legível de perseguição, como mobilidade, aumento temporário de Capacidade de ataque ou redução/contorno de Resistência.

## Modos de jogo e progressão

O modo principal pode ser jogado por um ou dois Players em cooperativo local com tela dividida. Multiplayer online não está planejado para PEGA.

### Campanha

Modo principal, acompanha a história de Rick e Petra e a evolução da PEGA em Larópolis. Pode ser jogado por um ou dois jogadores.

### Endless

Modo desbloqueável em que o jogador tenta deter o maior número possível de larápios durante um assalto contínuo. A dificuldade aumenta por ondas inimigas.

### Créditos e desbloqueio

O jogador recebe créditos conforme classificação. Cada estágio pode ter um custo em créditos para ser acessado. A progressão deve incentivar replay de fases para melhorar classificação e liberar novos contratos.

### Dificuldade

Cada cenário possui três níveis de dificuldade. O layout permanece reconhecível, mas obstáculos, inimigos, tempo, quantidade de itens e pressão aumentam.

:::configuration
**Escala de classificação:** `F`, `E`, `D`, `C`, `B`, `A`, `S`, `S+`

**Referência de design:** `S+` representa excelência, `A` representa resultado desejável e `F` representa fracasso.
:::

### Critérios de classificação

| Critério | Descrição |
|---|---|
| Prisão de inimigos | Mede quantos inimigos foram capturados em relação ao total e dificuldade deles. |
| Proteção de itens | Mede quantos itens de desejo foram recuperados ou preservados. |
| Tempo | Mede velocidade e eficiência do assalto. |

:::decision
**Decisão:** critérios e pesos são configuráveis por missão; nenhum critério domina universalmente. A missão comunica ao jogador os critérios relevantes. Tempo faz parte do modelo completo, mas pode ficar fora do primeiro recorte do MVP.

Métricas temporais derivam de eventos relevantes de gameplay e dos intervalos entre eles; seus pesos são configurados por missão. A fórmula exata de cada missão é conteúdo de balanceamento, não uma regra global do GDD.
:::

### Estado final dos itens e bônus de proteção

No fim do Tempo de Fuga, itens que ainda estão no cenário são **Recuperados**, inclusive os carregados por larápios que não escaparam. Itens que cruzaram uma saída válida com um larápio que escapou são **Roubados**. Itens depositados em cofres válidos podem receber bônus de proteção configurado pela missão; armários não concedem esse bônus por padrão.

O bônus é calculado uma única vez a partir do estado final de cada item ao encerrar o assalto. Transferências ou depósitos repetidos não acumulam bônus. Item retirado do cofre ou roubado não recebe bônus de proteção.

## Interface e HUD

### HUD principal

O HUD deve apresentar informações essenciais durante o gameplay:

- Tempo restante do assalto.
- Alerta de tempo crítico.
- Inventário atual do jogador.
- Itens recuperados.
- Itens salvos no cofre.
- Quantidade de inimigos capturados.
- Estado do personagem.
- Indicação de power up ativo.
- Minimapa.

### Minimapa

O minimapa segue padrão quadriculado, com a posição atual do jogador no centro. Deve indicar:

- Layout simplificado do cenário.
- Larápios próximos dentro de distância máxima de detecção.
- Itens de desejo sem limite de distância.
- Sala de controle.
- Cofres.
- Prisão.
- Possível opção de norte fixo ou norte relativo.

### Menus

#### Menu inicial

- Novo jogo.
- Modo solo.
- Multiplayer local cooperativo.
- Seleção de personagem.
- Endless, quando desbloqueado.
- Opções.
- Sair.

#### Menu de pausa

- Continuar.
- Opções.
- Sair do assalto conforme o fluxo de confirmação definido pela UI.

### Carreira e persistência

A carreira é persistida entre assaltos; não há salvamento nem retomada de um assalto em andamento. Ao concluir um assalto, os créditos concedidos pela classificação menos o custo operacional formam o saldo líquido do turno, que é somado ao saldo acumulado da carreira. Se o saldo acumulado ficar abaixo do limite de bancarrota, a carreira termina; reiniciar a carreira começa com saldo zero. Um único assalto de resultado ruim não encerra o jogo por si só.

**Economia provisória para playtest:** custo operacional fixo de 150 créditos por assalto. Há bancarrota quando o saldo acumulado fica estritamente abaixo de -1000 créditos; saldo exatamente igual a -1000 não encerra a carreira. Ambos os valores são configuráveis por balanceamento.

A ideia de empréstimos como opção de continue após bancarrota apareceu em material anterior, mas não foi aprovada nem faz parte do escopo atual. Sua eventual inclusão no jogo completo permanece sem decisão.

## Encerramento do assalto

:::decision
**Decisão:** cada missão configura separadamente o **Tempo de Assalto** e o **Tempo de Fuga**.

O Tempo de Assalto cobre a atividade normal; a Janela de Invasão ocupa seu período inicial. Ao terminar, o contexto global muda para **Fuga Final** e começa o Tempo de Fuga. Para apresentação, a UI pode exibir os dois valores como um único tempo total contínuo, mas internamente o Game State muda entre as etapas.

Durante o Tempo de Assalto, um larápio também pode fugir antecipadamente quando seus Desejos e prioridades levarem a **Escapar**; carregar loot não é uma precondição universal. Na Fuga Final, todos passam para o contexto correspondente, mas continuam usando GOAP para resolver portas, rotas, itens perdidos, interferência do jogador e outros estados.

Quando o **Tempo de Fuga termina**, o gameplay acaba imediatamente e o jogo passa aos resultados; não há Game Over imediato por desempenho ruim em um único assalto. Cada larápio recebe uma resolução final:

- **Capturado:** captura efetivamente concluída pelo jogador durante o gameplay.
- **Escapou:** atravessou uma saída válida antes do fim do Tempo de Fuga.
- **Não escapou:** ainda permanecia no assalto quando o Tempo de Fuga terminou; não conta automaticamente como captura efetiva.

Um item só é considerado **Roubado** quando atravessa uma saída válida com um larápio que escapou. Itens nunca são destruídos, então essa é a única forma de um item ser perdido. Itens ainda presentes no cenário ao fim do Tempo de Fuga, inclusive os carregados por larápios que não escaparam, são considerados **Recuperados**.

A classificação avalia captura, recuperação/proteção de itens e métricas temporais segundo os critérios e pesos configurados pela missão. Só detenções concluídas contam como Capturado; remanescentes recebem Não escapou. A carreira recebe o resultado após a conclusão do assalto.
:::

:::risk
**Risco:** tentar implementar todas as gangues e habilidades antes de validar o loop principal.

**Impacto:** alto.

**Mitigação:** validar primeiro uma fase vertical slice com armazém, uma gangue e classificação.
:::

## Questões abertas

:::open-question
**Questão de escopo:** o HUB da Demo é o escritório estático com hotspots descrito no MVP. A forma do HUB após a Demo ainda não foi definida.
:::

:::open-question
**Questão de conteúdo:** Rick e Petra são mecanicamente equivalentes na Demo. Diferenças mecânicas após a Demo dependem de playtests e decisão futura.
:::

:::open-question
**Questão de produção:** quais cenários além do Armazém entram em entregas posteriores? O Armazém é o primeiro cenário de referência.
:::

:::open-question
**Questão de conteúdo:** quais power-ups, se houver, serão incluídos depois do primeiro MVP? Efeitos e configurações devem servir à perseguição e dependem de playtests.
:::

## Glossário

| Termo | Definição |
|---|---|
| PEGA | Proteção e Estratégia na Guarda de Ativos em Larópolis. |
| PGA Larópolis | Polícia e Guarda Avançada de Larópolis. |
| Larópolis | Cidade fictícia onde o jogo acontece. |
| Larápios | Criminosos/inimigos do jogo. |
| Assalto | Unidade principal de gameplay dentro de uma fase ou missão. |
| Item de desejo | Item valioso que inimigos querem roubar. |
| Inventário | Itens que um personagem está segurando. Capacidade é configurada pela mecânica do Actor; cada item ocupa uma unidade inteira. Um ataque válido faz o personagem derrubar todo o inventário. |
| Armário | Ponto do cenário onde o personagem pode guardar e pegar itens; não é um cofre. |
| Cofre | Ponto de proteção de bens. Pode receber itens na Preparação ou durante recuperação e possuir proteções configuráveis que larápios precisam descobrir e superar. |
| Prisão | Local onde inimigos capturados devem ser depositados. |
| Janela de Invasão | Período inicial do Assalto Ativo coberto pelos timestamps absolutos das receitas de Spawn Points; informação principalmente de autoria e balanceamento. |
| Contexto de Desejos | Estado macro global e exclusivo que seleciona as configurações de desejos/pesos dos arquétipos. Na arquitetura atual: Normal, Item Principal em Jogo e Fuga Final. |
| Actor | Entidade/ocorrência sobre a qual o gameplay opera. Sua composição concreta determina quais mecânicas existem; identidade, ocorrência, composição física e lifecycle não são definidos pelo arquétipo. |
| Mecânica do Actor | Sistema presente na composição concreta do Actor que executa uma família de comportamento, como movimento, percepção, interação, confronto ou decisão. Nem todo Actor precisa possuir todas as mecânicas. |
| Arquétipo | Composição/configuração de gameplay que especializa as mecânicas existentes de um Actor por meio de capacidades, Actions, Desejos, modificadores e parâmetros; não materializa nem remove mecânicas/componentes da ocorrência. |
| Desejo / Goal | Intenção declarativa da IA: define aplicabilidade, predicados de conclusão, peso e satisfação, sem prescrever uma sequência fixa de Actions. |
| Desejo de Contingência | Desejo normal habilitado apenas quando nenhum Desejo normal do Contexto vigente possui plano válido; usa o mesmo sistema de peso, custo, satisfação e Actions. |
| Action | Unidade modular de solução do GOAP, com precondições, Effects, Target, Duration, capacidades, reservas/interrupções e modificadores conforme necessário. |
| Knowledge State | Melhor representação atualmente conhecida pelo agente/gangue; é a realidade usada pelo planner e pode divergir temporariamente do World State real. |
| Predicado | Condição componível avaliada sobre uma fonte de dados explícita, usada em aplicabilidade, precondições, Targets, conclusão de Desejos e outras validações. |
| Custo do plano | Estimativa temporal do MVP: deslocamento estimado + durações resolvidas das Actions necessárias. |
| Satisfação | Histórico individual acumulado de realizações de um Desejo; persiste entre Contextos e pode modificar seu peso conforme configuração. |
| Fuga final | Contexto iniciado ao fim do Tempo de Assalto e mantido durante o Tempo de Fuga; altera desejos/prioridades, mas não substitui o GOAP. Receitas de spawn ainda pendentes são canceladas como fallback. |
| Larápio ativo | Larápio que ainda participa do gameplay e pode executar comportamentos. Ao fim do Tempo de Fuga, larápios remanescentes recebem a resolução Não escapou. |
| Não escapou | Resolução de um larápio ainda presente quando o Tempo de Fuga termina. Não equivale a uma captura efetivamente concluída pelo jogador. |
| Capacidade de ataque | Limiar ofensivo atual usado para verificar se um ataque pode incapacitar o alvo. Não representa dano acumulado. |
| Resistência | Limiar atual que deve ser alcançado pela Capacidade de ataque para incapacitar um ator. Pode ser modificado por condições e deve ser comunicado visualmente. |
| Capacidade de interação | Aptidão determinística de um ator para executar determinado tipo/requisito de interação do cenário. Quando compatível, a ação possui um tempo de execução definido. |
| GOAP | Goal-Oriented Action Planning; arquitetura em que Desejos, conhecimento, capacidades, Actions, precondições e Effects formam planos dinâmicos. |
| Área | Unidade espacial de exploração e aquisição de informação usada pela IA. |
| POI | Point of Interest relevante ao planejamento, como cofre, terminal, chave ou porta especial. |
| Gangue | Unidade de cooperação, compartilhamento de conhecimento e reservas entre larápios. |
| Spawn Point | Ponto de invasão com receita temporal própria que define quantos e quais arquétipos de uma gangue entram em cada disparo. |
| Spawn Seguro | Área técnica protegida fora do gameplay normal onde uma leva é criada antes de sua entrada garantida no cenário. |
| Confrontando | Comportamento em que um larápio enfrenta outro ator para atender a um objetivo concreto; não é uma reação aleatória a dano. |
| Habilidade especial | Comportamento próprio de um arquétipo acionado por condições explícitas; não depende de uma barra universal de SP. |
| Incapacitado | Estado temporário em que o larápio não pode agir e pode ser colocado sob custódia antes de se recuperar. |
| Em transporte | Estado de um larápio incapacitado sob custódia de um jogador a caminho da sala de detenção. |
| Detido / Capturado | Resolução final em que o larápio foi entregue à sala de detenção. Incapacitação e transporte são estados intermediários, não capturas concluídas. |
| Cancelled | Resultado explícito de Action cuja execução foi encerrada pelo plano ou proprietário; progresso é descartado e reservas são liberadas. |

## Documentos relacionados

| Documento | Uso |
|---|---|
| [MVP](MVP.md) | Recorta as regras deste GDD no primeiro vertical slice e na Demo completa. |
| [TASKS](TASKS.md) | Tarefas executáveis derivadas do escopo do MVP. |
| [Art Book](ArtBook.md) | Direção visual e referências de personagens, gangues e cenários. |
| [Tabela de Atributos e Habilidades.xlsx](Tabela%20de%20Atributos%20e%20Habilidades.xlsx) | Referência auxiliar histórica para ideias de distribuição de habilidades por gangue e valores preliminares de classificação/créditos. Agilidade, HP/dano, Defesa, Vigor, fórmulas e valores da planilha não são contratos atuais nem dados validados. |

## Histórico de revisão

| Versão | Data | Alteração |
|---|---|---|
| 0.8.0 | 2026-10-10 | Consolidada a fonte normativa; incorporados os contratos aprovados de GOAP, Actor, preparação, inventário, captura, classificação, carreira e encerramento. Removidas referências a fontes e perguntas resolvidas. |
