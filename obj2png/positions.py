import math

from mathutils import Vector

# def normalize(v):
#     v = np.array(v)
#     return v / np.linalg.norm(v)

def generate_positions(N, postype = "fibonacci", ypos = 0):
    # Sampling de fibonnaci
    # theta_light et phi_light sont les angles en degrés de la lumière par rapport à la caméra sur la sphère centrée sur l'objet
    cameraPositions = []
    if postype == "fibonacci":
        N = N + 2
        for i in range(N):
            t = (1 + math.sqrt(5)) / 2
            theta = 2 * math.pi * i / t 
            phi = math.asin(1 - 2 * i / N)
            x = math.cos(theta) * math.cos(phi)
            y = math.sin(theta) * math.cos(phi)
            z = math.sin(phi)
            cameraPositions.append((x, y, z))
        # On supprime le premier et le dernier point car on n'a pas besoin des vues de dessus et dessous
        cameraPositions = cameraPositions[1:-1]
    elif postype == "yfixed":
        # Select Y from -1 to 1 : it si the vertical axis on the sphere
        
        costheta = math.cos(ypos * math.pi / 2)
        # multiply by costheta to get the radius of the circle on the sphere.
        z = math.copysign(1,ypos) * (1 - costheta**2) # Don't lose the sign of z
        # z = math.sin(math.asin(z) + phi_light)

        for i in range(N):
            phi = 2 * math.pi * i / N
            # Distance needs to be 1.
            x = math.cos(phi) * abs(costheta)
            y = math.sin(phi) * abs(costheta)
            # z = sqrt(1 - x^2 - y^2)
            cameraPositions.append((x, y, z))
    elif postype == "polyedric":
        # Il n'existe que 5 polyèdres réguliers convexes : 
        # - Tétraèdre = 4 Sommets
        # - Octaèdre = 6 Sommets
        # - Cube = 8 Sommets
        # - Icosaèdre = 12 Sommets
        # - Dodécaèdre = 20 Sommets
        # Attention à normer les positions pour qu'elles soient sur la sphère unité
        if N == 4:  
            # Tétraèdre
            cameraPositions = [
                ( 1,                          1,                          1),
                (-1,                         -1,                          1),
                (-1,                          1,                         -1),
                ( 1,                         -1,                         -1)
            ]
        elif N == 6:
            # Octaèdre
            cameraPositions = [
                ( 0,                          0,                          1),
                ( 1,                          0,                          0),
                ( 0,                          1,                          0),
                (-1,                          0,                          0),
                ( 0,                         -1,                          0),
                ( 0,                          0,                         -1)
            ]
        elif N == 8:
            # Cube
            cameraPositions = [
                ( 1,  1,  1),   # A
                (-1,  1,  1),   # D
                (-1, -1,  1),   # C
                ( 1, -1,  1),   # B
                ( 1, -1, -1),   # G
                ( 1,  1, -1),   # H
                (-1,  1, -1),   # E
                (-1, -1, -1)   # F
            ]
        elif N == 12:
            # Icosaèdre
            phi = (1 + math.sqrt(5)) / 2
            cameraPositions = [
                (   0,    1,   phi), #1
                (   0 ,  -1,   phi), #2
                ( phi,    0,    1),  #5
                (-phi,    0,    1),  #11
                (  -1, -phi,    0),  #10
                (   1, -phi,    0),  #9
                (   1,  phi,    0),  #3
                (  -1,  phi,    0),  #4
                (-phi,    0,   -1),  #12
                ( phi,    0,   -1),  #6
                (   0,    1, -phi),  #7
                (   0,   -1, -phi)   #8
            ]
        elif N == 20:
            # Dodécaèdre
            phi = (1 + math.sqrt(5)) / 2
            cameraPositions = [
                (   0,   -1,  phi), #10
                (   0,    1,  phi), #9
                (  -1,    1,    1), #7
                (-phi,    0,    1), #19
                (  -1,   -1,    1), #2
                (   1,   -1,    1), #8
                ( phi,    0,    1), #13
                (   1,    1,    1), #1
                (   1,  phi,    0), #11
                (  -1,  phi,    0), #12
                (  -1, -phi,    0), #18
                (   1, -phi,    0), #17 
                (   1,   -1,   -1), #4
                ( phi,    0,   -1), #14
                (   1,    1,   -1), #5
                (  -1,    1,   -1), #3
                (-phi,    0,   -1), #20
                (  -1,   -1,   -1), #6
                (   0,   -1, -phi), #16
                (   0,    1, -phi) #15
            ]
        else:
            print("Nombre de vues non supporté. Les nombres de vues supportés sont 4, 6, 8, 12 et 20. Vous avez entré N = ", N)
            return None
    print("Positions générées")
    return cameraPositions


def fibonacci_sphere(samples=1000):

    points = []
    phi = math.pi * (math.sqrt(5.) - 1.)  # golden angle in radians

    for i in range(samples):
        y = 1 - (i / float(samples - 1)) * 2  # y goes from 1 to -1
        radius = math.sqrt(1 - y * y)  # radius at y

        theta = phi * i  # golden angle increment

        x = math.cos(theta) * radius
        z = math.sin(theta) * radius

        points.append((x, y, z))

    return points

# Example usage
if __name__ == "__main__":
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d import Axes3D
    import numpy as np
    import matplotlib.cm as cm
    
    N = 100
    positions = generate_positions(N, postype="fibonacci")
    points = fibonacci_sphere(N)
    fig = plt.figure()
    ax = fig.add_subplot(111, projection='3d')
    # Forcer des limites identiques sur les 3 axes
    ax.set_xlim(-1, 1)
    ax.set_ylim(-1, 1)
    ax.set_zlim(-1, 1)

    # Et imposer un rapport d'aspect 1:1:1 (Matplotlib ≥ 3.3)
    ax.set_box_aspect((1, 1, 1))

    # Tracé d'une sphère unité
    u = np.linspace(0, 2*np.pi, 60)
    v = np.linspace(0, np.pi, 30)
    X = np.cos(u)[:,None]*np.sin(v)[None,:]
    Y = np.sin(u)[:,None]*np.sin(v)[None,:]
    Z = np.cos(v)[None,:]
    for pos in positions:
        ax.scatter(*pos, color='blue', marker='o', s=50)  # Caméra en bleu
    ax.plot_surface(X, Y, Z, alpha=1, linewidth=0, color='white')
    # Affichage des caméras et lumières

    # Réglages finaux
    ax.set_xlabel('X')
    ax.set_ylabel('Y')
    ax.set_zlabel('Z')
    ax.view_init(elev=30, azim=45)

    # plt.tight_layout()
    # plt.show()
    #################################################################
    # pts = generate_positions(100, postype="fibonacci")
    # pts = np.array(pts)

    # # Projection simple : garder X,Y
    # plt.figure(figsize=(5,5))
    # plt.scatter(pts[:,0], pts[:,1], s=30, c="black")
    # plt.axis("equal")
    # plt.axis("off")
    # plt.savefig("fibonacci_points.svg")
    # import csv

    # with open("fibonacci_positions.csv", mode="w", newline="") as f:
    #     writer = csv.writer(f)
    #     writer.writerow(["x", "y", "z"])  # en-tête
    #     writer.writerows(pts)
    ################################################################