# PEGA Player Camera — MC-01 / cartão #29

## Composição autorada

`PEGA_PlayerActorRuntimeHost` contém `PlayerActorDeclaration` e `ActorCameraSubjectAuthoring` no mesmo GameObject. O subject aponta explicitamente para o filho `CameraObservationAnchor`, inicialmente em `(0, 1, 0)`. A occurrence de P1 selecionada pelo assignment resolve o Actor materializado (Rick pelo perfil P1) e fornece esse alvo ao rig; não há busca pelo nome nem alvo global.

`PEGA_PlayerCameraAssignment_P1` usa `IndividualPerPlayer`, associação explícita ao slot P1 e mapeamento para o `PEGA_TechnicalCameraOutput` existente. `PEGAApplication` inicia com esse assignment. O prefab de Camera Output, a câmera física, o Brain e seu rig fallback permanecem inalterados.

## Rig Follow / ScreenComposer

- Framework `CameraRigComposer` com `FollowCameraRigBehaviorDefinition`; o modo ativo é `ScreenComposer` e `lookAtRequirement = NotUsed`.
- O root do prefab tem rotação identidade; o pitch capturado da CinemachineCamera é `30°` relativo a esse root, e o FOV é `50°`.
- `CinemachinePositionComposer.CameraDistance = 30`; ele controla a distância e o enquadramento. Não há controle adicional de rotação materializado.
- `FollowCameraRigBehaviorDefinition.FollowOffset = (0, 14.4, -20)` permanece serializado de uma configuração FixedOffset anterior, mas fica inativo enquanto `ScreenComposer` estiver selecionado. Não é um segundo offset aplicado à câmera.

O rig acompanha o `CameraObservationAnchor` do Actor e mantém o pitch capturado em relação ao root. A instância de cena deve herdar a rotação do prefab; não aplique Euler `55°` ao root para obter esse enquadramento. Refinamento visual depende do Preview no Unity e da imagem original da referência.
## Limite para split-screen

O assignment atual mapeia somente P1 para uma saída física e não declara layout de viewport. Uma evolução horizontal exigirá uma saída/rig independente por Player e uma decisão autorada de layout/topologia. Este cartão não cria a segunda saída, não altera P2, Player Session ou Actor Profiles.

## Validação manual no Unity

1. Reimportar e confirmar a ausência de Missing Script/asset reference nos três assets de câmera e no prefab de Player.
2. Abrir `PEGA_PlayerFollowCameraRig` em Prefab Mode; rodar **Validate** e **Apply/Rebuild** no `CameraRigComposer` e confirmar `ScreenComposer`, `CinemachinePositionComposer`, root identidade, Pitch `30°`, Distance `30` e FOV `50°`.
3. Iniciar a aplicação pela rota de Mission. Confirmar Rick enquadrado, orientação fixa e leitura de piso/fachadas; mover o Actor temporariamente no Play Mode para observar o follow e girá-lo para confirmar que a câmera não acompanha a rotação. Mouse não deve orbitar.
4. Acionar o fluxo existente Mission → Result e confirmar que a transição continua funcionando.
5. Comparar com a imagem anexada à conversa. O arquivo da referência deve também ficar anexado ao cartão Trello para inspeção fora desta conversa.

Unity Editor, Play Mode e aceitação visual não foram executados nesta edição estática.
