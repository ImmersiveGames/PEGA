# PEGA — Backlog da Demo

**Status:** backlog derivado do escopo aprovado
**Versão:** 0.4.1
**Atualização:** 2026-10-10
**Escopo:** primeiro vertical slice técnico e Demo completa

Este backlog transforma o recorte de [MVP.md](MVP.md) em entregas verificáveis. Regras e comportamento pertencem ao [GDD.md](GDD.md); tarefas não criam contratos adicionais. “Framework” abaixo significa configurar e integrar APIs disponíveis na versão instalada, sem reimplementar o framework nem presumir que documentação equivale a validação em Unity.

## Dependências técnicas e autoria no Immersive Framework

- [ ] **VS** Auditar as superfícies públicas do `com.immersive.framework` já instalado (`1.1.0-preview.6`) que o slice realmente usará e registrar contratos, maturidade e limites aplicáveis antes da integração. **Aceite:** nenhuma capacidade já fornecida pelo pacote é reimplementada pelo PEGA e nenhuma superfície experimental é tratada como validada sem confirmação no Editor.
- [ ] **FOUND-01 · VS · pré-requisito do cartão [#75](https://trello.com/c/EfPEV4Ip/75-mc-01-integrar-player-actor-do-framework-na-miss%C3%A3o)** Compor a fundação da aplicação PEGA e validar a navegação até Result sem Player.
  - **Aceite:** GameApplication próprio do PEGA referencia a Mission Route como Startup Route; Player Session e Progression Save permanecem desabilitados.
  - **Aceite:** incluir somente o Camera Output técnico necessário ao bootstrap da versão resolvida do Framework. Não criar Player Session, assignment de câmera para Player, câmera de gameplay ou movimento; a câmera top-down definitiva permanece no cartão #29.
  - **Aceite:** Mission Activity permite zero Players; Result é uma Activity distinta.
  - **Aceite:** Persistent Content e perfis de conteúdo são próprios do PEGA; cenas de produção estão declaradas no Build Profile ativo.
  - **Aceite:** inicialização e navegação funcionam sem depender de cenas, assets ou protótipos de FrameworkValidation.
  - **Aceite:** diagnóstico temporário registra alterações de fase Preparação → Assalto → Fuga e solicita Result. O diagnóstico demonstra somente composição e navegação, não gameplay funcional nem regras definitivas.
  - **Aceite:** evidências no Unity Editor registram importação do pacote resolvido, validação da configuração/autoria e execução do fluxo Mission → Result.
  - **Evidência desta execução:** [relatório de validação Unity FOUND-01](../docs/superpowers/reports/2026-10-10-found-01-unity-validation.md); pendências de aceite permanecem explícitas no relatório.
  - **Fora de escopo:** menu, HUB, carreira, retry/reset definitivo, gameplay funcional e integração Player/Actor.
- [ ] **VS** Inspecionar maturidade e contratos das APIs públicas de Game Flow, Actor/Player, Pause, Camera e Progression Save antes de integrar cada superfície. **Aceite:** decisões de integração registram a API pública concreta, limites documentados e qualquer bloqueio experimental; nenhum recurso é marcado como validado sem confirmação de importação/execução no Editor.
- [ ] **Demo** Configurar participação de dois Players locais e câmera/apresentação compatível com tela dividida usando a superfície pública disponível; desenhar solução explícita para limitações atuais de Pause/API single-player. **Aceite:** ambos entram, controlam Rick ou Petra, mantêm câmera/UX legíveis e pausa não produz estado inconsistente.
- [ ] **Demo** Integrar Save de Progression para persistência entre assaltos após verificar o perfil exigido e o contrato do backend. **Aceite:** saldo e progressão sobrevivem a reinício do jogo; nenhum assalto em andamento é salvo ou retomado.
- [ ] **Demo** Configurar reset/repetição de missão somente conforme o ciclo documentado e compatível com a carreira. **Aceite:** reinício não duplica recompensas nem perde indevidamente o resultado já confirmado.

## Mecânicas específicas de PEGA

- [ ] **VS** Integrar movimento do Actor e configurar especializações de velocidade por arquétipo/modificador, sem atributo universal de Agilidade. **Aceite:** Rick/Petra e larápios usam suas configurações; transporte de larápio mantém velocidade normal do policial.
- [ ] **VS** Implementar interação contextual e comandos distintos para interagir/atacar e soltar itens. **Aceite:** soltar descarta todos os itens; depósito transfere itens elegíveis; contexto não mistura ataque e descarte.
- [ ] **VS** Implementar inventário de capacidade configurável, itens de unidade inteira e armazenamento em cofre. **Aceite:** capacidade pode variar por tipo de Actor; depósito parcial segue ordem estável atual; excedentes permanecem no inventário; itens não são destruídos.
- [ ] **VS** Implementar transporte de larápio incapacitado e entrega na prisão como condição de captura efetiva. **Aceite:** transporte bloqueia ataque, salto, esquiva e interação comum, mas preserva itens do policial e sua capacidade; incapacitação/transporte não contam como captura.
- [ ] **VS** Implementar confronto por Capacidade de ataque versus Resistência, ataques válidos derrubando os itens do alvo e recuperação temporizada de incapacitação. **Aceite:** sem HP/dano acumulativo; resultados e feedback distinguem ataque insuficiente, queda e captura posterior.
- [ ] **VS** Implementar percepção por área radial e cone frontal, parâmetros configuráveis e bloqueio por obstáculos; propagar fatos apenas conforme escopo/política de Knowledge Data. **Aceite:** Individual, Gangue e Global permanecem distintos e percepção individual não é compartilhada automaticamente.
- [ ] **VS** Implementar coleta, carregamento, roubo, recuperação, fuga e estados finais dos itens conforme o GDD. **Aceite:** item no cenário ao fim da Fuga é recuperado; item levado por larápio que escapou é roubado; nenhum item desaparece.
- [ ] **Demo** Implementar cofres adicionais e armários quando incluídos no conteúdo aprovado. **Aceite:** armazenamento compartilha regras básicas, funções são distintas e armário não concede bônus de proteção por padrão.
- [ ] **Demo** Implementar pausa e saída no fluxo de UI da missão segundo os limites confirmados da API. **Aceite:** pause e retomada não deixam atores/timers em estados divergentes; sair encaminha ao fluxo definido.

## IA GOAP dos larápios

- [ ] **VS** Implementar arquitetura de planejamento GOAP separada da execução de ações; somente larápios usam GOAP. **Aceite:** Rick e Petra são controlados pelos Players; não existe FSM substituta conduzindo larápios.
- [ ] **VS** Definir estados do mundo, desejos/contextos globais e políticas de conhecimento usadas pelo planejamento. **Aceite:** roubo, fuga, recuperação, perseguição e investigação usam fatos com escopo correto.
- [ ] **VS** Implementar contrato comum de GOAP Action com ciclo de vida, resultados explícitos e término por sucesso, invalidação, interrupção ou cancelamento. **Aceite:** reservas são liberadas em todo término; progresso parcial cancelado é descartado conforme GDD.
- [ ] **VS** Implementar reservas apenas para operações incompatíveis com owner e liberação explícitos. **Aceite:** não há bloqueio por reservas desnecessárias; término, cancelamento, invalidação e interrupção liberam propriedade.
- [ ] **VS** Implementar seleção/execução de objetivos para proteger, roubar, recuperar item, perseguir portador, confrontar e escapar, incluindo Fuga Final com GOAP ativo. **Aceite:** decisões respondem a desejos, fatos e oportunidades sem prioridade fixa contraditória.
- [ ] **Demo** Configurar perfis e receitas temporais independentes de Spawn Points. **Aceite:** cada receita conta a partir do início do Assalto Ativo; Preparação não altera seus relógios.

## Autoria, conteúdo de cena e level design

- [ ] **VS** Configurar missão inicial com Preparação de duração fixa, cofre inicial padrão e pelo menos dois cofres válidos; escolha gratuita e sem confirmação de pronto no fluxo normal. **Aceite:** cofre selecionado comunica-se aos Players e não revela automaticamente a localização aos larápios; encerramento antecipado só existe como ferramenta de debug.
- [ ] **VS** Construir cenário compacto com rotas de perseguição, cofre, prisão, entradas e saídas necessárias ao ciclo. **Aceite:** fluxo completo é possível e os objetos usados têm referências/configuração válidas.
- [ ] **VS** Configurar Spawn Seguro para Player e pontos/receitas de invasão dos larápios. **Aceite:** nenhum spawn ocorre em posição inválida ou bloqueada nas condições cobertas pelo teste manual.
- [ ] **Demo** Completar o Armazém e suas rotas, obstáculos e pontos de interesse do GDD. **Aceite:** fluxo comporta dois Players, quatro arquétipos dos Trapalhões do Crime, fuga e legibilidade top-down.
- [ ] **Demo** Autorar rotas de saída e conteúdo de transição narrativa da P.G.A. Larópolis. **Aceite:** polícia oficial/PGA não atua como NPC policial durante assaltos.

## Multiplayer local

- [ ] **Demo** Integrar entrada, seleção/atribuição de Rick e Petra e controle simultâneo de dois Players. **Aceite:** personagens têm equivalência mecânica e os dois podem alterar a escolha do cofre; última escolha válida prevalece e atualiza a ambos.
- [ ] **Demo** Adaptar HUD, câmera, pausa, feedback de interação e legibilidade do cenário à tela dividida. **Aceite:** informação crítica e comandos contextuais são legíveis para cada Player durante a missão.
- [ ] **Demo** Exercitar conflitos de interação e estados cooperativos (carregar, atacar, transportar, depositar, pausar). **Aceite:** ownership e resultados permanecem consistentes quando ambos agem simultaneamente.

## UI/UX

- [ ] **VS** Exibir timer da Preparação, cofre escolhido, estado do objetivo, timer de Assalto/Fuga e feedback de captura/itens. **Aceite:** Preparação fixa e relógios de Spawn não são apresentados como o mesmo timer.
- [ ] **VS** Implementar feedback de percepção/ameaça, Resistência e incapacitação com sinais visuais e sonoros mínimos. **Aceite:** jogador distingue alvo apenas percebido, incapacitado, transportado e capturado.
- [ ] **Demo** Implementar fluxo de carreira entre HUB, seleção/briefing, assalto, resultado e resumo. **Aceite:** telas refletem a hierarquia de Game Flow e não duplicam regras do GDD.
- [ ] **Demo** Comunicar critérios relevantes da classificação F–S+ e a resolução dos itens. **Aceite:** critérios/pesos são configuráveis por missão e nenhum critério universal domina por definição.
- [ ] **Demo** Implementar pausa, opções e saída conforme o fluxo definido; omitir salvar/carregar durante assalto. **Aceite:** interface não sugere retomada de assalto persistido.

## Carreira e persistência

- [ ] **Demo** Calcular classificação e métricas temporais por eventos/intervalos configuráveis por missão. **Aceite:** relatório explica critérios relevantes e diferencia larápios capturados, escapados e não escapados.
- [ ] **Demo** Resolver itens ao fim do assalto e calcular bônus de proteção uma única vez sobre o estado final. **Aceite:** itens guardados podem receber bônus configurado; retirar/roubar remove elegibilidade; entregas repetidas não acumulam bônus.
- [ ] **Demo** Aplicar custo operacional provisório de 150 créditos e bancarrota somente abaixo de -1000, com valores configuráveis. **Aceite:** exatamente -1000 não dispara bancarrota; Game Over de carreira não ocorre por um único assalto ruim.
- [ ] **Demo** Persistir carreira apenas entre assaltos e oferecer reinício explícito após bancarrota. **Aceite:** saldo/progresso são restaurados, não existe save de partida em andamento, e novo ciclo não contamina a carreira anterior.

## Arte e animação

- [ ] **VS** Produzir modelos/animações mínimas para Player, larápio, item, cofre e prisão. **Aceite:** silhueta/estado de movimento, carregamento, queda e transporte são distinguíveis no cenário compacto.
- [ ] **Demo** Produzir Rick e Petra e os quatro arquétipos dos Trapalhões do Crime com leitura top-down. **Aceite:** Rick/Petra permanecem mecanicamente equivalentes; arte comunica grupos, ameaça e estados.
- [ ] **Demo** Produzir cenários, UI e animações restantes necessários ao fluxo demonstrável, mantendo referências e autoria identificadas no [ArtBook.md](ArtBook.md). **Aceite:** imagens e assets usados têm proveniência/estado de produção registrados.

## Testes e critérios de aceitação

- [ ] **VS** Validar manualmente no Unity Editor fluxo de cena, preparação, GOAP, percepção, reservas, confronto, inventário, cofre, captura, fuga e resultados. **Aceite:** checklist do GDD/MVP passa em uma sessão solo; registrar versão Unity/framework e limitações observadas.
- [ ] **VS** Verificar casos de interrupção/cancelamento de ações, capacidade insuficiente, múltiplas reservas e item deixado ao fim da fuga. **Aceite:** contratos de término/ownership e resolução final de itens são reproduzíveis.
- [ ] **Demo** Validar manualmente dois Players locais, split screen, escolha concorrente de cofre, interações simultâneas, pause/retomada, carreira e persistência entre reinicializações. **Aceite:** não há perda/duplicação de estado nem avanço indevido dos timers.
- [ ] **Demo** Executar cenários de aceitação de sucesso, captura, fuga com itens, falha de proteção e bancarrota limítrofe. **Aceite:** resultados, classificação e saldo seguem GDD e MVP.
- [ ] **Demo** Revisar cada item marcado como implementado com evidência de importação/execução no Editor. **Aceite:** documentação não é usada como prova de implementação.

## Pós-Demo, opcional e sujeito a playtest

- [ ] **Pós-Demo** Orçamento de Preparação, investimentos de segurança, novos cenários/gangues e conteúdo narrativo adicional.
- [ ] **Pós-Demo** Decidir inclusão e efeitos de power-ups a partir de playtests; não são requisito do primeiro vertical slice.
- [ ] **Playtest** Revisar ordem estável de depósito parcial, valores econômicos, duração dos timers, pesos da classificação e balanceamento de arquétipos.
- [ ] **Playtest** Avaliar proteção de armários, habilidades específicas por arquétipo e alternativas de cenário sem alterar as regras base sem decisão registrada no GDD.

## Referências

- [GDD — contratos completos](GDD.md)
- [MVP — escopo por entrega](MVP.md)
- [Art Book — direção visual](ArtBook.md)

## Histórico de revisão

| Versão | Data | Alteração |
|---|---|---|
| 0.4.1 | 2026-10-10 | Corrigido o critério FOUND-01 para explicitar o Camera Output técnico exigido pelo bootstrap, sem Player Session, assignment de Player ou câmera de gameplay; registrada a evidência Unity disponível. |
