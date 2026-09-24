# PEGA - Plano de MVP (Demo)

**Projeto:** `PEGA`
**Tipo de documento:** Plano de MVP / Demo
**Status:** Rascunho ativo
**Versão:** 0.3.0
**Documento base:** [GDD](GDD.md)
**Última atualização:** 2026-09-24

---

> [!NOTE] Objetivo
>
> **Objetivo do documento:** definir o escopo mínimo jogável de PEGA — um vertical slice que prove o loop de perseguição, captura e recuperação de itens, com uma economia de carreira simples entre partidas.
>
> **Critério de sucesso:** qualquer pessoa da equipe deve conseguir olhar este documento e saber exatamente o que entra na Demo, o que fica de fora, e quais números já estão validados versus quais ainda são estimativa.
>
> **Estado:** ativo

## HUB de Carreira (escritório da PEGA)

> [!IMPORTANT] Decisão
>
> **Decisão:** o HUB é uma tela temática estática com hotspots, não um espaço navegável. Resolve, para a Demo, a pergunta aberta do GDD sobre o HUB ser "apenas uma tela de seleção de fases ou um espaço navegável".
>
> **Cena:** a sala do escritório da P.E.G.A. Larópolis, com dois pontos de interação:
>
> - **Quadro com mapa da cidade** → abre a seleção de missão (mesmo com uma única missão disponível na Demo, já estabelece o padrão visual e de interação para quando houver mais contratos).
> - **Escrivaninha com PC** → abre a "saúde da empresa": saldo acumulado da carreira (tipo extrato/dashboard) e uma inbox de emails com o resultado do último turno, elogios ou reclamações do cliente, e novos contratos desbloqueados (ainda sem função na Demo, já que só existe uma missão, mas o espaço fica reservado no fluxo).
>
> **Motivo:** entrega a mesma informação da proposta original (mapa, saúde da empresa, emails, contratos) sem o custo de produção de um espaço 3D/2D navegável com colisão e câmera própria — evita expandir escopo antes de validar o loop principal, como o próprio GDD já recomendava.
>
> **Evolução futura (fora da Demo):** transformar o escritório num espaço navegável, com o personagem andando até o quadro e a escrivaninha fisicamente. Registrado aqui para não se perder, mas fica para depois da Demo.

> [!NOTE] Fora de escopo — todo o jogo, não só a Demo
>
> Multiplayer online não está nos planos para PEGA. O HUB navegável ganharia muito mais força com online, mas essa direção fica reservada para um possível próximo jogo, caso a decisão não mude. PEGA trabalha apenas com multiplayer local (tela dividida).

## Telas da Demo

Fluxo completo de telas, incluindo as que já existiam implícitas no GDD (briefing de cenário, splash de gangue, menu de pausa) e as novas que o Sistema de Carreira exige:

1. Splash/logo inicial
2. Menu principal
3. Opções/configurações (submenu, acessível também no menu de pausa)
4. HUB de Carreira (escritório) — ponto central de retorno entre turnos
5. A partir do HUB: entrada de jogadores + seleção de personagem
6. A partir do HUB: seleção de missão (quadro/mapa da cidade)
7. Briefing/apresentação do cenário (câmera mostra entradas, saídas, itens valiosos, armadilhas)
8. Splash de entrada da gangue (apresenta grupo inimigo e dificuldade)
9. Gameplay (HUD in-game, com menu de pausa acessível a qualquer momento)
10. Fim de missão / Classificação (F a S+)
11. Resumo do turno (crédito, custo operacional, saldo líquido, saldo acumulado da carreira)
12. Retorno ao HUB de Carreira — **ou** Game Over por bancarrota, se o saldo acumulado passou do limite, com opção de reiniciar a carreira do zero

## Escopo da Demo

