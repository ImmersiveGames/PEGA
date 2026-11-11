# PEGA — Composição de interação Player/NPC e Pick Up

**Data:** 11 de novembro de 2026<br>
**Natureza:** registro de implementação e integração; não altera GDD, MVP ou decisões arquiteturais.

## Organização funcional e nomenclatura

Os componentes de gameplay estão organizados por capacidade sob `Assets/PEGA`: movimento em `Movement/{Actor,NPC,Player}`, interação em `Interactions/Runtime/{Actor,ObjectEntry,Player}`, seleção em `Selection/Player`, descoberta em `Player/Perception` e apresentação em `Presentation/{Actor,ObjectEntry,Player}`. A separação mantém distintas as responsabilidades do Actor compartilhado, do Player, dos NPCs e dos Object Entries.

Os tipos de interação seguem as convenções C# e os namespaces do PEGA. As referências serializadas continuam nos prefabs e na cena; os `.meta` acompanham os movimentos e renomeações de scripts e assets com seus GUIDs preservados quando a identidade existente deve continuar estável.

## Composição da interação

`PegaActorObjectInteractionMechanic` é a base abstrata. Ela não expõe campos de Player ou NPC: concentra a validação comum do Actor e encaminha a operação escolhida ao `PegaObjectEntryInteractionAggregator` do alvo. Há uma única chamada de execução compartilhada nessa base.

- `PegaPlayerObjectInteractionMechanic` recebe a intenção genérica do Player e usa as referências de seleção e descoberta do Player para validar o candidato atual e resolver a operação oferecida pelo Object Entry. A intenção continua separada da execução.
- `PegaNpcObjectInteractionMechanic` recebe do provider GOAP a operação e o alvo já escolhidos. O GOAP e o Knowledge autorizado mantêm a responsabilidade pela decisão e pela seleção do Target.
- `PegaObjectEntryInteractionAggregator` compõe as operações oferecidas por cada Object Entry e os participantes admitidos. A Action reutilizável de Pick Up é a mesma para Player e NPC; as especializações apenas fornecem solicitações pela decisão pertinente a cada participante.

No caminho do NPC, `PegaNpcPickUpKnownObjectEntryAction` representa a capacidade que o GOAP planeja. Ela seleciona a identidade de Object Entry a partir dos Targets conhecidos e encaminha a operação física composta no objeto. `PegaObjectEntryPickUpAction` é a execução compartilhada. A Action de planejamento e a operação do Object Entry permanecem contratos distintos.

## Pick Up, autoria dos objetos e inventário

Cada Object Entry configura sua oferta no `PegaObjectEntryInteractionAggregator`. A oferta aponta para o componente de Action naquele mesmo Object Entry e declara seus participantes. O componente `PegaObjectEntryPickUpAction` expõe os parâmetros `Interaction Range Meters` e `Execution Duration Seconds` para autoria por objeto.

Na `MissionActivityContent`, os objetos `Object Entry - Pick Up (Player Proof)` e `Object Entry - Pick Up (NPC Proof)` usam a mesma composição: uma oferta para Player e NPC, alcance **1,35 m** e duração **1 s**. Suas posições são distintas. Os `ObjectEntryId`s existentes permanecem `pega.mission.player.rob-item` e `pega.mission.second-group.rob-item`; esses identificadores técnicos não definem a nomenclatura da operação.

O NPC demonstrador usa a Action GOAP `PickUpKnownObjectEntry` e o Desire `PickUpKnownObjectEntry`, exibido como **Pick Up a known item**. O GOAP usa Group Knowledge autorizado para conhecer posição, disponibilidade e parâmetros de planejamento do Object Entry. O prefab Player e o prefab base dos NPCs configuram `PegaActorPhysicalInventory.Capacity` como **2** itens. A validação da capacidade na elegibilidade e durante a execução permanece no owner do inventário.

## Contratos Immersive e identidade

O PEGA continua consumindo `ActorDeclaration`, `ObjectEntryDeclaration`, os bindings de consulta de ocorrência e a membership tipada de Actor Group do Framework. Identidade e ocorrência física continuam pertencendo ao Framework; descoberta e seleção do Player, decisão GOAP, conhecimento e regras da operação permanecem nos respectivos owners do PEGA. A alteração não muda lifecycle, ownership, colliders, layers ou regras de descoberta, e nenhum pacote Immersive foi modificado.

O PEGA usa `IActorOccurrenceRecognitionQuery` para associar um `Collider` candidato à ocorrência física comprometida de um Actor. A implementação correspondente está no Framework, em `ActorOccurrenceQueryBinding`, e retorna uma `ActorOccurrence` tipada. A consulta não faz descoberta espacial, visibilidade ou autorização de gameplay.

A API é **Experimental**. O Framework registra o contrato em [Public API](../../../../ImmersivePackages/com.immersive.framework/Documentation~/API/Public-API.md) e [Actor Occurrence References](../../../../ImmersivePackages/com.immersive.framework/Documentation~/Guides/Actor-Occurrence-References.md). Este registro não promove sua maturidade nem altera a versão do pacote.

## Verificações e validação

A revisão estática conferiu os nomes e namespaces nas declarações e referências PEGA, os GUIDs dos `.meta` movidos, as referências de scripts/Actions nos prefabs e na cena, os IDs dos dois Object Entries, os parâmetros da Action e a capacidade serializada nos prefabs.

O usuário informou que a nova composição especializada Player/NPC funciona no Unity. Esse relato cobre a composição da interação. A verificação desta consolidação é estática: não houve importação, compilação, Play Mode ou execução de testes pelo Codex nesta etapa, e a execução de Pick Up não é declarada como validada por esse relato.
