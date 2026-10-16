import os
import math
import bpy
import bmesh
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(bpy.data.filepath or __file__), "..", "..", ".."))
MODEL_FOLDER = os.path.join(ROOT, "Assets", "Models", "CyclicShelves")
SHUTTER_FBX_PATH = os.path.join(MODEL_FOLDER, "LockerShutter.fbx")
BOARD_FBX_PATH = os.path.join(MODEL_FOLDER, "DepartureBoard.fbx")
BLEND_PATH = os.path.join(ROOT, "Art", "Blender", "Lockers", "Lockers.blend")

CURTAIN_WIDTH = 0.96
CURTAIN_HEIGHT = 0.38
SLAT_HEIGHT = 0.035
WINDOW_TOP = -0.07
WINDOW_BOTTOM = -0.24
BAR_TOP = -0.345
FRAME_WIDTH = 0.012
MULLION_X = 0.155
MULLION_WIDTH = 0.014
SLOT_X = (1.15, 0.0, -1.15)
LAMP_X = 0.25
PANEL_WIDTH = 3.4
PANEL_HEIGHT = 0.22
WINDOW_WIDTH = 0.292
WINDOW_HEIGHT = 0.18
BEZEL_WIDTH = 0.01
LAMP_RADIUS = 0.029


def unity(x, y, z):
    return Vector((-x, -z, y))


def add_box(bm, center, size, bevel=0.0, material=0):
    before = set(bm.faces)
    sx, sy, sz = size
    verts = bmesh.ops.create_cube(bm, size=1.0)["verts"]
    bmesh.ops.scale(bm, vec=Vector((sx, sz, sy)), verts=verts)
    bmesh.ops.translate(bm, vec=unity(*center), verts=verts)

    if bevel > 0.0:
        edges = list({edge for vert in verts for edge in vert.link_edges})
        bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=2, profile=0.5, affect='EDGES')

    for face in set(bm.faces) - before:
        face.material_index = material
        face.smooth = False


def add_cylinder_x(bm, center, radius, length, segments=12, material=0):
    result = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=length)
    verts = result["verts"]
    bmesh.ops.rotate(bm, verts=verts, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.radians(90), 3, 'Y'))
    bmesh.ops.translate(bm, vec=unity(*center), verts=verts)
    mark_smooth(verts, material)


def add_cylinder_z(bm, center, radius, length, segments=16, material=0):
    result = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=radius, radius2=radius, depth=length)
    verts = result["verts"]
    bmesh.ops.rotate(bm, verts=verts, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    bmesh.ops.translate(bm, vec=unity(*center), verts=verts)
    mark_smooth(verts, material)


def add_dome(bm, center, radius, material=0):
    result = bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=10, radius=radius)
    verts = result["verts"]
    hidden = [vert for vert in verts if vert.co.z < -1e-6]
    bmesh.ops.delete(bm, geom=hidden, context='VERTS')
    verts = [vert for vert in verts if vert.is_valid]
    bmesh.ops.scale(bm, vec=Vector((1, 1, 0.55)), verts=verts)
    bmesh.ops.rotate(bm, verts=verts, cent=Vector((0, 0, 0)), matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    bmesh.ops.translate(bm, vec=unity(*center), verts=verts)
    mark_smooth(verts, material)


def add_ring_z(bm, center, outer_radius, inner_radius, depth, segments=24, material=0):
    before = set(bm.faces)
    cx, cy, cz = center
    layers = []

    for z in (cz, cz + depth):
        layer = []

        for radius in (outer_radius, inner_radius):
            layer.append([bm.verts.new(unity(cx + radius * math.cos(2 * math.pi * i / segments), cy + radius * math.sin(2 * math.pi * i / segments), z)) for i in range(segments)])

        layers.append(layer)

    (outer_back, inner_back), (outer_front, inner_front) = layers

    for i in range(segments):
        j = (i + 1) % segments
        bm.faces.new((outer_front[i], outer_front[j], inner_front[j], inner_front[i]))
        bm.faces.new((outer_back[j], outer_back[i], inner_back[i], inner_back[j]))
        bm.faces.new((outer_back[i], outer_back[j], outer_front[j], outer_front[i]))
        bm.faces.new((inner_back[j], inner_back[i], inner_front[i], inner_front[j]))

    faces = list(set(bm.faces) - before)
    bmesh.ops.recalc_face_normals(bm, faces=faces)

    for face in faces:
        face.material_index = material
        face.smooth = True


def mark_smooth(verts, material):
    for face in {face for vert in verts if vert.is_valid for face in vert.link_faces}:
        face.material_index = material
        face.smooth = True


def add_profile_strip(bm, profile, half_width, material=0):
    left = [bm.verts.new(unity(-half_width, y, z)) for y, z in profile]
    right = [bm.verts.new(unity(half_width, y, z)) for y, z in profile]

    for index in range(len(profile) - 1):
        face = bm.faces.new((left[index], right[index], right[index + 1], left[index + 1]))
        face_toward_camera(face, material)
        face.smooth = True


def slat_profile(top, count):
    points = []

    for slat in range(count):
        y = top - slat * SLAT_HEIGHT
        points += [
            (y, -0.001),
            (y - 0.004, 0.002),
            (y - 0.0175, 0.0055),
            (y - 0.031, 0.002),
        ]

    points.append((top - count * SLAT_HEIGHT, -0.001))
    return points


def add_quad(bm, x_range, y_range, z, material=0):
    x0, x1 = x_range
    y0, y1 = y_range
    face = bm.faces.new([bm.verts.new(unity(x, y, z)) for x, y in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))])
    face_toward_camera(face, material)


