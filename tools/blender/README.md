# Voxel Tool (add-on do Blender)

Ferramenta da Immersive Games. Leva um modelo exportado do MagicaVoxel para
o padrão de personagens voxel em um clique: malha otimizada só com quads,
escala e origem certas, UV aberto no grid de pixels e uma textura base já
pintada com as cores dos voxels, pronta para pintar por cima. Os valores
padrão vêm dos personagens do PEGA.

## Instalar

1. Gere o zip: `python tools/blender/build_addon_zip.py` (cria
   `tools/blender/dist/voxel_tool-<versão>.zip`).
2. Blender 4.2 ou mais novo: *Edit > Preferences > Get Extensions*, menu `v`
   no canto superior direito > *Install from Disk...* e escolha o zip.
   Blender 4.0/4.1: *Edit > Preferences > Add-ons > Install...* e escolha o zip.
3. O painel aparece em *View3D > Sidebar (N) > Voxel Tool*.

## Usar

1. No MagicaVoxel, exporte em **OBJ** (gera `.obj`, `.mtl` e a paleta `.png`).
2. No Blender, *File > Import > Wavefront (.obj)* com as opções padrão.
3. Selecione as malhas importadas (corpo, cabeça etc. podem ser objetos
   separados; todos compartilham um único material e textura).
4. Na aba **Voxel Tool**, preencha o *Nome* (ex.: `Striped_Robber`) e clique em
   **Converter voxel**.
5. Pinte `Textures/<Nome>_Main_Texture.png` no editor de imagem de sua
   preferência. `<Nome>_UV_Guide.png` é uma camada transparente com o contorno
   de cada ilha (magenta) e a grade de voxels, para usar como camada de
   referência e esconder antes de salvar.
6. Faça o rig como de costume e use **Exportar FBX para o Unity** com a seleção.

O resultado é criado como `<objeto>_Voxel` dentro da collection
`<Nome>_Voxel`; o original fica escondido e intacto. Rodar de novo não
apaga uma textura já pintada, a menos que *Sobrescrever textura* esteja
ligado.

## O padrão (inferido dos FBX do PEGA)

Medido em Rick Ronda (`Male_Security_01.fbx`), Petra Patrol
(`Female_Security_Rig.fbx`) e Striped Robber (`Striped Robber.fbx`):

| Item | Valor nos personagens | Opção no painel |
|---|---|---|
| Polígonos | só quads, faces coplanares fundidas; corpo com 158 a 228 quads | *Separar faces por cor* desligado |
| T-junctions | presentes (como no export do MagicaVoxel) | mantidas |
| Escala | 1 voxel = 0.1 unidade; personagem com ~5 a 5.5 de altura | *Voxel final* = 0.1 |
| Origem | centro em X, pés em Z=0, frente para -Y | *Origem* = Centro da base |
| UV | uma ilha por face, cantos no grid de pixels | automático |
| Densidade | 8 px por voxel | *Pixels por voxel* = 8 |
| Textura | 256x256 RGBA, filtro Point no Unity | *Textura mínima* = 256 (dobra se não couber) |
| Sombreamento | flat | automático |
| Olhos e boca | quads separados com atlas de expressões | fora do add-on |

A orientação de cada ilha segue a vista de fora da face: laterais em pé,
frente sem espelhamento, topo com a frente do personagem para baixo.

## Opções

- **Detectar tamanho do voxel**: lê o tamanho do voxel pela menor distância
  entre vértices. Desligue e informe *Voxel na importação* se o modelo tiver
  só blocos grandes (o OBJ do MagicaVoxel usa 1.0).
- **Girar**: gira em Z de 90° em 90° se a frente não estiver olhando para -Y.
- **Separar faces por cor**: só funde faces vizinhas da mesma cor. Gera mais
  quads, mas cada cor vira uma face inteira.
- **Espaço entre ilhas**: pixels livres entre ilhas (os personagens atuais
  usam 0; aumente se aparecer sangramento com mipmap).

## Limites

- Faces que não estão alinhadas aos eixos são ignoradas (com aviso).
- Não cria rig, pesos, nem os quads de olhos e boca.
- Não roda Unity; o FBX exportado ainda precisa ser conferido na importação
  do Unity (Point filter na textura, escala 1).
