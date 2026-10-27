# PEGA — ADR de Pré-produção

**Projeto:** PEGA (Unity 6)  
**Tipo:** registro de decisões arquiteturais transversais e de planejamento de produção<br>
**Status:** PP-01 a PP-36 e adendos anteriores preservados como histórico; PP-37 registra a estratégia de produção vigente desde 2026-10-16; a diretriz arquitetural transversal consta na §13 desde 2026-10-25; a decisão de ownership genérico e integração de GOAP/Perception consta na §7 desde 2026-10-26.
**Data-base:** 2026-10-10  
**Destino proposto no repositório:** `PEGA GDD/ADR-PREPRODUCAO.md`  
**Escopo:** diretrizes arquiteturais transversais do PEGA, microciclo, vertical slice técnico, Demo e estratégia de evolução do ecossistema Immersive Games.

> **Precedência documental:** `GDD.md` define regras e contratos de gameplay; este ADR registra a diretriz arquitetural transversal do PEGA e decisões de **produção**, integração e responsabilidades; os ADRs técnicos em `ADRs/` registram aplicações e decisões específicas de arquitetura derivadas dos contratos do GDD, sem substituí-los; `MVP.md` define o recorte obrigatório do vertical slice e da Demo; `ArtBook.md` orienta a apresentação; `TASKS.md` representa trabalho executável. Nenhum ADR altera gameplay ou declara implementado aquilo que é apenas planejado. Divergências de regras devem ser resolvidas no GDD, não por decisões concorrentes.

## 1. Contexto e objetivo

A produção do PEGA será realizada por duas pessoas, predominantemente em Programação/Game Development e Arte/Level Design, com disponibilidade limitada e irregular. O projeto utiliza o **Immersive Framework** para capacidades compatíveis de aplicação, Route/Activity, participação de Player/Actor, input, câmera e outras superfícies públicas. As mecânicas específicas do PEGA permanecem sob responsabilidade do jogo.

Objetivo deste ADR: manter a cadeia de decisões rastreável, estruturar resultados verificáveis e permitir planejamento progressivo **sem comprometer antecipadamente datas, carga horária fixa, abstrações especulativas ou funcionalidades além do MVP**.

## 2. Registro das decisões PP-01 a PP-35

As decisões abaixo são resumidas deliberadamente: detalhamento normativo de gameplay pertence ao GDD e ao MVP.

| ID | Decisão de produção |
|---|---|
| PP-01 | Organizar a produção por capacidades de gameplay e resultados demonstráveis. |
| PP-02 | Maturidade dos entregáveis: **Protótipo → Funcional → Integrado → Aprovado para Demo**. |
| PP-03 | Uma capacidade pode produzir entregas verificáveis em marcos e níveis de maturidade diferentes. |
| PP-04 | Dependências apenas quando realmente bloqueiam execução ou aceite; permitir paralelismo. |
| PP-05 | Estimar progressivamente, separando **esforço**, **duração** e **datas**. |
| PP-06 | Planejamento leve para dois integrantes, disponibilidade variável e IA como ferramenta, não integrante adicional. |
| PP-07 | Roadmap compartilhado, execução independente e pontos explícitos de integração. |
| PP-08 | Priorizar por marco, bloqueio, valor jogável, risco e capacidade, sem pontuação matemática obrigatória. |
| PP-09 | Tarefas em **A fazer / Em andamento / Concluído**; testes fazem parte dos critérios de aceite; bloqueio é anotação, não estado. |
| PP-10 | Cronograma no nível de entregáveis/marcos; datas por tarefa opcionais. |
| PP-11 | Reavaliar quando marcos ou eventos relevantes mudarem; sem reuniões periódicas obrigatórias. |
| PP-12 | Classificar novo trabalho como necessário ao contrato existente, melhoria opcional ou alteração real de escopo sujeita a aprovação. |
| PP-13 | Catálogo centrado em capacidades jogáveis, tratando sistemas técnicos como componentes. |
| PP-14 | Entregas em escala intermediária: demonstráveis, nem microações nem missão inteira. |
| PP-15 | Primeiro resultado jogável: microciclo integrado **dentro do vertical slice**, sem criar novo MVP ou fase normativa. |
| PP-16 | Permitir entregáveis de suporte independentes, verificáveis e ligados às capacidades, sem catálogo paralelo. |
| PP-17 | Microciclo mínimo: Player, larápio com GOAP e conhecimento limitado, item, roubo, tentativa de fuga, perseguição, ataque com queda do item e recuperação. Preparação, prisão, cofres, relógios e resolução completa vêm depois, ainda no mesmo slice. |
| PP-18 | Quatro entregáveis do microciclo: MC-01 controle/interação, MC-02 investigação/roubo GOAP, MC-03 confronto/recuperação e MC-04 integração. |
| PP-19 | Armazém como **único entregável evolutivo**, do blockout compacto à Demo, sem congelar layout inicial. |
| PP-20 | Primeiro blockout limitado aos requisitos funcionais do microciclo. |
| PP-21 | Contrato mínimo entre Programação e Level Design, com trabalho paralelo e integração explícita. |
| PP-22 | Investigações técnicas são tarefas dos entregáveis; suporte próprio apenas quando houver resultado reutilizável e verificável. |
| PP-23 | Aceite por demonstração funcional e checklist simples; automação quando apropriada. |
| PP-24 | Componente compartilhado possui implementação de referência ligada a um entregável e é reutilizado pelos outros. |
| PP-25 | Novas capacidades do vertical slice ampliam progressivamente o ciclo jogável; execução paralela permanece possível. |
| PP-26 | Um entregável final verifica o vertical slice completo ponta a ponta, sem reimplementar mecânicas ou criar fase separada de QA. |
| PP-27 | Demo em modelo misto: gameplay progressivamente integrado; conteúdo, arte e sistemas complementares amadurecem em paralelo com pontos de integração. |
| PP-28 | Cooperativo em duas entregas verificáveis: participação local e gameplay cooperativo. |
| PP-29 | Quatro arquétipos em um entregável da gangue, com quatro incrementos verificáveis e mecânicas/GOAP compartilhados. |
| PP-30 | HUB/fluxo, classificação/economia e persistência são três entregáveis separados e integrados. |
| PP-31 | Apresentação audiovisual como entregável transversal com incrementos verificáveis. |
| PP-32 | Aprovação da Demo funcional **e** de experiência para demonstração a terceiros, sem exigências de lançamento comercial. |
| PP-33 | Roadmap por dependências reais, antecipação proporcional de riscos e evolução jogável incremental. |
| PP-34 | Estimar incrementos verificáveis quando tamanho ou incerteza justificarem; detalhar tarefas perto da execução. |
| PP-35 | Capacidade semanal média por integrante, revisável; enquanto desconhecida, manter apenas esforço sem projetar datas. |