def face_toward_camera(face, material):
    face.normal_update()

    if face.normal.y > 0.0:
        face.normal_flip()

    face.material_index = material


def build_object(name, fill, materials):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    fill(bm)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    for material_name in materials:
        material = bpy.data.materials.get(material_name) or bpy.data.materials.new(material_name)
        mesh.materials.append(material)

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def fill_housing(bm):
    add_box(bm, (0, 0.285, 0.06), (1.02, 0.07, 0.1), bevel=0.008)
    add_box(bm, (0, 0.2535, 0.08), (0.99, 0.006, 0.05), bevel=0.0015)
    add_box(bm, (0, 0.285, 0.111), (0.98, 0.012, 0.003), bevel=0.001)

    for side in (-1, 1):
        add_box(bm, (side * 0.516, 0.285, 0.06), (0.014, 0.082, 0.108), bevel=0.004)
        add_cylinder_z(bm, (side * 0.516, 0.285, 0.115), 0.012, 0.004, segments=16)
        add_box(bm, (side * 0.4875, 0.06, 0.112), (0.034, 0.38, 0.006), bevel=0.0015)
        add_box(bm, (side * 0.501, 0.06, 0.095), (0.006, 0.38, 0.03), bevel=0.001)


def fill_curtain(bm):
    half = CURTAIN_WIDTH / 2
    add_profile_strip(bm, slat_profile(0.0, 2), half)
    add_profile_strip(bm, slat_profile(WINDOW_BOTTOM, 3), half)
    add_box(bm, (0, WINDOW_TOP - FRAME_WIDTH / 2, 0.004), (CURTAIN_WIDTH, FRAME_WIDTH, 0.008), bevel=0.0015)
    add_box(bm, (0, WINDOW_BOTTOM + FRAME_WIDTH / 2, 0.004), (CURTAIN_WIDTH, FRAME_WIDTH, 0.008), bevel=0.0015)
    window_center = (WINDOW_TOP + WINDOW_BOTTOM) / 2
    window_height = WINDOW_TOP - WINDOW_BOTTOM

    for x in (-half + 0.01, -MULLION_X, MULLION_X, half - 0.01):
        width = 0.02 if abs(x) > 0.3 else MULLION_WIDTH
        add_box(bm, (x, window_center, 0.004), (width, window_height, 0.008), bevel=0.0015)

    bar_height = CURTAIN_HEIGHT + BAR_TOP
    add_box(bm, (0, BAR_TOP - bar_height / 2, 0.005), (CURTAIN_WIDTH, bar_height, 0.014), bevel=0.003)
    add_box(bm, (0, BAR_TOP - bar_height / 2, 0.016), (0.13, 0.014, 0.012), bevel=0.004, material=1)

    for side in (-1, 1):
        add_box(bm, (side * 0.072, BAR_TOP - bar_height / 2, 0.011), (0.016, 0.02, 0.006), bevel=0.002, material=1)


