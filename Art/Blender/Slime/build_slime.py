import os
import bpy
from math import radians

SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(SCRIPT_DIRECTORY, "..", "..", ".."))
FBX_PATH = os.path.join(ROOT, "Assets", "Models", "Slime", "SlimeBlob.fbx")
BLEND_PATH = os.path.join(SCRIPT_DIRECTORY, "Slime.blend")
VOXEL_SIZE = 0.0045
TARGET_TRIANGLES = 1800

LOBES = (
    ("Puddle", (0.0, 0.0, 0.012), (0.145, 0.118, 0.016)),
    ("Dome", (-0.01, 0.0, 0.035), (0.095, 0.082, 0.07)),
    ("LobeLeft", (-0.085, 0.03, 0.016), (0.05, 0.04, 0.022)),
    ("LobeRight", (0.09, -0.035, 0.015), (0.045, 0.036, 0.02)),
    ("LobeBack", (0.03, 0.085, 0.014), (0.042, 0.034, 0.018)),
    ("LobeFront", (-0.02, -0.092, 0.013), (0.046, 0.032, 0.017)),
    ("Crest", (0.025, 0.012, 0.08), (0.045, 0.04, 0.042)),
)

DRIPS = (
    ("DripLeft", (-0.055, -0.07, 0.035), (0.016, 0.014, 0.034)),
    ("DripRight", (0.07, 0.05, 0.03), (0.014, 0.012, 0.03)),
)


def add_ellipsoid(name, location, scale):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=16, radius=1.0, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    return obj


def build():
    for obj in list(bpy.data.objects):
        if obj.type == 'MESH':
            bpy.data.objects.remove(obj, do_unlink=True)

    parts = [add_ellipsoid(name, location, scale) for name, location, scale in LOBES + DRIPS]
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.join()
    blob = bpy.context.active_object
    blob.name = "SlimeBlob"

    remesh = blob.modifiers.new("Remesh", 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = VOXEL_SIZE
    remesh.adaptivity = 0.0
    bpy.ops.object.modifier_apply(modifier="Remesh")

    smooth = blob.modifiers.new("Smooth", 'SMOOTH')
    smooth.factor = 0.8
    smooth.iterations = 6
    bpy.ops.object.modifier_apply(modifier="Smooth")

    triangle_count = sum(len(polygon.vertices) - 2 for polygon in blob.data.polygons)

    if triangle_count > TARGET_TRIANGLES:
        decimate = blob.modifiers.new("Decimate", 'DECIMATE')
        decimate.ratio = TARGET_TRIANGLES / triangle_count
        bpy.ops.object.modifier_apply(modifier="Decimate")

    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

    for vertex in blob.data.vertices:
        if vertex.co.z < 0.0:
            vertex.co.z = 0.0

    bpy.ops.object.shade_smooth()
    material = bpy.data.materials.get("SlimeJelly") or bpy.data.materials.new("SlimeJelly")
    material.diffuse_color = (0.6, 1.0, 0.1, 0.85)
    blob.data.materials.clear()
    blob.data.materials.append(material)
    return blob


def export(blob):
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    blob.select_set(True)
    bpy.context.view_layer.objects.active = blob
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
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


blob = build()
export(blob)
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("triangles", sum(len(polygon.vertices) - 2 for polygon in blob.data.polygons), "size", tuple(round(value, 3) for value in blob.dimensions))
