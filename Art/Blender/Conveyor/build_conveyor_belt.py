import math
import os

import bmesh
import bpy
import numpy
from mathutils import Matrix, Vector

QUEUE_STEP = 0.2
MOUNT_TILT_DEGREES = 12.0
PLANK_CLEARANCE_AT_ORIGIN = 0.0715

TRACK_WIDTH = 0.34
TRACK_THICKNESS = 0.016
TRACK_INNER_RADIUS = 0.010
TRACK_SIDE_U = 0.04
ARC_SEGMENTS = 3
FRONT_AXLE_Y = 0.062
BACK_AXLE_Y = -0.515

BODY_WIDTH = 0.30
BODY_RADIUS = 0.0095

LEG_THICKNESS = 0.012
LEG_OUTER_X = 0.184
FRONT_LEG_Y = 0.032
BACK_LEG_Y = -0.44
FOOT_RADIUS = 0.014
FOOT_HEIGHT = 0.006
FOOT_SEGMENTS = 6

TEXTURE_WIDTH = 128
TEXTURE_HEIGHT = 64
TEXTURE_TILE_LENGTH = 0.1

COLLECTION_NAME = "ConveyorBelt"
REFERENCE_COLLECTION_NAME = "ConveyorReference"
ROOT_NAME = "ConveyorBelt"

BODY_COLOR = (0.05, 0.05, 0.065, 1.0)
PLATE_RGB = (0.30, 0.29, 0.33)
PLATE_EDGE_RGB = (0.21, 0.20, 0.23)
GAP_RGB = (0.07, 0.07, 0.08)

EXPORT_SETTINGS = dict(
    use_selection=True,
    object_types={"EMPTY", "MESH"},
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",
    axis_up="Y",
    bake_space_transform=True,
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    bake_anim=False,
    embed_textures=False,
    path_mode="STRIP",
    add_leaf_bones=False,
    use_custom_props=False,
)


def track_outer_radius():
    return TRACK_INNER_RADIUS + TRACK_THICKNESS


def axle_z():
    return -track_outer_radius()


def world_up_in_mount():
    tilt = math.radians(MOUNT_TILT_DEGREES)
    return Vector((0.0, -math.sin(tilt), math.cos(tilt)))


def height_above_plank(y, z):
    up = world_up_in_mount()
    return PLANK_CLEARANCE_AT_ORIGIN + up.y * y + up.z * z


def drop_to_plank(y, z):
    up = world_up_in_mount()
    height = height_above_plank(y, z)
    return y - up.y * height, z - up.z * height


def belt_step_uv():
    return QUEUE_STEP / TEXTURE_TILE_LENGTH


def loop_samples():
    samples = []
    for step in range(ARC_SEGMENTS + 1):
        samples.append((FRONT_AXLE_Y, 270.0 + 180.0 * step / ARC_SEGMENTS))
    for step in range(ARC_SEGMENTS + 1):
        samples.append((BACK_AXLE_Y, 90.0 + 180.0 * step / ARC_SEGMENTS))
    return samples


def track_path():
    middle_y = (FRONT_AXLE_Y + BACK_AXLE_Y) / 2
    return [(middle_y, 270.0)] + loop_samples() + [(middle_y, 270.0)]


def path_point(sample, radius):
    center_y, angle = sample
    return center_y + math.cos(math.radians(angle)) * radius, axle_z() + math.sin(math.radians(angle)) * radius


