import Rhino.Geometry as rg
import math
import extend_surf
import geodesics
import ortho_geodesics
import filter_lines_dist_surf
import offset_surf_bounds
import trim_curve_boundary
import connect_curves_bounds
connect_curves_bounds = reload(connect_curves_bounds)

def spray_path(surf, angle, dist, overspray_dist):
    '''Generates spray path.

    Parameters:
        surf (Brep): Surface to spray. Input as Brep in order to maintain trims.
        angle (float): Angle to generate paths at in radians.
        dist (float): distance between path lines.
        overspray_dist (float): length to extend path lines past surface bounds.

    Returns:
        path (Curve): Spray path.
    '''
    
    # Define planes and 90 deg rotated plane
    plane = rg.Plane.WorldYZ
    plane.Rotate(angle, rg.Vector3d(0, 0, 1))

    # Get extended surface
    extended_surf = extend_surf.extend_surf(surf, plane)

    # Find geodesics
    geodesics_curves = geodesics.geodesics(surf, extended_surf, plane, 10)
    
    # Calculate ortho geodesic isolines
    path = ortho_geodesics.ortho_geodesics(geodesics_curves, dist)

    # Filter curves
    path = filter_lines_dist_surf.filter_lines_dist_surf(path, surf, dist / 2)

    # Trim paths
    bounds = offset_surf_bounds.offset_surf_bounds(surf, extended_surf, overspray_dist)

    path = [trim_curve_boundary.trim_curve_boundary(p, bounds)[0] for p in path]

    # Flatten lists
    path = [item for sublist in path for item in sublist]

    # Connect paths together through the bounds
    for i in range(1, len(path), 2):
        path[i].Reverse()
    
    path = connect_curves_bounds.connect_curves_bounds(path, bounds)

    return path