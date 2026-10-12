import os
import bpy
from math import radians

ROOT = os.path.abspath(os.path.join(os.path.dirname(bpy.data.filepath or __file__), "..", "..", ".."))
FBX_PATH = os.path.join(ROOT, "Assets", "Models", "Keys", "ShelfKey.fbx")


def add(operator, name, **kwargs):
    operator(**kwargs)
    obj = bpy.context.active_object
    obj.name = name
    return obj


def build():
    for obj in list(bpy.data.objects):
        if obj.type == 'MESH':
            bpy.data.objects.remove(obj, do_unlink=True)

    bow = add(bpy.ops.mesh.primitive_torus_add, "Bow", major_radius=0.042, minor_radius=0.013, major_segments=28, minor_segments=10, location=(0, 0, 0.17), rotation=(radians(90), 0, 0))
    bow.scale = (1, 1, 0.7)
    collar = add(bpy.ops.mesh.primitive_cylinder_add, "Collar", vertices=16, radius=0.018, depth=0.024, location=(0, 0, 0.118))
    shaft = add(bpy.ops.mesh.primitive_cylinder_add, "Shaft", vertices=16, radius=0.011, depth=0.104, location=(0, 0, 0.054))
    tooth_low = add(bpy.ops.mesh.primitive_cube_add, "ToothLow", size=1, location=(0.027, 0, 0.022))
    tooth_low.scale = (0.034, 0.012, 0.03)
    tooth_high = add(bpy.ops.mesh.primitive_cube_add, "ToothHigh", size=1, location=(0.022, 0, 0.056))
    tooth_high.scale = (0.024, 0.012, 0.018)

    parts = (bow, collar, shaft, tooth_low, tooth_high)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = shaft
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    for obj in (collar, shaft, tooth_low, tooth_high):
        modifier = obj.modifiers.new("Bevel", 'BEVEL')
        modifier.width = 0.0025
        modifier.segments = 2
        modifier.limit_method = 'ANGLE'
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier="Bevel")

    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = shaft
    bpy.ops.object.join()
    key = bpy.context.active_object
    key.name = "ShelfKey"
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.shade_auto_smooth(angle=radians(40))
    material = bpy.data.materials.get("KeyIron") or bpy.data.materials.new("KeyIron")
    material.diffuse_color = (0.16, 0.13, 0.11, 1)
    key.data.materials.clear()
    key.data.materials.append(material)
    return key


def export(key):
    bpy.ops.object.select_all(action='DESELECT')
    key.select_set(True)
    bpy.context.view_layer.objects.active = key
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


export(build())
