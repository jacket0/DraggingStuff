import os
import bpy

SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(SCRIPT_DIRECTORY, "..", "..", ".."))
FBX_DIRECTORY = os.path.join(ROOT, "Assets", "Models", "Slime")
BLEND_PATH = os.path.join(SCRIPT_DIRECTORY, "Slime.blend")
PREVIEW_SPACING = 0.45

PIECES = {
    "SlimePuddle": {
        "voxel": 0.003,
        "triangles": 1200,
        "lobes": (
            ((0.0, 0.0, 0.006), (0.15, 0.12, 0.012)),
            ((-0.06, 0.03, 0.008), (0.08, 0.07, 0.016)),
            ((0.07, -0.03, 0.01), (0.075, 0.06, 0.018)),
            ((0.0, 0.0, 0.014), (0.09, 0.07, 0.014)),
            ((-0.15, -0.04, 0.004), (0.035, 0.025, 0.008)),
            ((0.16, 0.05, 0.004), (0.03, 0.022, 0.008)),
            ((0.02, 0.11, 0.004), (0.04, 0.025, 0.008)),
            ((-0.04, -0.12, 0.004), (0.035, 0.025, 0.008)),
                    ((-0.07, 0.115, 0.016), (0.055, 0.03, 0.026)),
            ((0.01, 0.125, 0.02), (0.07, 0.03, 0.034)),
            ((0.08, 0.11, 0.016), (0.05, 0.03, 0.026)),
        ),
    },
    "SlimePuddleWall": {
        "voxel": 0.003,
        "triangles": 1800,
        "lobes": (
            ((0.0, 0.0, 0.006), (0.15, 0.12, 0.012)),
            ((-0.06, 0.03, 0.008), (0.08, 0.07, 0.016)),
            ((0.07, -0.03, 0.01), (0.075, 0.06, 0.018)),
            ((0.0, 0.0, 0.014), (0.09, 0.07, 0.014)),
            ((0.16, 0.05, 0.004), (0.03, 0.022, 0.008)),
            ((-0.04, -0.12, 0.004), (0.035, 0.025, 0.008)),
            ((-0.13, 0.0, 0.03), (0.04, 0.11, 0.03)),
            ((-0.148, 0.0, 0.07), (0.011, 0.11, 0.07)),
            ((-0.147, 0.03, 0.11), (0.009, 0.07, 0.04)),
            ((-0.147, 0.06, 0.07), (0.009, 0.02, 0.07)),
            ((-0.146, 0.06, 0.022), (0.014, 0.016, 0.016)),
            ((-0.147, -0.07, 0.08), (0.009, 0.014, 0.05)),
            ((-0.146, -0.07, 0.03), (0.011, 0.012, 0.012)),
                    ((-0.07, 0.115, 0.016), (0.055, 0.03, 0.026)),
            ((0.01, 0.125, 0.02), (0.07, 0.03, 0.034)),
            ((0.08, 0.11, 0.016), (0.05, 0.03, 0.026)),
        ),
    },
    "SlimeBridge": {
        "voxel": 0.003,
        "triangles": 700,
        "lobes": (
            ((0.0, 0.0, 0.006), (0.075, 0.1, 0.012)),
            ((0.0, 0.0, 0.012), (0.05, 0.07, 0.013)),
            ((0.0, 0.1, 0.004), (0.03, 0.02, 0.008)),
        ),
    },
    "SlimeForecastPuddle": {
        "voxel": 0.0025,
        "triangles": 500,
        "lobes": (
            ((0.0, 0.0, 0.004), (0.04, 0.032, 0.008)),
            ((0.03, 0.01, 0.003), (0.018, 0.014, 0.006)),
            ((-0.035, -0.012, 0.003), (0.014, 0.011, 0.005)),
        ),
    },
}


def clear_meshes():
    for obj in list(bpy.data.objects):
        if obj.type == 'MESH':
            bpy.data.objects.remove(obj, do_unlink=True)


def add_ellipsoid(name, location, scale):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, radius=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    return obj


def triangle_count(obj):
    return sum(len(polygon.vertices) - 2 for polygon in obj.data.polygons)


def build_piece(name, settings):
    parts = [add_ellipsoid(name + "_Lobe", location, scale) for location, scale in settings["lobes"]]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.join()
    piece = bpy.context.active_object
    piece.name = name

    remesh = piece.modifiers.new("Remesh", 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = settings["voxel"]
    remesh.adaptivity = 0.0
    bpy.ops.object.modifier_apply(modifier="Remesh")

    smooth = piece.modifiers.new("Smooth", 'SMOOTH')
    smooth.factor = 0.8
    smooth.iterations = 6
    bpy.ops.object.modifier_apply(modifier="Smooth")

    count = triangle_count(piece)
    if count > settings["triangles"]:
        decimate = piece.modifiers.new("Decimate", 'DECIMATE')
        decimate.ratio = settings["triangles"] / count
        bpy.ops.object.modifier_apply(modifier="Decimate")

    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

    for vertex in piece.data.vertices:
        if vertex.co.z < 0.0:
            vertex.co.z = 0.0

    bpy.ops.object.shade_smooth()
    material = bpy.data.materials.get("SlimeJelly") or bpy.data.materials.new("SlimeJelly")
    material.diffuse_color = (0.55, 0.7, 0.2, 0.9)
    piece.data.materials.clear()
    piece.data.materials.append(material)
    return piece


def export(piece):
    os.makedirs(FBX_DIRECTORY, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    piece.select_set(True)
    bpy.context.view_layer.objects.active = piece
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(FBX_DIRECTORY, piece.name + ".fbx"),
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


clear_meshes()
report = []
for index, (name, settings) in enumerate(PIECES.items()):
    piece = build_piece(name, settings)
    export(piece)
    report.append((name, triangle_count(piece), tuple(round(value, 3) for value in piece.dimensions)))
    piece.location.x = index * PREVIEW_SPACING

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
for line in report:
    print(line)
