# PEGA — Escopo do vertical slice e da Demo

**Projeto:** `PEGA`
**Tipo de documento:** Escopo de implementação
**Status:** Ativo
**Versão:** 0.4.0
**Última atualização:** 2026-10-10
**Fonte de regras:** [GDD](GDD.md)

---

## Propósito e categorias

Este documento recorta as regras do GDD em duas entregas. O GDD é a fonte normativa de gameplay; este arquivo define **quando** cada parte entra. A presença de uma tarefa ou sistema no escopo não comprova que já esteja implementado.

| Categoria | Significado |
|---|---|
| **Obrigatório no vertical slice** | Necessário para provar o ciclo jogável técnico mínimo. |
| **Obrigatório na Demo** | Necessário para a entrega demonstrável, além do slice. |
| **Posterior à Demo** | Parte possível do jogo completo, fora da Demo. |
| **Opcional / sujeito a playtest** | Só entra se escopo e evidência de playtest justificarem. |

## A. Primeiro vertical slice técnico

**Objetivo:** comprovar um ciclo jogável de Preparação, invasão, perseguição, roubo, recuperação/captura, fuga e resultado. Pode funcionar com um Player local. Os larápios usam GOAP desde este corte; não há IA alternativa baseada em máquina de estados.

| Área | Recorte obrigatório |
|---|---|
| Immersive Framework | Configurar apenas as superfícies necessárias na versão resolvida do projeto. O projeto declara `com.immersive.framework` **1.1.0-preview.6**; a API pública instalada deve ser consultada em `Documentation~/API/Public-API.md` e validada antes de cada uso. Esse número e os assets existentes não provam funcionamento integrado. |
| Player e Actor | Um Player controlável, solo. Movimento e mecânicas de gameplay pertencem ao PEGA; ActorProfile e alguns caminhos de Player do framework são experimentais. Usar e validar o caminho de autoria suportado pela instalação atual. |
| Cenário | Armazém compacto, com rotas, pelo menos dois cofres válidos, item principal, prisão, Spawn Point e Spawn Seguro. Completar leitura do mapa e interações necessárias ao ciclo. |
| Preparação | Duração fixa da missão; escolha gratuita entre cofres; um cofre padrão válido. O tempo encerra a fase. Não existe `Pronto` nem skip no jogo normal; antecipação é apenas ferramenta de debug. |
| Invasão | Receita temporal de Spawn Point começa no Assalto Ativo. Preparação e receitas são relógios separados. Manter o modelo de receitas independentes e o Spawn Seguro do GDD, mesmo que o cenário de teste autorize apenas um Spawn Point. |
| IA dos larápios | Um grupo inicial e pelo menos um arquétipo usando o contrato GOAP do GDD: conhecimento sem acesso a fatos ocultos, Desejos, Actions com execução e resultados explícitos, replanejamento por eventos, roubo e tentativa de fuga. |
| Percepção e conhecimento | Área radial percebida e cone de visão configuráveis, com obstruções. Preservar escopos Individual, Gangue e Global; só propagar os dados cuja política de Knowledge Data permitir. |
| Perseguição e captura | Movimento, ataque, Capacidade de ataque × Resistência, incapacitação temporária, recuperação, transporte e entrega efetiva na prisão. Só a entrega conclui a captura. |
| Itens e armazenamento | Inventário configurável por Actor, uma unidade por item, comando separado para soltar todos os itens, ataque que derruba todos os itens do alvo e depósito no cofre. Exercitar capacidade insuficiente com depósito parcial determinístico. |
| Encerramento | Tempo de Assalto, Fuga Final com GOAP ativo, Tempo de Fuga, estados Capturado/Escapou/Não escapou, classificação/resultados básicos e itens Roubados/Recuperados conforme o GDD. Não exigir métricas temporais no slice. |
| UI de prova | Controles e feedback suficientes para observar Preparação, cofre selecionado, inventário, incapacitação/transporte, temporizadores e resultado. Não exige acabamento da UI da Demo. |

### Não confundir com o vertical slice

Testes temporários de Restart Point, Scene-Provided Player, composição do Framework ou uma cena de prova são experimentos técnicos. Não são funcionalidades de PEGA, conteúdo de carreira, menu ou critério de conclusão do jogo. Só entram no escopo quando a evidência do teste for aplicada a uma configuração real do PEGA.

## B. Demo completa

**Objetivo:** entregar uma experiência demonstrável com fluxo de carreira, interface integrada e cooperativo local em tela dividida para dois Players. Cooperativo local é requisito desta entrega, não stretch goal.

