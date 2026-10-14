"""Algoritmos do Voxel Tool.

Fluxo: malha vinda do MagicaVoxel -> células de voxel (uma por face unitária,
com a cor amostrada) -> greedy meshing em quads -> UV por face no grid de
pixels -> textura base pintada com as cores dos voxels.

O padrão foi inferido dos personagens em Assets/PEGA/Arts/3D Models/Characters
(Rick Ronda, Petra Patrol, Striped Robber):
- só quads, faces coplanares fundidas (T-junctions aceitas, como no MagicaVoxel);
- 1 voxel = 0.1 unidade do Blender, origem no centro da base (pés em Z=0),
  frente olhando para -Y;
- uma ilha de UV por face, alinhada ao grid de pixels, 8 px por voxel,
  textura 256x256 com filtro Point no Unity;
- sombreamento flat.
"""

import math

import bmesh
import bpy
from mathutils import Vector

# Direção "u" (eixo horizontal da imagem) para cada normal, vista de fora,
# para que a pintura fique em pé e sem espelhamento. Chave: (eixo, sinal).
# Valor: (eixo_u, sinal_u, eixo_v, sinal_v).
_FACE_FRAMES = {
    (1, -1): (0, 1, 2, 1),    # frente (-Y): u=+X, v=+Z
    (1, 1): (0, -1, 2, 1),    # costas (+Y): u=-X, v=+Z
    (0, 1): (1, 1, 2, 1),     # direita (+X): u=+Y, v=+Z
    (0, -1): (1, -1, 2, 1),   # esquerda (-X): u=-Y, v=+Z
    (2, 1): (0, 1, 1, 1),     # topo (+Z): u=+X, v=+Y
    (2, -1): (0, 1, 1, -1),   # base (-Z): u=+X, v=-Y
}


class VoxelError(Exception):
    pass


# ---------------------------------------------------------------------------
# Leitura de cor
# ---------------------------------------------------------------------------

class _ImageSampler:
    """Amostragem nearest de uma imagem do Blender (valores como gravados)."""

    def __init__(self, image):
        self.w, self.h = image.size
        if self.w == 0 or self.h == 0:
            raise VoxelError("Imagem '%s' vazia ou não encontrada." % image.name)
        self.px = list(image.pixels[:])
        self.ch = image.channels

    def sample(self, uv):
        x = min(self.w - 1, max(0, int(math.floor((uv[0] % 1.0) * self.w))))
        y = min(self.h - 1, max(0, int(math.floor((uv[1] % 1.0) * self.h))))
        i = (y * self.w + x) * self.ch
        if self.ch >= 4:
            return tuple(self.px[i:i + 4])
        if self.ch == 3:
            return tuple(self.px[i:i + 3]) + (1.0,)
        v = self.px[i]
        return (v, v, v, 1.0)


def _material_image(mat):
    if mat is None or not mat.use_nodes:
        return None
    for node in mat.node_tree.nodes:
        if node.type == 'TEX_IMAGE' and node.image is not None:
            return node.image
    return None


def _material_color(mat):
    if mat is None:
        return (1.0, 1.0, 1.0, 1.0)
    if mat.use_nodes:
        for node in mat.node_tree.nodes:
            if node.type == 'BSDF_PRINCIPLED':
                c = node.inputs['Base Color'].default_value
                return _linear_to_srgb(tuple(c))
    return _linear_to_srgb(tuple(mat.diffuse_color))


def _linear_to_srgb(c):
    def f(x):
        if x <= 0.0031308:
            return x * 12.92
        return 1.055 * (x ** (1.0 / 2.4)) - 0.055
    return (f(c[0]), f(c[1]), f(c[2]), c[3] if len(c) > 3 else 1.0)


# ---------------------------------------------------------------------------
# Malha -> células de voxel
# ---------------------------------------------------------------------------

def detect_voxel_unit(objects, matrices):
    """Menor passo entre coordenadas distintas dos vértices."""
    best = None
    for obj, mw in zip(objects, matrices):
        for axis in range(3):
            vals = sorted({round((mw @ v.co)[axis], 5) for v in obj.data.vertices})
            for a, b in zip(vals, vals[1:]):
                d = b - a
                if d > 1e-4 and (best is None or d < best):
                    best = d
    if best is None:
        raise VoxelError("Não foi possível detectar o tamanho do voxel.")
    return best


