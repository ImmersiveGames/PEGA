# PEGA — Skills para análise de objetivos do GDD

**Status:** skills autorizadas e implementadas localmente; análise macro atualizada
**Data:** 2026-10-15
**Escopo:** análise de atividades derivadas do GDD; sem alteração de gameplay, fontes normativas ou Trello.

## 1. Problema

A análise atual do GDD mistura atividades percebidas pelo jogador com comportamentos produzidos pelo gameplay. Isso dá cobertura razoável à jornada do jogador, mas deixa resultados do mundo — relógios, entrada e decisões de NPCs, comportamento de objetos interativos, power-ups e resolução — pouco visíveis. Uma skill única não separa essas leituras e ainda precisa transformar ambas em atividades, rastreando lacunas e dependências.

## 2. Resultado desejado

O ecossistema deve analisar o GDD em três responsabilidades distintas:

1. **Ciclo do jogador:** identificar objetivos, escolhas, ações, informação e feedback vistos ou realizados pelo jogador.
2. **Ciclo de gameplay:** identificar os resultados de simulação do jogo — tempo e transições, entrada, conhecimento, decisões e ações de NPCs, respostas dos objetos, estados de itens, power-ups e encerramento.
3. **Síntese e planejamento de atividades:** cruzar as duas análises, verificar cobertura do GDD e recorte do MVP, identificar relações causais/dependências/lacunas e propor as atividades macro entregáveis corretas.

O resultado é uma decomposição de **o que o jogo precisa permitir ou produzir**, não um plano de componentes, scripts, cenas, assets, APIs ou ordem técnica de implementação. Nesta primeira análise, priorizar a seleção correta das atividades macro. Detalhes e checks entram somente quando necessários para diferenciar resultados, demonstrar dependência ou revelar uma lacuna; não exigir especificação exaustiva antes de organizar as atividades.

## 3. Opções de arquitetura

1. **Manter uma skill única e ampliá-la:** reduz roteamento, mas conserva a mistura entre perspectiva do jogador, simulação e síntese que causou a lacuna atual.
2. **Criar duas skills de análise e deixar a síntese na governança:** separa as perspectivas, mas mantém descoberta de objetivos, dependências e fronteiras de atividade dentro de uma skill que já atende outros fluxos de produção.
3. **Criar três skills especializadas e manter a governança como roteador:** explicita cada contrato, com custo de coordenação entre análises e síntese. Esta opção corresponde à divisão solicitada e é a recomendação desta proposta.

Os nomes locais propostos são `pega-player-cycle-analysis`, `pega-gameplay-cycle-analysis` e `pega-gdd-activity-planning`.

## 4. Contratos das skills

### 4.1 Análise do ciclo do jogador

- Lê o GDD para encontrar objetivos e ações do jogador, escolhas, momentos de apresentação, informações e feedback necessários.
- Usa MVP para marcar o recorte de cada resultado e registra atores, fonte, dependências e decisões em aberto.
- Produz uma lista de resultados da perspectiva do jogador com IDs locais e rastreáveis.
- Não deduz comportamento de NPC, estado simulado ou implementação técnica; sinaliza quando a experiência depende de regra do mundo ainda não analisada.

### 4.2 Análise do ciclo de gameplay

- Lê o GDD para encontrar resultados que o jogo/mundo deve produzir: relógios e transições, entradas e saídas, conhecimento e decisões de NPCs, ações de gameplay, estados e respostas de objetos/itens, modificadores e resolução.
- Usa MVP para classificar cada resultado como slice, Demo, posterior, opcional ou pendente.
- Produz uma lista própria, com IDs locais, comportamento observável, atores/estados, fonte, dependências e lacunas.
- Não transforma conceitos de gameplay em componentes, sistemas técnicos ou cartões automaticamente; não preenche comportamento que as fontes não definem.

### 4.3 Síntese e planejamento de atividades