| Área | Acréscimo obrigatório à Demo |
|---|---|
| Players | Rick e Petra selecionáveis e mecanicamente equivalentes. Dois Players locais com tela dividida, cada um com entrada, Actor e apresentação corretos. |
| Framework | Fluxo entre telas e atividades, autoria dos Players, câmera, Pause, transições e persistência compostos pelas capacidades existentes e compatíveis do Framework. Não duplicar autoridade já pertencente ao pacote. |
| Armazém | Cenário inicial completo, incluindo level design, conteúdos, caminhos, Spawn Points, Spawn Seguro, objetos e condições de captura/escape legíveis. |
| Inimigos | Uma gangue da Demo, Os Trapalhões do Crime, com seus quatro arquétipos configurados sobre mecânicas disponíveis. Os comportamentos usam o GOAP compartilhado do PEGA, não uma implementação diferente por arquétipo. |
| Preparação cooperativa | Ambos os Players podem mudar a seleção do cofre; a última escolha válida prevalece. Ambos recebem a seleção atual e ela não é divulgada automaticamente aos larápios. |
| UI/UX | Fluxo de menus, HUB da Demo como escritório estático com hotspots, briefing, splash, HUD, feedback de critérios e resultados, resumo do turno, Pause e telas de bancarrota. |
| Carreira | Repetir a missão do Armazém na Demo, acumular créditos líquidos (créditos da classificação menos custo operacional provisório de 150), bancarrota apenas com saldo estritamente menor que -1000, e reinício explícito da carreira com saldo zero. Configurar os valores para playtest. |
| Persistência | Salvar carreira entre assaltos. Não salvar nem retomar gameplay em andamento. Configurar e validar o backend escolhido no Framework; a documentação instalada classifica perfil e backend JSON como experimentais. |
| Classificação | Critérios e pesos por missão, sem critério universalmente dominante; comunicar ao jogador os fatores considerados. Configurar a classificação da missão da Demo. |
| Arte e áudio | Assets, animações e identidade visual necessários para a Demo, apoiados pelo Art Book e pelos pacotes instalados. A lista completa de ativos pertence à produção, não a este contrato de gameplay. |
| Verificação | Cenários manuais de um e dois Players, preparação concorrente, fluxo completo de assalto, captura, escape, resultado, save entre missões, Pause e encerramento de carreira. Registrar resultados reais; não inferir validação pela presença de assets. |

### Apresentação e fluxo de telas da Demo

O HUB é o escritório estático da P.E.G.A. Larópolis, sem navegação livre. O quadro com o mapa abre a seleção de missão; a escrivaninha/PC apresenta o saldo da carreira e a inbox de resultados, mensagens de clientes e contratos desbloqueados. Como a Demo tem uma missão, contratos futuros aparecem apenas como espaço de apresentação, sem função de desbloqueio. A forma navegável do HUB permanece posterior à Demo.

O fluxo de apresentação cobre splash/logo, menu principal, opções, HUB, entrada e seleção de Players/personagem, seleção de missão, briefing do cenário, apresentação da gangue, gameplay com Pause, classificação, resumo financeiro do turno e retorno ao HUB ou encerramento/reinício da carreira após bancarrota.

O escopo de PEGA prevê cooperativo local em tela dividida; multiplayer online não está planejado para este jogo.

## C. Posterior à Demo

- HUB navegável ou outro formato além do escritório estático da Demo.
- Novos cenários, contratos, gangues, campanha expandida e progressão de desbloqueios.
- Modo Endless.
- Habilidades e conteúdos não necessários à gangue e ao Armazém da Demo.
- Novos backends de progressão ou políticas de persistência, se necessários.

## D. Opcional ou sujeito a playtest

- Power-ups: não entram no primeiro MVP; inclusão e efeitos em entregas posteriores dependem de escopo e playtests.
- Métricas temporais na classificação do primeiro vertical slice; fazem parte do modelo completo, mas podem não pontuar nesse corte.
- Ordem estável atual de depósito parcial; manter substituível e revisar com playtests.
- Ajustes de custo operacional, limite de bancarrota, pesos de classificação, duração de Preparação, Assalto e Fuga, e balanceamento por arquétipo.

## Dependências e decisões técnicas pendentes

- Player/Actor, ActorProfile, configuração de câmera e Progression Save possuem maturidades diferentes no `1.1.0-preview.6`; confirmar a composição e as limitações pelas superfícies instaladas antes de assumir cobertura.
- A configuração do projeto tem `GameApplication`, Route e Activity, mas não tem `ProgressionSaveProfile` atribuído ao GameApplication. A tarefa de save exige autoria e validação no PEGA.
- O contrato de Pause instalado documenta escopo single-player; a política de Pause para dois Players na Demo requer verificar o suporte atual e tomar decisão de produto se a API não cobrir o comportamento desejado.
- Tempos, critérios, pesos e curva de crédito específicos da missão continuam sendo dados de balanceamento a definir e testar.

## Documentos relacionados

| Documento | Uso |
|---|---|
| [GDD](GDD.md) | Regras, contratos e arquitetura conceitual; fonte normativa. |
| [TASKS](TASKS.md) | Backlog executável e critérios de aceitação derivados deste escopo. |
| [Art Book](ArtBook.md) | Direção visual e referências. |

## Histórico de revisão

| Versão | Data | Alteração |
|---|---|---|
| 0.4.0 | 2026-10-10 | Separados o primeiro vertical slice técnico e a Demo completa; atualizado escopo do multiplayer, GOAP, preparação, inventário, carreira, framework e níveis de entrega. |