def fill_glass(bm):
    add_quad(bm, (-CURTAIN_WIDTH / 2 + 0.015, CURTAIN_WIDTH / 2 - 0.015), (WINDOW_BOTTOM + FRAME_WIDTH, WINDOW_TOP - FRAME_WIDTH), 0.001)


def fill_board_panel(bm):
    add_box(bm, (0, 0, 0), (PANEL_WIDTH, PANEL_HEIGHT, 0.04), bevel=0.01)

    for side in (-1, 1):
        for y in (-0.075, 0.075):
            add_cylinder_z(bm, (side * (PANEL_WIDTH / 2 - 0.04), y, 0.021), 0.009, 0.004, segments=12, material=1)

    bezel_x = (WINDOW_WIDTH + BEZEL_WIDTH) / 2
    bezel_y = (WINDOW_HEIGHT + BEZEL_WIDTH) / 2

    for slot_x in SLOT_X:
        add_box(bm, (slot_x, bezel_y, 0.025), (WINDOW_WIDTH + BEZEL_WIDTH * 2, BEZEL_WIDTH, 0.012), bevel=0.002, material=1)
        add_box(bm, (slot_x, -bezel_y, 0.025), (WINDOW_WIDTH + BEZEL_WIDTH * 2, BEZEL_WIDTH, 0.012), bevel=0.002, material=1)
        add_box(bm, (slot_x - bezel_x, 0, 0.025), (BEZEL_WIDTH, WINDOW_HEIGHT, 0.012), bevel=0.002, material=1)
        add_box(bm, (slot_x + bezel_x, 0, 0.025), (BEZEL_WIDTH, WINDOW_HEIGHT, 0.012), bevel=0.002, material=1)
        add_box(bm, (slot_x, 0, 0.0205), (WINDOW_WIDTH, WINDOW_HEIGHT, 0.001), material=2)
        add_cylinder_x(bm, (slot_x, 0, 0.0285), 0.0025, WINDOW_WIDTH, segments=8, material=1)
        add_ring_z(bm, (slot_x + LAMP_X, 0, 0.02), LAMP_RADIUS + 0.009, LAMP_RADIUS, 0.007, material=1)


def fill_lamp(bm):
    add_dome(bm, (0, 0, 0), LAMP_RADIUS)


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)

    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)


def export(objects, path):
    bpy.ops.object.select_all(action='DESELECT')

    for obj in objects:
        obj.select_set(True)

    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'MESH'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False)


def build():
    clear_scene()
    os.makedirs(MODEL_FOLDER, exist_ok=True)
    housing = build_object("ShutterHousing", fill_housing, ["ShutterHousing"])
    curtain = build_object("ShutterCurtain", fill_curtain, ["ShutterCurtain", "ShutterHandle"])
    glass = build_object("ShutterGlass", fill_glass, ["ShutterGlass"])
    panel = build_object("DepartureBoardPanel", fill_board_panel, ["DepartureBoard", "DepartureBezel", "DepartureWindow"])
    lamp = build_object("DepartureLamp", fill_lamp, ["DepartureLamp"])
    export([housing, curtain, glass], SHUTTER_FBX_PATH)
    export([panel, lamp], BOARD_FBX_PATH)

    for obj in (housing, curtain, glass, panel, lamp):
        print(f"{obj.name}: {sum(len(polygon.vertices) - 2 for polygon in obj.data.polygons)} triangles")

    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)


build()