def grid_shift(objects, matrices, unit):
    """Fração de voxel para alinhar os vértices a coordenadas inteiras.

    O OBJ do MagicaVoxel centraliza o modelo, então dimensões ímpares geram
    vértices em meio voxel.
    """
    for obj, mw in zip(objects, matrices):
        if obj.data.vertices:
            c = (mw @ obj.data.vertices[0].co) / unit
            return Vector([x - round(x) for x in c])
    return Vector((0.0, 0.0, 0.0))


def _point_in_poly(pt, poly):
    x, y = pt
    inside = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if (y1 > y) != (y2 > y):
            xi = x1 + (y - y1) * (x2 - x1) / (y2 - y1)
            if x < xi:
                inside = not inside
    return inside


def _barycentric_2d(p, a, b, c):
    v0 = (b[0] - a[0], b[1] - a[1])
    v1 = (c[0] - a[0], c[1] - a[1])
    v2 = (p[0] - a[0], p[1] - a[1])
    den = v0[0] * v1[1] - v1[0] * v0[1]
    if abs(den) < 1e-12:
        return None
    v = (v2[0] * v1[1] - v1[0] * v2[1]) / den
    w = (v0[0] * v2[1] - v2[0] * v0[1]) / den
    return (1.0 - v - w, v, w)


def collect_cells(obj, matrix, unit, shift, report):
    """Retorna {(eixo, sinal, plano, i, j): cor_rgba} em coordenadas de voxel.

    i, j são os índices da célula nos dois outros eixos, em ordem crescente
    de eixo (ex.: normal Z -> i=X, j=Y).
    """
    mesh = obj.data
    mw = matrix
    uv_layer = mesh.uv_layers.active
    color_attr = mesh.color_attributes.active_color if hasattr(mesh, "color_attributes") else None
    samplers = {}
    mats = [s.material for s in obj.material_slots]

    def sampler_for(mat_index):
        if mat_index not in samplers:
            mat = mats[mat_index] if mat_index < len(mats) else None
            img = _material_image(mat)
            samplers[mat_index] = (_ImageSampler(img) if img is not None else None,
                                   _material_color(mat))
        return samplers[mat_index]

    corner_colors = None
    if color_attr is not None:
        if color_attr.domain == 'CORNER':
            corner_colors = [tuple(d.color_srgb) for d in color_attr.data]
        elif color_attr.domain == 'POINT':
            pc = [tuple(d.color_srgb) for d in color_attr.data]
            corner_colors = [pc[l.vertex_index] for l in mesh.loops]

    cells = {}
    skipped = 0
    tri_by_poly = {}
    mesh.calc_loop_triangles()
    for tri in mesh.loop_triangles:
        tri_by_poly.setdefault(tri.polygon_index, []).append(tri.loops)

    for poly in mesh.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        axis = max(range(3), key=lambda a: abs(n[a]))
        if abs(n[axis]) < 0.999:
            skipped += 1
            continue
        sign = 1 if n[axis] > 0 else -1
        ua, va = [a for a in range(3) if a != axis]
        loops = list(poly.loop_indices)
        co = {li: (mw @ mesh.vertices[mesh.loops[li].vertex_index].co) / unit - shift for li in loops}
        plane = round(sum(c[axis] for c in co.values()) / len(co))
        poly2d = [(co[li][ua], co[li][va]) for li in loops]
        umin = math.floor(min(p[0] for p in poly2d) + 1e-4)
        umax = math.ceil(max(p[0] for p in poly2d) - 1e-4)
        vmin = math.floor(min(p[1] for p in poly2d) + 1e-4)
        vmax = math.ceil(max(p[1] for p in poly2d) - 1e-4)
        sampler, flat = sampler_for(poly.material_index)
        for i in range(umin, umax):
            for j in range(vmin, vmax):
                pt = (i + 0.5, j + 0.5)
                if not _point_in_poly(pt, poly2d):
                    continue
                color = flat
                if (sampler is not None and uv_layer is not None) or corner_colors is not None:
                    for tl in tri_by_poly.get(poly.index, []):
                        bc = _barycentric_2d(pt, *[(co[l][ua], co[l][va]) for l in tl])
                        if bc is None or min(bc) < -1e-6:
                            continue
                        if sampler is not None and uv_layer is not None:
                            uv = [sum(bc[k] * uv_layer.data[tl[k]].uv[c] for k in range(3)) for c in range(2)]
                            color = sampler.sample(uv)
                        else:
                            color = tuple(sum(bc[k] * corner_colors[tl[k]][c] for k in range(3)) for c in range(4))
                        break
                key = (axis, sign, plane, i, j)
                opposite = (axis, -sign, plane, i, j)
                if opposite in cells:
                    # Faces internas coincidentes (dois voxels encostados) se anulam.
                    del cells[opposite]
                    continue
                cells[key] = color
    if skipped:
        report({'WARNING'}, "%s: %d faces não alinhadas aos eixos foram ignoradas." % (obj.name, skipped))
    return cells


