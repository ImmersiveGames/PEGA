"""Voxel Tool (Immersive Games): malhas do MagicaVoxel prontas para pintar e exportar.

Painel: View3D > Sidebar (N) > Voxel Tool.
"""

bl_info = {
    "name": "Voxel Tool",
    "author": "Immersive Games",
    "version": (0, 2, 1),
    "blender": (4, 0, 0),
    "location": "View3D > Sidebar > Voxel Tool",
    "description": "Otimiza malhas do MagicaVoxel, abre UV no grid de pixels e gera a textura base para pintura",
    "category": "Mesh",
}

import math
import os

import bpy
from bpy.props import BoolProperty, EnumProperty, FloatProperty, IntProperty, StringProperty
from mathutils import Matrix

from . import core


class VOXELTOOL_Settings(bpy.types.PropertyGroup):
    base_name: StringProperty(
        name="Nome",
        description="Prefixo dos objetos, material e texturas gerados (vazio = nome do objeto ativo)",
        default="",
    )
    auto_unit: BoolProperty(
        name="Detectar tamanho do voxel",
        description="Detecta o tamanho do voxel na malha importada pelo menor passo entre vértices",
        default=True,
    )
    source_unit: FloatProperty(
        name="Voxel na importação",
        description="Tamanho de 1 voxel na malha importada (OBJ do MagicaVoxel = 1.0)",
        default=1.0, min=1e-5, precision=4,
    )
    voxel_size: FloatProperty(
        name="Voxel final",
        description="Tamanho de 1 voxel no resultado. Rick, Petra e Striped Robber usam 0.1",
        default=0.1, min=1e-4, precision=3,
    )
    rotate_z: EnumProperty(
        name="Girar",
        description="Rotação em Z para a frente do personagem olhar para -Y (padrão do Blender e dos FBX do PEGA)",
        items=[('0', "0°", ""), ('90', "90°", ""), ('180', "180°", ""), ('270', "270°", "")],
        default='0',
    )
    origin: EnumProperty(
        name="Origem",
        items=[
            ('BASE', "Centro da base", "Centro em X/Y, pés em Z=0 (como nos personagens do PEGA)"),
            ('KEEP', "Manter", "Mantém a posição do voxel importado"),
        ],
        default='BASE',
    )
    split_by_color: BoolProperty(
        name="Separar faces por cor",
        description="Só funde faces vizinhas da mesma cor (mais polígonos). Desligado, a cor fica só na textura",
        default=False,
    )
    pixels_per_voxel: IntProperty(
        name="Pixels por voxel",
        description="Densidade de textura. Os personagens do PEGA usam 8 px por voxel",
        default=8, min=1, max=64,
    )
    texture_size: EnumProperty(
        name="Textura mínima",
        description="Tamanho inicial da textura; dobra automaticamente se as ilhas não couberem",
        items=[(str(s), "%d px" % s, "") for s in (64, 128, 256, 512, 1024, 2048)],
        default='256',
    )
    padding: IntProperty(
        name="Espaço entre ilhas",
        description="Pixels livres entre ilhas de UV",
        default=0, min=0, max=16,
    )
    make_guide: BoolProperty(
        name="Gerar guia de UV",
        description="Gera também uma imagem transparente com contorno das ilhas e grade de voxels, para usar como camada no editor de imagem",
        default=True,
    )
    texture_dir: StringProperty(
        name="Pasta das texturas",
        description="Onde salvar os PNG (// = pasta do .blend)",
        default="//Textures/",
        subtype='DIR_PATH',
    )
    overwrite: BoolProperty(
        name="Sobrescrever textura",
        description="Permite substituir um PNG já existente com o mesmo nome (cuidado: apaga a pintura)",
        default=False,
    )
    hide_source: BoolProperty(
        name="Esconder originais",
        description="Esconde os objetos importados depois de gerar o resultado",
        default=True,
    )


def _settings(context):
    return context.scene.voxel_tool


def _texture_path(directory, name):
    if directory.startswith("//") and not bpy.data.filepath:
        return None
    return os.path.join(bpy.path.abspath(directory), name + ".png")


def _save_image(image, directory, report):
    path = _texture_path(directory, image.name)
    if path is None:
        report({'WARNING'}, "Salve o .blend para gravar '%s' em disco; a imagem ficou só na cena." % image.name)
        image.pack()
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.filepath_raw = path
    image.file_format = 'PNG'
    image.save()


