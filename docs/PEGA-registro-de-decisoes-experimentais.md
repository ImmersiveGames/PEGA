# PEGA — Registro de mecanismos da demonstração a reavaliar

**Natureza:** registro vivo de achados arquiteturais e decisões provisórias; não é especificação normativa nem plano de implementação.  
**Início:** 24/10/2026  
**Escopo:** distinguir soluções legítimas usadas nas demonstrações técnicas de responsabilidades adequadas ao gameplay definitivo do PEGA.

## Propósito

Durante a construção do MVP, é legítimo utilizar superfícies oficiais do Immersive Framework para comprovar fluxos específicos. O fato de uma integração funcionar, entretanto, **não demonstra que o acoplamento escolhido deva permanecer no design final**.

Este registro mantém visíveis essas distinções, sem desqualificar o Framework nem solicitar refatorações antecipadas. Cada achado deve indicar **o que foi observado, por que a solução existe hoje, qual é a dúvida arquitetural e quando vale reavaliá-la**. Não se deve substituir componentes apenas para eliminar dependências aparentes: antes de decidir, conferir o código, os contratos e os recursos existentes no workspace.

### Critérios de classificação

- **Uso confirmado:** o código de gameplay realmente consome o mecanismo na amostra atual.
- **Preparado, não acionado:** há composição válida, mas não foi identificado o produtor/disparador do comportamento.
- **Provisório:** configuração ou comportamento declaradamente orientado ao experimento, ainda não aprovado como design final.
- **Hipótese arquitetural:** possível inadequação de responsabilidades, **não** um defeito comprovado.
- **Encerrado:** artefato de demonstração removido ou decisão já consolidada; preservar somente o registro histórico necessário.

## Achados abertos

### PEGA-ARQ-001 — Percepção dos NPCs depende da observação da Player Session

**Classificação:** uso confirmado + hipótese arquitetural.  
**Estado:** manter na amostra; reavaliar antes de consolidar o modelo geral de percepção.

**Evidência observada**

- Os dois larápios possuem `PEGANpcPerceptionSource`, associado ao `PlayerSessionObserver` da Activity.
- `PEGANpcPerceptionSource` utiliza `ActorOccurrenceReferenceBinding` para resolver a ocorrência física associada ao Player Slot e `PlayerSessionObserver.TryGetObservation()` para verificar correlação do Actor com a sessão e confirmar seu `ActorProfileId` atual.
- Após essa verificação, o NPC ainda depende de alcance, cone visual e oclusão para produzir observações de Rick. **Consultar a Session não equivale a perceber Rick fisicamente.**
- O `PEGANpcMissionSpawnEntry` também recebe o Observer para vinculá-lo à percepção dos NPCs criados por ingresso; esse fluxo não possui disparador ativo identificado na composição atual.
- Sem o Observer, a configuração de percepção atual é considerada inválida; removê-lo agora afetaria o comportamento da amostra.

**Por que existe hoje**

A demonstração precisava reconhecer Rick como Actor atualmente controlado por P1, sem confundir a identidade do personagem com o mero ocupante do slot. O Observer é uma superfície legítima, oficial e somente leitura do Framework para informações da Player Session.

**Questão arquitetural**

Há possível acoplamento entre dois domínios: **a Session informa participação/controle**, enquanto **a percepção conhece Actors presentes no mundo por evidência física**. O NPC deveria conseguir perceber e identificar um Actor independentemente de ele ser controlado por P1, P2, uma IA ou não ter Player Slot. A consulta à Session para identificar Rick pode ser adequada à demonstração, mas ainda não foi justificada como dependência permanente da percepção.

**O que já existe para investigar, sem presumir uma solução**

O Framework oferece `ActorOccurrenceReferenceBinding` e resolução de ocorrências (`ActorId`, `ActorRoot`, owner, identificação de Player Slot). **Ainda não foi comprovado** se os contratos oficiais oferecem, por esse caminho, todas as evidências de perfil/identidade necessárias ao gameplay futuro sem o Observer. Comparar as autoridades e os contratos reais antes de escolher outra abordagem.