### Diretrizes complementares acordadas após PP-35

- **Capacidade atual:** horas semanais de ambos **a definir**; disponibilidade limitada, irregular e não garantida. Não estabelecer sprints, jornadas, datas ou duração fictícias.
- **Comparação estimado × real:** registrar estimativa original, revisão quando necessária, horas humanas realizadas por frente, esforço restante e desvio ao concluir; não contabilizar horas fictícias de agentes de IA.
- **Delegação de escolhas:** quando GDD, MVP, framework e decisões já determinarem a solução, escolher o mínimo realmente executável; consultar a direção do projeto apenas por impacto relevante de escopo, experiência ou arquitetura.
- **Evolução pós-Demo das demais capacidades:** implementar e validar primeiro no PEGA; somente **depois da Demo** considerar promoção de capacidades maduras ao Immersive Framework ou a package independente. Candidatura não é decisão de extração. GOAP e Perception seguem a decisão vigente da §7, que autoriza consolidar seus mecanismos genéricos nesta etapa sem criar packages. Não generalizar prematuramente as demais capacidades nem incluir publicação de packages no esforço do MVP/Demo.

## 3. Catálogo inicial de entregáveis

Identificadores provisórios de planejamento; não implicam datas. A estratégia PP-37 abaixo prevalece quanto à ordem de trabalho. MC-01 a MC-04 preservam os recortes e referências do planejamento anterior, mas não constituem uma sequência de pequenos ciclos que cada capacidade precise fechar antes de avançar.

### 3.1 Referência histórica: primeiro microciclo (subconjunto do vertical slice)

O recorte abaixo preserva a composição planejada antes de PP-36. É referência de cobertura integrada, não prioridade atual nem gate para avançar capacidades.

| ID | Resultado de aceite principal |
|---|---|
| **MC-01** | Player controlável, colisão, interação e **posse mínima compartilhada** de um item, com portador identificável e sem posse duplicada. |
| **MC-02** | Larápio com **GOAP real**, sem conhecimento onisciente: investiga item inicialmente desconhecido, percebe sob condições legítimas, rouba, tenta fugir e replaneja quando a oportunidade deixa de existir **após obter informação válida**. |
| **MC-03** | Player intercepta e incapacita larápio portando item; ataque provoca queda, posse é atualizada e Player recupera item. Captura por entrega à prisão é posterior. |
| **MC-04** | Sessão integrada de investigação → roubo → tentativa de fuga → perseguição → confronto → queda → recuperação; teste alternativo de oportunidade perdida e replanejamento sem informação mágica. |
| **LD-01** | Blockout compacto com área transitável, colisões, obstáculos que efetivamente afetem percepção, ponto de investigação, item, rotas e saída funcional de teste. |

**Escopo explicitamente adiado dentro do mesmo vertical slice:** Preparação, invasão temporal completa, captura na prisão, armazenamento em cofres, Fuga Final, relógios globais, classificação e encerramento. **Não são removidos do MVP.** QA técnico pode usar cenas auxiliares sem transformá-las em cenário de produção.

**Composição da posse mínima:** MC-01 estabelece a referência compartilhada de aquisição, portador e exclusividade; MC-02 reutiliza ao roubar; MC-03 acrescenta liberação por ataque e recuperação. Não criar inventários paralelos. A implementação deve preservar as regras de inventário configurável e itens do GDD quando evoluir no vertical slice.

### 3.2 Completar o vertical slice

| ID | Resultado proposto |
|---|---|
| **VS-10** | Preparação temporal e escolha entre cofres válidos conforme GDD/MVP. |
| **VS-11** | Invasão por Spawn Points, receitas e Spawn Seguro conforme GDD/MVP. |
| **VS-12** | Incapacitação, transporte e **entrega efetiva à prisão** para concluir captura. |
| **VS-13** | Depósito e proteção de itens/valores em cofres, incluindo elegibilidade, capacidade e casos exigidos pelo MVP. |
| **VS-14** | Fuga Final com regras temporais e replanejamento GOAP, conforme GDD/MVP. |
| **VS-15** | Encerramento de missão, estados de larápios/itens, classificação e resultados básicos. |
| **VS-16** | Validação ponta a ponta: Preparação → invasão → assalto/roubo → perseguição/recuperação/captura → Fuga Final → resultados. |

Os IDs são recortes de produção, **não substituem** os contratos detalhados do GDD/MVP. Integração progressiva não exige implementação estritamente sequencial.

### 3.3 Demo completa

| ID | Resultado de produção |
|---|---|
| **DEM-01** | Participação local de Rick e Petra: dois Players, input independente, câmeras e split-screen. |
| **DEM-02** | Gameplay cooperativo compartilhado: itens, larápios, preparação, cofres, confronto e captura corretos com dois Players. |
| **DEM-03** | Os Trapalhões do Crime: **um entregável e quatro incrementos verificáveis**, reutilizando GOAP e mecânicas. |
| **DEM-04** | Armazém completo, evolução de LD-01 com level design, conteúdo e apresentação de Demo. |
| **DEM-05** | HUB estático do escritório e fluxo de telas: contrato → missão → resultados → HUB. |
| **DEM-06** | Classificação, créditos, custos operacionais e bancarrota segundo GDD/MVP. |
| **DEM-07** | Persistência da carreira entre sessões, sem save/resume de assalto em andamento. |
| **DEM-08** | Apresentação audiovisual transversal: HUD/feedback, personagens/animação, VFX/áudio, HUB/resultados e polimento, por incrementos. |
| **DEM-09** | Demo integrada e estável, funcional e apresentável a terceiros; valida o fluxo de carreira e gameplay completo com um ou dois Players. Não exige publicação comercial. |

