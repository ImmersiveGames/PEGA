# PEGA — Planejamento de produção do vertical slice e da Demo

**Status:** backlog derivado do escopo aprovado e da estratégia vigente em ADR PP-37
**Versão:** 0.6.0
**Atualização:** 2026-10-16
**Escopo:** capacidades de gameplay do vertical slice e evolução posterior da Demo

Este backlog operacionaliza o [MVP.md](MVP.md). O [GDD.md](GDD.md) permanece normativo para regras e comportamento; o MVP define escopo e aceitação; o [ADR-PREPRODUCAO.md](ADR-PREPRODUCAO.md) registra decisões de produção; este arquivo organiza trabalho executável. Nenhuma tarefa acrescenta ou remove regra de jogo.

## Estratégia de execução vigente — PP-37

Priorizar: **(1)** controles, movimento e capacidades do Player necessárias à perseguição; **(2)** movimento e autonomia dos NPCs com sistemas reais do GDD; **(3)** preparar somente então um ambiente mínimo para observar essas capacidades juntas; **(4)** compor e avaliar a perseguição central; **(5)** depois expandir progressivamente o gameplay; **(6)** amadurecer o conjunto até o vertical slice completo e a Demo. Cada capacidade deve avançar em incrementos coerentes e aproveitáveis. Não fechar microciclos artificiais entre sistemas nem exigir polimento definitivo antes de avaliar a perseguição.

O ambiente de teste conjunto depende da suficiência de Player e NPCs; a composição depende desse ambiente. Isso não exige o Armazém completo nem a Demo para o primeiro teste integrado de perseguição. O escopo final do MVP permanece intacto.

O Immersive Framework é infraestrutura adotada pelo PEGA. Usar contratos/capacidades existentes; não criar tarefas ou checks para repetir validações genéricas de lifecycle, Input Gate, troca de Player ou comportamento interno de componentes. Corrigir falhas concretas de integração PEGA e validar gameplay pertencente ao PEGA.

