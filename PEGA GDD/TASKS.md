# PEGA - Lista de Tarefas (Demo)

**Projeto:** `PEGA`
**Tipo de documento:** Backlog de tarefas
**Status:** Rascunho ativo
**Versão:** 0.2.0
**Documento base:** [MVP](MVP.md)
**Última atualização:** 2026-09-24

---

> [!NOTE] Objetivo
>
> Lista de tarefas organizada por área, servindo de base para popular o quadro Trello ("PEGA Larapio") quando o trabalho começar. Ainda não inclui tarefas relacionadas ao framework de jogo próprio da equipe (a base sobre a qual o PEGA vai rodar — controle de fluxo de jogo, etc.) — o Renato está instalando ele no projeto, e essa seção será adicionada, e as tarefas de Programação abaixo possivelmente revisadas, depois que o framework for conhecido.

## 🟡 Design/Decisão

Perguntas que precisam ser fechadas antes de virarem tarefa de desenvolvimento (ver `MVP.md` para o contexto completo):

- Definir valor final do custo operacional por turno e limite de bancarrota (validar a recomendação inicial de 150/-1000 créditos)
- Definir formato de persistência do save da carreira
- Decidir se itens de desejo podem ser destruídos ou só roubados/recuperados

## 🔵 Programação (Renato)

> [!CAUTION] Pergunta em aberto
>
> **Pergunta:** as tarefas abaixo foram pensadas de forma independente de qualquer arquitetura de base. Quanto delas (ex: sistema de interação, máquina de estados de IA, sistema de save) já é resolvido pelo framework de jogo da equipe, em instalação pelo Renato?
>
> **Recomendação:** revisitar esta lista assim que o framework estiver instalado, para não duplicar trabalho que ele já resolve.

### Sistemas core
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

### Sistema de Carreira
- Cálculo de classificação (F a S+) e crédito ao fim do turno
- Cálculo de saldo líquido (crédito − custo operacional) e acumulado
- Condição de bancarrota + Game Over
- Save/load local do saldo de carreira

### Telas (fluxo completo)
- Splash inicial, Menu principal, Opções
- HUB de Carreira (hotspots: quadro de missão, PC de saúde da empresa)
- Entrada de jogadores + seleção de personagem
- Briefing do cenário, Splash de entrada da gangue
- HUD in-game, minimapa, câmera top-down fixa, Menu de pausa
- Fim de missão/Classificação, Resumo do turno, Game Over (bancarrota)

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

## 🟣 Framework de jogo (pendente)

Framework próprio da equipe (não é o de documentação), usado como base para controle de fluxo de jogo e outros sistemas centrais sobre os quais o PEGA vai rodar. Em instalação pelo Renato no repositório.

> [!CAUTION] Pergunta em aberto
>
> **Pergunta:** o que exatamente o framework já resolve (gerenciamento de estado, cenas, input, save, etc.) e o que ainda precisa ser construído por cima dele especificamente para o PEGA?
>
> **Recomendação:** revisitar esta seção — e possivelmente reorganizar as tarefas de Programação acima — assim que o framework estiver instalado e conhecido.

## Documentos relacionados

| Documento | Uso |
|---|---|
| [MVP](MVP.md) | Escopo, decisões e sistemas que originaram estas tarefas. |
| [GDD](GDD.md) | Documento de design completo. |

## Histórico de revisão

| Versão | Data | Alteração |
|---|---|---|
| 0.2.0 | 2026-09-24 | Corrigido: a seção pendente é sobre o framework de jogo próprio da equipe (base de execução do PEGA), não um framework de documentação. Adicionada pergunta sobre sobreposição entre as tarefas de Programação listadas e o que o framework já resolve. |
| 0.1.0 | 2026-09-24 | Criação do documento: tarefas de Programação, Arte/Visual e Design/Decisão a partir do MVP.md. |