| Área | Escopo do MVP |
|---|---|
| Personagens | Rick e Petra jogáveis, **mecanicamente equivalentes** (ver decisão abaixo). |
| Cenário | Armazém completo (ver [GDD § Armazém](GDD.md#armazém)). |
| Inimigos | Uma gangue: **Os Trapalhões do Crime**, 4 arquétipos. |
| Sistemas core | Movimento, impulso, salto, interação, carregar/soltar, prisão, cofre. |
| IA | Máquina de estados reduzida: `Furtando`, `Fugindo`, `Agressivo`, `Caído`, `Capturado`. |
| Power-ups | 3 efeitos: velocidade, força, recuperação de vigor. |
| Interface | HUD, minimapa, tempo, resultado da missão. |
| Classificação | `F` a `S+`, com créditos por faixa (ver [GDD § Dificuldade](GDD.md#dificuldade)). |
| **Carreira** | Saldo acumulado entre partidas, com condição de bancarrota (novo — ver seção própria). |
| Multiplayer | Tela dividida local: stretch goal, não bloqueante. |

Fora de escopo por enquanto: outras gangues, habilidades especiais, HUB navegável, modo Endless, empréstimos como mecânica de continue.

## Gangue da Demo: Os Trapalhões do Crime

Números validados em `Tabela de Atributos e Habilidades.xlsx` (fórmulas corrigidas em 2026-09-24 — ver histórico de revisão).

| Arquétipo | Força | Agilidade | Presença | Defesa | CD (peso) | Dano melee ao jogador | Golpes p/ imobilizar (melee) | Recup. de incapacidade |
|---|---|---|---|---|---|---|---|---|
| Ladrão Clássico | 1 | 2 | 1 | 1 | 27 | 1 | 6 | 4,5s |
| Ladrões Molhados | 2 | 2 | 3 | 1 | 35 | 0 | 6 | 4,0s |
| Minion | 1 | 3 | 2 | 1 | 32 | 1 | 6 | 4,0s |
| Os Marombas | 3 | 2 | 2 | 2 | 39 | 2 | 3 | 4,6s |

> [!WARNING] Risco
>
> **Risco:** a planilha de origem tinha fórmulas sem piso (`<=0`) que geravam valores negativos para inimigos fracos (Ladrão Clássico, Minion, Os Cavalheiros).
>
> **Impacto:** médio — números errados de balanceamento entrariam direto no código.
>
> **Mitigação:** corrigido em 79 células (linhas de Dano base do jogador no inimigo, Quantidade de golpes melee e Quantidade de golpes range), recalculado sem erros. Arquivo corrigido salvo na mesma pasta.

## Captura vs. Derrubada (Incapacitação)

> [!IMPORTANT] Decisão
>
> **Decisão:** derrubar (incapacitar) e capturar são estados diferentes.
>
> - **Caído/Incapacitado:** o larápio apanhou o suficiente para ficar desmaiado ou tonto. Não se move, mas **não está sob custódia** do jogador. Um temporizador de recuperação começa a contar (já existe na planilha: linha "Tempo de recuperação de incapacidade em segundos", 4,0s a 4,6s para os Trapalhões).
> - **Capturado:** o jogador prende o larápio, ou o carrega em direção à prisão até deixá-lo lá retido. A partir do momento em que o jogador começa a carregar, o inimigo sai do estado `Caído` e entra em `Capturado` (ou um estado intermediário de "sendo carregado").
>
> **Consequência de gameplay:** se o jogador demorar demais pra chegar até o inimigo caído, o temporizador de recuperação zera e ele volta a `Fugindo`/`Agressivo` — essa é a fonte principal de tensão da perseguição.
>
> **Motivo:** resposta à pergunta aberta do GDD sobre a diferença mecânica entre capturar e derrubar.

## Rick e Petra

> [!IMPORTANT] Decisão
>
> **Decisão:** Rick e Petra são mecanicamente equivalentes na Demo — mesmos atributos, mesmas habilidades.
>
> **Motivo:** simplifica balanceamento e implementação inicial. Diferenças cosméticas ficam livres.
>
> **Reavaliação:** se playtests indicarem que diferenças mecânicas melhoram a cooperação em multiplayer local, isso pode ser revisto pós-Demo.

## Sistema de Carreira (novo para a Demo)

O jogo é tratado como uma empresa (P.E.G.A. Larópolis). Como a Demo só tem uma fase disponível, a progressão de "novos contratos" do GDD completo não se aplica ainda — em vez disso, a Demo simula a carreira através de repetição da mesma missão, com saldo acumulado entre tentativas.

1. O jogador escolhe jogar um "turno" (uma partida do Armazém).
2. Ao final do turno, o jogo calcula a classificação (`F` a `S+`) e o crédito correspondente (0 a 800, já definido na planilha de classificação).
3. Um custo operacional fixo do turno é descontado do crédito ganho (aluguel, salário da dupla, manutenção do equipamento).
4. O saldo líquido do turno (crédito − custo) é somado ao saldo acumulado da carreira.
5. Se o saldo acumulado cair abaixo do limite de bancarrota, o jogo entra em Game Over de carreira.
6. Em caso de Game Over, o jogador reinicia a carreira do zero (saldo acumulado volta a 0).

> [!CAUTION] Pergunta em aberto
>
> **Pergunta:** qual o valor do custo operacional por turno e qual o limite de bancarrota?
>
> **Impacto:** define o ritmo de risco/recompensa da Demo — custo alto demais frustra, custo baixo demais remove a tensão da carreira.
>
> **Recomendação inicial (a validar em playtest):** custo operacional fixo de **150 créditos por turno** (uma missão `C` já cobre o custo; `F` ou `D` geram prejuízo); limite de bancarrota em **-1000 créditos** acumulados (equivalente a ~6-7 turnos ruins seguidos antes do Game Over).

> [!CAUTION] Pergunta em aberto
>
> **Pergunta:** o saldo de carreira persiste entre sessões de jogo (save em disco) ou só dentro da mesma sessão?
>
> **Impacto:** afeta escopo técnico — save persistente exige serialização e tela de carregamento.
>
> **Recomendação inicial:** para a Demo, persistir localmente (save simples), já que a "carreira" só faz sentido se sobreviver ao fechar o jogo.

> [!NOTE] Fora de escopo, documentado para o futuro
> Empréstimos como mecânica de "continue" após bancarrota são uma ideia já levantada para o jogo completo, mas **não entram na Demo**. Ficam registrados aqui para não se perderem.

## Derrota total (fim de missão)

> [!IMPORTANT] Decisão
>
> **Decisão:** a missão sempre termina quando o tempo (turno de serviço) acaba — não há derrota "instantânea" por outros motivos (ex: todos os itens roubados). O que muda é a classificação (`F` a `S+`) resultante, e essa classificação alimenta o Sistema de Carreira acima.
>
> **Motivo:** resposta à pergunta aberta do GDD sobre derrota total. Mantém o jogo sempre jogável até o fim do turno, empurrando a "derrota real" para o nível da carreira (bancarrota), não da missão individual.

## Perguntas ainda em aberto

- Valor exato do custo operacional por turno e limite de bancarrota (ver acima).
- Persistência de save (ver acima).
- Itens de desejo podem ser destruídos, ou só roubados/recuperados? (herdada do GDD, ainda sem resposta)

## Documentos relacionados

| Documento | Uso |
|---|---|
| [GDD](GDD.md) | Documento de design completo, fonte das regras gerais. |
| [Art Book](ArtBook.md) | Referência visual de personagens, gangues e cenários. |
| `Tabela de Atributos e Habilidades.xlsx` | Números de balanceamento validados dos inimigos. |
| [Tarefas](TASKS.md) | Backlog de tarefas por área, derivado deste documento. |

## Histórico de revisão

| Versão | Data | Alteração |
|---|---|---|
| 0.3.0 | 2026-09-24 | Convertidos os blocos customizados (`:::objective` etc.) para blockquotes de alerta padrão (`> [!NOTE]` etc.), compatíveis com o visualizador do chat e com GitHub. Adicionada decisão sobre multiplayer online (fora de escopo para todo o jogo). |
| 0.2.0 | 2026-09-24 | Adicionado HUB de Carreira (escritório com hotspots), resolvendo a pergunta aberta do GDD sobre navegabilidade do HUB, e seção de fluxo completo de telas da Demo. |
| 0.1.0 | 2026-09-24 | Criação do documento: escopo da Demo, números validados dos Trapalhões do Crime, decisões sobre captura/derrubada, Rick e Petra, sistema de carreira e derrota total. |
