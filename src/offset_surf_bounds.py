import Rhino.Geometry as rg
import Rhino.RhinoDoc as rd

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
    perim_length = perim.GetLength()

    # Offset curve
    bounds = perim.OffsetOnSurface(extended_surf, dist, tol)[0]

    # If the offset curve is shorter than surface perimeter, offset the other way
    if (bounds.GetLength() < perim_length):
        bounds = perim.OffsetOnSurface(extended_surf, -dist, tol)[0]
        
    return bounds