class VOXELTOOL_OT_convert(bpy.types.Operator):
    bl_idname = "voxel_tool.convert"
    bl_label = "Converter voxel"
    bl_description = ("Otimiza as malhas selecionadas (quads, faces fundidas), aplica escala/origem, "
                      "abre UV no grid de pixels e gera a textura base com as cores dos voxels")
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return any(o.type == 'MESH' for o in context.selected_objects)

    def execute(self, context):
        s = _settings(context)
        if context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
        objects = sorted((o for o in context.selected_objects if o.type == 'MESH'), key=lambda o: o.name)
        active = context.active_object if context.active_object in objects else objects[0]
        base = s.base_name.strip() or active.name

        rot = Matrix.Rotation(math.radians(float(s.rotate_z)), 4, 'Z')
        matrices = [rot @ o.matrix_world for o in objects]
        try:
            unit = core.detect_voxel_unit(objects, matrices) if s.auto_unit else s.source_unit
            shift = core.grid_shift(objects, matrices, unit)
            per_object = []
            for obj, mw in zip(objects, matrices):
                cells = core.collect_cells(obj, mw, unit, shift, self.report)
                if not cells:
                    self.report({'WARNING'}, "%s: nenhuma face de voxel encontrada." % obj.name)
                    continue
                per_object.append((obj, core.greedy_rects(cells, s.split_by_color)))
            if not per_object:
                self.report({'ERROR'}, "Nada para converter.")
                return {'CANCELLED'}

            offset = [0.0, 0.0, 0.0]
            if s.origin == 'BASE':
                lo = [math.inf] * 3
                hi = [-math.inf] * 3
                for _, rects in per_object:
                    for r in rects:
                        for c in core._rect_corners(r):
                            for a in range(3):
                                lo[a] = min(lo[a], c[a])
                                hi[a] = max(hi[a], c[a])
                offset = [(lo[0] + hi[0]) / 2.0, (lo[1] + hi[1]) / 2.0, lo[2]]

            ppv = s.pixels_per_voxel
            all_rects = [r for _, rects in per_object for r in rects]
            sizes = [core._island_size(r, ppv) for r in all_rects]
            tex, positions = core.pack_rects(sizes, int(s.texture_size), 8192, s.padding)
        except core.VoxelError as exc:
            self.report({'ERROR'}, str(exc))
            return {'CANCELLED'}

        tex_name = base + "_Main_Texture"
        existing = _texture_path(s.texture_dir, tex_name)
        if existing and os.path.exists(existing) and not s.overwrite:
            self.report({'ERROR'}, "Já existe %s. Ligue 'Sobrescrever textura' ou mude o nome para não perder a pintura." % existing)
            return {'CANCELLED'}
        image = bpy.data.images.get(tex_name)
        if image is not None and tuple(image.size) != (tex, tex):
            bpy.data.images.remove(image)
            image = None
        if image is None:
            image = bpy.data.images.new(tex_name, tex, tex, alpha=True)
        core.paint_texture(image, all_rects, positions, tex, ppv, background=None)
        _save_image(image, s.texture_dir, self.report)

        if s.make_guide:
            guide_name = base + "_UV_Guide"
            guide = bpy.data.images.get(guide_name)
            if guide is not None and tuple(guide.size) != (tex, tex):
                bpy.data.images.remove(guide)
                guide = None
            if guide is None:
                guide = bpy.data.images.new(guide_name, tex, tex, alpha=True)
            core.paint_guide(guide, all_rects, positions, tex, ppv)
            _save_image(guide, s.texture_dir, self.report)

        mat = core.make_material(base + "_Main_Mat", image)

        # Resultado numa collection própria, separada dos objetos importados.
        out_col = bpy.data.collections.get(base + "_Voxel")
        if out_col is None:
            out_col = bpy.data.collections.new(base + "_Voxel")
        if out_col.name not in context.scene.collection.children:
            context.scene.collection.children.link(out_col)

        new_objects = []
        k = 0
        unit_scale = s.voxel_size
        for obj, rects in per_object:
            pos = positions[k:k + len(rects)]
            k += len(rects)
            name = obj.name + "_Voxel"
            old = bpy.data.objects.get(name)
            if old is not None:
                old_mesh = old.data
                bpy.data.objects.remove(old)
                if old_mesh.users == 0:
                    bpy.data.meshes.remove(old_mesh)
            mesh = core.build_mesh(name, rects, pos, tex, ppv, unit_scale, offset)
            mesh.materials.append(mat)
            new_obj = bpy.data.objects.new(name, mesh)
            out_col.objects.link(new_obj)
            new_objects.append(new_obj)
            if s.hide_source:
                obj.hide_set(True)

        for o in context.selected_objects:
            o.select_set(False)
        for o in new_objects:
            o.select_set(True)
        context.view_layer.objects.active = new_objects[0]

        faces = sum(len(o.data.polygons) for o in new_objects)
        self.report({'INFO'}, "%d objeto(s), %d quads, textura %dx%d (%s)." % (
            len(new_objects), faces, tex, tex, tex_name))
        return {'FINISHED'}


