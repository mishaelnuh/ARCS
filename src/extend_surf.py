import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd
import math
from System import Array

def extend_surf(surf, plane):
    '''Extends a surface using consecutive bounding boxes.

    Parameters:
        surf (Brep): Surface to extend. Input as Brep in order to maintain trims.
        plane (Plane): Plane to orient extended surface borders to.

    Returns:
        extended_surf (Brep): Extended surface.
    '''
    tol = rd.ActiveDoc.ModelAbsoluteTolerance

    # Get rotated plane to 45 deg
    rotated_plane = plane.Clone()
    rotated_plane.Rotate(math.pi / 4, rg.Vector3d(0, 0, 1))

    # Calculate large boundary to extend to using consecutive bounding boxes
    bounds = rg.Box(plane, surf.GetBoundingBox(plane))
    bounds = rg.Box(rotated_plane, bounds.GetCorners())
    bounds = rg.Box(plane, bounds.GetCorners())
    bounds_corners = bounds.GetCorners()

    # Find a boundary polyline curve to extend to
    extended_outline = rg.Polyline([bounds_corners[i] for i in (0, 4, 5, 1, 0)])
    extended_outline = extended_outline.ToPolylineCurve()

    # Extend surface using Brep patch
    extended_surf = rg.Brep.CreatePatch(Array[rg.GeometryBase]([extended_outline,
        rg.Mesh.CreateFromBrep(surf)[0]]), None, 20, 20, False, True,
        extended_outline.GetLength() / 100, 50, 10, 
        Array[bool]([True, True, True, True]), tol)

    return extended_surf