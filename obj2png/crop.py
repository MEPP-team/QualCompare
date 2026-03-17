import os
import cv2
import numpy as np

def crop_image(image_path, output_path, background_color = None, tolerance=30):
    # Lire l'image en conservant les couleurs
    image = cv2.imread(image_path, cv2.IMREAD_UNCHANGED)
    # Les images sont prises telles que ce sera toujours vérifié qu'importe la géométrie de l'objet
    background_color = image[0, 0] if background_color is None else background_color
    # print(f"La couleur de fond est {background_color}") # renvoit [71 71 71 255]
    # L'image n'est pas transparente
    diff = cv2.absdiff(image[:, :, :3], np.array(background_color, dtype=np.uint8))
    mask = np.any(diff > tolerance, axis=-1)

    # Trouver les coordonnées de la bounding box
    coords = cv2.findNonZero(mask.astype(np.uint8))

    if coords is not None:
        x, y, w, h = cv2.boundingRect(coords)

        # Recadrer l'image selon la bounding box
        cropped_image = image[y:y+h, x:x+w]

        # Sauvegarder l'image croppée
        cv2.imwrite(output_path, cropped_image)
        print(f"Image croppée enregistrée : {output_path}")
    else:
        print(f"Aucun objet détecté dans l'image {image_path}")


def crop_images_in_directory(directory, background_color=None):
    for filename in os.listdir(directory):
        if filename.endswith(".png"):
            image_path = os.path.join(directory, filename)
            output_path = os.path.join(directory, f"cropped_{filename}")
            crop_image(image_path, output_path, background_color)

# Exemple