- Recebe as duas listas (ou executa/solicita ambas) e verifica cada passo/objetivo do GDD contra as duas perspectivas e o recorte do MVP.
- Cria ligações causais explícitas entre resultados do jogador e do gameplay, sem duplicar nem fundir resultados independentes por conveniência.
- Identifica objetivos sem cobertura, resultados do mundo sem observação/uso pelo jogador, ações sem suporte de gameplay, dependências ausentes, sobreposições e lacunas/conflitos entre GDD, MVP e ADR.
- Propõe atividades macro entregáveis por objetivo/resultados que possam ser aceitos independentemente. Registra checks/ações menores só quando necessários para explicar a fronteira, dependência ou lacuna; não transforma cada passo ou linha de análise em cartão separado.
- Separa lacunas que bloqueiam definição de uma atividade macro das que são detalhes de conteúdo/configuração a decidir numa etapa posterior. Não inventa preenchimentos nem altera GDD, MVP, TASKS, ADR ou Trello.
- Entrega uma análise integrada que preserva as duas trilhas, links/fontes e pendências.

## 5. Integração com governança

`pega-production-governance` continua responsável pelos fluxos de Audit, Prepare Task, Reconcile e Review Delivery, autoridade das fontes e limites de escrita. No fluxo de análise de atividades do GDD, atua como roteador para as três especialidades e aplica as regras compartilhadas de autoridade/rastreabilidade. A síntese de atividades não substitui os fluxos de governança nem toma decisões normativas de produto.

As três skills são descobertas como skills locais do projeto PEGA em `.agents/skills/`. Cada uma deve ter gatilhos claros e um contrato de entrada/saída conciso. A implementação não deve copiar skills globais nem alterar configuração global do Codex.

## 6. Forma do resultado integrado

O documento resultante contém:

1. fontes, autoridade, objetivo e recorte do MVP;
2. resumo do jogo e inventários transversais aplicáveis;
3. trilha do ciclo do jogador;
4. trilha do ciclo de gameplay;
5. mapa de relações/dependências entre `ACT` e `GP`;
6. atividades macro candidatas com objetivo/resultado observável, atores, fase do MVP, fontes e dependências;
7. cobertura do GDD/MVP e lacunas agrupadas entre decisões necessárias agora e detalhes que podem esperar;
8. fronteiras propostas entre atividades independentes; checks menores apenas quando esclarecem a decomposição.

IDs de análise são locais ao documento e nunca são apresentados como IDs do Trello. Uma linha de análise não determina por si só um cartão separado.

## 7. Limites e critérios de sucesso

- Regras do GDD continuam normativas; o MVP define a entrega; o ADR registra decisões de produção; TASKS e Trello não inventam objetivos.
- Toda conclusão mantém referência local à fonte. Ausência de definição vira lacuna, não inferência.
- A análise cobre ações do jogador **e** resultados de gameplay sem reduzir um ao outro e encontra as atividades macro antes de detalhar checks.
- Eventos e fases como Preparação, Assalto Ativo e Fuga Final são contexto/regras temporais; só viram atividade se houver um resultado independentemente aceito.
- Menus/telas são momentos de apresentação, não Unity Scenes. Os resultados não prescrevem Framework Route/Activity, Session, Actor, prefab, API ou script.
- Conteúdo fora do escopo do MVP, opcional ou condicionado a playtest permanece marcado como tal.
- A skill não altera fontes normativas nem grava no Trello durante análise/preparação/reconciliação/revisão.

## 8. Verificação da arquitetura antes de disponibilizar

Testar cada skill individualmente com cenários de entrada sem sua orientação e, após escrevê-la, repetir os mesmos cenários com a skill. Cobrir ao menos: (a) um GDD rico em ações do jogador mas com gameplay subdescrito; (b) um GDD rico em timers/NPCs/objetos mas sem jornada clara; (c) ações e resultados dependentes com risco de virar um cartão amplo; (d) requisito fora do MVP ou ainda sem decisão; e (e) conteúdo que tente induzir escolha de API/componente durante análise de produto. Registrar os casos em que a saída ignora uma perspectiva, inventa regra ou mistura resultados independentes e ajustar apenas o contrato responsável.

O usuário autorizou a edição das skills e esclareceu que a autorização não inclui implementar gameplay. Cada skill foi validada em contexto novo antes da seguinte e recebeu commit local isolado no checkout PEGA visível. A governança foi validada após integrar as três especialidades. Não houve push nem alteração de fontes normativas.

## 9. Direção confirmada

O usuário confirmou que a separação em três responsabilidades é uma boa base, que o nível inicial deve priorizar as atividades macro corretas e que lacunas identificadas devem ser tratadas sem exigir detalhamento exaustivo. A análise integrada continua no documento `docs/pega-gdd-activity-analysis.md`, com as duas perspectivas em seções próprias.
