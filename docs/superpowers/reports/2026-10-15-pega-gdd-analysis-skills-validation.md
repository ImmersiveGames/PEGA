# Validação das skills de análise do GDD PEGA

## Skill: `pega-player-cycle-analysis`

### Cenário baseline sem skill

Pedido ao agente: ler GDD e MVP e descrever somente o ciclo macro vivido pelo jogador, com referências e escopo; não prescrever implementação nem preencher regras ausentes.

### Resultado observado

O agente manteve a perspectiva do jogador, separou decisões dos NPCs como simulação e não inventou APIs ou cenas. Produziu cinco fases macro, mas agrupou no mesmo objetivo a escolha de personagem, escolha de missão, briefing, escolha do cofre e preparação. A escolha de personagem não apareceu como resultado observável próprio. Esse agrupamento esconde escolhas com resultados independentes, mesmo sem detalhar controles.

### Mudança mínima prevista

O contrato da skill pede que escolhas com resultados observáveis independentes permaneçam visíveis, sem converter cada interação ou controle em atividade. A síntese posterior decide as fronteiras de entregáveis.

### Pós-skill

O mesmo pedido foi executado por outro agente em contexto novo com as instruções da skill. A saída manteve os ciclos e fases do jogador; mostrou separadamente modo de jogo, escolha de personagem, escolha de missão, proteção, assalto, fuga, resultado e continuidade. Preservou referências e recorte slice/Demo/posterior/opcional, manteve NPCs e relógios como causas do que o jogador vê e registrou lacunas sem decidir por elas. Não indicou cenas, APIs, componentes, scripts ou assets. **Resultado: passou** no critério do cenário. A resposta de validação teve mais detalhe que uma síntese final precisa, mas isso é escolha de concisão da análise, não fragmentação em microcontroles; nenhuma correção da skill se mostrou necessária.

### Deployment

Commit local `f207b12` contém somente `.agents/skills/pega-player-cycle-analysis/SKILL.md`; sem push.

## Skill: `pega-gameplay-cycle-analysis`

### Cenário baseline sem skill

Pedido ao agente: ler GDD e MVP e descrever somente o ciclo macro de gameplay/mundo, cobrindo tempo/transições, NPCs, objetos/itens, power-ups, resolução, fontes e escopo; não prescrever implementação.

### Resultado observado

O agente cobriu os domínios pedidos, manteve as ações do jogador distinguíveis dos resultados do mundo e não prescreveu implementação. Porém, a análise ficou extensa e listou mecânicas de baixo nível (por exemplo, consequências de ataques, atrasos e entrada sincronizada) como detalhes centrais do ciclo. Também descreveu um inventário amplo de objetos previsto pelo GDD sem classificar claramente quais itens são entregas obrigatórias do slice, apesar de o MVP exigir apenas completar as interações necessárias ao ciclo e reservar o cenário completo para a Demo. O resultado evidencia risco de excesso de detalhe e promoção implícita de conteúdo do GDD ao escopo do MVP.

### Mudança mínima prevista

A skill agrupa regras sob resultados macro, trata mecânicas como apoio salvo resultado independente, e exige escopo explícito do MVP; conteúdo descrito no GDD sem escopo definido permanece não alocado.

### Pós-skill

O mesmo pedido foi executado por outro agente em contexto novo com as instruções da skill. A saída agrupou o ciclo em seis resultados macro: preparação/objetivo, invasão/relógios, investigação/decisões, respostas de cenário/itens/captura, power-ups e fuga/resolução. Separou comportamento do GDD de alocação do MVP; manteve objetos sem alocação como conteúdo não confirmado, tratou power-ups como fora do primeiro MVP e não transformou ações do jogador em resultados do mundo. Não prescreveu implementação. **Resultado: passou** no cenário; a análise permanece detalhada, mas os detalhes apoiam regras dentro de seis resultados macro, sem criar uma atividade por mecânica.

### Deployment

Commit local `332d7b2` contém somente `.agents/skills/pega-gameplay-cycle-analysis/SKILL.md`; sem push.

## Skill: `pega-gdd-activity-planning`

### Cenário baseline sem skill

Pedido ao agente: sintetizar duas listas compactas de ciclo do jogador e gameplay em atividades macro independentes, preservar dependências, cobrir o MVP, e classificar lacunas como bloqueio de objetivo, conflito de fonte/escopo ou detalhe que pode esperar.

### Resultado observado

O agente separou alguns resultados, mas omitiu a atividade de selecionar/iniciar a missão ao assumir que um único cenário elimina a escolha como objetivo. Não forneceu um mapa de cobertura que mostrasse onde cada resultado das duas perspectivas foi absorvido. Também classificou como bloqueio atual o conteúdo exato da tela de resultados, embora isso não impeça definir a atividade macro de revisar o resultado; esse conteúdo pode ser refinado depois. O restante manteve desconhecidos como perguntas e agrupou o ciclo dos NPCs sem detalhar cada mecânica.

### Mudança mínima prevista

A skill exige manter duas trilhas, mapear todo resultado para atividade/dependência/escopo fora ou sem alocação, e avaliar lacunas na mesma granularidade do objetivo: detalhe posterior não bloqueia uma atividade macro já identificável.

### Pós-skill

O mesmo cenário foi repetido em contexto novo com as instruções da skill. A resposta recuperou a seleção/inicialização da missão, preservou as duas trilhas e as relações, e corretamente não tratou diferença de escopo entre slice e Demo como contradição. Também classificou o conteúdo detalhado da tela de resultados e os power-ups futuros como itens que podem esperar. **A validação ainda não passou:** algumas atividades agrupam comportamentos de fases distintas (como timer de preparação e escolha do cofre, ou chegada/investigação/roubo/fuga) sem declarar um único resultado observável que justifique a fronteira. A saída também não permite verificar a aceitação independente de cada linha.

