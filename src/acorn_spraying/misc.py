import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd
import math
import clr
from System import Type, Array

def extend_surf(surf, plane):
    '''Extends a surface using consecutive bounding boxes.

    Parameters:
        surf (Brep): Surface to extend. Input as Brep in order to maintain trims.
        plane (Plane): Plane to orient extended surface borders to.

    Returns:
        extended_surf (Surface): Extended surface.
    '''
    tol = rd.ActiveDoc.ModelAbsoluteTolerance

    # Get rotated plane to 45 deg
    rotated_plane = plane.Clone()
    rotated_plane.Rotate(math.pi / 4, rg.Vector3d(0, 0, 1))

    # Calculate large boundary to extend to using consecutive bounding boxes
    bounds = rg.Box(plane, surf.GetBoundingBox(plane))
    bounds = rg.Box(rotated_plane, bounds.GetCorners())
    bounds = rg.Box(plane, bounds.GetCorners())
    bounds = rg.Box(rotated_plane, bounds.GetCorners())
    bounds = rg.Box(plane, bounds.GetCorners())
    bounds_corners = bounds.GetCorners()

    # Find a boundary polyline curve to extend to
    extended_outline = rg.Polyline([bounds_corners[i] for i in (0, 4, 5, 1, 0)])
    extended_outline = extended_outline.ToPolylineCurve()

    # Populate the Brep with 10000 points
    objects = []
    bound_box = surf.GetBoundingBox(False)

    for i in range(100):
        for j in range(100):
            x = bound_box.Min.X + (bound_box.Max.X - bound_box.Min.X) / 99 * i
            y = bound_box.Min.Y + (bound_box.Max.Y - bound_box.Min.Y) / 99 * j
            z = (bound_box.Min.Z + bound_box.Max.Z) / 2
            point = rg.Point3d(x, y, z)
            point = surf.ClosestPoint(point)
            objects.append(rg.Point(point))
    
    # Extend surface using Brep patch
    objects.append(extended_outline)
    extended_surf = rg.Brep.CreatePatch(Array[rg.GeometryBase](objects),
        None, 20, 20, False, True,
        extended_outline.GetLength() / 100, 50, 10, 
        Array[bool]([True, True, True, True]), tol)
    extended_surf = extended_surf.Surfaces[0]
    
    return extended_surf

def offset_surf_bounds(surf, extended_surf, dist):
    '''Offset the surface boundary outwards by a specified distance.

    Parameters:
        surf (Brep): Surface to offset border from. Input as Brep in order to maintain trims.
        extended_surf (Surface): Surface to extend border on.
        dist (float): Distance to offset by.

    Returns:
        bounds (Curve): Offset surface bound curve.
    '''
    tol = rd.ActiveDoc.ModelAbsoluteTolerance

    perim = rg.Curve.JoinCurves(surf.GetWireframe(-1), tol)[0]

    if (dist == 0):
        return perim
    
    perim_length = perim.GetLength()

    # Offset curve
    bounds = perim.OffsetOnSurface(extended_surf, dist, tol)[0]

    # If the offset curve is shorter than surface perimeter, offset the other way
    if (dist > 0 and bounds.GetLength() < perim_length):
        bounds = perim.OffsetOnSurface(extended_surf, -dist, tol)[0]
    elif (dist < 0 and bounds.GetLength() > perim_length):
        bounds = perim.OffsetOnSurface(extended_surf, -dist, tol)[0]
        
    return bounds

def filter_lines_dist_surf(curves, surf, dist):
    '''Filters curves to be within a specified distance to surface.

    Parameters:
        curves (List[Curve]): Curves to filter.
        surf (Brep): Surface to measure to.
        dist (float): Max allowable distance from curve to surface.

    Returns:
        filtered_curves (List[Curve]): Filtered curves.
    '''

    filtered_curves = []

    for c in curves:
        points = c.DivideByCount(100, True)
        points = [c.PointAt(p) for p in points]
        surf_points = [surf.ClosestPoint(p) for p in points]
        d = list(map(lambda p1, p2: p1.DistanceTo(p2), points, surf_points))
        if (min(d) < dist):
            filtered_curves.append(c)
    
    return filtered_curves

def trim_curve_boundary(curve, bounds):
    '''Trim curve using a closed boundary curve.

    Parameters:
        curve (Curve): Curve to trim.
        bounds (Curve): Closed curve to trim curve with.

    Returns:
        inside_curves (List[Curve]): Curves inside the bounds.
        outside_curves (List[Curve]): Curves outside the bounds.
    '''
    tol = rd.ActiveDoc.ModelAbsoluteTolerance
    tol_angle = rd.ActiveDoc.ModelAngleToleranceRadians
    tol_z = rg.Vector3d(0, 0, tol)

    # Get an extrusion from the bounds to use as intersection Brep
    curve_bounds = curve.GetBoundingBox(False)
    base_plane = rg.Plane(curve_bounds.Min - 2 * tol_z, rg.Vector3d.ZAxis)
    projected_bounds = rg.Curve.ProjectToPlane(bounds, base_plane)
    extrude_dir = rg.Vector3d(0, 0, curve_bounds.Max.Z - curve_bounds.Min.Z + 4 * tol)
    extrusion = rg.Surface.CreateExtrusion(projected_bounds, extrude_dir)
    extrusion = extrusion.ToBrep().CapPlanarHoles(tol)

    # Get intersection parameters
    intersections = [curve.Domain.Min]
    intersections.extend(rg.Intersect.Intersection.CurveBrep(
        curve, extrusion, tol, tol_angle)[1])
    intersections.append(curve.Domain.Max)

    # Determine if inside or outside extrusion
    inside_curves = []
    outside_curves = []
    for i in range(len(intersections) - 1):
        c = curve.Trim(intersections[i], intersections[i + 1])
        if (extrusion.IsPointInside(c.PointAtNormalizedLength(0.5), tol, True)):
            inside_curves.append(c)
        else:
            outside_curves.append(c)
            
    # Do check with a point outside extrusion and flip inside and outside depending on result
    check_point = rg.Point3d(curve_bounds.Min.X - 100, curve_bounds.Min.Y - 100, curve_bounds.Min.Z - 100)
    if (extrusion.IsPointInside(check_point, tol, True)):
        tmp = outside_curves
        outside_curves = inside_curves
        inside_curves = tmp

    return [inside_curves, outside_curves]