**Gatilho para reavaliação:** ao generalizar a percepção para vários personagens/atores, permitir troca de Actor, participação P2 ou NPCs sem Player associado; ou antes de transformar a lógica específica de Rick em percepção de produção.

**Decisão atual:** não retirar o `PlayerSessionObserver`; não modificar o Framework; não fixar o acoplamento atual como arquitetura definitiva.

### PEGA-ARQ-002 — Alvo Rick/P1 específico na percepção da demonstração

**Classificação:** provisório + questão de generalização.  
**Estado:** manter durante o teste de perseguição; avaliar junto de PEGA-ARQ-001.

**Evidência observada:** `PEGANpcPerceptionSource` possui referência explícita ao perfil de Rick, enquanto os bindings usados na amostra apontam para P1. O recorte atual foi desenvolvido para perseguir Rick e confirmar a identidade correta do Actor observado.

**Questão arquitetural:** uma implementação específica de Rick/P1 é apropriada para o recorte técnico, mas não define necessariamente como cada larápio distinguirá outros Actors relevantes no gameplay final. Isso deve ser decidido pelas regras de percepção, identificação e conhecimento do PEGA, sem pressupor que todo alvo percebido é um Player.

**Gatilho para reavaliação:** quando houver diferentes personagens/slots e critérios de ameaça ou identificação definidos para o NPC.

**Decisão atual:** manter como recorte intencional da amostra, sem generalizar prematuramente.

### PEGA-ARQ-003 — Desejos e destinos GOAP provisórios da perseguição

**Classificação:** provisório, com consequências de gameplay.  
**Estado:** revisar durante a definição dos comportamentos definitivos dos larápios.

**Evidência observada:** o perfil `PEGANpcDesireProfile_N91_NormalPursuit_Provisional` foi construído para perseguição de Rick, investigação da última posição conhecida e fuga para saída conhecida. O próprio nome do asset identifica seu caráter provisório. Uma saída potencial pode ser aprendida pelo uso físico de um Spawn Point e compartilhada pela gangue. O antigo marcador `NPC Escape Reference E01` não foi identificado como fonte ativa desse conhecimento.

**Questão arquitetural:** a presença de capacidade legítima de fuga no código não significa que o Desire e suas prioridades experimentais sejam adequados ao gameplay final. A saída conhecida pode competir com a perseguição e produzir o efeito indesejado de interromper o experimento de perseguição contínua.

**Gatilho para reavaliação:** ao neutralizar a amostra para perseguição sem saída e, depois, ao modelar deslocamento orientado a POI e fuga física de Rick.

**Decisão atual:** preservar mecanismos reutilizáveis; tratar os Desejos, pesos e destinos atuais como configuração experimental, não baseline definitivo.

### PEGA-ARQ-004 — Ingresso de NPCs preparado, mas não acionado pela amostra

**Classificação:** preparado, não acionado.  
**Estado:** preservar isolado e reavaliar quando a dinâmica de ingresso for necessária.

**Evidência observada:** `PEGANpcMissionSpawnEntry` e seus pontos (`Spawn Safe`, `North Entry Spawn Point`, `Mission Interior Destination`) compõem um ingresso físico legítimo e utilizam `ActorOccurrencePublicationScope`. O código exige um pedido explícito; a auditoria estática não encontrou chamador/produtor de receita no PEGA. Os dois larápios observados na Mission são instâncias já authoradas na cena e não dependem desse ingresso para aparecer.

**Questão arquitetural:** distinguir a capacidade futura de ingresso, o estado compartilhado da gangue e a configuração concreta de um experimento de passagem. A reorganização da Hierarchy já separou `Gang Knowledge`, `Thief Actors` e `Spawn Point Ingress`, mas isso não equivale a validar os pontos e parâmetros para uma missão definitiva.

**Gatilho para reavaliação:** quando forem introduzidos o agendamento/receitas de Spawn Points e o ciclo de assalto previstos no GDD.

**Decisão atual:** não adicionar disparador artificial, não confundir com os dois NPCs estáticos e não remover o sistema por estar inativo na amostra.

### PEGA-ARQ-005 — Configuração explícita de referências de Actor não escala para muitos NPCs