## 4. Dependências e responsabilidades

- A prioridade vigente e as interpretações das dependências estão registradas em **PP-37**. A decomposição anterior em MC-01 a MC-04 não obriga cada capacidade a ser entregue como microciclo isolado. Trabalho paralelo não antecipa o ambiente conjunto nem a composição: eles começam somente depois que Player e NPCs alcançarem suficiência para seus objetivos atuais.
- A implementação de referência de componentes compartilhados pertence ao primeiro entregável que precisa deles; os demais integram ou estendem, sem duplicar autoridade.
- **Programação** explicita requisitos de navegação, colisão, percepção, interação e pontos de integração; **Arte/Level Design** fornece o blockout e o evolui sem congelar geometria prematuramente.
- A investigação técnica pode ocorrer sem virar implementação antecipada da Demo; conteúdo, Level Design completo e polimento continuam posteriores à experiência central de perseguição satisfatória.
- Os critérios de aceite estão dentro de cada entregável; somente uma demonstração reproduzível e checklist cumprido autorizam conclusão.

## 5. Immersive Framework × código do PEGA

### Fronteira de responsabilidade

**Framework:** inicialização de aplicação, Route/Activity, Session e participação Player, composição e ciclo de vida do Actor/Host, input e disponibilidade de gameplay, câmera, superfícies públicas compatíveis de Pause e armazenamento, conforme maturidade e escopo reais.

**PEGA:** regras e integração de gameplay — locomotion e gameplay físico, interação/posse/inventário, confronto, fatos e políticas do conhecimento dos larápios, Goals/Desejos, Actions, Targets, arquétipos, assalto, regras financeiras e apresentação específica. Os mecanismos genéricos de GOAP e Perception e suas fronteiras de integração seguem a decisão da §7; não se confundem com essas regras.

**Não inferir:** `ActorProfile` não implementa inventário, GOAP ou habilidades; o caminho de Player/Actor não comprova automaticamente suporte de ciclo de vida para NPCs. O Framework não disponibiliza uma UI genérica completa. Não tratar API experimental como estável, nem assets como evidência de execução no Editor.

### Evidência e limites da inspeção (2026-10-10)

- O PEGA declara **`com.immersive.framework` `1.1.0-preview.6`** no `Packages/manifest.json`; a branch `master` consultada do repositório do Framework anuncia **`1.1.0-preview.7`**. **Não presumir paridade** entre essas versões; conferir a versão efetivamente resolvida no projeto e sua documentação instalada.
- No repositório PEGA existe `Assets/_Project/Prototyping/PlayerPlaceholder/PlayerMovementPlaceholder.cs`, usando `PlayerGameplayInputReader`, `InputActionReference` e `CharacterController`, além de composição Scene-Provided descrita em seu README. É **protótipo**, não gameplay de produção.
- O README relata um primeiro Play Mode bem-sucedido informado pelo usuário, porém ressalva testes pendentes de reentrada/lifecycle e não fornece nova certificação de Editor nesta inspeção.
- Não foi identificada implementação GOAP específica do PEGA no material inspecionado. Isso **não demonstra ausência em alterações locais não publicadas**.
- A documentação pública do Framework contém superfícies de maturidade mista: `SceneProvidedLocalPlayerAuthoring` e `LocalPlayerHostAuthoring` são Stable, enquanto `ActorProfile` e partes da sessão/câmera são Experimental; Pause público tem escopo single-player documentado; backend de save também exige validação.
- Esta foi **inspeção estática de repositórios**; compilação, runtime e API efetiva do pacote resolvido **não foram testados** nesta etapa.

Fontes de leitura: `https://github.com/ImmersiveGames/PEGA`; `https://github.com/ImmersiveGames/com.immersive.framework`; documentação de API e `Player-Usage.md` do Framework. Na incorporação ao repositório, preferir links relativos quando aplicável.

## 6. Estimativas iniciais e comparação com esforço real

Estimativas **hipotéticas de esforço humano**, não compromissos, velocidade comprovada ou prazos. Sem capacidade semanal definida não há cronograma defensável.

| ID | Referência original | Referência revisada | Principal incerteza |
|---|---:|---:|---|
| MC-01 | 21 h | **20 h** | Adaptação do Player prototipado, interação e posse mínima; revisão condicionada a verificação na versão instalada. |
| MC-02 | 70 h | **70 h** | Planejador GOAP do PEGA, conhecimento não onisciente e replanejamento. |
| MC-03 | 35 h | **35 h** | Contratos de combate, queda e recuperação. |
| MC-04 | 28 h | **28 h** | Integração e condições de demonstração. |
| LD-01 | 23,5 h | **23,5 h** | Iteração de geometria, navegação e percepção. |
| **Total** | **177,5 h** | **176,5 h** | Estimativas de diferentes revisões; ver histórico abaixo. |

**Histórico sem sobrescrita:** a referência inicial de MC-01 foi 21 h; ao incluir posse mínima foi revisada para **27 h** (total **183,5 h**); a inspeção estática do protótipo reduziu a hipótese para **20 h** (total **176,5 h**). A coluna original representa a primeira estimativa e a revisada, a mais recente. **Nenhum valor foi medido em execução**.

Para cada entregável acompanhar, quando houver execução: estimativa inicial congelada, revisões datadas e motivadas, horas humanas efetivas por frente, esforço restante e desvio absoluto/percentual no encerramento. Tempo de revisão ou integração de resultados de IA conta como esforço humano; tempo de execução do agente não é uma terceira jornada fictícia. Não otimizar apenas horas: aceite funcional continua obrigatório.

### Adendo de planejamento — linha de base simulada do Trello (2026-10-10)

O cartão `[PLANO] Microciclo 01 — Linha de base de cronograma` no Trello (`https://trello.com/c/hO3uDL1F`) registra uma **simulação de planejamento** para o período de **13/10/2026 a 04/12/2026**, usando as premissas de **20 h/semana para Programação** e **10 h/semana para Arte/Level Design**. Essas capacidades são parâmetros da simulação, **não disponibilidade comprovada nem nova decisão de capacidade semanal**. A capacidade real continua a definir conforme PP-35 e as diretrizes complementares após PP-35; as estimativas da seção permanecem esforço hipotético, não compromisso de duração.

