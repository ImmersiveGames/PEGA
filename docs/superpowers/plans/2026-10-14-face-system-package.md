# Face System Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Criar e integrar o package UPM `com.immersivegames.face-system` na versão pré-estável `0.1.0`, preservando os dados de personagens do PEGA e removendo a cópia ativa do sistema de dentro do projeto consumidor.

**Architecture:** Repositório independente com namespace `ImmersiveGames.FaceSystem`, assembly Runtime que referencia Unity, assembly Editor separado e amostras opcionais para URP e para `FaceControllerTester`. Os assets de estado, animação, atlas, materiais, prefab e cenas permanecem no PEGA; a migração preserva GUIDs e atualiza os contratos públicos antes de remover a implementação local.

**Tech Stack:** Unity `6000.5`, Unity Package Manager, C#, ShaderLab, Shader Graph, Git e SemVer.

**Spec:** `docs/superpowers/specs/2026-10-14-face-system-package-design.md`

## Global Constraints

- Package ID: `com.immersivegames.face-system`; namespace público: `ImmersiveGames.FaceSystem`.
- Baseline: Unity `6000.5`; o PEGA resolve `6000.5.0f1`.
- Licença MIT e metadados alinhados aos packages irmãos.
- Não depender de `com.immersive.framework`, `foundation`, `logging` ou `pooling`.
- Runtime pode referenciar `UnityEngine`; assemblies Editor usam plataforma `Editor` e referenciam Runtime.
- Dados de personagens, atlas, estados, animações, materiais, prefabs e cenas continuam no PEGA.
- Preservar os GUIDs `.meta` dos scripts e assets movidos; validar a serialização no projeto consumidor.
- Não gravar caminhos locais absolutos em manifesto, README, código ou instruções canônicas do package.
- Instalar o package no PEGA com URL Git fixada na tag `0.1.0`, sem branch móvel.
- Não alterar GDD, MVP, TASKS, ADR, Trello ou GanttFlow.
- Não adicionar nem executar testes automatizados; usar checagens estáticas e a lista manual Unity abaixo.
- Não executar build, importação, Play Mode, smoke ou batchmode do Unity por Codex; o usuário confirma a validação no Editor.
- Inspecionar o diff pré-existente de `Packages/manifest.json` e `Packages/packages-lock.json` antes da integração; preservar a atualização do Framework já presente.

## Review Focus

- ID vazio, duplicado ou após renomear asset: inspecionar as mensagens do Inspector e confirmar que o ID serializado não muda.
- Estado inicial ausente e chamada de ID desconhecido: confirmar que não ocorre escolha por ordem nem interrupção silenciosa; conferir o retorno `false` no sample e no Console.
- Referências de renderer no prefab após trocar de assembly: abrir os três prefabs consumidores e a cena de preview e confirmar os bindings de `MeshRenderer`/`SkinnedMeshRenderer`.
- Propriedade de textura em Built-in e URP: confirmar `_MainTex`, atlas, `_Offset` e `_Tiling` nos materiais de PEGA e nas amostras.
- GUIDs, `m_EditorClassIdentifier` e amostras: confirmar que nenhum ScriptableObject, componente, shader ou `FaceControllerTester` aparece como Missing após instalar/importar as amostras.

---

### Task 1: Criar o repositório público e o esqueleto UPM

**Arquivos:**
- Criar em `ImmersivePackages/com.immersivegames.face-system/`: `package.json`, `README.md`, `CHANGELOG.md`, `LICENSE.md`.
- Criar `Runtime/ImmersiveGames.FaceSystem.Runtime.asmdef`.
- Criar `Editor/ImmersiveGames.FaceSystem.Editor.asmdef`.

**Interfaces:**
- Produz: package `com.immersivegames.face-system`, versão inicial `0.1.0`, sem dependências externas.
- Runtime: assembly `ImmersiveGames.FaceSystem.Runtime`, `rootNamespace` `ImmersiveGames.FaceSystem`, referências Unity habilitadas.
- Editor: assembly `ImmersiveGames.FaceSystem.Editor`, `includePlatforms: ["Editor"]`, referência à assembly Runtime.

- [ ] Criar o repositório local como irmão dos packages existentes e o repositório público `ImmersiveGames/com.immersivegames.face-system`, seguindo a convenção verificada nos packages irmãos.
- [ ] Criar o manifesto UPM com `name: com.immersivegames.face-system`, `displayName: Immersive Face System`, `version: 0.1.0`, `unity: 6000.5`, `unityRelease: 0f1`, licença MIT, autor Immersive Games e URLs de documentação/changelog.
- [ ] Criar README de instalação/escopo, changelog inicial e licença MIT.
- [ ] Criar os dois asmdefs com as interfaces descritas acima; não declarar dependência do Framework.
- [ ] Validar o JSON do manifesto e dos asmdefs com `ConvertFrom-Json`; conferir `git diff --check`.
- [ ] Commitar como `chore: scaffold Face System package` e enviar a branch `main` ao repositório remoto.

