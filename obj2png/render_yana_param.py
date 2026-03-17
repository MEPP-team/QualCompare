import bpy
import os
import sys
import math
import argparse
import cv2
import numpy as np
import itertools
from mathutils import Vector, Matrix, Euler, Quaternion
import multiprocessing

# IMPORT CUSTOM FILES BELOW ---------------------------
current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.append(current_dir)

import render_single
import positions

def place_object(obj, scale_factor, rotation_unity, position_unity):
    # Appliquer l’échelle  
    # euler_unity_to_blenderXYZ = Euler([rotation_euler_unity[0], rotation_euler_unity[1], rotation_euler_unity[2]], 'XYZ')
    
    # q_unity.x = rotation_quaternion_unity[0]
    # q_unity.y = rotation_quaternion_unity[1]    
    # q_unity.z = rotation_quaternion_unity[2]
    # q_unity.w = rotation_quaternion_unity[3]

    # # print("q_unity", q_unity)
    # q_correction = Euler((math.radians(90), 0, math.radians(180)), 'XYZ').to_quaternion() # Correction for Blender's default camera orientation
    # print("q_correction", q_correction)
    # q_blender = q_unity @ q_correction  # Non commutative product : 1st correction then rotation
    # print("q_blender", q_blender)

    # In blender, apply the rotation such as : original (90,Z,180) + delta(-X,0,-Y) 

    rotation_correction = Euler((math.radians(90)           , math.radians(rotation_unity[2]), math.radians(180               )), 'XYZ') # (90,Z,180)
    rotation_delta = Euler((math.radians(-rotation_unity[0]), 0                              , math.radians(-rotation_unity[1])), 'XYZ') # (-X,0,-Y)

    
    obj.rotation_euler = rotation_correction
    obj.delta_rotation_euler = rotation_delta    
    
    obj.scale *= scale_factor

    obj.location = (position_unity[0], position_unity[2], position_unity[1]) # Unity's Y is Blender's Z
    # Set obj origin to geometry as center of bounding box for light tracking
    # bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    
def main():
    TMQroot = r"D:\These\BDD\TexturedMeshQuality" #r"D:\These\BDD\TMQ"
    models_carac_file = "D:\These\Projets\obj2png\Models_characteristics_and_settings.csv"
    output_folder = r"D:\These\Projets\obj2png\out"
    # CSV STRUCTURE:
    # Model ID,Model name (.obj),Folder name,# Vertices,Semantic Category,Scale factor (in Unity),Main viewpoint transform rotation (in Unity),Model position (in Unity scene),,,,
    # We will search for the model name in the folder name
    # Then we will render with the usual parameters

    # Open the CSV file and read the lines
    # Skip first line (header)
    with open(models_carac_file, 'r') as f:
        lines = f.readlines()
        lines = lines[1:]
        for line in lines:
            # Split the line into columns
            columns = line.strip().split(',')
            # Get the model name and folder name
            model_name = columns[1]
            folder_name = columns[2]
            model_scale_factor = float(columns[5])
            model_rotation = [float(columns[6][1:]), float(columns[7]), float(columns[8][:-1])] # ["(x" ,"y" , "z)"]
            model_position = [float(columns[9][1:]), float(columns[10]), float(columns[11][:-1])]
            # model_rotation_quaternion = [float(columns[12]), float(columns[13]), float(columns[14]), float(columns[15])] # ["(x" ,"y" , "z)"]

            # Get the model path
            model_source_path = os.path.join(TMQroot,"TexturedMeshes_DB", folder_name,"source")
            model_distorted_path = os.path.join(TMQroot,"TexturedMeshes_DB", folder_name,"distortions")
            # TODO distorted obj processiong to add later. Find all the obj files in the distorions folder
            # Check if the model path exists
            if not os.path.exists(model_source_path):
                print(f"Model path does not exist: {model_source_path}")
                continue
            obj_path = os.path.join(model_source_path + "\\" + model_name + ".obj")
            # Load the model in Blender
            # bpy.ops.import_scene.obj(filepath=model_source_path + "\\" + model_name + ".obj")
            # obj = bpy.context.selected_objects[0]

            # Use render_single.py functions to start the scene. NOT ALL THE FUNCTIONS
            bpy.context.scene.render.filepath = os.path.join(output_folder + "\\" + model_name + ".png")
            obj = render_single.setup_scene(obj_path)
            
            place_object(obj, model_scale_factor, model_rotation, model_position)

            # Setup new camera --------------------------------------------------
            
            camera_data = bpy.data.cameras.new("Camera")
            camera = bpy.data.objects.new("Camera", camera_data)
            bpy.context.collection.objects.link(camera)
            bpy.context.scene.camera = camera

            camera.location = (0, 0, 0)
            camera.rotation_euler = (math.radians(90), 0, 0)
            camera.data.lens_unit = 'MILLIMETERS'
            camera.data.sensor_width = 36.0
            camera.data.sensor_height = 24.0 # 24mm
            camera.data.lens = 20.78 # fov = 60
            camera.data.sensor_fit = 'VERTICAL'
            
            # Setup new light ----------------------------------------------------
            
            light_data = bpy.data.lights.new(name="SunLight", type='SUN')
            # Light is rotated with theta = 30° and phi = 50° in spherical coordinates
            # We need to convert it to cartesian coordinates
            # theta = 30° = 0.5235987756 rad
            # phi = 50° = 0.872664625 rad
            theta = math.radians(30) - math.pi/2 # -90° to look towards the object
            phi = math.radians(50)
            r = 1 # distance from the light to the object
            x = r * math.cos(theta) * math.cos(phi)
            y = r * math.sin(theta) * math.cos(phi)
            z = r * math.sin(phi)

            light_data.energy = 7
            light_data.angle = 0
            light_data.use_shadow = False


            light = bpy.data.objects.new(name="SunLight", object_data=light_data)   
            light.location = obj.location + Vector((x, y, z))
            bpy.context.collection.objects.link(light)
            constraint_light = light.constraints.new(type='TRACK_TO')
            constraint_light.target = obj
            
            # Place the camera as in Unity's default settings
            # (0, 0, 0)
            # Looking towards Unity +Z (= Blender +Y) while default Blender camera is looking towards Blender -Z (Unity -Y)
            cam_pos, light_pos = positions.generate_positions(4, postype="yfixed")

            # debug : we render the scene with the default camera and light
            bpy.ops.render.render(write_still=True)
    return
if __name__ == "__main__":
    main()
    bpy.ops.wm.quit_blender() 