Para acompanhamento operacional da simulação e de seus cartões derivados, a referência é o cartão de plano do Trello acima. Esta anotação preserva a linha de base como registro operacional, sem substituir nem sobrescrever o histórico de estimativas e decisões deste ADR.

### Adendo v2 — fundação da aplicação antes do MC-01 (2026-10-10)

Este adendo complementa PP-01 a PP-35 e preserva as estimativas e a linha de base anteriores. GDD e MVP continuam normativos; TASKS continua sendo a decomposição executável. A auditoria estática do consumidor confirmou que a aplicação ativa ainda aponta para os assets e cenas isolados em `Assets/FrameworkValidation`; a composição de produção PEGA precisa ser criada e validada antes da integração Player/Actor.

**Decisões complementares de arquitetura e produção:**

- O vertical slice inicia diretamente na **Mission Route**, sem menu inicial nem HUB.
- Um GameApplication próprio do PEGA seleciona essa Route como Startup Route.
- A Route contém duas Activities distintas: **Mission** e **Result**.
- Preparação, Assalto e Fuga são fases/estados internos da Mission Activity, não Activities separadas.
- **FOUND-01** é o pré-requisito técnico de composição e validação da aplicação antes do cartão #75; deriva da tarefa de fluxo já existente em TASKS e não cria requisito de gameplay adicional.
- FrameworkValidation permanece isolado como ambiente técnico de validação. Nenhum asset ou cena dali se torna conteúdo de produção por simples troca de referência.
- O cartão #75 passa a cobrir somente integração Player/Actor Manager-Provisioned, Player Slot, ActorProfile com seleção padrão sem UI, Input Gate e validação de lifecycle. Câmera definitiva e movimento permanecem nos cartões #29 e #34. A composição Scene-Provided descrita no registro de protótipo permanece histórica e não define o fluxo de produção de #75.
- Retry/reset definitivo, menu, HUB, carreira e gameplay não pertencem ao FOUND-01.

**Estimativas do plano v2 — hipótese, não esforço realizado:**

| ID | Estimativa v2 | Nota |
|---|---:|---|
| FOUND-01 | **10 h** | Hipótese inicial dentro da faixa investigada de 8–12 h. |
| MC-01 | **19 h** | #75 revisado para 4 h; #29, #34, #37 e #38 mantêm suas estimativas. FOUND-01 é pré-requisito separado e não integra este subtotal. |
| MC-02 | **70 h** | Mantida. |
| MC-03 | **35 h** | Mantida. |
| MC-04 | **28 h** | Mantida. |
| LD-01 | **23,5 h** | Mantida. |
| **Total v2** | **185,5 h** | Acréscimo líquido de 9 h sobre a linha de base de 176,5 h; hipótese, sem medição. |

A linha de base histórica permanece **176,5 h, 13/10/2026–04/12/2026**. O plano v2 estima 185,5 h e prevê término em **08/12/2026**, usando apenas como simulação Programação 20 h/semana (4 h/dia útil) e Arte/Level Design 10 h/semana (2 h/dia útil). Essas capacidades continuam hipotéticas; as datas não são compromisso nem registro de trabalho executado.

**Cronograma simulado v2 dos 17 cartões executáveis:**

| ID / cartão | Frente | Esforço | Início planejado | Término previsto |
|---|---|---:|---|---|
| FOUND-01 | Programação | 10 h | 13/10/2026 | 15/10/2026 |
| #75 | Programação | 4 h | 15/10/2026 | 16/10/2026 |
| #29 | Programação | 3 h | 16/10/2026 | 19/10/2026 |
| #34 | Programação | 5 h | 19/10/2026 | 20/10/2026 |
| #37 | Programação | 3 h | 20/10/2026 | 21/10/2026 |
| #38 | Programação | 4 h | 21/10/2026 | 22/10/2026 |
| #68 | Programação | 10 h | 22/10/2026 | 26/10/2026 |
| #69 | Programação | 9 h | 26/10/2026 | 28/10/2026 |
| #53 (LD-01) | Arte / Level Design | 23,5 h | 13/10/2026 | 28/10/2026 |
| #63 | Programação | 9 h | 28/10/2026 | 02/11/2026 |
| #64 | Programação | 7 h | 02/11/2026 | 03/11/2026 |
| #76 | Programação | 20 h | 03/11/2026 | 10/11/2026 |
| #70 | Programação | 8 h | 10/11/2026 | 12/11/2026 |
| #72 | Programação | 5 h | 12/11/2026 | 16/11/2026 |
| #66 | Programação | 2 h | 16/11/2026 | 16/11/2026 |
| #40 | Programação | 35 h | 16/11/2026 | 27/11/2026 |
| #77 | Programação | 28 h | 27/11/2026 | 08/12/2026 |

O escalonamento respeita a capacidade simulada de uma frente de Programação a 4 h por dia útil, permite tarefas no mesmo dia quando resta capacidade, mantém LD-01 em paralelo e conserva as dependências MC-01 → MC-03 → MC-04 e MC-02 → MC-04 já registradas nos cartões. A sequência datada aloca #68 após #38 por capacidade, sem declarar dependência lógica nova entre eles. #70 está calendarizado após #76, mas sua descrição atual registra como dependências #69, #63 e LD-01; não se cria dependência lógica adicional. #40 permanece após #38; #77 após MC-01/02/03 e LD-01.

As datas de início do Trello/GanttFlow não são gravadas por esta integração; o campo nativo disponível para gravação é o vencimento. A tabela é a previsão v2 registrada neste adendo e no cartão de plano. Isso, por si só, não significa que o GanttFlow esteja atualizado.

## 7. Evolução arquitetural de GOAP e Perception

**Decisão vigente para GOAP e Perception (2026-10-26):** ambos serão mecanismos genéricos, independentes entre si, reutilizáveis em diferentes jogos e integráveis ao ecossistema Immersive. A implementação de cada mecanismo não incorpora regras particulares de um jogo nem depende do outro mecanismo. Cada jogo fornece sua própria superfície de integração e mantém nela suas regras, dados, conhecimento e políticas específicas.

