# PEGA — Actor recognition e demonstração de roubo por NPC

**Data:** 10 de novembro de 2026<br>
**Natureza:** registro de implementação e integração; não altera GDD, MVP ou decisões arquiteturais.

## Organização funcional e nomenclatura

Os componentes de gameplay foram agrupados por capacidade sob `Assets/PEGA`: movimento em `Movement/{Actor,NPC,Player}`, interação em `Interactions/Runtime/{Actor,ObjectEntry,Player}`, seleção em `Selection/Player`, descoberta em `Player/Perception` e apresentação em `Presentation/{Actor,ObjectEntry,Player}`. A organização preserva os papéis de Actor compartilhado, NPC, Player e Object Entry. Os `.meta` dos scripts movidos acompanham os arquivos para manter os GUIDs usados por prefabs e cenas.

Os identificadores C# seguem a padronização aplicada pelo IDE, incluindo o prefixo `Pega` e os namespaces ajustados. Os identificadores atuais permanecem nos assets serializados; esta entrega não antecipa renomeações posteriores.

## Reconhecimento físico de Actors

O PEGA usa `IActorOccurrenceRecognitionQuery` para associar um `Collider` candidato à ocorrência física comprometida de um Actor. A implementação correspondente está no Framework, em `ActorOccurrenceQueryBinding`, e retorna uma `ActorOccurrence` tipada. A consulta não faz descoberta espacial, visibilidade ou autorização de gameplay; essas decisões continuam com os sistemas PEGA e suas autoridades existentes.

A API é **Experimental**. O Framework registra o contrato em [Public API](../../../ImmersivePackages/com.immersive.framework/Documentation~/API/Public-API.md) e [Actor Occurrence References](../../../ImmersivePackages/com.immersive.framework/Documentation~/Guides/Actor-Occurrence-References.md). Este registro não promove sua maturidade nem altera a versão do pacote.

## NPCs demonstradores na Mission

Na cena `Assets/PEGA/Scenes/MissionActivityContent.unity`, `Larapio NPC - Second Group Demo` permanece com o perfil `Demo_SecondGroupInvestigationAndExit` e a sequência de investigação dos POIs. Seu perfil não inclui o desejo de roubo.

O novo `Larapio NPC - Rob Demo` tem identidade de Actor própria (`pega.mission.larapio.rob-demo`), usa o perfil `Demo_SecondGroupRobOnly` e compartilha o perfil de grupo e o acesso ao Knowledge de grupo. O perfil de desejo contém somente roubo; a lista de comportamentos do prefab mantém as quatro capacidades comuns, usa navegação para Object Entry e inclui a ação de roubo. O alvo é o Object Entry existente `Object Entry - Rob (NPC Proof)`; não foi criado outro item para essa demonstração.

O desejo, a ação de planejamento `PegaNpcRobKnownObjectEntryAction`, a condição de posse e os efeitos de GOAP formam o fluxo de demonstração do roubo. Os contratos de identidade, composição, ocorrência e Knowledge continuam nos respectivos owners PEGA/Framework. A mudança separa os dois objetivos na composição dos NPCs para que a demonstração de roubo não substitua a sequência de POIs.

## Serialização e estado da validação

As referências a scripts movidos preservam os GUIDs dos `.meta`; o novo perfil de desejo recebe GUID próprio. O campo serializado do snapshot de roubo preserva o nome anterior via `FormerlySerializedAs`, e a cena usa o nome atual `objectEntryRobActionValue`.

O usuário reportou anteriormente que o projeto voltou a rodar sem erros após os reparos de Knowledge. A nova composição do NPC de roubo foi adicionada depois dessa confirmação e ainda não foi reaberta/validada no Unity. A revisão desta entrega foi estática; não houve import, compilação, Play Mode ou execução de testes Unity. A validação manual pendente é reabrir a cena, confirmar os dois perfis e listas de comportamentos no Inspector e observar o fluxo de cada NPC em Play Mode.