### Task 2: Migrar fontes, ferramentas, shaders e documentação

**Arquivos:**
- Criar em `Runtime/`: `FaceController.cs`, `FaceState.cs`, `FaceSpriteLibrary.cs`, `FaceSpriteEntry.cs`, `FaceStateFrame.cs`, `FaceAnimation.cs`, `FaceAnimationTypes.cs`, `FaceAnimationTrack.cs`, `FaceAnimationFrames.cs`, `FaceAnimationPlayer.cs` e `FaceTrackPlayer.cs`, com seus `.meta`.
- Criar em `Editor/`: `FaceStateEditor.cs`, `FaceSpriteLibraryEditor.cs`, `FaceEditorUtility.cs` e `FaceAnimationEditor.cs`, com seus `.meta`.
- Criar em `Samples~/FaceSystemTester/Runtime/`: `FaceControllerTester.cs` e `ImmersiveGames.FaceSystem.Samples.asmdef`.
- Criar em `Samples~/FaceSystemTester/Editor/`: `FaceControllerTesterEditor.cs` e `ImmersiveGames.FaceSystem.Samples.Editor.asmdef`.
- Criar em `Shaders/`: `FaceAtlasBuiltIn.shader` e seu `.meta`.
- Criar em `Samples~/UniversalRenderPipeline/`: `FaceAtlasShader.shadergraph` e seu `.meta`.
- Criar em `Documentation~/`: `face_system_user_manual.md`, `face_system_spec.md`, `FACESYSTEM_ARCHITECTURE_V2.md` e `FaceExpressionSystemDocumentation.md`, revisados para os caminhos do package.

**Interfaces:**
- Consome: package scaffold da Task 1.
- Produz: runtime, ferramentas de authoring, amostras e documentação organizados pelas assemblies do package.

- [ ] Copiar os 11 scripts de runtime listados acima, junto dos `.meta`, para `Runtime/`; manter todas as declarações `partial` de cada classe na mesma assembly. Preservar `.meta` existentes e criar `.meta` estáveis para as novas pastas e assemblies.
- [ ] Mover os quatro inspectors/utility de produto para `Editor/` e criar a assembly Editor-only que referencia Runtime.
- [ ] Mover `FaceControllerTester` e seu inspector para a amostra, com assembly runtime de amostra e assembly Editor-only de amostra; preservar os GUIDs de ambos os scripts.
- [ ] Mover o shader Built-in para `Shaders/` e o Shader Graph URP para `Samples~/UniversalRenderPipeline/`; não adicionar URP como dependência do manifesto.
- [ ] Mover os quatro documentos atuais para `Documentation~/`; corrigir referências antigas a `Assets/Shader`, `Assets/Shaders` e `Assets/Docs` para os caminhos de package/amostra correspondentes.
- [ ] Confirmar por busca que cada script e asset movido conserva o GUID do `.meta` original e que não há `UnityEditor` nos scripts Runtime.
- [ ] Executar checagens estáticas de JSON, referências, GUIDs e `git diff --check`; não abrir Unity.
- [ ] Commitar como `feat: package Face System sources` e enviar ao repositório remoto.

### Task 3: Estabilizar identidade, runtime e contrato de shader

**Arquivos:**
- Modificar `Runtime/FaceState.cs`, `Runtime/FaceAnimation.cs`, `Runtime/FaceController.cs` e declarações parciais relacionadas.
- Modificar `Editor/FaceStateEditor.cs`, `Editor/FaceAnimationEditor.cs` e editores de library/controller afetados pela validação.
- Modificar `Samples~/UniversalRenderPipeline/FaceAtlasShader.shadergraph`.
- Modificar `Samples~/FaceSystemTester/Editor/FaceControllerTesterEditor.cs` para exibir um `HelpBox` quando uma chamada de demonstração retornar `false`.
- Modificar `Documentation~/` para refletir as APIs e requisitos definitivos.

**Interfaces:**
- `public bool SetState(string stateId)`: retorna `false` para ID vazio/desconhecido ou configuração inválida, sem alterar o estado ativo; retorna `true` somente quando aplica o estado registrado.
- `public bool PlayAnimation(string animationId)`: retorna `false` para ID vazio/desconhecido ou animação que não pode iniciar, sem parar tracks atuais; retorna `true` somente quando inicia o player.
- Campo serializado `FaceState defaultState` em `FaceController`; é obrigatório e inicializa o rosto sem consultar a ordem de `states`.