**Classificação:** uso confirmado + hipótese arquitetural de escalabilidade.  
**Estado:** preservar a composição atual; reavaliar após a checagem dos demais mecanismos do NPC.

**Evidência observada:** o prefab `PEGA_NPCActorRuntimeBase` contém `ActorOccurrenceReferenceBinding` configurado com referência explícita a um asset de Player Slot. `PEGANpcPerceptionSource` e `PEGANpcAutonomy` dependem desse componente; a percepção o utiliza para resolver a ocorrência atual do alvo. O padrão atende ao recorte atual com Rick/P1.

**Questão arquitetural:** exigir que cada NPC receba manualmente referências a slots e outros assets de contexto já conhecidos ou gerenciados pelo Framework pode gerar configuração repetitiva, risco de inconsistência e custo de manutenção desproporcional quando houver dezenas ou centenas de instâncias. Precisamos distinguir referências que fazem parte da intenção de gameplay do NPC daquelas que poderiam ser obtidas legitimamente do contexto ativo. **Ainda não está demonstrado** se o Framework oferece descoberta ou resolução contextual suficiente, nem se o binding atual exige essa configuração em todos os modos de uso.

**O que investigar:** contratos efetivos de `ActorOccurrenceReferenceBinding`, seus modos de seleção e o ciclo de resolução; fontes oficiais de contexto ativo e limites de suas responsabilidades. Avaliar se a configuração explícita é uma necessidade semântica ou apenas uma escolha da demonstração, sem pressupor resolução global automática nem introduzir acoplamentos ocultos.

**Gatilho para reavaliação:** após a checagem dos componentes de navegação, GOAP e percepção e, em especial, antes de escalar a autoria de NPCs.

**Decisão atual:** não alterar o binding nem automatizar referências nesta etapa; manter a preocupação registrada para avaliação conjunta com PEGA-ARQ-001 e PEGA-ARQ-002.

## Pontos de limpeza ainda sujeitos a confirmação

| Item | Evidência | Tratamento |
|---|---|---|
| Override `wanderRandomSeed: 102` em instância de NPC | Valor serializado sem campo correspondente encontrado na versão de código auditada | Confirmar no **Prefab Overrides** do Unity antes de remover |
| Referências antigas no Build Settings | Auditoria anterior encontrou duas cenas `FrameworkValidation` habilitadas, mas ausentes do workspace | Verificar no Editor quando revisar a preparação de build; não é um problema arquitetural demonstrado |

## Artefatos experimentais já tratados

- **FOUND-01:** objetos `Diagnostic` e `Result Request`, `MissionFlowDiagnostic` e guia operacional removidos; as capacidades de Route/Activity e os assets de Result foram preservados.
- **Prova visual #102:** `PEGAActorVisualCompositionProof` e helpers exclusivos removidos após confirmação de aceite; composição visual oficial e recursos compartilhados preservados.
- **Hierarchy da Activity:** Player Admission, Player Session Observation, Gang Knowledge, Thief Actors e Spawn Point Ingress separados por responsabilidade; referências funcionais preservadas na checagem estática.

Esses itens não devem ser reabertos sem nova evidência. A reorganização recente ainda requer a confirmação habitual no Unity, pois o Codex não executou Play Mode.

## Como registrar novos achados

Para cada novo mecanismo suspeito, acrescente uma entrada com:

1. **Identificador e título.**
2. **Uso comprovado na amostra:** o que o código realmente faz; separar uso em runtime de referência apenas serializada.
3. **Razão para existir hoje:** qual demonstração ou restrição ele atende.
4. **Dúvida para o produto final:** qual responsabilidade ou decisão pode não se sustentar.
5. **Recurso oficial a investigar:** somente se já houver indício concreto; sem antecipar implementação.
6. **Gatilho e decisão atual:** quando reavaliar e o que, por enquanto, deve ser mantido.

### Princípio orientador

**Não transformar a solução usada para comprovar uma capacidade em uma exigência arquitetural permanente por inércia.** Também não substituir uma integração legítima apenas por parecer provisória. A revisão deve ser motivada pelo comportamento real que o PEGA precisa oferecer, respeitando a autoridade do Immersive Framework e a evidência do código.