# ---------------------------------------------------------------------------
# Greedy meshing
# ---------------------------------------------------------------------------

def _same_color(a, b, tol=1.0 / 512.0):
    return all(abs(x - y) <= tol for x, y in zip(a, b))


def greedy_rects(cells, split_by_color):
    """Agrupa células coplanares em retângulos.

    Retorna lista de dicts: axis, sign, plane, i0, j0, w, h, colors (h x w).
    """
    planes = {}
    for (axis, sign, plane, i, j), color in cells.items():
        planes.setdefault((axis, sign, plane), {})[(i, j)] = color

    rects = []
    for (axis, sign, plane), grid in sorted(planes.items()):
        used = set()
        for (i, j) in sorted(grid, key=lambda k: (k[1], k[0])):
            if (i, j) in used:
                continue
            base = grid[(i, j)]

            def ok(ci, cj):
                if (ci, cj) in used or (ci, cj) not in grid:
                    return False
                return not split_by_color or _same_color(grid[(ci, cj)], base)

            w = 1
            while ok(i + w, j):
                w += 1
            h = 1
            while all(ok(i + k, j + h) for k in range(w)):
                h += 1
            for dj in range(h):
                for di in range(w):
                    used.add((i + di, j + dj))
            colors = [[grid[(i + di, j + dj)] for di in range(w)] for dj in range(h)]
            rects.append(dict(axis=axis, sign=sign, plane=plane, i0=i, j0=j, w=w, h=h, colors=colors))
    return rects


# ---------------------------------------------------------------------------
# Empacotamento de UV no grid de pixels
# ---------------------------------------------------------------------------

def _island_size(rect, ppv):
    """Largura/altura da ilha em pixels, já na orientação de pintura."""
    ua, va = [a for a in range(3) if a != rect['axis']]
    fu, _, fv, _ = _FACE_FRAMES[(rect['axis'], rect['sign'])]
    size = {ua: rect['w'], va: rect['h']}
    return size[fu] * ppv, size[fv] * ppv


def pack_rects(sizes, tex_min, tex_max, padding):
    """Shelf packing determinístico. Retorna (tamanho_textura, [(x, y)]) em pixels,
    com y medido de baixo para cima como no UV."""
    order = sorted(range(len(sizes)), key=lambda k: (-sizes[k][1], -sizes[k][0], k))
    tex = tex_min
    while tex <= tex_max:
        pos = [None] * len(sizes)
        x = y = shelf_h = 0
        fits = True
        for k in order:
            w, h = sizes[k]
            if w > tex or h > tex:
                fits = False
                break
            if x + w > tex:
                x = 0
                y += shelf_h + padding
                shelf_h = 0
            if y + h > tex:
                fits = False
                break
            pos[k] = (x, y)
            x += w + padding
            shelf_h = max(shelf_h, h)
        if fits:
            # Preenche a partir do topo da imagem (UV tem origem embaixo).
            return tex, [(px, tex - py - sizes[k][1]) for k, (px, py) in enumerate(pos)]
        tex *= 2
    raise VoxelError("As ilhas não cabem numa textura de %dpx. Reduza os pixels por voxel." % tex_max)


# ---------------------------------------------------------------------------
# Construção da malha final
# ---------------------------------------------------------------------------

def _rect_corners(rect):
    """4 cantos (coordenadas de voxel) em ordem anti-horária vista de fora."""
    axis, sign, plane = rect['axis'], rect['sign'], rect['plane']
    ua, va = [a for a in range(3) if a != axis]
    i0, j0, w, h = rect['i0'], rect['j0'], rect['w'], rect['h']
    pts2 = [(i0, j0), (i0 + w, j0), (i0 + w, j0 + h), (i0, j0 + h)]
    corners = []
    for pu, pv in pts2:
        c = [0, 0, 0]
        c[axis] = plane
        c[ua] = pu
        c[va] = pv
        corners.append(tuple(c))
    # (ua x va) aponta para +axis quando (axis, ua, va) é cíclico.
    cyclic = (ua - axis) % 3 == 1
    if (sign > 0) != cyclic:
        corners.reverse()
    return corners


