import Rhino.Geometry as rg
import clr
from System import Type

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
        else:
            raise Exception('Only curves and points supported.')
        
        if (clr.GetClrType(rg.Curve).IsAssignableFrom(geometries[i + 1].GetType())):
            p_1 = geometries[i + 1].PointAtStart
        elif (geometries[i + 1].GetType() == rg.Point):
            p_1 = geometries[i + 1].Location
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
    c_0 = get_subcurve(curve, t_0, t_1)
    c_1 = get_subcurve(curve, t_1, t_0)
    c_1.Reverse()
    
    if (c_0.GetLength() < c_1.GetLength()):
        return c_0
    else:
        return c_1

def get_subcurve(curve, t_0, t_1):
    if (t_0 < t_1):
        return curve.Trim(t_0, t_1)
    else:
        domain = curve.Domain
        c_0 = curve.Trim(t_0, domain.T1)
        c_1 = curve.Trim(domain.T0, t_1)
        joined = rg.Curve.JoinCurves([c_0, c_1])[0]
        return joined