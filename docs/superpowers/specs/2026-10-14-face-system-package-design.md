# Face System como Unity package

**Estado:** proposta para revisão do usuário
**Data:** 2026-10-14

## Objetivo

Transformar o Face System existente em um Unity package reutilizável e versionado, preservando os dados de expressão específicos dos personagens do PEGA e estabelecendo um contrato público estável antes de declarar `1.0.0`.

## Decisão de arquitetura

Criar um package independente chamado `com.immersivegames.face-system`, com namespace público `ImmersiveGames.FaceSystem`, em um repositório irmão aos packages atuais no workspace ImmersivePackages. O repositório remoto proposto segue a convenção existente: `ImmersiveGames/com.immersivegames.face-system`.

Esse produto é um package técnico reutilizável: controla estados e animações de expressão em modelos voxel e aplica a textura facial aos renderers configurados. Ele não pertence a `com.immersive.framework` e não depende de `foundation`, `logging`, `pooling` nem do framework. Os dados de personagem — atlases, bibliotecas, estados, animações, prefabs e cenas — continuam pertencendo ao projeto consumidor, como PEGA.

A alternativa de incluir o sistema como módulo do Framework foi rejeitada: o Face System possui ciclo de reutilização e versionamento independente e pode ser consumido sem o Framework.

## Estrutura do package

- `Runtime/`: `FaceSpriteLibrary`, `FaceState`, `FaceAnimation` e `FaceController`, em assembly Unity-linked (`noEngineReferences: false`).
- `Editor/`: inspectors, previews e validações de authoring, em assembly Editor-only.
- `Shaders/`: shader base compatível com Built-in e contrato de propriedade de textura documentado.
- `Samples~/UniversalRenderPipeline/`: Shader Graph opcional para URP, sem dependência obrigatória de URP no manifesto do package.
- `Samples~/FaceSystemTester/`: ferramenta de demonstração/teste manual, fora do runtime principal. Referências existentes ao `FaceControllerTester` devem ser migradas antes de removê-lo do local atual.
- `Documentation~/`: guia de instalação, authoring, bindings de renderer, shaders e migração.

O manifesto deve seguir a identidade, metadados e licença MIT dos packages irmãos. O primeiro baseline suportado será Unity `6000.5` (PEGA resolve `6000.5.0f1`); a implementação deve confirmar as APIs usadas nesse baseline.

## Contratos a estabilizar antes do primeiro release público

1. **Identidade de estado/animação:** IDs deixam de ser recalculados pelo nome do asset. São valores serializados estáveis e únicos dentro da biblioteca, com validação de duplicidade no Editor. Renomear um asset não muda sua identidade.
2. **Falhas explícitas:** operações de troca de estado/animação informam sucesso ou falha para IDs desconhecidos. Não há retorno silencioso nem substituição automática por outro estado.
3. **Estado inicial:** o controller referencia explicitamente seu estado inicial. A ordem da lista não determina o rosto inicial.
4. **Bindings de renderer:** cada renderer facial é configurado explicitamente. A procura implícita por nome de GameObject e tipo de renderer é removida após migrar os prefabs consumidores.
5. **Propriedade do shader:** `_MainTex` é o nome canônico para a textura facial. O Shader Graph URP existente será alinhado ao contrato; o controller não precisa escrever nomes alternativos.
6. **Limite Unity/runtime:** o package pode usar `UnityEngine` no runtime; os tipos e ferramentas de Editor ficam em assemblies separados. Nenhuma dependência do Framework é introduzida.

## Migração e preservação de dados

A origem é `Assets/_Project/FaceSystem`. Migrar os scripts, shaders, ferramentas e documentação que pertencem ao produto para o package, preservando os `.meta` GUIDs dos scripts e assets movidos sempre que possível. Não mover para o package os atlas, estados, animações, materiais de personagem, prefabs ou cenas do PEGA.

O PEGA contém assets serializados com identificadores da assembly `Assembly-CSharp`, por exemplo `Assets/PEGA/Arts/3D Models/Characters/Player/Rick Ronda/Emotions/Face States/FS_Normal_Male_Security_Model_01.asset`. A migração também atualiza os scripts consumidores de C# para o namespace público `ImmersiveGames.FaceSystem`. A mudança de assembly/namespace precisa ser validada após importar o package: referências ScriptableObject, componentes em prefabs/cenas, bindings, shaders e materiais devem resolver sem dados perdidos. Corrigir identificadores serializados apenas se a importação demonstrar que é necessário; preservar GUIDs por si só não será tratado como prova de migração correta.

`FaceControllerTester` está anexado a objetos existentes e não pode simplesmente desaparecer do runtime sem migrar essas referências para a amostra ou remover seus componentes de forma deliberada.

## Versão e publicação

O primeiro package importável será `0.1.0`, ainda pré-estável. Não publicar `1.0.0` até que o contrato acima esteja documentado, a migração do PEGA tenha sido validada no Unity `6000.5.0f1`, os assets existentes resolvam e os caminhos Built-in/URP incluídos estejam identificados claramente.

Cada distribuição posterior usa tags Git compatíveis com SemVer. A dependência no PEGA deve apontar para uma tag, nunca para uma branch móvel. O repositório terá `package.json`, `README.md`, `CHANGELOG.md`, licença MIT, documentação de uso e assemblies versionados junto do código.

## Critérios de aceitação

- O repositório é independente e produz um package UPM com identidade `com.immersivegames.face-system`.
- Runtime e Editor estão separados, sem uma assembly runtime acidentalmente Editor-only e sem dependência do Framework.
- IDs, estado inicial, falhas e bindings não dependem de ordem de lista, renomeação ou busca por nome de objeto.
- Projetos consumidores mantêm seus dados de personagem fora do package e conseguem configurar os renderers explicitamente.
- A importação do package no PEGA preserva referências de estados, animações, componentes, materiais e shaders existentes ou documenta/migra cada referência afetada.
- A instalação documentada usa versão/tag reproduzível. A declaração de estabilidade é posterior à validação Unity pelo usuário.

## Validação manual necessária

No Unity `6000.5.0f1`, importar o package no PEGA e confirmar: ausência de erros de compilação/importação; referências de ScriptableObjects e componentes preservadas; mudança para estados e animações conhecidos; falha observável para IDs desconhecidos; renderers `MeshRenderer` e `SkinnedMeshRenderer`; aplicação do atlas via `_MainTex`; shader Built-in e amostra URP em projeto com URP; e ausência de dependência obrigatória do Framework.

Codex não executará build, testes, importação, Play Mode ou batchmode do Unity. A validação Unity e a evidência de importação ficam para execução manual no projeto consumidor.

## Fora de escopo

- Alterar GDD, MVP, TASKS, ADR, Trello ou GanttFlow.
- Mover para o package dados específicos do PEGA.
- Integrar o Face System ao `com.immersive.framework`.
- Declarar compatibilidade com versões de Unity ou render pipelines que não tenham sido verificadas.
- Publicar `1.0.0` antes da validação manual do consumidor.
