import bpy
import os
import sys
import math
import argparse
import cv2
import numpy as np
from mathutils import Vector, Matrix
import multiprocessing
from dataclasses import dataclass, field
from typing import Tuple

# Add current directory to sys.path to import helper modules
current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.append(current_dir)
import positions


@dataclass
class RenderConfig:
    """Central configuration for the render pipeline."""
    res_x: int = 650
    res_y: int = 550
    render_engine: str = "BLENDER_EEVEE_NEXT"
    taa_samples: int = 64
    filter_size: float = 1
    world_color: str = "#34322C"
    mask_threshold: int = 10
    sun_energy: float = 5.0
    sun_theta_deg: float = 30.0
    sun_phi_deg: float = 50.0
    point_radius_fraction: float = 0.003
    threads: int = field(default_factory=multiprocessing.cpu_count)


def clean_scene():
    """Remove all objects and orphan data from the scene."""
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for datablock in (bpy.data.meshes,
                      bpy.data.lights,
                      bpy.data.cameras,
                      bpy.data.materials):
        for block in list(datablock):
            datablock.remove(block)

    bpy.ops.outliner.orphans_purge(do_recursive=True)


def import_obj(filepath: str):
    """Import an OBJ or PLY file and return the main mesh object."""
    if filepath.endswith(".obj"):
        bpy.ops.wm.obj_import(filepath=filepath)
    elif filepath.endswith(".ply"):
        bpy.ops.wm.ply_import(filepath=filepath)
    else:
        raise RuntimeError(f"Unsupported file extension for {filepath}")

    for obj in bpy.context.selected_objects:
        if obj.type == "MESH":
            return obj

    raise RuntimeError("No mesh object found after import.")


def setup_scene(obj_path: str, config: RenderConfig):
    """Create a clean scene and configure render settings."""
    clean_scene()
    obj = import_obj(obj_path)

    scene = bpy.context.scene
    eevee = scene.eevee

    scene.render.resolution_x = config.res_x
    scene.render.resolution_y = config.res_y
    scene.render.resolution_percentage = 100
    scene.render.engine = config.render_engine
    scene.cycles.device = "GPU"

    # Color management
    scene.display_settings.display_device = "Display P3"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.sequencer_colorspace_settings.name = "sRGB"

    if bpy.data.images:
        first_image = bpy.data.images[0]
        first_image.colorspace_settings.name = "sRGB"
        first_image.alpha_mode = "NONE"

    eevee.taa_render_samples = config.taa_samples
    scene.render.filter_size = config.filter_size
    eevee.use_taa_reprojection = False
    eevee.use_shadow_jitter_viewport = False
    eevee.use_shadows = False

    # # Forcing full frame render
    # scene.render.use_border = True
    # scene.render.use_crop_to_border = True
    # scene.render.border_min_x = 0.0
    # scene.render.border_min_y = 0.0
    # scene.render.border_max_x = 1.0
    # scene.render.border_max_y = 1.0
    
    scene.render.image_settings.compression = 0
    scene.render.threads = config.threads

    scene.world.use_nodes = False
    scene.world.color = config.world_color

    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)

    return obj


def compute_properties(obj, up_axis: str = "Y"):
    """Normalize object scale and orientation, return geometry properties."""
    bbox_corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    min_corner = Vector([min(c[i] for c in bbox_corners) for i in range(3)])
    max_corner = Vector([max(c[i] for c in bbox_corners) for i in range(3)])

    center = (max_corner + min_corner) / 2.0
    size_vec = max_corner - min_corner
    dist = size_vec.length
    max_size = max(size_vec)

    if max_size == 0.0:
        scale_factor = 1.0
    else:
        scale_factor = 1.0 / max_size

    obj.scale *= scale_factor
    dist *= scale_factor
    max_size *= scale_factor

    obj.location -= center

    if up_axis == "X":
        obj.rotation_euler = (0.0, 0.0, math.radians(-90.0))
    elif up_axis == "Z":
        obj.rotation_euler = (math.radians(90.0), 0.0, 0.0)

    bpy.ops.object.transform_apply(location=True, scale=True)

    bbox_corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return obj.location.copy(), max_size, dist, bbox_corners

