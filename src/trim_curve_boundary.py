import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd

def trim_curve_boundary(curve, bounds):
    '''Extends a surface using consecutive bounding boxes.

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