### Correção e nova validação

O contrato agora exige um resultado observável de conclusão para cada atividade e pede que agrupamentos compartilhem esse resultado; quando a fonte não sustenta a fronteira, ela deve aparecer como incerta. A repetição seguinte corrigiu a cobertura, mas fragmentou cada regra do cenário em atividade própria (incluindo timer, cofre, chegada, investigação, roubo e fuga), confundindo condição observável com objetivo entregável.

### Segunda correção e nova validação

A skill agora instrui a agrupar os resultados-fontes em objetivos/capacidades macro antes de propor atividades. Regras observáveis viram checks quando apoiam um objetivo maior; só resultados independentes e úteis como entregáveis permanecem separados. A terceira repetição evitou a fragmentação por regra, mas agrupou todas as quatro famílias de gameplay (preparação, atividade autônoma dos ladrões, estados de itens/captura e resolução) em uma atividade genérica de ciclo de heist. Isso ainda torna a decomposição pouco útil.

### Terceira correção e nova validação

A skill agora preserva famílias de resultado de alto nível que o GDD define separadamente, sem transformar cada mecânica dentro delas em atividade. A quarta repetição separou as famílias, mas elevou timers, alterações de estado e ações a atividades individuais. Isso ainda confunde critérios/checks com objetivos macro.

### Quarta correção e nova validação

A skill agora pede uma hierarquia de objetivos derivada das metas do GDD. Cada linha deve ser um objetivo macro; timers, regras de NPCs, mudanças de estado e ações são checks agrupados. A seleção de personagem mantém-se separada da seleção da missão, enquanto a materialização do personagem selecionado é critério do objetivo de seleção.

### Pós-skill aprovado

A repetição final produziu seis atividades candidatas macro: escolha de personagem, escolha de missão, progressão temporizada, comportamento autônomo dos ladrões, resposta do jogador com resultados de itens/captura, e resolução/revisão. As condições detalhadas ficaram agrupadas como verificações em vez de cartões independentes. As trilhas P/G foram mantidas, todos os resultados do cenário foram mapeados, e não houve lacunas classificadas como bloqueio ou conflito sem evidência. **Resultado: passou.**

### Deployment

Commit local `cf2d893` contém somente `.agents/skills/pega-gdd-activity-planning/SKILL.md`; sem push.

## Roteamento em `pega-production-governance`

### Cenário baseline sem as skills especializadas

Pedido ao agente: analisar GDD/MVP/ADR e produzir objetivos macro integrados com dependências, escopo e lacunas.

### Resultado observado

A skill atual já orientava contra fragmentação e prescrição técnica, mas exigia uma tabela única de atividades com Players/atores. A saída identificou atividades e inventários transversais, porém misturou objetivos do jogador com transições/estados do mundo. A autochecagem do agente concluiu que a cobertura de timers, mudanças de estado de NPCs/itens e resolução terminal não era exigida de modo explícito.

### Mudança mínima prevista

O fluxo de governança passa a rotear pela análise do jogador, pela análise de gameplay e pela síntese de atividades. Requer lanes separadas e cobertura explícita; preserva autoridade/read-only, inventários transversais úteis e os outros quatro fluxos.

### Pós-skill

No contexto novo, o agente leu as três skills e os documentos normativos. O resultado preservou lanes independentes, relacionou as atividades macro sem granularidade por mecânica, cobriu temporizadores, receitas/NPCs, objetos e itens, captura, resolução e resultados, e classificou lacunas sem inventar conflito. Os inventários transversais de personagens, apresentação e controles permaneceram. Não houve escrita em fontes, Trello ou implementação. **Resultado: passou.**

### Deployment

Commit local `cf499e1` contém somente `.agents/skills/pega-production-governance/SKILL.md`; sem push.

## Análise integrada do GDD

### Revisão do documento

`docs/pega-gdd-activity-analysis.md` foi reconstruído usando as três skills com GDD, MVP e ADR como fontes. A revisão manteve seis resultados na trilha do jogador e seis resultados na trilha de gameplay, sintetizados em dez atividades macro candidatas. A progressão temporal ficou separada da escolha do cofre; atividades de seleção de personagem e missão, relógios/fases, autonomia dos larápios, objetos, itens, captura, resolução e carreira têm resultados próprios e relações explícitas.

### Resultado da triagem de lacunas

- Nenhuma lacuna impede reconhecer ou nomear as atividades macro candidatas.
- Nenhum conflito GDD/MVP/ADR foi identificado; a entrada direta do slice e o fluxo mais amplo da Demo são recortes diferentes já declarados.
- Permanecem para refinamento posterior: exclusividade/identidade padrão de personagem, ponto de entrada do Player, valores de relógios e receitas, inventário de objetos por entrega, detalhes de classificação/carreira e bindings ainda sem mapeamento.
- Power-ups permanecem fora do primeiro MVP, conforme as fontes.

**Resultado:** análise em nível de atividade macro concluída. A lista resultante pode orientar uma comparação futura com TASKS/Trello; ela não cria nem altera cartões. GDD, MVP, ADR, TASKS e Trello permaneceram sem alterações; nenhuma implementação de jogo foi feita.

### Deployment e escopo de validação

As quatro skills foram implantadas por commits locais isolados: `f207b12`, `332d7b2`, `cf2d893` e `cf499e1`. O usuário autorizou a edição das skills e excluiu implementação de gameplay. Não houve push. Revisão foi estática e documental; não foram executados build/import/testes Unity, Play Mode, smoke ou batchmode.
