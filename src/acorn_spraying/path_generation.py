import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd
import math

from acorn_spraying.misc import extend_surf, connect_geometries_bounds, offset_surf_bounds, filter_lines_dist_surf, trim_curve_boundary

def connect_paths_through_bounds(geometries, surf, extended_surf, overspray_dist):
    '''Connects spray paths and other points of interests through connectors
    made from a curve offset from the surface edges. This ensures that the
    path connectors do not go over the surface itself.

    Parameters:
        geometries (List[GeometryBase]): Geometries to connect. Only curves and points.
        surf (Brep): Surface to get bounds from.
        extend_surf (Surface): Extended surface.
        overspray_dist (float): Distance to offset edge curve by.

    Returns:
        connected_path (Curve): Connected curve.
    '''
    bounds = offset_surf_bounds(surf, extended_surf, overspray_dist)
    return connect_geometries_bounds(geometries, bounds)


def spray_path(surf, extended_surf, angle, dist, overspray_dist):
    '''Generates spray path.

    Parameters:
        surf (Brep): Surface to spray. Input as Brep in order to maintain trims.
        extended_surf (Surface): Extended surface. Use extend_surf or untrim the Brep.
        angle (float): Angle to generate geodesics at in radians.
        dist (float): Distance between path lines.
        overspray_dist (float): Length to extend path lines past surface bounds.

    Returns:
        path (Curve): Spray path.
    '''
    tol = rd.ActiveDoc.ModelAbsoluteTolerance

    # Define planes and 90 deg rotated plane
    plane = rg.Plane.WorldYZ
    plane.Rotate(angle, rg.Vector3d(0, 0, 1))

    # Find geodesics
    geodesics_curves = geodesics(surf, extended_surf, plane, 10)
    
    # Calculate ortho geodesic isolines
    path = ortho_geodesics(geodesics_curves, dist)

    # Filter curves
    path = filter_lines_dist_surf(path, surf, dist / 2)

    # Pull paths to surface
    path = [extended_surf.Pullback(p, tol) for p in path]
    path = [extended_surf.Pushup(p, tol) for p in path]

    # Trim paths
    bounds = offset_surf_bounds(surf, extended_surf, overspray_dist)

    path = [trim_curve_boundary(p, bounds)[0] for p in path]

    # Flatten lists
    path = [item for sublist in path for item in sublist]

    # Connect paths together through the bounds
    for i in range(1, len(path), 2):
        path[i].Reverse()
    
    path = connect_geometries_bounds(path, bounds)

    return path

def geodesics(surf, extended_surf, plane, num_geo):
    '''Calculates geodesic curves covering the surface using the extended surface

    Parameters:
        surf (Brep): Surface to cover. Input as Brep in order to maintain trims.
        extended_surf (Surface): Extended surface used to form geodesics on.
        plane (Plane): Rhino plane to orient geodesics to.
        num_geo (int): Number of geodesics to generate.

    Returns:
        geodesics (List[Curve]): Geodesic curves.
    '''
    tol = rd.ActiveDoc.ModelAbsoluteTolerance
    
    ## Find rectangle to start geodesic with
    # Get rotated plane to 45 deg
    rotated_plane = plane.Clone()
    rotated_plane.Rotate(math.pi / 4, rg.Vector3d(0, 0, 1))

    # Calculate large boundary to extend to using consecutive bounding boxes
    bounds = rg.Box(plane, surf.GetBoundingBox(plane))
    bounds = rg.Box(rotated_plane, bounds.GetCorners())
    bounds = rg.Box(plane, bounds.GetCorners())
    bounds_corners = bounds.GetCorners()

    # Find a boundary polyline curve to extend to
    outline = rg.Polyline([bounds_corners[i] for i in (0, 4, 5, 1, 0)])
    geo_outline = rg.Curve.ProjectToBrep(outline.ToPolylineCurve(),
        extended_surf.ToBrep(), rg.Vector3d.ZAxis, tol)

    geo_outline = geo_outline[0].Explode()
    # Assume that we have 4 curves from the box
    # Take the first and opposite ones to form geodesics
    geo_outline = [geo_outline[0], geo_outline[2]]
            
    # Calculate geodesics
    geo_t = [g.DivideByCount(num_geo - 1, True) for g in geo_outline]
    geo_p = []
    for i in range(len(geo_outline)):
        geo_p.append([])
        for j in range(len(geo_t[i])):
            t = geo_outline[i].PointAt(geo_t[i][j])
            p = extended_surf.ClosestPoint(t)
            geo_p[i].append(rg.Point2d(p[1], p[2]))
    
    # Calculated geodesics
    geodesics = []
    
    for i in range(len(geo_p[0])):
        geodesics.append(
            extended_surf.ShortPath(geo_p[0][i], geo_p[1][-1-i], tol))

    return geodesics

def ortho_geodesics(geodesics, dist):
    '''Calculates isolines separated by a specified distance using geodesics.

    Parameters:
        geodesics (List[Curve]): Geodesic curves.
        dist (float): Distance between isolines.

    Returns:
        ortho_geodesics (List[Curve]): Equally spaced isolines generated.
    '''
    t_params = [g.DivideByLength(dist, False) for g in geodesics]
    points = []
    min_len = 0
    for i in range(len(geodesics)):
        points.append([])
        points[-1] = [geodesics[i].PointAt(t) for t in t_params[i]]
        if (i == 0):
            min_len = len(points[-1])
        else:
            min_len = min(min_len, len(points[-1]))
            
    ortho_geodesics = []
    for i in range(min_len):
        ortho_geodesics.append(rg.Polyline([p[i] for p in points]).ToPolylineCurve())

    return ortho_geodesics