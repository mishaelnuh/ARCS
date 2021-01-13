import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd
import math

def geodesics(surf, extended_surf, plane, num_geo):
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