Nesta etapa não serão criados novos pacotes. Os mecanismos podem permanecer no repositório PEGA enquanto suas fronteiras são consolidadas; essa localização não torna seus contratos dependentes das regras do PEGA nem transfere ao Framework as regras de gameplay de cada jogo. Uma integração ao Framework deve respeitar as autoridades existentes de identidade, ocorrência, lifecycle e composição do Actor, sem presumir que já exista uma API genérica de GOAP ou Perception no Framework.

A composição do Actor deve favorecer um Root físico mínimo. GOAP e Perception podem ser organizados separadamente, inclusive em objetos filhos quando as dependências, a identidade e o lifecycle da ocorrência forem compatíveis com os contratos do Framework. O Root continua sendo a autoridade física de pose e posicionamento; para Actors do Framework, as declarações de identidade e publicação de ocorrência permanecem no Root exigido pelos contratos oficiais. Use composição keyed e mounts existentes somente quando o ownership e o ciclo de vida da parte justificarem isso; não crie um Composer paralelo, componentes de coordenação ou abstrações apenas para organizar código.

Priorize classes e dados comuns para responsabilidades internas. Use `MonoBehaviour` quando a responsabilidade depender de lifecycle Unity, authoring serializado ou interação direta com a cena. A presença de uma mecânica na composição concreta do Actor continua distinta da configuração de arquétipo: arquétipos especializam capacidades, Actions, Desejos, parâmetros e perfis, mas não materializam nem removem mecânicas da ocorrência.

Esta decisão não altera as regras de gameplay do GDD nem as semânticas dos ADRs GOAP-02, GOAP-03 e GOAP-04. A avaliação pós-Demo continua aplicável às demais capacidades — por exemplo, inventário/posse — para decidir se permanecem no PEGA, se são integradas ao Framework ou se futuramente justificam um package próprio. Essa avaliação não cria compromisso de pacote, estimativa ou validação antecipada.

### Histórico — decisão anterior sobre GOAP e Perception (supersedida)

Até 2026-10-25, a política desta seção mantinha inicialmente no PEGA as capacidades de gameplay e previa avaliar após a Demo se sistemas maduros permaneceriam no PEGA, seriam incorporados ao Framework ou se tornariam packages independentes. GOAP e percepção/conhecimento eram candidatos a essa avaliação, sem decisão de generalização antecipada. Essa decisão foi substituída, apenas para os mecanismos GOAP e Perception, pela decisão vigente acima; o histórico permanece para rastreabilidade, sem efeito normativo atual sobre essas duas capacidades. Para outros sistemas, como inventário/posse, a política anterior continua aplicável.

## 8. Encaminhamento e integridade

1. Usar este ADR como autoridade para a diretriz arquitetural transversal do PEGA e como registro de decisões de produção; não gerar outro catálogo normativo de gameplay. As aplicações e decisões técnicas específicas de GOAP ficam nos ADRs em `ADRs/`, subordinadas ao GDD e sem duplicar suas regras.
2. Verificar, antes de planejar implementação, a API do Framework **efetivamente instalada** e a diferença entre os protótipos/QA e as capacidades de produção do PEGA.
3. **Encaminhamento histórico, reordenado por PP-36 e PP-37:** a primeira validação foi planejada como MC-01 a MC-04 + LD-01. O microciclo continua como referência histórica de cobertura. PP-37 agora prioriza capacidades de perseguição, composição do comportamento central e só depois sua expansão; o MVP continua regendo o escopo final do vertical slice.
4. Planejar novas estimativas de esforço próximo à execução; converter em duração apenas com informação de capacidade suficiente.
5. Ao incorporar no repositório, conferir que IDs, links e termos são consistentes com GDD/MVP/TASKS; não alterar as fontes normativas por implicação silenciosa.

**Estado de execução no momento deste ADR:** planejamento somente. Este documento não implica tarefas iniciadas, cenas criadas, testes de Unity executados, alterações de pacote nem cronograma aprovado.

## 9. FOUND-01 — Camera Output técnico e validação Unity (2026-10-10)

Este adendo registra uma composição de consumidor observada no `com.immersive.framework` resolvido em `1.1.0-preview.6`. Ele complementa o adendo v2 sem reescrever seu histórico de planejamento.

**Decisão de composição PEGA:**

- O `PEGAApplication` mantém Player Session e Progression Save desabilitados e configura um único Camera Output técnico com Unity Camera, Cinemachine Brain e Fallback Camera Rig, conforme o contrato instalado de `CameraSessionConfiguration`.
- O Output é infraestrutura necessária ao bootstrap desta versão resolvida. Ele não configura Startup Camera Assignment, associação a Player, Actor, alvo ou câmera de gameplay.
- A câmera top-down definitiva continua no cartão #29. FOUND-01 não antecipa movimento nem gameplay.
- O diagnóstico de Preparação, Assalto e Fuga continua sendo somente observabilidade e navegação temporária. Alterar a fase pelo Inspector não prova regras, timer ou gameplay.

**Base técnica observada:** `FrameworkBootValidator.Validate` chama `GameApplicationAsset.CameraSession.TryValidate` sem condicionar a validação à Player Session. `CameraSessionConfiguration.TryValidate` rejeita a lista vazia e valida cada Output quanto à definição explícita, Unity Camera, Cinemachine Brain e Fallback Camera Rig. As localizações e a versão estão registradas no [relatório de validação FOUND-01](../docs/superpowers/reports/2026-10-10-found-01-unity-validation.md). Esta evidência descreve o contrato da versão inspecionada; não prova que o Framework deva exigir esse Output em todas as aplicações ou versões.

**Documentação do Framework a investigar:** o Getting Started descreve uma aplicação navegável sem Player e apresenta Camera entre as features adicionáveis depois do fluxo de navegação. O boot da versão resolvida, porém, valida `CameraSessionConfiguration` incondicionalmente, e essa configuração rejeita zero Outputs. Registrar a divergência como questão de documentação/contrato; não classificá-la como bug antes de o owner do Framework confirmar se Camera Session deve ser obrigatória ou opcional em aplicações sem Player.

