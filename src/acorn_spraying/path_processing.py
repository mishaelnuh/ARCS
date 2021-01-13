import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd
import math

from acorn_spraying.misc import offset_surf_bounds, trim_curve_boundary

def split_path_on_off_surf(path, surf, extended_surf, spray_diameter):
    '''Split the path into parts and flag each segment as either spraying
    on or off the surface. Set spray_diameter to 0 to split paths at
    surface edge.

    Parameters:
        path (Curve): Spray path.
        surf (Brep): Surface to get bounds from.
        extend_surf (Surface): Extended surface.
        spray_diameter (float): Distance from which a point is considered
            not to be spraying on the surface.

    Returns:
        paths (List[Curve]): Split paths.
        on_surface (List[bool]): Flag to tell if path is spraying on
            or off the surface.
    '''

    bounds = offset_surf_bounds(surf, extended_surf, spray_diameter / 2)

    # Scale the bounds by 99.9% on X and Y direction so that paths that are 
    # on the boundary will count as being off.
    bounds = rg.Curve.ProjectToPlane(bounds, rg.Plane.WorldXY)
    centroid = rg.AreaMassProperties.Compute(bounds).Centroid
    bounds.Transform(rg.Transform.Scale(rg.Plane(centroid, rg.Vector3d.ZAxis),
        0.999, 0.999, 1))

    inside_curves, outside_curves = trim_curve_boundary(path, bounds)

    # Weave the two lists together.
    # Closest one to the path start is the first curve added
    point_start = path.PointAtStart
    dist_inside = min(inside_curves[0].PointAtStart.DistanceToSquared(point_start),
        inside_curves[0].PointAtEnd.DistanceToSquared(point_start))
    dist_outside = min(outside_curves[0].PointAtStart.DistanceToSquared(point_start),
        outside_curves[0].PointAtEnd.DistanceToSquared(point_start))
    
    paths = []
    on_surface = []
    true_list = [True] * len(inside_curves)
    false_list = [False] * len(outside_curves)
    if (dist_inside < dist_outside):
        paths = [a for b in zip(inside_curves, outside_curves) for a in b]
        on_surface = [a for b in zip(true_list, false_list) for a in b]
    else:
        paths = [a for b in zip(outside_curves, inside_curves) for a in b]
        on_surface = [a for b in zip(false_list, true_list) for a in b]

    # Add on any extra paths
    if (len(inside_curves) < len(outside_curves)):
        paths.extend(outside_curves[len(inside_curves):len(outside_curves)])
        on_surface.extend([False] * (len(outside_curves) - len(inside_curves)))
    elif (len(inside_curves) > len(outside_curves)):
        paths.extend(outside_curves[len(outside_curves):len(inside_curves)])
        on_surface.extend([True] * (len(inside_curves) - len(outside_curves)))

    return [paths, on_surface]


