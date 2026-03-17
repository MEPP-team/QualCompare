# render_yana_param_single.py
import bpy
import os
import sys
import math
import argparse
import csv
from mathutils import Vector, Euler
import shlex
import cv2
# IMPORT CUSTOM FILES BELOW ---------------------------

current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.append(current_dir)

import render_single
import positions
world_color = (0.201555, 0.198069, 0.174648) # (0.4, 0.37, 0.3)
def place_object(obj, scale_factor, rotation_unity, position_unity):

    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
        
    obj.scale *= scale_factor
    obj.location = (position_unity[0], position_unity[2], position_unity[1])
    rotation_correction = Euler((math.radians(90), math.radians(rotation_unity[2]), math.radians(180)), 'XYZ')
    rotation_delta = Euler((math.radians(-rotation_unity[0]), 0, math.radians(-rotation_unity[1])), 'XYZ')
    obj.rotation_euler = rotation_correction
    obj.delta_rotation_euler = rotation_delta    
    # print(f"[DEBUG] Placed object {obj.name} at {obj.location} with scale {obj.scale} and rotation {obj.rotation_euler}.")

    # Placing origin to geometry AFTER scaling and rotation because it could change the results

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    
def setup_camera_and_light(obj):	

    # Setup camera
    camera_data = bpy.data.cameras.new("Camera")
    camera = bpy.data.objects.new("Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    bpy.context.scene.camera = camera
    camera.location = (0, 0, 0)
    camera.rotation_euler = (math.radians(90), 0, 0)
    camera.data.lens_unit = 'MILLIMETERS'
    camera.data.lens = 20.78
    camera.data.sensor_fit = 'VERTICAL'
    camera.data.sensor_width = 36.0
    camera.data.sensor_height = 24.0

    # Setup light
    # bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    light_data = bpy.data.lights.new(name="SunLight", type='SUN')
    theta = math.radians(30) - math.pi/2
    phi = math.radians(50)
    x = math.cos(theta) * math.cos(phi)
    y = math.sin(theta) * math.cos(phi)
    z = math.sin(phi)
    light_data.energy = 5
    light_data.angle = 0
    light_data.use_shadow = False
    light = bpy.data.objects.new(name="SunLight", object_data=light_data)
    light.location = obj.location + Vector((x, y, z))
    bpy.context.collection.objects.link(light)
    light.constraints.new(type='TRACK_TO').target = obj
    return camera, light

def render_views(obj, view_dir, mask_dir, view_num, ext):
    # Set the output file path

    # Render the view
    # TODO : Rotate the object on itsef
    for i in range(view_num):
        view_path = os.path.join(view_dir, f"view_{i + 1}.{ext}")
        mask_path = os.path.join(mask_dir, f"mask_{i + 1}.{ext}")
        temp_mask_path = os.path.join(mask_dir, f"temp_mask_{i + 1}_temp.{ext}")
        obj.select_set(True)

        # Save View
        bpy.context.scene.render.filepath = view_path
        bpy.context.scene.render.engine = 'BLENDER_EEVEE_NEXT'
        bpy.context.scene.display.shading.light = 'STUDIO'
        bpy.context.scene.display.shading.color_type = 'MATERIAL'
        bpy.context.scene.world.color = world_color
        bpy.ops.render.render(write_still=True)
        
        # Save Mask
        bpy.context.scene.render.engine = 'BLENDER_WORKBENCH'
        bpy.context.scene.display.shading.light = 'FLAT'
        bpy.context.scene.display.shading.color_type = 'SINGLE'
        bpy.context.scene.display.shading.single_color = (1, 1, 1)
        bpy.context.scene.world.color = (0, 0, 0)
        bpy.context.scene.render.filepath = temp_mask_path
        bpy.ops.render.render(write_still=True)
    
        mask = cv2.imread(temp_mask_path, cv2.IMREAD_GRAYSCALE)
        _, binary_mask = cv2.threshold(mask, 10, 255, cv2.THRESH_BINARY)
        cv2.imwrite(mask_path, binary_mask)
        os.remove(temp_mask_path)

        obj.delta_rotation_euler[2] -= math.radians(360 / view_num)

# [COMMAND] blender --background --python "..\render_single.py" -- --obj "..\name.obj" --out "..\out\TMQ_ref_4f_2" --nb_views 4 --positions_type yfixed --file_type source --ext png
def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument('--obj')
    parser.add_argument('--out', help="Output folder")
    parser.add_argument('--nb_views', type = int, help="view number")
    parser.add_argument('--ext', default = "jpg", choices=["jpg", "png"], help="Image format")
    parser.add_argument('--folder_name', help="input folder name")
    parser.add_argument('--model_name')
    parser.add_argument('--scale', type = float, required=True)
    parser.add_argument('--rot', nargs = 3, type = float, required=True)
    parser.add_argument('--pos', nargs = 3, type = float, required=True)
    # print("[DEBUG] Command line arguments:", sys.argv)
    args = parser.parse_args(argv)
    
    # print("[DEBUG] Parsed args:", args)  

    # scale, rotation, position, folder = parse_model_info(args.models_carac_file, args.model_name)
    obj_path = args.obj
    # output_path = os.path.join(args.out, args.model_name + ".png")

    if not os.path.exists(obj_path):
        print(f"[ERROR] Model not found: {obj_path}")
        return

    obj = render_single.setup_scene(obj_path)
    place_object(obj, args.scale, args.rot, args.pos)
    print(f"[INFO] Processing model: {args.model_name} from folder: {args.folder_name}")

    view_dir = os.path.join(args.out, args.model_name, "views")
    mask_dir = os.path.join(args.out, args.model_name, "masks")
    os.makedirs(view_dir, exist_ok=True)
    os.makedirs(mask_dir, exist_ok=True)

    setup_camera_and_light(obj)

    render_views(obj, view_dir, mask_dir, args.nb_views, args.ext)
    # Générer les positions yfixed (en forçant le postype)
    # cam_pos, _ = positions.generate_positions(4, postype="yfixed")
    # camera.location = cam_pos[0]

    # bpy.context.scene.render.filepath = output_path
    # bpy.ops.render.render(write_still=True)
    bpy.ops.wm.quit_blender()

if __name__ == "__main__":
    main()
    bpy.ops.wm.quit_blender()