def compute_radius_from_vertices(obj):
    # Assumes the object is centered at origin in local space
    if obj.type != "MESH" or not obj.data.vertices:
        return 0.0
    return max(v.co.length for v in obj.data.vertices)

def compute_bounding_sphere_radius(center, bbox_corners):
    c = Vector(center)
    return max((Vector(p) - c).length for p in bbox_corners)

def set_camera_fov_to_fit_sphere(camera, center, radius, res_x, res_y, margin=1.02):
    aspect = float(res_x) / float(res_y)
    d = (Vector(center) - Vector(camera.location)).length
    r = radius * margin

    # Required angular diameter to fit the sphere
    req = 2.0 * math.atan(r / d)

    # We control angle_y (vertical). Horizontal depends on aspect:
    # angle_x = 2 * atan(tan(angle_y/2) * aspect)
    # Ensure both vertical and horizontal are >= req.
    needed_angle_y = max(req, 2.0 * math.atan(math.tan(req * 0.5) / aspect))

    camera.data.sensor_fit = "VERTICAL"
    camera.data.angle_y = needed_angle_y

def add_debug_bounding_sphere(center, radius, name="DEBUG_BoundingSphere", wire_thickness=0.002, show_in_render=True):
    # Create a dedicated collection
    coll = bpy.data.collections.get("DEBUG")
    if coll is None:
        coll = bpy.data.collections.new("DEBUG")
        bpy.context.scene.collection.children.link(coll)

    # Remove previous sphere if any
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)

    # Create sphere
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3, radius=radius, location=Vector(center))
    sphere = bpy.context.active_object
    sphere.name = name

    # Put in DEBUG collection only
    for c in list(sphere.users_collection):
        c.objects.unlink(sphere)
    coll.objects.link(sphere)

    # Viewport display
    sphere.display_type = "WIRE"
    sphere.show_in_front = True

    # Render visibility
    sphere.hide_render = not show_in_render

    if show_in_render:
        # Make the sphere render as wireframe
        wf = sphere.modifiers.new(name="Wireframe", type="WIREFRAME")
        wf.thickness = wire_thickness
        wf.use_replace = True

        # Simple emission material
        mat = bpy.data.materials.get("DEBUG_Wire_Emission")
        if mat is None:
            mat = bpy.data.materials.new("DEBUG_Wire_Emission")
            mat.use_nodes = True
            nodes = mat.node_tree.nodes
            links = mat.node_tree.links
            nodes.clear()

            out = nodes.new(type="ShaderNodeOutputMaterial")
            em = nodes.new(type="ShaderNodeEmission")
            em.inputs["Color"].default_value = (1.0, 0.2, 0.2, 1.0)
            em.inputs["Strength"].default_value = 5.0
            links.new(em.outputs["Emission"], out.inputs["Surface"])

        if sphere.data.materials:
            sphere.data.materials[0] = mat
        else:
            sphere.data.materials.append(mat)

    return sphere
def setup_camera_and_light(obj, config: RenderConfig):
    """Create camera and key light tracking the object."""
    scene = bpy.context.scene

    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    cam_constraint = camera.constraints.new(type="TRACK_TO")
    cam_constraint.target = obj
    cam_constraint.track_axis = "TRACK_NEGATIVE_Z"
    cam_constraint.up_axis = "UP_Y"

    light_data = bpy.data.lights.new(name="SunLight", type="SUN")
    light_data.energy = config.sun_energy
    light_data.angle = 0.0
    light = bpy.data.objects.new(name="SunLight", object_data=light_data)

    theta = math.radians(config.sun_theta_deg)
    phi = math.radians(config.sun_phi_deg)
    x = math.cos(theta) * math.cos(phi)
    y = math.sin(theta) * math.cos(phi)
    z = math.sin(phi)
    light.location = obj.location + Vector((x, y, z))

    scene.collection.objects.link(light)

    light_constraint = light.constraints.new(type="TRACK_TO")
    light_constraint.target = obj

    return camera, light