- [ ] Atribuir namespace `ImmersiveGames.FaceSystem` aos scripts Runtime e namespace `ImmersiveGames.FaceSystem.Editor` às ferramentas Editor; atualizar referências internas e do sample.
- [ ] Em `FaceState` e `FaceAnimation`, preservar IDs serializados e remover o comportamento de `OnValidate` que os sobrescreve pelo nome do asset; validar vazio e duplicidade.
- [ ] Interpretar “único dentro da biblioteca” da especificação como único dentro de cada coleção de estados e de animações registrada no controller; manter IDs de tracks/sprites validados nas respectivas coleções da `FaceSpriteLibrary`.
- [ ] Alterar `SetState` e `PlayAnimation` para as assinaturas e regras de retorno definidas; não interromper estado/track atual quando o ID de entrada não existir.
- [ ] Adicionar `defaultState` serializado e validar ausência/referência não registrada como erro; remover seleção do primeiro item válido da lista.
- [ ] Manter `TrackBinding.targetRenderer` explícito, mas remover a busca runtime por nome de GameObject e tipo de renderer; validar a referência serializada e o índice de material antes de aplicar.
- [ ] Manter `MaterialPropertyBlock` por renderer/índice; usar `_MainTex` como propriedade canônica, remover escrita em `_MainText` e alinhar a propriedade do Shader Graph URP.
- [ ] Atualizar inspectors e documentação para mostrar IDs estáveis, estado inicial obrigatório, retorno booleano e configuração de bindings.
- [ ] Atualizar o inspector de `FaceControllerTester` para exibir um `HelpBox` quando `SetState` ou `PlayAnimation` retornar `false`.
- [ ] Executar checagens estáticas de assinaturas, IDs recalculados, busca por renderer e nomes de propriedades; conferir `git diff --check`.
- [ ] Commitar como `feat: stabilize Face System runtime contracts` e enviar ao repositório remoto.

### Task 4: Integrar o package e migrar os consumidores PEGA

**Arquivos:**
- Modificar `Packages/manifest.json`; `Packages/packages-lock.json` será escrito pelo Package Manager do Unity ao resolver a nova dependência.
- Modificar `Assets/PEGA/Arts/Scenes/CharacterPreview.unity` e os prefabs:
  - `Assets/PEGA/Arts/3D Models/Characters/Player/Rick Ronda/Male_Security_01.prefab`
  - `Assets/PEGA/Arts/3D Models/Characters/Larapios/Striped Robber/Stripped_Robber.prefab`
  - `Assets/PEGA/Arts/3D Models/Characters/Player/Petra Patrol/Female_Security_Rig.prefab`
- Inspecionar sem mover os assets de face `FL_Male_Security_Model_01_Atlas.asset`, `Face States/FS_Normal_Male_Security_Model_01.asset` e `Face Animations/FA_Blink_Male_Security_Model_01.asset` em `Assets/PEGA/Arts/3D Models/Characters/Player/Rick Ronda/Emotions`; `FL_Striped_Robber.asset`, `FaceState/FS_Normal_Stripped_Robber.asset`, `FaceState/FS_Sneaking_Stripped_Robber.asset` e `FaceAnimation/FA_Blink_Stripped_Robber.asset` em `Assets/PEGA/Arts/3D Models/Characters/Larapios/Striped Robber/FaceAssets`; e `FS_Normal_Female_Security.asset`, `FA_Blink_Female_Security.asset` e `FL_Female_Security_Atlas.asset` em `Assets/PEGA/Arts/3D Models/Characters/Player/Petra Patrol/Emotions`. Preservar IDs serializados e só preencher um ID ausente com o nome atual do asset.
- Modificar estes seis materiais, confirmando `_MainTex` antes de remover o override duplicado `_MainText`:
  - `Assets/PEGA/Arts/3D Models/Characters/Player/Rick Ronda/Materials/Male_Security_Model_01_Eyes_Material.mat`
  - `Assets/PEGA/Arts/3D Models/Characters/Player/Rick Ronda/Materials/Male_Security_Model_01_Mouth_Material.mat`
  - `Assets/PEGA/Arts/3D Models/Characters/Player/Petra Patrol/Textures/Female_Security_Eyes_Material.mat`
  - `Assets/PEGA/Arts/3D Models/Characters/Player/Petra Patrol/Textures/Female_Security_Mouth_Mat.mat`
  - `Assets/PEGA/Arts/3D Models/Characters/Larapios/Striped Robber/Texture/Striped_Robber_Eyes_Mat.mat`
  - `Assets/PEGA/Arts/3D Models/Characters/Larapios/Striped Robber/Texture/Striped_Robber_Mouth_Mat.mat`
