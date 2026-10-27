import os
import bpy
from mathutils import Vector

SCRIPT_DIRECTORY = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(SCRIPT_DIRECTORY, "..", "..", ".."))
FBX_PATH = os.path.join(ROOT, "Assets", "Models", "Slime", "SlimeDigits.fbx")
BLEND_PATH = os.path.join(SCRIPT_DIRECTORY, "SlimeDigits.blend")
DIGIT_HEIGHT = 0.06
EXTRUDE = 0.14
BEVEL = 0.03
VOXEL_SIZE = 0.0016
TARGET_TRIANGLES = 700
PREVIEW_SPACING = 0.05


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)


def build_digit(value):
    curve = bpy.data.curves.new("DigitCurve" + str(value), 'FONT')
    curve.body = str(value)
    curve.extrude = EXTRUDE
    curve.bevel_depth = BEVEL
    curve.bevel_resolution = 3
    curve.align_x = 'CENTER'
    curve.align_y = 'BOTTOM_BASELINE'
    obj = bpy.data.objects.new("Digit" + str(value), curve)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target='MESH')
    obj = bpy.context.active_object
    obj.name = "Digit" + str(value)

    bounds = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    height = max(point.y for point in bounds) - min(point.y for point in bounds)
    scale = DIGIT_HEIGHT / height
    obj.scale = (scale, scale, scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    remesh = obj.modifiers.new("Remesh", 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = VOXEL_SIZE
    remesh.adaptivity = 0.0
    bpy.ops.object.modifier_apply(modifier="Remesh")

    smooth = obj.modifiers.new("Smooth", 'SMOOTH')
    smooth.factor = 0.7
    smooth.iterations = 5
    bpy.ops.object.modifier_apply(modifier="Smooth")

    count = sum(len(polygon.vertices) - 2 for polygon in obj.data.polygons)
    if count > TARGET_TRIANGLES:
        decimate = obj.modifiers.new("Decimate", 'DECIMATE')
        decimate.ratio = TARGET_TRIANGLES / count
        bpy.ops.object.modifier_apply(modifier="Decimate")

    obj.rotation_euler = (1.5707963, 0.0, 3.1415926)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    corners = [Vector(corner) for corner in obj.bound_box]
    center_x = (min(point.x for point in corners) + max(point.x for point in corners)) / 2.0
    center_y = (min(point.y for point in corners) + max(point.y for point in corners)) / 2.0
    lowest = min(point.z for point in corners)
    for vertex in obj.data.vertices:
        vertex.co.x -= center_x
        vertex.co.y -= center_y
        vertex.co.z -= lowest

    for vertex in obj.data.vertices:
        vertex.co.x = -vertex.co.x
    obj.data.flip_normals()

    bpy.ops.object.shade_smooth()
    material = bpy.data.materials.get("SlimeJelly") or bpy.data.materials.new("SlimeJelly")
    obj.data.materials.clear()
    obj.data.materials.append(material)
    return obj


clear_scene()
digits = [build_digit(value) for value in range(1, 10)]

bpy.ops.object.select_all(action='DESELECT')
for digit in digits:
    digit.select_set(True)
bpy.context.view_layer.objects.active = digits[0]
os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
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

for index, digit in enumerate(digits):
    digit.location.x = index * PREVIEW_SPACING

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
for digit in digits:
    print(digit.name, sum(len(polygon.vertices) - 2 for polygon in digit.data.polygons), tuple(round(value, 4) for value in digit.dimensions))