**Frente imediata:** evolução funcional do Player em [#80 — responsividade do controle](https://trello.com/c/5vyBuvLX), em continuidade ao movimento-base de [#34](https://trello.com/c/6pE7J5R0). O #34 continua incompleto: colisão e aceite de gameplay permanecem pendentes. A evidência estática de blockout em [#53](https://trello.com/c/t7gPfzuM) não é aceite Unity nem prioridade atual de implementação.

## 1. Player — controles e movimento para perseguição

- [ ] **VS · Movimento-base e velocidades configuráveis** — movimento do Actor relativo à câmera, projeção no solo, aceleração, rotação, parada, limite diagonal e velocidades especializadas conforme GDD, sem atributo universal de Agilidade. Rastreabilidade: [#34 — movimento e colisão](https://trello.com/c/6pE7J5R0). E03–E04 do cartão permanecem pendentes; a checagem de colisão com o ambiente conjunto ocorre após a preparação do estágio 3.
- [ ] **VS · Responsividade funcional do controle** — evoluir frenagem/desaceleração, resposta a inversões e intensidade analógica proporcional, preservando controles digitais previsíveis, orientação, direção relativa à câmera e limite de velocidade. Rastreabilidade: [#80 — resposta do controle](https://trello.com/c/5vyBuvLX). A parte funcional antecede NPCs e composição; Inspector final, tuning definitivo, extensão para Petra/P2 e sinais visuais de animação permanecem polimento posterior. Não reimplementar Input Reader/Input Gate do Framework.
- Esquiva, salto, interação contextual, combate e recuperação não são pré-requisitos universais da perseguição. Antecipá-los para esta prioridade somente se uma necessidade concreta estiver sustentada pelo GDD; caso contrário, evoluem após a avaliação da perseguição central.

## 2. NPCs/larápios — movimento e autonomia para perseguição

- [ ] **VS · Locomoção e comportamento autônomo com GOAP real** — evoluir movimento/velocidade de acordo com a mecânica do Actor e o GDD; usar conhecimento legítimo, percepção, Actions, Desejos e replanejamento na medida necessária para larápios reagirem, fugirem e lidarem com perda/readquisição de alvo sem informação oculta, sequência roteirizada ou FSM substituta. Rastreabilidade dos work packages: [#63 Actions](https://trello.com/c/KAbJVyUl), [#64 Desejos](https://trello.com/c/iWwXiDee), [#68 Knowledge State](https://trello.com/c/WzynxvkM), [#69 percepção](https://trello.com/c/KQmXJWoa), [#72 replanejamento](https://trello.com/c/5ruY3fkZ) e [#76 autonomia para perseguição](https://trello.com/c/T42AjD8K). #66 e #71 só entram se os casos de contingência/conflito do GDD os exigirem.
- A suficiência do NPC é avaliada no comportamento autônomo pertinente à perseguição, não pela conclusão de roubo, inventário ou combate. O GDD permite fuga sem loot e define o comportamento após perda de um alvo; isso permite desenvolver essa capacidade antes da expansão por itens.
- [ ] **VS · Investigação, POIs e roubo GOAP** — desenvolver busca por oportunidade, investigação ativa, descoberta legítima e roubo com Actions executáveis depois da composição central, sem abandonar o GOAP real nem simplificar as regras do GDD. Rastreabilidade: [#70 — investigação](https://trello.com/c/hFYlIZ5l), extensões do [#63](https://trello.com/c/KAbJVyUl) e aceitação expandida do [#76](https://trello.com/c/T42AjD8K).
- [ ] **VS-14 · Fuga Final e relógios** — manter GOAP ativo e aplicar contextos/receitas temporais conforme GDD/MVP quando o gameplay expandido e o vertical slice forem compostos. Rastreabilidade: [#73 — temporizadores e resultado](https://trello.com/c/pyQ3yyzf); contextos globais/satisfação de Desejos em [#65](https://trello.com/c/oziocz9k) e [#67](https://trello.com/c/1vUPA1IG).

## 3. Ambiente de teste conjunto

- [ ] **LD-01 · Ambiente mínimo de perseguição** — somente após Player e NPCs satisfazerem seus objetivos atuais, preparar na Mission geometria transitável, obstáculos/colliders e referência de saída suficientes para observar fuga, perseguição, perda/readquisição de contato e interceptação. Não validar layout ideal, conteúdo completo, cofres, prisão, invasão ou apresentação da Demo neste estágio. Rastreabilidade: [#53 — ambiente de teste e evolução do Armazém](https://trello.com/c/t7gPfzuM). E01 permanece com inclusão estática registrada, mas sem aceite manual no Unity; E02–E03 permanecem abertos.

## 4. Composição da perseguição central

- [ ] **VS · Compor e avaliar a perseguição PEGA** — depois da suficiência do Player/NPC e do ambiente mínimo, integrar as capacidades para observar Player perseguir, larápio fugir, antecipação de rota, perda/readquisição de contato e interceptação, conforme GDD. Não exigir item, roubo, combate/confronto ou recuperação como pré-condição desta primeira composição. Rastreabilidade: [#77 — composição da perseguição](https://trello.com/c/YXKa2Hvi).
- [ ] **VS · Critério de saída da perseguição central** — registrar evidência real de gameplay e limitações; prosseguir para expansão quando os comportamentos especificados estiverem satisfatórios. Não definir regras novas nem usar validação genérica do Framework como aceite. O critério deve permanecer ligado aos contratos do GDD, sem inventar limiar numérico de satisfação.

## 5. Expansão progressiva até o vertical slice completo

Depois da perseguição central satisfatória, desenvolver os demais resultados necessários ao MVP em incrementos coerentes, preservando suas fronteiras e a autoridade de posse:

- [ ] **VS · Interação contextual do Player** — seleção de alvos por contexto/alcance e distinção entre interação, ataque e soltura. Rastreabilidade: [#37 — interação contextual](https://trello.com/c/wWbleLBj).
- [ ] **VS · Itens, posse e transferência** — disponibilidade, portador, posse exclusiva, coleta, soltura, roubo, recuperação e depósito sem duplicação/perda; capacidade configurável por Actor. Rastreabilidade: [#38 — posse e soltura](https://trello.com/c/dCc2lbu7), [#70 — investigação/roubo](https://trello.com/c/hFYlIZ5l) e [#83 — depósito em cofre](https://trello.com/c/687JSVZ0).
- [ ] **VS · Confronto** — aplicar ataque válido, Capacidade de Ataque × Resistência, incapacitação e queda de itens conforme GDD; sem HP ou desgaste acumulativo. Rastreabilidade: [#40 — ataque e confronto](https://trello.com/c/7EGgunYM).
- [ ] **VS · Recuperação e captura** — recuperação temporizada, transporte e captura efetiva por entrega na prisão; manter as restrições de ação, posse e inventário previstas no GDD. Rastreabilidade: [#42 — recuperação](https://trello.com/c/BJUgV7EY) e [#39 — captura](https://trello.com/c/Trj9hyl6).
- [ ] **VS · Esquiva, salto e obstáculos especiais** — incluir conforme necessidade do recorte sustentada pelo GDD; não inventar ameaça/obstáculo nem torná-los pré-requisitos da perseguição básica. Rastreabilidade: [#35 — esquiva](https://trello.com/c/ePIM6alK) e [#36 — salto](https://trello.com/c/Vnmzl21P).
- [ ] **VS · Preparação e invasão** — configurar duração, escolha de cofre, Spawn Seguro e receitas/relógios de invasão segundo GDD/MVP, sem misturar relógios ou declarar pronto/skip fora das regras aprovadas.
- [ ] **VS · GOAP de roubo, proteção e recuperação** — integrar investigação/descoberta de item, ações de roubo, replanejamento por informação válida, posse, confronto e recuperação conforme GDD; se o Player obtiver o item antes, o larápio só altera sua decisão depois de informação legítima. Não substituir o GOAP por roteiro.
- [ ] **VS · Encerramento completo** — compor Preparação, invasão, Assalto, Fuga Final, estados Capturado/Escapou/Não escapou, resolução de itens e Result Activity conforme GDD/MVP; preservar os dados necessários antes de descarregar conteúdo de gameplay.
- [ ] **VS · Feedback funcional e sinais mínimos** — comunicar os estados e timers necessários para observar o gameplay expandido; apresentação/animação finais não bloqueiam comportamento funcional.
- [ ] **VS-16 · Aceitação integrada do vertical slice** — validar manualmente no Unity os cenários solo exigidos pelo MVP após as capacidades correspondentes estarem prontas. Registrar versão, evidência e limitações reais; não repetir testes internos genéricos do Framework.

## 6. Demo — cooperação, conteúdo e refinamento

- [ ] **DEM-01/02 · Dois Players e gameplay cooperativo** — entrada/seleção e controle independente de Rick/Petra, câmera/split-screen e operações compartilhadas conforme MVP; exercitar conflitos sem perda/duplicação de estado. Rastreabilidade: [#28 — entrada e seleção local](https://trello.com/c/oUlBYsNI).
- [ ] **DEM-03 · Gangue da Demo** — aplicar mecânicas e GOAP compartilhados aos quatro arquétipos dos Trapalhões do Crime, variando configurações conforme GDD sem duplicar a lógica.
- [ ] **DEM-04 · Armazém completo e conteúdo** — evoluir o ambiente de teste para Level Design, conteúdo e apresentação da Demo a partir de gameplay real; incluir cofres, prisão, Spawn Points/Spawn Seguro, item principal e rotas da P.G.A. Larópolis conforme MVP. Rastreabilidade: [#53](https://trello.com/c/t7gPfzuM), [#81 — circulação](https://trello.com/c/5Qliadzo) e [#82 — POIs/leitura](https://trello.com/c/r4t1CVUL).
- [ ] **DEM-05 · HUB e fluxo da carreira** — seleção de missão/Briefing, entrada na Mission, resultados/resumo e retorno ao HUB conforme MVP.
- [ ] **DEM-06 · Economia e classificação** — implementar critérios e valores configuráveis do GDD/MVP; não inventar estimativa ou progresso.
- [ ] **DEM-07 · Persistência e repetição** — persistir carreira entre assaltos, sem salvar/retomar assalto em andamento; reinício explícito após bancarrota.
- [ ] **DEM-08 · Apresentação e animações** — clips, Animator/representação, HUD, áudio e feedback final para capacidades funcionais. Rastreabilidade: [#52 — animações de gameplay](https://trello.com/c/zZ0r1BZa).
- [ ] **DEM-09 · Aceitação da Demo** — validar manualmente um e dois Players, carreira, cooperação, gameplay, apresentação, Pause, resultados e persistência, registrando evidência real.

## Fundação e integração PEGA–Framework já aceitas

- [x] **FOUND-01 · Composição PEGA e navegação Mission → Result sem Player.** Aceite e evidências Unity registrados no [relatório FOUND-01](../docs/superpowers/reports/2026-10-10-found-01-unity-validation.md) e no [cartão #79](https://trello.com/c/5BvgU409). Esse resultado cobre somente a fundação/fluxo observado; não comprova movimento, perseguição, gameplay, GOAP ou o encerramento funcional do assalto.
- Capacidades públicas do Framework são consumidas conforme a necessidade concreta da integração PEGA; não existe entregável aberto para revalidar comportamento genérico do pacote.

## Pós-Demo, opcional ou sujeito a playtest

- [ ] Avaliar orçamento de Preparação, investimentos de segurança, novos cenários/gangues, conteúdo narrativo e power-ups conforme GDD/MVP.
- [ ] Avaliar proteção de armários, habilidades por arquétipo, duração de timers, pesos de classificação e balanceamento sem alterar regras-base sem decisão no GDD.

## Referências

- [GDD — contratos completos](GDD.md)
- [MVP — escopo por entrega](MVP.md)
- [ADR de pré-produção — decisões de produção](ADR-PREPRODUCAO.md)
- [Art Book — direção visual](ArtBook.md)

## Histórico de revisão

| Versão | Data | Alteração |
|---|---|---|
| 0.6.0 | 2026-10-16 | Reordenada a produção para satisfazer Player e NPCs, preparar ambiente conjunto, compor a perseguição central e depois expandir gameplay; preservado todo o escopo final do MVP. |
| 0.5.0 | 2026-10-16 | Reordenado o backlog por capacidades do Player, dos larápios, mecânicas compartilhadas, composição do gameplay e evolução da Demo; antecipada a parte funcional da responsividade; separadas fronteiras de Framework, Level Design, captura e depósito; registrada evidência de FOUND-01 concluído. |
| 0.4.1 | 2026-10-10 | Corrigido o critério FOUND-01 para explicitar o Camera Output técnico exigido pelo bootstrap, sem Player Session, assignment de Player ou câmera de gameplay; registrada a evidência Unity disponível. |