**Registro inicial de evidência:** a primeira revisão deste adendo recebeu o relato de bootstrap PEGA, Mission sem Player, Output técnico, diagnóstico no Inspector e transição para Result. Naquele momento, importação/versionamento, validadores e enumeração das fases ainda não estavam comprovados, e a errata Trello estava apenas proposta. Esse retrato é histórico e foi superado pela revisão final abaixo.

**Estado após a revisão final (2026-10-10):** o Editor.log local registra importação do pacote resolvido, boot PEGA/Mission bem-sucedido, Camera Output inicializado, sequência diagnóstica Preparation → Assault → Escape, solicitação Mission → Result concluída com Result Ready e liberação do conteúdo anterior sem bloqueios. A execução dos Inspectors/validadores e a Scene List com as quatro cenas PEGA habilitadas foram confirmadas manualmente pelo usuário; essa evidência é informada, não uma captura anexada. A política UniqueAcrossJoinedSlots foi escolhida para o PEGA e o GameApplication passou a Valid; a política é compatível com o contrato instalado, sem ser regra geral para consumidores.

**Estado da errata e do aceite:** a errata Camera Output do item 2 e seu comentário já foram aplicados ao cartão FOUND-01, preservando o histórico. A revisão final classificou os nove critérios como atendidos, com os limites de proveniência descritos no relatório de validação vinculado. FOUND-01 está tecnicamente aprovado; este adendo não altera nem conclui o estado operacional do cartão Trello.

## 10. Lifecycle da missão e consolidação de resultados (2026-10-10)

Este adendo consolida a auditoria estática de Route, Activity e preservação de estado registrada no [relatório de lifecycle](../../ImmersivePackages/docs/superpowers/reports/2026-10-10-pega-activity-lifecycle-audit.md). Os contratos de Framework abaixo são específicos ao `com.immersive.framework` `1.1.0-preview.6` efetivamente resolvido no PEGA (`Packages/manifest.json`, `packages-lock.json` e cache instalado). Não generalizar para outra versão sem verificar sua instalação. A auditoria não executou Unity ou testes.

### Decisões de composição e ownership

- **Manter uma única Mission Activity** para as fases internas Preparação, Assalto Ativo e Fuga Final, com a Result Activity separada dentro da Mission Route. Esta decisão, já estabelecida no adendo v2, é reafirmada com base na continuidade exigida pelo assalto: Actors, cenário, objetos, conhecimento, posse, relógios e GOAP pertencem à mesma ocorrência de gameplay no desenho do PEGA. As fases não devem provocar transições de Activity sem uma necessidade arquitetural concreta.
- A escolha por uma Activity única é de adequação ao gameplay do PEGA; não é limitação do Framework. Activities independentes são tecnicamente viáveis, mas cada troca estabelece novo contexto Activity e traz fronteiras adicionais de lifecycle, admission, readiness e bindings. O modelo híbrido continua possível se uma etapa futura exigir contexto, apresentação ou lifecycle realmente independente.
- A **Mission Route é a fronteira candidata de lifecycle** para o estado compartilhado de uma ocorrência da missão. O Framework controla o lifecycle da Route; o PEGA continua responsável por criar, manter consistente e encerrar o estado de gameplay. Route Content, serviços de escopo Route e dados associados à ocorrência são possibilidades a avaliar na implementação, não uma decisão fechada de ownership físico ou mecanismo. Route não gerencia nem preserva automaticamente o estado de gameplay.

### Contratos do Framework comprovados na versão resolvida

- `Seamless` seleciona a apresentação sem Transition Surface/Loading Surface; não garante retenção de cena, GameObject, componentes ou estado.
- `KeepOnActivityChange` pode manter carregada uma cena declarada como Activity Content durante a troca interna. Isso preserva os objetos enquanto a cena ficar carregada, mas não transfere por si só ownership semântico nem estado lógico. O conteúdo de Route persiste nas trocas de Activities daquela Route.
- Player Session e a ocorrência física admitida do Player podem continuar entre Activities enquanto a Session/ocorrência permanecer ativa, sem Leave ou substituição. Participação, admission, bindings, readiness e relocation continuam contextuais e podem ser reavaliados na Activity de destino.
- A continuidade física do Player não comprova continuidade de NPCs nem a integração de gameplay de GOAP/Perception. A identidade, ocorrência e lifecycle do Actor seguem os contratos do Framework; conhecimento, regras e integração dos larápios permanecem sob responsabilidade do PEGA. Nenhum desses contratos é inferido da API de Player.

Esses pontos descrevem o contrato e a implementação inspecionados no `.6`; não são evidência de validação Unity de três Activities no PEGA. A auditoria encontrou teste de sequência Exit/Enter e fontes de testes Player, mas nenhum teste executado que prove continuidade integrada de mundo, NPC ou GOAP entre Preparação, Assalto Ativo e Fuga Final.

### Encerramento e Result Activity

Ao terminar a Fuga Final, o PEGA deve resolver os estados finais de larápios e itens e **consolidar os dados necessários ao cálculo do resultado antes de liberar o conteúdo de gameplay requerido por esse cálculo**. A Result Activity consome dados já consolidados e não depende de Actors ou cenas de gameplay que possam ter sido descarregados na transição.

Formato do resultado, owners concretos dos dados e mecanismo explícito de transferência para Result permanecem para a implementação do encerramento no VS-15. Este registro não escolhe uma solução técnica nem cria tarefa nova; mantém a cadeia do entregável VS-15 existente. A confirmação FOUND-01 de transição Mission → Result comprova somente o fluxo observado naquela composição, não este contrato de dados ou um encerramento de missão já implementado.

## 11. PP-36 — Evolução por capacidades antes da composição integrada (2026-10-16; decisão histórica)

**Decisão registrada em PP-36:** orientar a produção pela evolução progressiva das capacidades necessárias à experiência central de **PERSEGUIR**, aprofundando sua implementação antes de exigir uma composição integrada. A prioridade então registrada era: (1) Player; (2) larápios; (3) mecânicas compartilhadas; (4) composição incluindo confronto e recuperação; (5) Demo. A ordem foi refinada e substituída em PP-37, preservando PP-36 como histórico.