def connect_geometries_bounds(geometries, bounds):
    '''Connects geometries using sub curves of the bounds.

    Parameters:
        geometries (List[GeometryBase]): Geometries to connect. Only curves and points.
        bounds (Curve): Closed boundary curve to use as connectors.

    Returns:
        connected_geometries (Curve): Connected curve.
    '''
    connected_geometries = rg.PolyCurve()

    for i in range(len(geometries) - 1):
        p_0 = None
        p_1 = None
        
        if (clr.GetClrType(rg.Curve).IsAssignableFrom(geometries[i].GetType())):
            connected_geometries.Append(geometries[i])
            p_0 = geometries[i].PointAtEnd
        elif (geometries[i].GetType() == rg.Point):
            p_0 = geometries[i].Location
        elif (geometries[i].GetType() == rg.Point3d):
            p_0 = geometries[i]
        else:
            raise Exception('Only curves and points supported.')
        
        if (clr.GetClrType(rg.Curve).IsAssignableFrom(geometries[i + 1].GetType())):
            p_1 = geometries[i + 1].PointAtStart
        elif (geometries[i + 1].GetType() == rg.Point):
            p_1 = geometries[i + 1].Location
        elif (geometries[i + 1].GetType() == rg.Point3d):
            p_1 = geometries[i + 1]
        else:
            raise Exception('Only curves and points supported.')
        
        t_0 = bounds.ClosestPoint(p_0)[1]
        t_1 = bounds.ClosestPoint(p_1)[1]
        connector = shortest_subcurve(bounds, t_0, t_1)
        connected_geometries.Append(rg.Line(p_0, connector.PointAtStart))
        connected_geometries.Append(connector)
        connected_geometries.Append(rg.Line(connector.PointAtEnd, p_1))
    
    if (clr.GetClrType(rg.Curve).IsAssignableFrom(geometries[-1].GetType())):
        connected_geometries.Append(geometries[-1])

    return connected_geometries 

def shortest_subcurve(curve, t_0, t_1):
    '''Finds shortest subcurve in closed curve between parameters.

    Parameters:
        curve (Curve): Closed curve.
        t_0 (float): Start parameter.
        t_1 (float): End parameter.

    Returns:
        c (Curve): Subcurve.
    '''
    c_0 = get_subcurve(curve, t_0, t_1)
    c_1 = get_subcurve(curve, t_1, t_0)
    c_1.Reverse()
    
    if (c_0.GetLength() < c_1.GetLength()):
        return c_0
    else:
        return c_1

def get_subcurve(curve, t_0, t_1):
    '''Finds subcurve between the two parameters.

    Parameters:
        curve (Curve): Closed curve.
        t_0 (float): Start parameter.
        t_1 (float): End parameter.

    Returns:
        c (Curve): Subcurve.
    '''
    if (t_0 < t_1):
        return curve.Trim(t_0, t_1)
    else:
        domain = curve.Domain
        c_0 = curve.Trim(t_0, domain.T1)
        c_1 = curve.Trim(domain.T0, t_1)
        joined = rg.Curve.JoinCurves([c_0, c_1])[0]
        return joined

def polyline_centroid(polyline):
    '''Calculates the area and centroid of a polyline.

    Parameters:
        polyline (Polyline): Closed polyline.

    Returns:
        area (float): Area of polyline.
        centroid (Point3D): Centroid of polyline.
    '''

    # Explode to lines
    exploded_outline = polyline.DuplicateSegments()

    # Find centroid
    area = 0
    centroid_x = 0
    centroid_y = 0

    # Algorithm only works when all values are positive so perform calc on
    # translated vertices then translate back
    bounds = polyline.GetBoundingBox(rg.Plane.WorldXY)

    for i in range(len(exploded_outline) - 1):
        p1 = exploded_outline[i].PointAtStart - bounds.Min
        p2 = exploded_outline[i + 1].PointAtStart - bounds.Min
        tmp = p1.X * p2.Y - p2.X * p1.Y
        area += tmp
        centroid_x += (p1.X + p2.X) * tmp
        centroid_y += (p1.Y + p2.Y) * tmp
    
    # Flip signs if the polyline was oriented counterclockwise
    if (area < 0):
        area *= -1
        centroid_x *= -1
        centroid_y *= -1
        
    area /= 2
    centroid_x /= 6 * area
    centroid_y /= 6 * area
    centroid = rg.Point3d(centroid_x, centroid_y, 0) + bounds.Min

    return [area, centroid]