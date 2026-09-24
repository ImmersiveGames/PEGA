# PEGA - Lista de Tarefas (Demo)

**Projeto:** `PEGA`
**Tipo de documento:** Backlog de tarefas
**Status:** Rascunho ativo
**Versão:** 0.3.0
**Documento base:** [MVP](MVP.md)
**Última atualização:** 2026-09-24

---

> [!NOTE] Objetivo
>
> Lista de tarefas organizada por área, servindo de base para popular o quadro Trello ("PEGA Larapio") quando o trabalho começar.

## 🟡 Design/Decisão

Perguntas que precisam ser fechadas antes de virarem tarefa de desenvolvimento (ver `MVP.md` para o contexto completo):

- Definir valor final do custo operacional por turno e limite de bancarrota (validar a recomendação inicial de 150/-1000 créditos)
- Definir formato de persistência do save da carreira
- Decidir se itens de desejo podem ser destruídos ou só roubados/recuperados

## 🔵 Programação (Renato)

> [!NOTE] Framework de jogo próprio da equipe
>
> O PEGA roda sobre o `com.immersive.framework` (Immersive Games, v1.0.2), instalado como pacote Unity via OpenUPM. É uma arquitetura em camadas (`GameApplicationAsset → Session → Route → Activity`), com módulos prontos e testados para fluxo de telas, participação de jogadores (incluindo split-screen local), câmera, save de progressão, pause, reset e transições. Renato é o autor do framework, então a autoria dessas partes não depende de curva de aprendizado — as tarefas abaixo já refletem essa divisão.

### Autoria via framework (não é construído do zero)
- Estrutura de Route/Activity para as 12 telas do fluxo da Demo (splash → menu → HUB → seleção → briefing → gameplay → resultado → resumo do turno)
- Player Participation: entrada de jogadores + seleção de personagem (Rick/Petra)
- Camera Presentation para a câmera top-down fixa
- Progression Save (backend JSON nativo) para o saldo acumulado da carreira
- Pause via sistema nativo do framework
- Reset/Cycle Reset para repetir o turno (rejogar a mesma missão)
- Transições entre telas

### Regras de jogo específicas do PEGA (lógica nova)
- Movimento base (direções, velocidade por Agilidade)
- Impulso/dash (fórmula `2 - Vigor/Agilidade`, cooldown)
- Salto (altura por Força)
- Sistema de interação (prioridade de alvo, contexto pega/solta/ataque)
- Carregar/soltar objeto e personagem
- Depositar inimigo na prisão / item no cofre
- Sistema de dano e combate (usando a tabela corrigida dos Trapalhões)

### IA
- Máquina de estados: `Furtando` → `Fugindo`/`Agressivo` → `Caído` → `Capturado`
- Timer de recuperação de incapacitação por arquétipo
- Lógica de percepção/reação (teste de presença)

### Sistema de Carreira (regras específicas do PEGA)
- Cálculo de classificação (F a S+) e crédito ao fim do turno
- Cálculo de saldo líquido (crédito − custo operacional) e acumulado
- Condição de bancarrota + Game Over
- Persistência do saldo via Progression Save do framework (ver acima)

### Conteúdo das telas
- HUD in-game, minimapa
- Conteúdo/lógica interna de cada tela (o que aparece: quadro de missão e PC do HUB, briefing, splash de gangue, resumo do turno) — a navegação entre elas já é do framework

### Power-ups
- Lógica dos 3 efeitos (velocidade, força, recuperação de vigor)

## 🟢 Arte/Visual (Ubiratan)

### Personagens
- Concept + assets de Rick e Petra
- Concept + assets dos 4 arquétipos dos Trapalhões do Crime (Ladrão Clássico, Ladrões Molhados, Minion, Os Marombas)
- Animações básicas (andar, correr, atacar, carregar, cair/imobilizado)

### Cenário
- Armazém completo: caixas/corredores, baú de ouro, doca, prédio administrativo, cofre, prisão, sala de controle

### UI
- Arte do HUB/escritório (quadro com mapa + escrivaninha com PC)
- Ícones de HUD (tempo, inventário, capturados, power-up ativo)
- Design do minimapa
- Telas de menu (principal, opções, seleção de personagem/missão, briefing, splash de gangue, resumo do turno, game over)

## Documentos relacionados

| Documento | Uso |
|---|---|
| [MVP](MVP.md) | Escopo, decisões e sistemas que originaram estas tarefas. |
| [GDD](GDD.md) | Documento de design completo. |

## Histórico de revisão

| Versão | Data | Alteração |
|---|---|---|
| 0.3.0 | 2026-09-24 | Reorganizada a seção de Programação com base na avaliação do `com.immersive.framework` (v1.0.2): tarefas divididas entre "autoria via framework" (fluxo de telas, seleção de jogador, câmera, save, pause, reset) e "regras de jogo específicas do PEGA" (movimento, combate, IA, economia da carreira). Removida a seção de framework pendente. |
| 0.2.0 | 2026-09-24 | Corrigido: a seção pendente é sobre o framework de jogo próprio da equipe (base de execução do PEGA), não um framework de documentação. Adicionada pergunta sobre sobreposição entre as tarefas de Programação listadas e o que o framework já resolve. |
| 0.1.0 | 2026-09-24 | Criação do documento: tarefas de Programação, Arte/Visual e Design/Decisão a partir do MVP.md. |