Essa ordem expressa prioridade, não fases rígidas, bloqueios universais ou proibição de desenvolvimento paralelo. Uma dependência só ordena trabalho quando a fonte normativa, o contrato ou uma relação de implementação demonstrável mostrar que uma etapa não pode avançar sem outra. Capacidades podem atingir maturidade funcional incrementalmente; não é necessário fechar um pequeno ciclo jogável completo a cada avanço. Implementação funcional e polimento final devem permanecer identificáveis como trabalhos distintos.

**Fronteira do Immersive Framework:** o PEGA adota contratos e capacidades existentes do Framework. Lifecycle genérico, Input Gate, troca de Player e comportamento geral de componentes não são entregáveis nem critérios para revalidar o produto Framework no PEGA. O PEGA continua responsável por corrigir defeitos concretos da sua configuração e integração e por demonstrar o gameplay de sua propriedade. Mudança de Framework segue o repositório proprietário e sua governança própria.

**Relação com decisões anteriores — histórico preservado:**

| Decisões anteriores | Leitura após PP-36 |
|---|---|
| **PP-01 a PP-04** | Reafirmadas: planejar por capacidades e resultados, usar maturidade como atributo de cada capacidade, permitir avanço progressivo e registrar apenas dependências reais. “Demonstrável” não obriga uma composição de missão a cada incremento. |
| **PP-13** | Reafirmada: capacidades jogáveis são o centro. Sistemas técnicos são componentes de suporte; sua implementação isolada não é, por si só, resultado de gameplay. |
| **PP-14** | Reinterpretada: tamanho de entrega deve ser determinado pelo avanço significativo de uma capacidade, e não por uma escala intermediária fixa que fragmente desenvolvimento ou force composição prematura. |
| **PP-15** | Substituída quanto à prioridade: o microciclo não é mais o primeiro resultado que precisa ser composto antes da evolução das capacidades. Sua cobertura permanece como referência de integração dentro do vertical slice, sem criar outro MVP. |
| **PP-16, PP-22 a PP-24** | Mantidas com limite mais claro: trabalho de suporte pode existir quando habilita uma capacidade concreta; não criar entregáveis para repetir prova de infraestrutura do Framework nem checks de consequências triviais. Implementação compartilhada permanece no owner PEGA adequado e é reutilizada. |
| **PP-17** | Mantida como cobertura de gameplay para a composição da perseguição — Player, larápio com GOAP e conhecimento limitado, item, roubo, fuga, perseguição, confronto, queda e recuperação. Não define a ordem de implementação das capacidades. Preparação, captura, cofres, relógios e resolução continuam no escopo do vertical slice conforme MVP, ainda que entrem na composição mais ampla posteriormente. |
| **PP-18** | Substituída como decomposição de produção sequencial. Os rótulos MC-01 a MC-04 são referências históricas/operacionais; não exigem quatro entregas mínimas independentes. MC-04 representa a composição da perseguição quando as capacidades necessárias estiverem maduras o suficiente. |
| **PP-19 a PP-21** | PP-19 permanece como cenário único evolutivo. PP-20 passa a significar autoria espacial mínima para apoiar as capacidades em desenvolvimento, sem exigir um blockout que feche o microciclo. PP-21 mantém contratos reais entre Programação e Level Design, sem criar pontos de integração artificiais ou tornar o cenário uma dependência geral. Refinamentos de proporção, composição dos POIs e apresentação amadurecem com gameplay suficiente e na evolução da Demo. |
| **PP-25 a PP-27 e PP-33 a PP-35** | Mantidas, subordinadas à prioridade de capacidade: ampliar comportamento progressivamente e permitir paralelismo; compor a validação ponta a ponta depois; evoluir a Demo em paralelo quando houver benefício real, sem antecipar polimento como gate; estimar apenas com fundamento e manter horas/datas simuladas como tais. |

**Autoridade e escopo:** PP-36 é decisão de produção; não altera regras do GDD nem categorias/aceites do MVP. Quando a necessidade de esquiva, salto, ameaça, interação ou outra capacidade depender de uma regra ou conteúdo não definido, a decisão permanece limitada ao que o GDD/MVP já sustentam; PP-36 não cria novas regras de jogo.

**Efeito documental na vigência de PP-36:** `TASKS.md` e Trello refletiram aquela prioridade sem alterar estimativas ou evidências. PP-37 registra abaixo a substituição da ordem; mantém-se a regra de preservar histórico e não declarar tarefa concluída sem evidência de aceitação ou Unity validado sem observação real no Editor/runtime.

## 12. PP-37 — Primeiro satisfazer as capacidades da perseguição, depois expandir o gameplay (2026-10-16)

**Decisão vigente:** aprofundar capacidades em incrementos coerentes e aproveitáveis, sem exigir pequenos ciclos artificiais entre sistemas diferentes. A ordem de prioridade é:

1. **Player:** evoluir controles e movimentação, incluindo responsividade, até satisfazer o movimento e as capacidades necessárias à perseguição. Implementação funcional não exige polimento definitivo. Interação com itens, combate, recuperação e mobilidade para obstáculos especiais não são pré-requisitos automáticos; entram antes somente quando uma necessidade concreta da perseguição estiver definida no GDD.
2. **NPCs/larápios:** evoluir movimentação e comportamento autônomo até satisfazer o comportamento necessário à perseguição, usando os sistemas reais do GDD — GOAP, conhecimento, percepção, Actions, Desejos e replanejamento conforme a necessidade. Não substituir comportamento por roteiros ou por uma IA simplificada. A fuga sem loot e a perda/readquisição de um alvo permitem evoluir e avaliar autonomia sem exigir primeiro o sistema de roubo.
3. **Ambiente de teste:** somente depois da suficiência das capacidades de Player e NPC, preparar um ambiente mínimo apropriado para observar sua interação. Isto é uma bancada funcional para a experiência, não a conclusão antecipada do Armazém da Demo.
4. **Composição da perseguição:** integrar Player e larápios e avaliar perseguir, fugir, antecipar rotas, perder contato, recuperar contato e interceptar. A composição central não exige itens, roubo, combate ou recuperação como aceites prévios.
5. **Expansão progressiva:** quando a experiência central de perseguição estiver satisfatória, acrescentar mecânicas do GDD em incrementos coerentes, incluindo itens, investigação/roubo, combate/confronto, recuperação/captura, obstáculos especiais e demais sistemas necessários ao MVP. No fluxo de roubo, se o Player obtiver o item primeiro, o larápio só altera sua decisão depois de adquirir informação válida, conforme o GDD.
6. **Vertical slice e Demo:** amadurecer a experiência composta até cobrir todo o escopo final do MVP e, depois, completar conteúdo, Level Design, animações, apresentação, cooperação e refinamento da Demo.

