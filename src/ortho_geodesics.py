import Rhino.Geometry as rg

def ortho_geodesics(geodesics, dist):
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