def path_distances(samples):
    distances = [0.0]
    for previous, current in zip(samples, samples[1:]):
        a = path_point(previous, track_outer_radius())
        b = path_point(current, track_outer_radius())
        distances.append(distances[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    return distances


def mesh_from_bmesh(bm, name):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def build_track_mesh(name):
    samples = track_path()
    distances = path_distances(samples)
    half_width = TRACK_WIDTH / 2
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    rings = []
    for sample in samples:
        outer_y, outer_z = path_point(sample, track_outer_radius())
        inner_y, inner_z = path_point(sample, TRACK_INNER_RADIUS)
        rings.append((
            bm.verts.new((half_width, outer_y, outer_z)),
            bm.verts.new((-half_width, outer_y, outer_z)),
            bm.verts.new((-half_width, inner_y, inner_z)),
            bm.verts.new((half_width, inner_y, inner_z)),
        ))

    def add_face(corners, uvs):
        face = bm.faces.new(corners)
        face.smooth = False
        for loop, uv in zip(face.loops, uvs):
            loop[uv_layer].uv = uv

    left_side, right_side = TRACK_SIDE_U, 1 - TRACK_SIDE_U
    for index in range(len(samples) - 1):
        a, b = rings[index], rings[index + 1]
        va = distances[index] / TEXTURE_TILE_LENGTH
        vb = distances[index + 1] / TEXTURE_TILE_LENGTH
        add_face((a[0], a[1], b[1], b[0]), ((1, va), (0, va), (0, vb), (1, vb)))
        add_face((a[3], b[3], b[2], a[2]), ((1, va), (1, vb), (0, vb), (0, va)))
        add_face((a[1], a[2], b[2], b[1]), ((left_side, va), (left_side, va), (left_side, vb), (left_side, vb)))
        add_face((a[0], b[0], b[3], a[3]), ((right_side, va), (right_side, vb), (right_side, vb), (right_side, va)))
    for vertex_first, vertex_last in zip(rings[0], rings[-1]):
        bmesh.ops.pointmerge(bm, verts=[vertex_first, vertex_last], merge_co=vertex_first.co.copy())
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_from_bmesh(bm, name)


def add_prism(bm, profile_yz, x_min, x_max):
    left = [bm.verts.new((x_min, y, z)) for y, z in profile_yz]
    right = [bm.verts.new((x_max, y, z)) for y, z in profile_yz]
    bm.faces.new(left)
    bm.faces.new(list(reversed(right)))
    count = len(profile_yz)
    for index in range(count):
        following = (index + 1) % count
        bm.faces.new((left[index], left[following], right[following], right[index]))


def add_box(bm, x_range, y_range, z_range):
    profile = [(y_range[1], z_range[1]), (y_range[1], z_range[0]), (y_range[0], z_range[0]), (y_range[0], z_range[1])]
    add_prism(bm, profile, x_range[0], x_range[1])


def add_foot(bm, x, y, z):
    up = world_up_in_mount()
    rotation = Vector((0.0, 0.0, 1.0)).rotation_difference(up).to_matrix().to_4x4()
    placement = Matrix.Translation((x, y, z)) @ rotation @ Matrix.Translation((0.0, 0.0, FOOT_HEIGHT / 2))
    bmesh.ops.create_cone(bm, cap_ends=True, segments=FOOT_SEGMENTS, radius1=FOOT_RADIUS,
                          radius2=FOOT_RADIUS, depth=FOOT_HEIGHT, matrix=placement)


def add_leg(bm, side, leg_y):
    half = LEG_THICKNESS / 2
    column_x = side * (LEG_OUTER_X - half)
    outer_x = side * LEG_OUTER_X
    inner_x = side * (LEG_OUTER_X - LEG_THICKNESS)
    body_x = side * BODY_WIDTH / 2
    add_box(bm, sorted((body_x, outer_x)), (leg_y - half, leg_y + half), (axle_z() - half, axle_z() + half))
    top_z = axle_z() - half
    up = world_up_in_mount()
    column = []
    for y in (leg_y + half, leg_y - half):
        ground_y, ground_z = drop_to_plank(y, top_z)
        column.append(((y, top_z), (ground_y + up.y * FOOT_HEIGHT, ground_z + up.z * FOOT_HEIGHT)))
    (front_top, front_bottom), (back_top, back_bottom) = column
    add_prism(bm, [front_top, front_bottom, back_bottom, back_top], *sorted((inner_x, outer_x)))
    foot_y, foot_z = drop_to_plank(leg_y, top_z)
    add_foot(bm, column_x, foot_y, foot_z)


def build_frame_mesh(name):
    bm = bmesh.new()
    body_profile = [path_point(sample, BODY_RADIUS) for sample in loop_samples()]
    add_prism(bm, body_profile, -BODY_WIDTH / 2, BODY_WIDTH / 2)
    for side in (1, -1):
        for leg_y in (FRONT_LEG_Y, BACK_LEG_Y):
            add_leg(bm, side, leg_y)
    for face in bm.faces:
        face.smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return mesh_from_bmesh(bm, name)


def track_texture_pixels():
    supersample = 4
    across = (numpy.arange(TEXTURE_WIDTH * supersample) + 0.5) / (TEXTURE_WIDTH * supersample)
    along = (numpy.arange(TEXTURE_HEIGHT * supersample) + 0.5) / (TEXTURE_HEIGHT * supersample)
    u, v = numpy.meshgrid(across, along)
    x = numpy.abs(u - 0.5) * TRACK_WIDTH
    y = numpy.abs(v - 0.5) * TEXTURE_TILE_LENGTH
    gap = 0.006
    corner_radius = 0.012
    half_length = TEXTURE_TILE_LENGTH / 2 - gap
    half_width = TRACK_WIDTH / 2 - 0.002
    corner_x = numpy.maximum(x - (half_width - corner_radius), 0.0)
    corner_y = numpy.maximum(y - (half_length - corner_radius), 0.0)
    plate = (x < half_width) & (y < half_length) & (numpy.hypot(corner_x, corner_y) < corner_radius)
    edge = plate & (v < 0.5) & (y > half_length - 0.008)
    rgb = numpy.empty(u.shape + (3,))
    rgb[...] = GAP_RGB
    rgb[plate] = PLATE_RGB
    rgb[edge] = PLATE_EDGE_RGB
    rgb = rgb.reshape(TEXTURE_HEIGHT, supersample, TEXTURE_WIDTH, supersample, 3).mean(axis=(1, 3))
    rgba = numpy.concatenate([rgb, numpy.ones(rgb.shape[:2] + (1,))], axis=2)
    return rgba.astype(numpy.float32).ravel()


def build_track_image():
    image = bpy.data.images.get("ConveyorBelt")
    if image is not None:
        bpy.data.images.remove(image)
    image = bpy.data.images.new("ConveyorBelt", TEXTURE_WIDTH, TEXTURE_HEIGHT, alpha=False, float_buffer=False)
    image.colorspace_settings.name = "sRGB"
    image.pixels.foreach_set(track_texture_pixels())
    image.update()
    return image


def build_material(name, color, roughness, image=None):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    bsdf = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = roughness
    material.diffuse_color = color
    for node in [node for node in nodes if node.type == "TEX_IMAGE"]:
        nodes.remove(node)
    if image is not None:
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = image
        material.node_tree.links.new(texture.outputs["Color"], bsdf.inputs["Base Color"])
    return material


def reset_collection(name):
    collection = bpy.data.collections.get(name)
    if collection is None:
        collection = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(collection)
    for obj in list(collection.objects):
        data = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if isinstance(data, bpy.types.Mesh) and data.users == 0:
            bpy.data.meshes.remove(data)
    return collection


def add_object(collection, name, data, parent, location=(0.0, 0.0, 0.0)):
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = location
    return obj


def build():
    collection = reset_collection(COLLECTION_NAME)
    belt_material = build_material("ConveyorBelt", PLATE_RGB + (1.0,), 0.6, build_track_image())
    frame_material = build_material("ConveyorFrame", BODY_COLOR, 0.5)

    root = add_object(collection, ROOT_NAME, None, None)
    root.empty_display_type = "ARROWS"
    root.empty_display_size = 0.1

    track = add_object(collection, "Belt", build_track_mesh("Belt"), root)
    track.data.materials.append(belt_material)
    frame = add_object(collection, "Frame", build_frame_mesh("Frame"), root)
    frame.data.materials.append(frame_material)
    bpy.context.view_layer.update()
    return root


def mount_point_from_world(world, mount_origin):
    tilt = math.radians(MOUNT_TILT_DEGREES)
    relative = Vector(world) - Vector(mount_origin)
    unity_y = relative.y * math.cos(tilt) + relative.z * math.sin(tilt)
    unity_z = -relative.y * math.sin(tilt) + relative.z * math.cos(tilt)
    return Vector((relative.x, unity_z, unity_y))


def add_world_box(collection, name, mount_origin, x_range, y_range, z_range):
    bm = bmesh.new()
    corners = [bm.verts.new(mount_point_from_world((x, y, z), mount_origin))
               for x in x_range for y in y_range for z in z_range]
    bmesh.ops.convex_hull(bm, input=corners)
    obj = add_object(collection, name, mesh_from_bmesh(bm, name), None)
    obj.color = (0.55, 0.45, 0.33, 1.0)
    return obj


def build_reference(mount_origin=(0.0, 1.9069, -0.419), plank_top=1.8354):
    collection = reset_collection(REFERENCE_COLLECTION_NAME)
    add_world_box(collection, "PlankBelow", mount_origin, (-0.25, 0.25), (plank_top - 0.03, plank_top), (-0.885, -0.330))
    item_size = 0.333
    item_top_above_anchor = 0.158
    anchor_height = 0.09
    for depth in range(3):
        scale = 0.85 ** depth
        bottom = anchor_height - anchor_height * scale
        top = anchor_height + item_top_above_anchor * scale
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        item = add_object(collection, f"Item_{depth}", mesh_from_bmesh(bm, f"Item_{depth}"), None,
                          (0.0, -QUEUE_STEP * depth, (bottom + top) / 2))
        item.scale = (item_size * scale, item_size * scale, top - bottom)
        item.display_type = "WIRE"
    return collection


def save_track_texture(path):
    image = bpy.data.images["ConveyorBelt"]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()


def triangle_count(root):
    total = 0
    for obj in [root] + list(root.children_recursive):
        if obj.type == "MESH":
            obj.data.calc_loop_triangles()
            total += len(obj.data.loop_triangles)
    return total


def export_fbx(path):
    root = bpy.data.objects[ROOT_NAME]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root] + list(root.children_recursive):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=path, **EXPORT_SETTINGS)


if __name__ == "__main__":
    build()
    build_reference()