A ordem é de prioridade e as dependências explicitadas nos itens 3 e 4 são gates reais: não iniciar a preparação do ambiente de integração nem a composição conjunta antes que Player e NPCs estejam satisfatórios para seus objetivos atuais; não exigir a Demo completa para testar a perseguição. Cada capacidade pode avançar por incrementos internos aproveitáveis, sem que cada incremento crie um aceite isolado de sistema nem force integração prematura.

**Base normativa sem alteração:** o GDD define perseguição como experiência central, velocidades de movimento/perseguição e comportamento GOAP, inclusive perda do alvo com uso da última posição conhecida e readquisição pela percepção normal. O GDD também permite que um larápio escolha fugir antes da Fuga Final sem carregar loot. Isso sustenta avaliar a perseguição antes do roubo; não altera regras de percepção, planejamento ou fuga. Os objetivos de perseguir/interceptar, confrontar/neutralizar e concluir captura permanecem distintos no GDD. O MVP continua incluindo Preparação, invasão, roubo, recuperação/captura, fuga e resultados no produto final.

**Fronteira do Immersive Framework:** permanece adotado como infraestrutura. Não criar tarefa de revalidar lifecycle, Input Gate, troca de Player ou comportamento genérico de componente; corrigir apenas falhas concretas de integração PEGA e validar o gameplay de propriedade do PEGA.

**Relação com decisões anteriores — histórico preservado:**

| Decisões | Leitura após PP-37 |
|---|---|
| **PP-01 a PP-04 e PP-13** | Mantidas: evolução por capacidades e dependências reais; incrementos devem ser coerentes e contribuir à experiência, sem exigir composição completa a cada passo. |
| **PP-14, PP-15 e PP-18** | Mantidas as revisões de PP-36 contra entregas artificiais; PP-37 especifica a sequência atual. Os rótulos MC-01 a MC-04 continuam rastreáveis, mas o microciclo antigo não define a primeira composição prioritária. |
| **PP-16, PP-22 a PP-24 e PP-26** | Mantidas: suporte necessário pertence ao owner adequado, checks não repetem consequências triviais ou testes do Framework, e a aceitação integrada ocorre após evolução das capacidades. |
| **PP-17** | Preservada como cobertura de gameplay do vertical slice; reordenada. A avaliação inicial da perseguição pode anteceder itens/roubo/confronto/recuperação, que continuam necessários no escopo final conforme GDD/MVP. |
| **PP-19 a PP-21** | O mesmo Armazém continua evolutivo. A preparação de um ambiente para integrar Player/NPC ocorre somente após essas capacidades estarem satisfatórias; Level Design completo e refinamentos continuam posteriores. |
| **PP-25 a PP-27 e PP-33 a PP-35** | Mantidas no que não conflita: estimativas somente com fundamento, preservação das projeções históricas, evolução incremental e maturação posterior da Demo. PP-27 não autoriza antecipar acabamento ou ambiente conjunto como gate da perseguição. |
| **PP-36** | Preservada como decisão histórica da revisão anterior. PP-37 substitui especificamente sua ordem que antecipava mecânicas compartilhadas/itens e blockout para antes da primeira avaliação da perseguição. A salvaguarda de não retestar internals do Framework permanece. |

**Autoridade e escopo:** PP-37 altera estratégia e ordem de construção, não regras do GDD nem escopo final/aceitação do MVP. Não elimina Preparação, invasão, itens/roubo, confronto, recuperação/captura, Fuga Final, resultados, Demo ou cooperação. Não cria regras para antecipação/interceptação nem critérios de “satisfatório” além dos contratos e evidências de gameplay aplicáveis.

**Efeito documental:** atualizar `TASKS.md`, o roadmap operacional e Trello para tornar explícita a sequência Player → NPCs → ambiente de teste → composição central da perseguição → expansão das mecânicas → vertical slice/Demo. Preservar status, checklists, evidências, estimativas e datas; mover checks que agora pertencem a etapas posteriores sem marcá-los concluídos. Não recalcular cronograma sem base de capacidade/estimativa e não registrar validação Unity sem observação real.

## 13. Diretriz arquitetural transversal do PEGA (2026-10-25)

**Decisão:** os sistemas do PEGA evoluem preferencialmente por composição de capacidades e responsabilidades independentes. Novas mecânicas, comportamentos, fontes de conhecimento, critérios de avaliação e perfis devem poder ser acrescentados ou substituídos por contratos compatíveis, sem exigir alterações recorrentes em seus núcleos.

- **Modularidade:** responsabilidades delimitadas e dependências justificadas.
- **Intercambialidade:** alternativas compatíveis podem cumprir o mesmo contrato e ser combinadas por configuração.
- **Escalabilidade:** ampliar repertório e complexidade com estrutura sustentável e custos operacionais controláveis.

Esta diretriz vale para os sistemas do PEGA, inclusive quando não pertencem ao GOAP. Ela orienta decisões de arquitetura sem exigir que tudo seja abstrato, intercambiável ou configurável. Soluções específicas são legítimas quando generalizar não acrescenta reutilização ou clareza; o núcleo muda quando surgir uma necessidade semântica geral comprovada, e não apenas pela adição de uma mecânica. A diretriz não cria uma regra de gameplay, um pacote, serviço ou infraestrutura genérica obrigatórios, nem altera fronteiras de ownership por implicação. A decisão explícita da §7 atualiza essa fronteira para os mecanismos GOAP e Perception, preservando as regras específicas em cada jogo.
