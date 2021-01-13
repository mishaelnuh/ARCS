import Rhino.Geometry as rg

def connect_curves_bounds(curves, bounds):
    '''Connects curves using sub curves of the bounds.

    Parameters:
        curves (List[Curve]): Curves to connect.
        bounds (Curve): Closed boundary curve to use as connectors.

    Returns:
        connected_curve (Curve): Connected curve.
    '''
    curve_parts = []

    for i in range(len(curves) - 1):
        curve_parts.append(curves[i])
        p_0 = curves[i].PointAtNormalizedLength(1.0)
        p_1 = curves[i + 1].PointAtNormalizedLength(0.0)
        t_0 = bounds.ClosestPoint(p_0)[1]
        t_1 = bounds.ClosestPoint(p_1)[1]
        connector = shortest_subcurve(bounds, t_0, t_1)
        c_0 = rg.LineCurve(p_0, connector.PointAtNormalizedLength(0.0))
        c_1 = rg.LineCurve(connector.PointAtNormalizedLength(1.0), p_1)
        curve_parts.extend([c_0, connector, c_1])
    
    curve_parts.append(curves[-1])

    connected_curve = rg.Curve.JoinCurves(curve_parts)[0]
    
    return connected_curve

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