def estimate_point_radius(obj, fraction: float):
    """Estimate point radius as a fraction of bounding box diagonal."""
    verts = [v.co for v in obj.data.vertices]
    if len(verts) < 2:
        return 0.01

    min_corner = Vector((min(v[i] for v in verts) for i in range(3)))
    max_corner = Vector((max(v[i] for v in verts) for i in range(3)))
    diag_length = (max_corner - min_corner).length

    return diag_length * fraction


def create_material():
    """Create or reuse a simple emission material driven by vertex colors."""
    mat = bpy.data.materials.get("PointMaterial")
    if mat is None:
        mat = bpy.data.materials.new(name="PointMaterial")
        mat.use_nodes = True
        nodes_mat = mat.node_tree.nodes
        links_mat = mat.node_tree.links
        nodes_mat.clear()

        output = nodes_mat.new(type="ShaderNodeOutputMaterial")
        emission = nodes_mat.new(type="ShaderNodeEmission")
        col_attr = nodes_mat.new(type="ShaderNodeAttribute")

        col_attr.attribute_name = "Col"
        links_mat.new(col_attr.outputs["Color"], emission.inputs["Color"])
        links_mat.new(emission.outputs["Emission"], output.inputs["Surface"])
    return mat


def setup_ply_rendering(obj, mode: str = "sphere", point_radius_fraction: float = 0.003):
    """
    Configure rendering of a PLY object using geometry nodes.

    mode:
        - "sphere": points visualization through MeshToPoints
        - "surface": oriented instancing
    """
    # obj.rotation_euler = (math.radians(90.0), 0.0, math.radians(-90.0))
    # bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    mat = create_material()

    if mode == "sphere":
        modifier = obj.modifiers.new(name="GeometryNodes", type="NODES")
        node_group = bpy.data.node_groups.new(name="PointCloudDirect", type="GeometryNodeTree")
        modifier.node_group = node_group

        node_group.interface.new_socket(name="Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
        node_group.interface.new_socket(name="Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")

        nodes = node_group.nodes
        links = node_group.links
        nodes.clear()

        input_node = nodes.new(type="NodeGroupInput")
        input_node.location = (-400, 0)

        output_node = nodes.new(type="NodeGroupOutput")
        output_node.location = (300, 0)

        mesh_to_points = nodes.new(type="GeometryNodeMeshToPoints")
        mesh_to_points.location = (-200, 0)
        mesh_to_points.inputs["Radius"].default_value = estimate_point_radius(obj, point_radius_fraction)
        mesh_to_points.mode = "VERTICES"

        set_material = nodes.new(type="GeometryNodeSetMaterial")
        set_material.location = (100, 0)
        set_material.inputs["Material"].default_value = mat

        links.new(input_node.outputs["Geometry"], mesh_to_points.inputs["Mesh"])
        links.new(mesh_to_points.outputs["Points"], set_material.inputs["Geometry"])
        links.new(set_material.outputs["Geometry"], output_node.inputs["Geometry"])

    elif mode == "surface":
        node_group = bpy.data.node_groups.new("PointNormalSurface", "GeometryNodeTree")
        modifier = obj.modifiers.new("GN_Surface", "NODES")
        modifier.node_group = node_group

        node_group.interface.new_socket(socket_type="NodeSocketGeometry", in_out="INPUT", name="Geometry")
        node_group.interface.new_socket(socket_type="NodeSocketRotation", in_out="INPUT", name="Rotation")
        node_group.interface.new_socket(socket_type="NodeSocketVector", in_out="INPUT", name="Scale")
        node_group.interface.new_socket(socket_type="NodeSocketMaterial", in_out="INPUT", name="Material")
        node_group.interface.new_socket(socket_type="NodeSocketGeometry", in_out="OUTPUT", name="Geometry")

        nodes = node_group.nodes
        links = node_group.links
        nodes.clear()

        input_node = nodes.new("NodeGroupInput")
        input_node.location = (-600, 0)
        output_node = nodes.new("NodeGroupOutput")
        output_node.location = (800, 0)

        circle_node = nodes.new("GeometryNodeMeshCircle")
        circle_node.location = (-600, -300)
        circle_node.inputs["Vertices"].default_value = 5
        circle_node.inputs["Radius"].default_value = 1.0

        normal_node = nodes.new("GeometryNodeInputNormal")
        normal_node.location = (-600, -150)

        align_node = nodes.new("FunctionNodeAlignRotationToVector")
        align_node.location = (-300, -150)
        align_node.axis = "Z"

        instance_node = nodes.new("GeometryNodeInstanceOnPoints")
        instance_node.location = (0, 0)

        realize_node = nodes.new("GeometryNodeRealizeInstances")
        realize_node.location = (200, 0)

        set_mat_node = nodes.new("GeometryNodeSetMaterial")
        set_mat_node.location = (400, 0)

        links.new(input_node.outputs["Geometry"], instance_node.inputs["Points"])
        links.new(input_node.outputs["Material"], set_mat_node.inputs["Material"])
        links.new(circle_node.outputs["Mesh"], instance_node.inputs["Instance"])
        links.new(normal_node.outputs["Normal"], align_node.inputs["Vector"])
        links.new(align_node.outputs["Rotation"], instance_node.inputs["Rotation"])
        links.new(instance_node.outputs["Instances"], realize_node.inputs["Geometry"])
        links.new(realize_node.outputs["Geometry"], set_mat_node.inputs["Geometry"])
        links.new(set_mat_node.outputs["Geometry"], output_node.inputs["Geometry"])


def quantize_mesh_vertices_on_bits(obj, bits: int = 10):
    """Quantize mesh vertex positions on N bits inside the object bounding box."""
    if obj.type != "MESH" or bits <= 0:
        return None

    mesh = obj.data
    if not mesh or not mesh.vertices:
        return None

    min_x = min_y = min_z = float("inf")
    max_x = max_y = max_z = float("-inf")

    for v in mesh.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        if x < min_x:
            min_x = x
        if y < min_y:
            min_y = y
        if z < min_z:
            min_z = z
        if x > max_x:
            max_x = x
        if y > max_y:
            max_y = y
        if z > max_z:
            max_z = z

    size_x = max_x - min_x
    size_y = max_y - min_y
    size_z = max_z - min_z

    if size_x == 0.0:
        size_x = 1.0
    if size_y == 0.0:
        size_y = 1.0
    if size_z == 0.0:
        size_z = 1.0

    levels = 2 ** bits
    denom = float(levels - 1)

    def quantize_axis(val, vmin, size):
        t = (val - vmin) / size
        idx = int(t * denom + 0.5)
        if idx < 0:
            idx = 0
        elif idx > levels - 1:
            idx = levels - 1
        q = idx / denom
        return vmin + q * size

    for v in mesh.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        v.co.x = quantize_axis(x, min_x, size_x)
        v.co.y = quantize_axis(y, min_y, size_y)
        v.co.z = quantize_axis(z, min_z, size_z)

    max_size = max(size_x, size_y, size_z)
    voxel_size = max_size / denom

    mesh.update()
    return voxel_size


def setup_ply_rendering_voxel(obj, bits: int = 10):
    """
    Voxel rendering of a PLY point cloud:
    - Quantizes vertices on N bits in object bounding box.
    - Instances solid cubes on each quantized point.
    """


    voxel_size = quantize_mesh_vertices_on_bits(obj, bits=bits)
    if voxel_size is None:
        print("Quantization failed or object is not a mesh.")
        return

    mat = create_material()

    modifier = obj.modifiers.new(name="GN_Voxel", type="NODES")
    node_group = bpy.data.node_groups.new(name="PointCloudVoxel", type="GeometryNodeTree")
    modifier.node_group = node_group

    node_group.interface.new_socket(name="Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    node_group.interface.new_socket(name="Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")

    nodes = node_group.nodes
    links = node_group.links
    nodes.clear()

    input_node = nodes.new(type="NodeGroupInput")
    input_node.location = (-800, 0)

    output_node = nodes.new(type="NodeGroupOutput")
    output_node.location = (400, 0)

    mesh_to_points = nodes.new(type="GeometryNodeMeshToPoints")
    mesh_to_points.location = (-600, 0)
    mesh_to_points.mode = "VERTICES"

    cube_node = nodes.new(type="GeometryNodeMeshCube")
    cube_node.location = (-600, -250)

    try:
        cube_node.inputs["Size"].default_value = (voxel_size, voxel_size, voxel_size)
    except KeyError:
        cube_node.inputs["Size X"].default_value = voxel_size
        cube_node.inputs["Size Y"].default_value = voxel_size
        cube_node.inputs["Size Z"].default_value = voxel_size

    instance_node = nodes.new(type="GeometryNodeInstanceOnPoints")
    instance_node.location = (-350, 0)

    realize_node = nodes.new(type="GeometryNodeRealizeInstances")
    realize_node.location = (-100, 0)

    set_mat_node = nodes.new(type="GeometryNodeSetMaterial")
    set_mat_node.location = (150, 0)
    set_mat_node.inputs["Material"].default_value = mat

    links.new(input_node.outputs["Geometry"], mesh_to_points.inputs["Mesh"])
    links.new(mesh_to_points.outputs["Points"], instance_node.inputs["Points"])
    links.new(cube_node.outputs["Mesh"], instance_node.inputs["Instance"])
    links.new(instance_node.outputs["Instances"], realize_node.inputs["Geometry"])
    links.new(realize_node.outputs["Geometry"], set_mat_node.inputs["Geometry"])
    links.new(set_mat_node.outputs["Geometry"], output_node.inputs["Geometry"])

def setup_ply_rendering_voxel_via_volume(
    obj,
    voxel_size: float,
    radius: float,
    density_factor: float = 1.0,
):
    # Build a voxel-like representation using Points to Volume + Distribute Points in Volume (Grid).
    mat = create_material()

    modifier = obj.modifiers.new(name="GN_VoxelVolume", type="NODES")
    node_group = bpy.data.node_groups.new(name="PointCloudVoxelVolume", type="GeometryNodeTree")
    modifier.node_group = node_group

    node_group.interface.new_socket(name="Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    node_group.interface.new_socket(name="Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")

    nodes = node_group.nodes
    links = node_group.links
    nodes.clear()

    def _first_socket_by_name(node, candidates):
        for n in candidates:
            if n in node.inputs:
                return node.inputs[n]
        return None

    def _first_output_by_name(node, candidates):
        for n in candidates:
            if n in node.outputs:
                return node.outputs[n]
        return None

    input_node = nodes.new(type="NodeGroupInput")
    input_node.location = (-900, 0)

    output_node = nodes.new(type="NodeGroupOutput")
    output_node.location = (900, 0)

    mesh_to_points = nodes.new(type="GeometryNodeMeshToPoints")
    mesh_to_points.location = (-700, 0)
    mesh_to_points.mode = "VERTICES"

    points_to_volume = nodes.new(type="GeometryNodePointsToVolume")
    points_to_volume.location = (-450, 0)

    # Points to Volume settings (voxel size + radius influence)
    sock = _first_socket_by_name(points_to_volume, ["Voxel Size", "Voxel Size (Legacy)"])
    if sock is not None:
        sock.default_value = voxel_size
    sock = _first_socket_by_name(points_to_volume, ["Radius"])
    if sock is not None:
        sock.default_value = radius
    sock = _first_socket_by_name(points_to_volume, ["Density"])
    if sock is not None:
        sock.default_value = 1.0

    # Distribute Points in Volume - prefer the dedicated grid node type if available.
    try:
        dist = nodes.new(type="GeometryNodeDistributePointsInGrid")
    except RuntimeError:
        dist = nodes.new(type="GeometryNodeDistributePointsInVolume")
        # Try to switch to grid mode if the property exists in this build.
        for prop_name in ["distribute_method", "distribution_method", "mode"]:
            if hasattr(dist, prop_name):
                try:
                    setattr(dist, prop_name, "DENSITY_GRID")
                except Exception:
                    try:
                        setattr(dist, prop_name, "GRID")
                    except Exception:
                        pass

    dist.location = (-200, 0)

    # Grid spacing controls the sampling resolution
    sock = _first_socket_by_name(dist, ["Spacing", "Distance", "Grid Spacing"])
    if sock is not None:
        sock.default_value = voxel_size

    # Optional factor to make point creation more/less permissive (depends on node implementation)
    sock = _first_socket_by_name(dist, ["Density", "Density Factor"])
    if sock is not None:
        sock.default_value = density_factor

    cube_node = nodes.new(type="GeometryNodeMeshCube")
    cube_node.location = (100, -250)

    try:
        cube_node.inputs["Size"].default_value = (voxel_size, voxel_size, voxel_size)
    except KeyError:
        cube_node.inputs["Size X"].default_value = voxel_size
        cube_node.inputs["Size Y"].default_value = voxel_size
        cube_node.inputs["Size Z"].default_value = voxel_size

    instance_node = nodes.new(type="GeometryNodeInstanceOnPoints")
    instance_node.location = (150, 0)

    realize_node = nodes.new(type="GeometryNodeRealizeInstances")
    realize_node.location = (400, 0)

    set_mat_node = nodes.new(type="GeometryNodeSetMaterial")
    set_mat_node.location = (650, 0)
    set_mat_node.inputs["Material"].default_value = mat

    # Links: Geometry -> points -> volume -> distributed grid points -> cubes -> output
    links.new(input_node.outputs["Geometry"], mesh_to_points.inputs["Mesh"])

    out_points = _first_output_by_name(mesh_to_points, ["Points"])
    in_points = _first_socket_by_name(points_to_volume, ["Points"])
    if out_points is not None and in_points is not None:
        links.new(out_points, in_points)

    out_vol = _first_output_by_name(points_to_volume, ["Volume"])
    in_vol = _first_socket_by_name(dist, ["Volume"])
    if out_vol is not None and in_vol is not None:
        links.new(out_vol, in_vol)

    out_dist_pts = _first_output_by_name(dist, ["Points"])
    in_inst_pts = _first_socket_by_name(instance_node, ["Points"])
    if out_dist_pts is not None and in_inst_pts is not None:
        links.new(out_dist_pts, in_inst_pts)

    links.new(cube_node.outputs["Mesh"], instance_node.inputs["Instance"])
    links.new(instance_node.outputs["Instances"], realize_node.inputs["Geometry"])
    links.new(realize_node.outputs["Geometry"], set_mat_node.inputs["Geometry"])
    links.new(set_mat_node.outputs["Geometry"], output_node.inputs["Geometry"])
    
def auto_align_object(obj):
    """Optional helper to auto align object based on its major axis."""
    bbox = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    extent = [max(c[i] for c in bbox) - min(c[i] for c in bbox) for i in range(3)]
    major_axis = int(extent.index(max(extent)))

    if major_axis == 1:
        obj.rotation_euler = (math.radians(90.0), 0.0, 0.0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)


def compute_fov_NOPROJ(camera, center, sizemax, bbox_corners):
    """Compute camera FOV to fit the object bounding box."""
    max_angle = 0.0
    campos = Vector(camera.location)
    distance = (Vector(center) - campos).length
    angle = 2.0 * math.atan(sizemax / (2.0 * distance))
    max_angle = max(max_angle, angle)
    return max_angle, max_angle


def render_views_and_masks(
    obj_name,
    obj,
    cam_rotations,
    camera,
    center,
    size,
    bbox_corners,
    output_dir,
    args,
    config: RenderConfig,
):
    """Render all views and corresponding binary masks."""
    scene = bpy.context.scene

    view_dir = os.path.join(output_dir, obj_name, "views")
    mask_dir = os.path.join(output_dir, obj_name, "masks")
    os.makedirs(view_dir, exist_ok=True)
    os.makedirs(mask_dir, exist_ok=True)

    # required_fov_x, required_fov_y = compute_fov_NOPROJ(camera, center, size, bbox_corners)
    # camera.data.angle_x = required_fov_x
    # camera.data.angle_y = required_fov_y

    obj.rotation_mode = "QUATERNION"

    for i, rot in enumerate(cam_rotations):
        obj.rotation_quaternion = rot

        view_path = os.path.join(view_dir, f"view_{i + 1}.{args.ext}")
        mask_path = os.path.join(mask_dir, f"mask_{i + 1}.{args.ext}")
        temp_mask_path = os.path.join(mask_dir, f"temp_mask_{i + 1}_temp.{args.ext}")

        scene.render.filepath = view_path
        scene.render.engine = config.render_engine
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "MATERIAL"
        scene.world.color = config.world_color
        bpy.ops.render.render(write_still=True)

        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "FLAT"
        scene.display.shading.color_type = "SINGLE"
        scene.display.shading.single_color = (1.0, 1.0, 1.0)
        scene.world.color = (0.0, 0.0, 0.0)
        scene.render.filepath = temp_mask_path
        bpy.ops.render.render(write_still=True)

        mask = cv2.imread(temp_mask_path, cv2.IMREAD_GRAYSCALE)
        _, binary_mask = cv2.threshold(mask, config.mask_threshold, 255, cv2.THRESH_BINARY)
        cv2.imwrite(mask_path, binary_mask)
        os.remove(temp_mask_path)


def object_rotation_from_virtual_camera(cam_pos):
    """
    Compute object rotation to mimic a virtual camera at cam_pos
    when the real Blender camera is fixed at [1, 0, 0].
    """
    static_cam_pos = Vector((1.0, 0.0, 0.0)).normalized()
    new_cam_pos = Vector(cam_pos).normalized()
    static_quat = static_cam_pos.to_track_quat("Z", "Y")
    virtual_quat = new_cam_pos.to_track_quat("Z", "Y")
    rotation = static_quat @ virtual_quat.inverted()
    return rotation


def build_config_from_args(args) -> RenderConfig:
    """Create a RenderConfig instance from CLI arguments."""
    return RenderConfig(
        res_x=args.resx,
        res_y=args.resy,
        render_engine=args.engine,
        taa_samples=args.taa,
        filter_size=args.filter_size,
        world_color=hex_to_rgb01(args.bg_color),
        mask_threshold=args.mask_threshold,
        sun_energy=args.sun_energy,
        sun_theta_deg=args.sun_theta,
        sun_phi_deg=args.sun_phi,
        point_radius_fraction=args.point_radius_fraction,
    )

def hex_to_rgb01(s):
    s = s.strip()
    if s.startswith("#"):
        s = s[1:]
    if len(s) != 6:
        raise ValueError("Invalid hex color length")
    r = int(s[0:2], 16) / 255.0
    g = int(s[2:4], 16) / 255.0
    b = int(s[4:6], 16) / 255.0
    return (r, g, b)

def main():
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()

    # Core inputs
    parser.add_argument("--obj", type=str, required=True, help="Path to the object file (.obj or .ply).")
    parser.add_argument("--out", type=str, required=True, help="Root output directory for renders.")
    parser.add_argument("--nb_views", type=int, default=4, help="Number of views to render.")
    parser.add_argument(
        "--positions_type",
        type=str,
        choices=["yfixed", "fibonacci", "polyedric"],
        default="yfixed",
        help="Camera position sampling strategy.",
    )
    parser.add_argument(
        "--ext",
        type=str,
        default="png",
        choices=["jpg", "png"],
        help="Output file format extension.",
    )
    parser.add_argument("--file_type", type=str, default="source", help="Semantic type of the file (unused in script).")
    parser.add_argument(
        "--obj_type",
        type=str,
        default="obj",
        choices=["obj", "ply"],
        help="Object format to render.",
    )
    parser.add_argument(
        "--ypos",
        type=float,
        default=0.0,
        help="Height of the virtual camera positions along the Y axis.",
    )
    parser.add_argument(
        "--up_axis",
        type=str,
        choices=["X", "Y", "Z"],
        default="Y",
        help="Up axis of the model before normalization.",
    )

    # Render configuration
    parser.add_argument("--resx", type=int, default=650, help="Horizontal resolution in pixels.")
    parser.add_argument("--resy", type=int, default=550, help="Vertical resolution in pixels.")
    parser.add_argument("--taa", type=int, default=64, help="Eevee TAA render samples.")
    parser.add_argument("--filter_size", type=float, default=1.5, help="Pixel filter size.")
    parser.add_argument(
        "--engine",
        type=str,
        default="BLENDER_EEVEE_NEXT",
        help="Render engine used for view rendering.",
    )
    parser.add_argument(
        "--mask_threshold",
        type=int,
        default=10,
        help="Threshold used to binarize mask images.",
    )
    parser.add_argument(
        "--sun_energy",
        type=float,
        default=5.0,
        help="Main directional light energy.",
    )
    parser.add_argument(
        "--sun_theta",
        type=float,
        default=30.0,
        help="Azimuth angle of the main light in degrees.",
    )
    parser.add_argument(
        "--sun_phi",
        type=float,
        default=50.0,
        help="Elevation angle of the main light in degrees.",
    )
    parser.add_argument(
        "--point_radius_fraction",
        type=float,
        default=0.003,
        help="Fraction of bounding diagonal used for point radius in PLY sphere mode.",
    )

    # PLY specific configuration
    parser.add_argument(
        "--ply_render",
        type=str,
        default="sphere",
        choices=["sphere", "surface", "voxel", "voxel_volume"],
        help="PLY rendering mode.",
    )
    parser.add_argument(
        "--ply_voxel_bits",
        type=int,
        default=10,
        help="Number of bits used for voxel quantization in voxel mode.",
    )
    parser.add_argument(
        "--voxel_radius_multiplier",
        type=float,
        default=1.0,
        help="Multiplier for voxel radius when using volume-based voxel rendering.",
    )
        
    parser.add_argument(
        "--bg_color", 
        type=str, 
        default="#34322C",
        help="Background color as hex #RRGGBB (example: #1A2B3C)"
    )

    args = parser.parse_args(argv)

    config = build_config_from_args(args)

    image_format = "JPEG" if args.ext == "jpg" else "PNG"
    bpy.context.scene.render.image_settings.file_format = image_format

    obj_path = args.obj
    output_dir = args.out
    obj_name = os.path.splitext(os.path.basename(obj_path))[0]


    obj = setup_scene(obj_path, config)
    center, size, dist, bbox_corners = compute_properties(obj, up_axis=args.up_axis)

    camera, light = setup_camera_and_light(obj, config)

    cam_positions = positions.generate_positions(
        args.nb_views,
        postype=args.positions_type,
        ypos=args.ypos,
    )

    camera.location = Vector(center) + Vector((1.0, 0.0, 0.0)) * dist
   
    radius = compute_radius_from_vertices(obj)
    #! DEBUG SPHERE
    # debug_sphere = add_debug_bounding_sphere(center, radius, show_in_render=True)
    
    set_camera_fov_to_fit_sphere(camera, center, radius, config.res_x, config.res_y, margin=1.00)
    cam_rotations = [object_rotation_from_virtual_camera(pos) for pos in cam_positions]
    if args.obj_type == "ply":
        radius = compute_bounding_sphere_radius(center, bbox_corners) # We compute from bounding sphere : higher FOV so less holes seen
        set_camera_fov_to_fit_sphere(camera, center, radius, config.res_x, config.res_y, margin=1.00)
        
        padding = 0.0
        if args.ply_render in ("sphere", "surface"):
            # padding = radius * config.point_radius_fraction #estimate_point_radius(obj, config.point_radius_fraction)
            setup_ply_rendering(obj, mode=args.ply_render, point_radius_fraction=config.point_radius_fraction)
        elif args.ply_render == "voxel":
            setup_ply_rendering_voxel(obj, bits=args.ply_voxel_bits)
        elif args.ply_render == "voxel_volume":
            voxel_size = quantize_mesh_vertices_on_bits(obj, bits=args.ply_voxel_bits)
            if voxel_size is not None:
                padding = 0.8660254037844386 * voxel_size  # sqrt(3)/2
            setup_ply_rendering_voxel_via_volume(
                obj,
                voxel_size=voxel_size,
                radius=voxel_size * args.voxel_radius_multiplier,
            )
        radius = compute_radius_from_vertices(obj) + padding

    render_views_and_masks(
        obj_name,
        obj,
        cam_rotations,
        camera,
        center,
        size,
        bbox_corners,
        output_dir,
        args,
        config,
    )


if __name__ == "__main__":
    main()
    bpy.ops.wm.quit_blender()