class VOXELTOOL_OT_export_fbx(bpy.types.Operator):
    bl_idname = "voxel_tool.export_fbx"
    bl_label = "Exportar FBX para o Unity"
    bl_description = ("Exporta a seleção como FBX com transformações aplicadas (escala 1, sem rotação no Unity), "
                      "só deform bones, sem animação, sombreamento flat e sem leaf bones")
    bl_options = {'REGISTER'}

    filepath: StringProperty(subtype='FILE_PATH')
    filter_glob: StringProperty(default="*.fbx", options={'HIDDEN'})

    @classmethod
    def poll(cls, context):
        return bool(context.selected_objects)

    def invoke(self, context, event):
        if not self.filepath:
            name = (_settings(context).base_name.strip() or context.active_object.name) + ".fbx"
            self.filepath = os.path.join(os.path.dirname(bpy.data.filepath) or os.path.expanduser("~"), name)
        context.window_manager.fileselect_add(self)
        return {'RUNNING_MODAL'}

    def execute(self, context):
        if context.mode != 'OBJECT':
            bpy.ops.object.mode_set(mode='OBJECT')
        selected = set(context.selected_objects)
        # Malhas soltas já saem certas com bake_space_transform; só o Armature precisa do truque.
        roots = [o for o in selected if o.type == 'ARMATURE' and o.parent not in selected]
        state = _rotate_for_unity(roots)
        try:
            bpy.ops.export_scene.fbx(
                filepath=bpy.path.ensure_ext(self.filepath, ".fbx"),
                use_selection=True,
                object_types={'ARMATURE', 'MESH'},
                apply_scale_options='FBX_SCALE_ALL',
                axis_forward='-Z',
                axis_up='Y',
                bake_space_transform=True,
                mesh_smooth_type='FACE',
                use_mesh_modifiers=True,
                add_leaf_bones=False,
                use_armature_deform_only=True,
                bake_anim=False,
                path_mode='AUTO',
            )
        finally:
            _restore_rotation(state)
        self.report({'INFO'}, "FBX exportado: %s" % self.filepath)
        return {'FINISHED'}


# O export do Blender converte Z-up para Y-up com -90 em X no Armature.
# O mesmo truque manual (girar -90 em X, aplicar, voltar para +90 sem aplicar)
# é feito aqui só durante o export e desfeito em seguida: no Unity o objeto
# chega em pé com rotação 0 e escala 1, sem precisar de Bake Axis Conversion.
_TO_UNITY = Matrix.Rotation(math.radians(-90.0), 4, 'X')


def _rotate_for_unity(roots):
    state = []
    rotated_data = set()
    for obj in roots:
        children = [c for c in obj.children if c.parent_type == 'OBJECT']
        state.append((obj, obj.matrix_basis.copy(), [(c, c.matrix_parent_inverse.copy()) for c in children]))
        if obj.data not in rotated_data:
            obj.data.transform(_TO_UNITY)
            rotated_data.add(obj.data)
        obj.matrix_basis = obj.matrix_basis @ _TO_UNITY.inverted()
        for c in children:
            c.matrix_parent_inverse = _TO_UNITY @ c.matrix_parent_inverse
    bpy.context.view_layer.update()
    return (state, rotated_data)


def _restore_rotation(saved):
    state, rotated_data = saved
    back = _TO_UNITY.inverted()
    for data in rotated_data:
        data.transform(back)
    for obj, basis, children in state:
        obj.matrix_basis = basis
        for c, inv in children:
            c.matrix_parent_inverse = inv
    bpy.context.view_layer.update()


class VOXELTOOL_PT_main(bpy.types.Panel):
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "Voxel Tool"
    bl_label = "Voxel Tool"

    def draw(self, context):
        s = _settings(context)
        layout = self.layout
        layout.prop(s, "base_name")

        box = layout.box()
        box.label(text="Malha", icon='MESH_CUBE')
        box.prop(s, "auto_unit")
        row = box.row()
        row.enabled = not s.auto_unit
        row.prop(s, "source_unit")
        box.prop(s, "voxel_size")
        box.prop(s, "rotate_z", expand=True)
        box.prop(s, "origin")
        box.prop(s, "split_by_color")

        box = layout.box()
        box.label(text="UV e textura", icon='TEXTURE')
        box.prop(s, "pixels_per_voxel")
        box.prop(s, "texture_size")
        box.prop(s, "padding")
        box.prop(s, "make_guide")
        box.prop(s, "texture_dir")

        layout.prop(s, "overwrite")
        layout.prop(s, "hide_source")
        layout.operator(VOXELTOOL_OT_convert.bl_idname, icon='MOD_REMESH')
        layout.separator()
        layout.operator(VOXELTOOL_OT_export_fbx.bl_idname, icon='EXPORT')


_classes = (VOXELTOOL_Settings, VOXELTOOL_OT_convert, VOXELTOOL_OT_export_fbx, VOXELTOOL_PT_main)


def register():
    for cls in _classes:
        bpy.utils.register_class(cls)
    bpy.types.Scene.voxel_tool = bpy.props.PointerProperty(type=VOXELTOOL_Settings)


def unregister():
    del bpy.types.Scene.voxel_tool
    for cls in reversed(_classes):
        bpy.utils.unregister_class(cls)