def _corner_uv(rect, corner, origin_px, tex, ppv):
    """UV de um canto (coordenadas de voxel) dentro da ilha do retângulo."""
    fu, su, fv, sv = _FACE_FRAMES[(rect['axis'], rect['sign'])]
    ua, va = [a for a in range(3) if a != rect['axis']]
    lo = {ua: rect['i0'], va: rect['j0']}
    span = {ua: rect['w'], va: rect['h']}
    du = corner[fu] - lo[fu]
    dv = corner[fv] - lo[fv]
    if su < 0:
        du = span[fu] - du
    if sv < 0:
        dv = span[fv] - dv
    return ((origin_px[0] + du * ppv) / tex, (origin_px[1] + dv * ppv) / tex)


def _cell_pixel_origin(rect, di, dj, origin_px, ppv):
    """Canto inferior esquerdo (px) da célula (di, dj) do retângulo na ilha."""
    fu, su, fv, sv = _FACE_FRAMES[(rect['axis'], rect['sign'])]
    ua, va = [a for a in range(3) if a != rect['axis']]
    local = {ua: di, va: dj}
    span = {ua: rect['w'], va: rect['h']}
    cu = local[fu] if su > 0 else span[fu] - 1 - local[fu]
    cv = local[fv] if sv > 0 else span[fv] - 1 - local[fv]
    return origin_px[0] + cu * ppv, origin_px[1] + cv * ppv


def build_mesh(name, rects, uv_pos, tex, ppv, voxel_size, offset):
    """Cria a malha final. offset: deslocamento em voxels (origem do personagem)."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    verts = {}

    def vert(c):
        if c not in verts:
            verts[c] = bm.verts.new(Vector(((c[0] - offset[0]) * voxel_size,
                                            (c[1] - offset[1]) * voxel_size,
                                            (c[2] - offset[2]) * voxel_size)))
        return verts[c]

    for rect, origin_px in zip(rects, uv_pos):
        corners = _rect_corners(rect)
        face = bm.faces.new([vert(c) for c in corners])
        face.smooth = False
        for loop, c in zip(face.loops, corners):
            loop[uvl].uv = _corner_uv(rect, c, origin_px, tex, ppv)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def paint_texture(image, rects, uv_pos, tex, ppv, background):
    """Pinta cada célula de voxel como um bloco ppv x ppv da sua cor."""
    px = [0.0] * (tex * tex * 4)
    if background is not None:
        for k in range(tex * tex):
            px[k * 4:k * 4 + 4] = background
    for rect, origin_px in zip(rects, uv_pos):
        for dj in range(rect['h']):
            for di in range(rect['w']):
                color = rect['colors'][dj][di]
                x0, y0 = _cell_pixel_origin(rect, di, dj, origin_px, ppv)
                rgba = list(color[:3]) + [1.0]
                for y in range(y0, y0 + ppv):
                    row = (y * tex + x0) * 4
                    for x in range(ppv):
                        px[row + x * 4:row + x * 4 + 4] = rgba
    image.pixels[:] = px


def paint_guide(image, rects, uv_pos, tex, ppv):
    """Camada transparente com o contorno de cada ilha e grade de voxels."""
    px = [0.0] * (tex * tex * 4)

    def put(x, y, rgba):
        if 0 <= x < tex and 0 <= y < tex:
            px[(y * tex + x) * 4:(y * tex + x) * 4 + 4] = rgba

    grid = [0.0, 0.0, 0.0, 0.25]
    edge = [1.0, 0.0, 1.0, 1.0]
    for rect, (ox, oy) in zip(rects, uv_pos):
        w, h = _island_size(rect, ppv)
        for x in range(ox, ox + w, ppv):
            for y in range(oy, oy + h):
                put(x, y, grid)
        for y in range(oy, oy + h, ppv):
            for x in range(ox, ox + w):
                put(x, y, grid)
        for x in range(ox, ox + w):
            put(x, oy, edge)
            put(x, oy + h - 1, edge)
        for y in range(oy, oy + h):
            put(ox, y, edge)
            put(ox + w - 1, y, edge)
    image.pixels[:] = px


def make_material(name, image):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    out.location = (300, 0)
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.location = (-350, 0)
    tex.image = image
    tex.interpolation = 'Closest'
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    return mat