- Remover a implementação antiga de `Assets/_Project/FaceSystem/Scripts`, `Shaders` e `Docs` depois que a referência UPM estiver preparada.

**Interfaces:**
- Consome: package `0.1.0` enviado ao remoto pela Task 3.
- Produz: PEGA depende do Git package `https://github.com/ImmersiveGames/com.immersivegames.face-system.git#0.1.0`; os assets de personagens seguem no PEGA.

- [ ] Criar a tag `0.1.0` no commit do package e enviá-la ao remoto.
- [ ] Rever o diff atual de `manifest.json` e `packages-lock.json`; adicionar só a dependência do Face System e manter `com.immersive.framework: 1.1.0` e as demais alterações pré-existentes.
- [ ] Definir o campo `defaultState` em cada controller com o primeiro estado válido já registrado, preservando a aparência inicial anterior; manter os `targetRenderer` serializados.
- [ ] Importar pelo Package Manager as amostras `FaceSystemTester` e `UniversalRenderPipeline` para preservar o inspector de demonstração e o Shader Graph referenciado por materiais PEGA.
- [ ] Remover os scripts, shaders e documentos da cópia local `Assets/_Project/FaceSystem` somente após a dependência e as duas amostras estarem disponíveis; manter atlas, bibliotecas, estados, animações, materiais, prefabs e cenas do PEGA.
- [ ] Nos seis materiais listados, confirmar o valor de textura em `_MainTex` e só então remover a entrada duplicada `_MainText`; atualizar identificadores serializados de assembly somente se a reimportação demonstrar que Unity não os reconcilia por GUID.
- [ ] Repetir a busca por referências C# fora do Face System; a auditoria atual encontrou apenas referências serializadas em prefabs, cena e assets, sem call sites C# consumidores.
- [ ] Conferir o diff PEGA para garantir que as alterações preexistentes do manifest/lock não foram revertidas; não editar manualmente o lock gerado pelo Unity.
- [ ] Não commitar a migração PEGA nem `manifest.json`/`packages-lock.json` nesta etapa: os dois arquivos já têm alterações preexistentes e a resolução do lock depende da ação manual no Unity. Entregar o diff revisável sem stage/commit desses arquivos.

### Task 5: Fechar a validação pré-estável e documentar a distribuição

**Arquivos:**
- Modificar `README.md`, `CHANGELOG.md` e `Documentation~/` do package conforme resultados manuais do usuário.
- Modificar `Packages/manifest.json` e `Packages/packages-lock.json` do PEGA somente se a versão Git/tag mudar.

**Interfaces:**
- Consome: package candidato `0.1.0` e projeto PEGA migrado.
- Produz: instruções reproduzíveis por tag e registro explícito do estado pré-estável.

- [ ] Rodar checagens estáticas: JSON de manifesto/asmdefs, `git diff --check`, busca por `Assembly-CSharp::FaceState`, `_MainText`, seleção de primeiro estado e resolução de renderer por nome/tipo.
- [ ] Solicitar validação manual do usuário no Unity `6000.5.0f1`: import sem erro; referências dos assets preservadas; estado/ID conhecidos e desconhecidos; `MeshRenderer` e `SkinnedMeshRenderer`; `_MainTex` no Built-in e na amostra URP; `FaceControllerTester` nos prefabs/cena.
- [ ] Registrar no changelog as limitações e resultados informados; se houver correção depois da tag candidata, publicar `0.1.1` ou posterior, sem mover a tag `0.1.0`.
- [ ] Declarar `1.0.0` somente após confirmação manual dos critérios de aceitação da especificação; o escopo deste plano termina no package `0.1.0` pré-estável.
- [ ] Commitar ajustes de documentação e publicar a tag apropriada somente depois de revisar o diff e os resultados fornecidos pelo usuário.

## Riscos e dependências

- Os scripts e assets do PEGA estão fora do workspace `ImmersivePackages`; a escrita/Git nesse diretório precisa estar disponível na execução. A tentativa anterior de `git -C` ainda retornou `Permission denied`, apesar da leitura dos packages.
- A integração GitHub não expõe operação de criação de repositório; o remoto terá de ser criado pela sessão GitHub autenticada no navegador ou por ferramenta autorizada disponível durante a execução.
- O Unity pode não atualizar `m_EditorClassIdentifier` só com preservação de GUID; confirmar manualmente após a importação antes de alterar YAML serializado.
- As duas amostras precisam ser importadas no PEGA antes de remover cópias locais para evitar shader ou script Missing.
- A versão `0.1.0` é importável e pré-estável. A evidência de Unity necessária para estabilidade não pode ser produzida pelo Codex segundo as regras atuais do projeto.
