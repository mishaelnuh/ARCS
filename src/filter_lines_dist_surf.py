import Rhino.Geometry as rg

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