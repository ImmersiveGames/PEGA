# PEGA — ADR de Pré-produção

**Projeto:** PEGA (Unity 6)  
**Tipo:** registro de decisões de planejamento de produção  
**Status:** decisões PP-01 a PP-35 consolidadas para revisão/incorporação  
**Data-base:** 2026-10-10  
**Destino proposto no repositório:** `PEGA GDD/ADR-PREPRODUCAO.md`  
**Escopo:** microciclo, vertical slice técnico, Demo e estratégia de evolução do ecossistema Immersive Games.

> **Precedência documental:** `GDD.md` define regras e arquitetura de gameplay; `MVP.md` define o recorte obrigatório do vertical slice e da Demo; `ArtBook.md` orienta a apresentação; este ADR registra decisões de **produção**, granularidade, estimativa, integração e responsabilidades; `TASKS.md` representa trabalho executável. Este ADR **não altera** GDD/MVP nem declara implementado aquilo que é apenas planejado. Divergências de requisitos devem ser resolvidas na fonte normativa, não por novas regras neste ADR.

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
- **Extração pós-Demo:** implementar e validar primeiro no PEGA; somente **depois da Demo** considerar promoção de capacidades maduras ao Immersive Framework ou a package independente. Candidatura não é decisão de extração. Não generalizar prematuramente nem incluir publicação de packages no esforço do MVP/Demo.

## 3. Catálogo inicial de entregáveis

Identificadores provisórios de planejamento; não implicam ordem rígida de início ou datas.

### 3.1 Primeiro microciclo (subconjunto do vertical slice)

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

- **MC-01, MC-02 e LD-01** podem avançar em paralelo; MC-03 pode usar alvo controlado; **MC-04 depende da integração funcional** dos anteriores.
- A implementação de referência de componentes compartilhados pertence ao primeiro entregável que precisa deles; os demais integram ou estendem, sem duplicar autoridade.
- **Programação** explicita requisitos de navegação, colisão, percepção, interação e pontos de integração; **Arte/Level Design** fornece o blockout e o evolui sem congelar geometria prematuramente.
- Entregáveis da Demo podem iniciar investigação técnica antecipada, **sem antecipar sua conclusão como condição do microciclo**.
- Os critérios de aceite estão dentro de cada entregável; somente uma demonstração reproduzível e checklist cumprido autorizam conclusão.

## 5. Immersive Framework × código do PEGA

### Fronteira de responsabilidade

**Framework:** inicialização de aplicação, Route/Activity, Session e participação Player, composição e ciclo de vida do Actor/Host, input e disponibilidade de gameplay, câmera, superfícies públicas compatíveis de Pause e armazenamento, conforme maturidade e escopo reais.

**PEGA:** locomotion e gameplay físico, interação/posse/inventário, confronto, conhecimento/percepção de larápios, GOAP, arquétipos, assalto, regras financeiras, apresentação específica e integração das capacidades.

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
- O cartão #75 passa a cobrir somente integração Player/Actor, Player Slot, ActorProfile, Scene-Provided, Input Gate e validação de lifecycle. Câmera definitiva e movimento permanecem nos cartões #29 e #34.
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

## 7. Política de evolução pós-Demo

**Decisão:** desenvolvimento das capacidades de gameplay permanece inicialmente no PEGA. Somente após concluir a Demo avaliaremos, sem compromisso de extração, se alguma capacidade está suficientemente madura para:

1. **Permanecer no PEGA:** depende da fantasia, conteúdo ou regras particulares do jogo.
2. **Ser incorporada ao Immersive Framework:** responsabilidade genérica alinhada a uma autoridade transversal já existente no framework.
3. **Tornar-se package independente:** sistema reutilizável com fronteira própria e sem dependência indevida do domínio PEGA.

GOAP, percepção/conhecimento e inventário/posse são **exemplos de candidatos à avaliação**, não decisões de produto, pacotes aprovados nem exigências de generalização antecipada. Arquétipos e conteúdo da gangue permanecem no domínio PEGA. A decisão futura requer evidência de reutilização, independência de domínio, contrato suficientemente estável, custo de manutenção e testes. Não aumentar escopo ou estimativas atuais para preparar publicação de pacotes.

## 8. Encaminhamento e integridade

1. Usar este ADR como **registro de decisões**; não gerar outro catálogo normativo de gameplay.
2. Verificar, antes de planejar implementação, a API do Framework **efetivamente instalada** e a diferença entre os protótipos/QA e as capacidades de produção do PEGA.
3. Manter a primeira validação jogável no MC-01 a MC-04 + LD-01, sem confundi-la com o vertical slice completo.
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
