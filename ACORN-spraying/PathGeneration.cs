using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;
using static ACORNSpraying.Miscellaneous;

namespace ACORNSpraying
{
    public static class PathGeneration
    {
        public static List<Curve> SprayInnerPaths(Brep surf, Surface extSurf, double dist, double expandDist, int numGeo, List<int> sourceEdges,
            out List<List<Curve>> segments, out List<List<bool>> isConnector, out List<Curve> boundaryEdges)
        {
            segments = new List<List<Curve>>();
            isConnector = new List<List<bool>>();
            boundaryEdges = new List<Curve>();

            // Find direction of spray paths to be used
            var boundary = OffsetSurfBoundary(surf, extSurf, expandDist);
            List<Curve> edges = new List<Curve>();
            if (boundary is PolylineCurve)
            {
                edges = (boundary as PolylineCurve).DuplicateSegments().ToList();
            }
            else if (boundary is PolyCurve)
            {
                edges = (boundary as PolyCurve).Explode().ToList();
            }

            // Ignore corners from offset surf boundary
            int skipOffset = 0;
            if (expandDist > 0)
            {
                var perim = surf.Boundary();

                if (perim == null)
                    throw new Exception("Unable to get Brep boundary.");

                var testPoint = perim.PointAtStart;
                var midPoints = new PointCloud(edges.Select(e => e.PointAtNormalizedLength(0.5)));
                var closestIndex = midPoints.ClosestPoint(testPoint);
                if (closestIndex % 2 == 1)
                    skipOffset = 1;
            }

            var angles = edges
                .Select(e =>
                {
                    var v = e.PointAtEnd - e.PointAtStart;
                    v.Z = 0;
                    if (v.Y == 0)
                    {
                        if (v.X > 0)
                            return 0;
                        else
                            return Math.PI;
                    }
                    else
                    {
                        var angle = Vector3d.VectorAngle(v, Vector3d.XAxis);
                        if (v.Y > 0)
                            return angle;
                        else
                            return 2 * Math.PI - angle;
                    }
                })
                .ToList();

            var paths = new List<Curve>();
            var edgeCounter = -1;
            for (int i = 0; i < angles.Count; i++)
            {
                if (expandDist > 0 && (i + skipOffset) % 2 == 0)
                    continue;

                edgeCounter++;

                if (sourceEdges.Count > 0 && !sourceEdges.Contains(edgeCounter))
                    continue;

                // Define planes and 90 deg rotated plane
                var plane = Plane.WorldYZ;
                plane.Rotate(angles[i], Vector3d.ZAxis);

                // Find geodesics
                var geodesicsCurves = Geodesics(surf, extSurf, plane, numGeo);

                // Calculate ortho geodesic isolines
                var path = OrthoGeodesics(geodesicsCurves, edges[i], dist);

                // Filter curves
                path = FilterCurvesByDistToSurf(path, surf, dist / 2 + Math.Abs(expandDist));

                // Pull paths to surface and trim
                path = path
                    .Select(p => extSurf.Pullback(p, ToleranceDistance))
                    .Select(p => extSurf.Pushup(p, ToleranceDistance))
                    .SelectMany(p =>
                    {
                        List<Curve> insideCurves;
                        TrimCurveBoundary(p, boundary, out insideCurves, out _);
                        return insideCurves;
                    })
                    .ToList();

                // Align path order
                Point3d pT1, pT2;
                edges[i].ClosestPoints(new List<GeometryBase>() { path[0] }, out pT1, out pT2, out _);
                var dist1 = pT1.DistanceToSquared(pT2);
                edges[i].ClosestPoints(new List<GeometryBase>() { path.Last() }, out pT1, out pT2, out _);
                var dist2 = pT1.DistanceToSquared(pT2);

                if (dist2 < dist1)
                    path.Reverse();

                // Replace the curve closest to the edge with the edge itself
                var flag = true;
                do
                {
                    int matchIndex;
                    Point3d p1, p2;
                    edges[i].ClosestPoints(path, out p1, out p2, out matchIndex);
                    if (p1.DistanceToSquared(p2) < dist * dist / 4)
                        path.RemoveAt(matchIndex);
                    else
                        flag = false;
                } while (flag);

                // Insert edge curve
                path.Insert(0, edges[i]);

                if (path[0].PointAtStart.DistanceToSquared(path[1].PointAtStart) > path[0].PointAtStart.DistanceToSquared(path[1].PointAtEnd))
                    path[0].Reverse();

                // Connect paths together through the bounds
                for (int j = 0; j < path.Count; j += 2)
                    path[j].Reverse();

                List<Curve> tmp1;
                List<bool> tmp2;
                var connectedPath = ConnectGeometriesThroughBoundary(
                    path.Cast<GeometryBase>().ToList(),
                    Enumerable.Repeat(false, path.Count).ToList(),
                    boundary,
                    false,
                    out tmp1, out tmp2);

                segments.Add(tmp1);
                isConnector.Add(tmp2);
                paths.Add(connectedPath);

                boundaryEdges.Add(edges[i]);
            }

            return paths;
        }

        public static Curve SprayEdgePath(Brep surf, Surface extSurf, Point3d startP, double expandDist)
        {
            var boundary = OffsetSurfBoundary(surf, extSurf, expandDist);

            double t;

            // Go to corner points from boundary
            if (Math.Abs(expandDist) > ToleranceDistance)
            {
                var actualBoundary = surf.Boundary();
                List<Point3d> cornerPoints = new List<Point3d>();
                if (actualBoundary is PolylineCurve)
                {
                    cornerPoints = (actualBoundary as PolylineCurve).DuplicateSegments().Select(s => s.PointAtStart).ToList();

                }
                else if (boundary is PolyCurve)
                {
                    cornerPoints = (actualBoundary as PolyCurve).Explode().Select(s => s.PointAtStart).ToList();
                }

                foreach (var p in cornerPoints)
                {
                    boundary.ClosestPoint(p, out t);
                    var closestPoint = boundary.PointAt(t);
                    var newBoundary = new PolyCurve();
                    newBoundary.Append(boundary.Trim(boundary.Domain.Min, t));
                    newBoundary.Append(new LineCurve(closestPoint, p));
                    newBoundary.Append(new LineCurve(p, closestPoint));
                    newBoundary.Append(boundary.Trim(t, boundary.Domain.Max));
                    boundary = newBoundary;
                }
            }

            // Adjust seam
            boundary.ClosestPoint(startP, out t);
            boundary.ChangeClosedCurveSeam(t);

            return boundary;
        }

        /// <summary>
        /// Calculates geodesic curves covering the surface using the extended surface.
        /// </summary>
        /// <param name="surf">Surface to cover. Input as Brep in order to maintain trims.</param>
        /// <param name="extSurf">Extended surface used to form geodesics on.</param>
        /// <param name="plane">Rhino plane to orient geodesics to.</param>
        /// <param name="num">Number of geodesics to generate.</param>
        /// <returns>Geodesic curves.</returns>
        public static List<Curve> Geodesics(Brep surf, Surface extSurf, Plane plane, int num)
        {
            // Find rectangle to start geodesic with
            // Get rotated plane to 45 deg
            var rotatedPlane = plane.Clone();
            rotatedPlane.Rotate(Math.PI / 4, Vector3d.ZAxis);

            // Calculate large boundary to extend to using consecutive bounding boxes
            var bounds = new Box(plane, surf.GetBoundingBox(plane));
            bounds = new Box(rotatedPlane, bounds.GetCorners());
            bounds = new Box(plane, bounds.GetCorners());
            var boundCorners = bounds.GetCorners();

            // Find a boundary polyline curve to extend to
            var outline = new Polyline(new List<Point3d>()
            {
                boundCorners[0],
                boundCorners[4],
                boundCorners[5],
                boundCorners[1],
                boundCorners[0],
            }).ToPolylineCurve();
            var geodesicOutline = (Curve.ProjectToBrep(outline, extSurf.ToBrep(), Vector3d.ZAxis, ToleranceDistance)[0] as PolyCurve).Explode();

            // Assume that we have 4 curves from the box
            // Take the first and opposite ones to form geodesics
            geodesicOutline = new Curve[] { geodesicOutline[0], geodesicOutline[2] };

            // Calculate geodesic start and end points
            var geodesicParam = geodesicOutline.Select(o => o.DivideByCount(num - 1, true)).ToList();
            var geodesicStartPoints = geodesicParam[0]
                .Select(t =>
                {
                    var p = geodesicOutline[0].PointAt(t);
                    double u, v;
                    extSurf.ClosestPoint(p, out u, out v);
                    return new Point2d(u, v);
                })
                .ToList();
            var geodesicEndPoints = geodesicParam[1]
                .Select(t =>
                {
                    var p = geodesicOutline[1].PointAt(t);
                    double u, v;
                    extSurf.ClosestPoint(p, out u, out v);
                    return new Point2d(u, v);
                })
                .Reverse()
                .ToList();

            // Calculate geodesics
            var geodesics = new List<Curve>();
            for (int i = 0; i < geodesicStartPoints.Count; i++)
            {
                geodesics.Add(extSurf.ShortPath(geodesicStartPoints[i], geodesicEndPoints[i], ToleranceDistance));
            }

            return geodesics;
        }

        /// <summary>
        /// Calculates isolines separated by a specified distance using geodesics.
        /// </summary>
        /// <param name="geodesics">Geodesic curves.</param>
        /// <param name="dist">Distance between isolines.</param>
        /// <returns>Equally spaced isolines generated.</returns>
        public static List<Curve> OrthoGeodesics(List<Curve> geodesics, double dist)
        {
            var tParams = geodesics
                .Select(g => g.DivideByLength(dist, true))
                .ToList();

            for(int i = 0; i < tParams.Count; i++)
            {
                if (tParams[i] == null || tParams[i].Count() == 0)
                    tParams[i] = new double[] { geodesics[i].Domain.Min };
            }

            var points = new List<List<Point3d>>();
            var minLength = tParams.Select(l => l.Length).Min();

            for (int i = 0; i < geodesics.Count; i++)
            {
                points.Add(tParams[i].Select(t => geodesics[i].PointAt(t)).ToList());
            }

            var orthoGeodesics = new List<Curve>();

            for (int i = 0; i < minLength; i++)
            {
                orthoGeodesics.Add(new PolylineCurve(points.Select(p => p[i])) as Curve);
            }

            return orthoGeodesics;
        }

        /// <summary>
        /// Calculates isolines separated by a specified distance using geodesics and a guiding curve.
        /// </summary>
        /// <param name="geodesics">Geodesic curves.</param>
        /// <param name="guide">Guiding curve.</param>
        /// <param name="dist">Distance between isolines.</param>
        /// <returns>Equally spaced isolines generated.</returns>
        public static List<Curve> OrthoGeodesics(List<Curve> geodesics, Curve guide, double dist)
        {
            var geoBounds = new BoundingBox();
            foreach (var g in geodesics)
                geoBounds.Union(g.GetBoundingBox(false));

            guide = guide.Extend(CurveEnd.Both, geoBounds.Diagonal.Length / 2, CurveExtensionStyle.Line);

            var startParams = geodesics.Select(g =>
                {
                    Point3d p;
                    double t;
                    g.ClosestPoints(guide, out p, out _);
                    g.ClosestPoint(p, out t);
                    return t;
                })
                .ToList();

            var geodesicCut0 = new List<Curve>();
            var geodesicCut1 = new List<Curve>();

            for (int i = 0; i < startParams.Count; i++)
            {
                var curve0 = geodesics[i].Trim(geodesics[i].Domain.Min, startParams[i]);
                if (curve0 == null)
                    continue;
                var curve1 = geodesics[i].Trim(startParams[i], geodesics[i].Domain.Max);
                if (curve1 == null)
                    continue;
                geodesicCut0.Add(curve0);
                geodesicCut0[geodesicCut0.Count - 1].Reverse();
                geodesicCut1.Add(curve1);
            }

            var orthoCut0 = OrthoGeodesics(geodesicCut0, dist);
            var orthoCut1 = OrthoGeodesics(geodesicCut1, dist);

            orthoCut0.RemoveAt(0);
            orthoCut0.Reverse();
            orthoCut0.AddRange(orthoCut1);

            return orthoCut0;
        }

        /// <summary>
        /// Connects geometries using sub curves of the bounds.
        /// </summary>
        /// <param name="geometries">Geometries to connect. Only curves and points.</param>
        /// <param name="isGeometryConnector">Flag to show whether original geometry is a connector. Should be same length as the geometries list.</param>
        /// <param name="surf">Surface to offset border from. Input as Brep in order to maintain trims.</param>
        /// <param name="extSurf">Surface to extend border on.</param>
        /// <param name="dist">Distance to offset by.</param>
        /// <param name="maintainDir">Whether to maintain or flip curve directions.</param>
        /// <param name="segments">Curve segments exploded.</param>
        /// <param name="isConnectorSegment">Flags to show which segments are connectors.</param>
        /// <returns>Connected curve passing through geometries.</returns>
        public static Curve ConnectGeometriesThroughBoundary(List<GeometryBase> geometries, List<bool> isGeometryConnector,
            Brep surf, Surface extSurf, double dist,
            bool maintainDir, out List<Curve> segments, out List<bool> isConnectorSegment)
        {
            var boundary = OffsetSurfBoundary(surf, extSurf, dist);
            return ConnectGeometriesThroughBoundary(geometries, isGeometryConnector, boundary, maintainDir, out segments, out isConnectorSegment);
        }

        /// <summary>
        /// Connects geometries using sub curves of the bounds.
        /// </summary>
        /// <param name="geometries">Geometries to connect. Only curves and points.</param>
        /// <param name="isGeometryConnector">Flag to show whether original geometry is a connector. Should be same length as the geometries list.</param>
        /// <param name="boundary">Closed boundary curve to use as connectors.</param>
        /// <param name="maintainDir">Whether to maintain or flip curve directions.</param>
        /// <param name="segments">Curve segments exploded.</param>
        /// <param name="isConnectorSegment">Flags to show which segments are connectors.</param>
        /// <returns>Connected curve passing through geometries.</returns>
        public static Curve ConnectGeometriesThroughBoundary(List<GeometryBase> geometries, List<bool> isGeometryConnector,
            Curve boundary, bool maintainDir,
            out List<Curve> segments, out List<bool> isConnectorSegment)
        {
            isConnectorSegment = new List<bool>();
            segments = new List<Curve>();

            for (int i = 0; i < geometries.Count - 1; i++)
            {
                Point3d pEnd, pNext;

                if (typeof(Curve).IsAssignableFrom(geometries[i].GetType()))
                {
                    segments.Add(geometries[i] as Curve);
                    isConnectorSegment.Add(isGeometryConnector[i]);
                    pEnd = (geometries[i] as Curve).PointAtEnd;
                }
                else if (geometries[i].GetType() == typeof(Point))
                    pEnd = (geometries[i] as Point).Location;
                else
                    throw new Exception("Only curves and points supported.");

                if (typeof(Curve).IsAssignableFrom(geometries[i + 1].GetType()))
                {
                    if (maintainDir)
                    {
                        pNext = (geometries[i + 1] as Curve).PointAtStart;
                    }
                    else
                    {
                        var p1 = (geometries[i + 1] as Curve).PointAtStart;
                        var p2 = (geometries[i + 1] as Curve).PointAtEnd;

                        if (p1.DistanceToSquared(pEnd) > p2.DistanceToSquared(pEnd) && p1.DistanceToSquared(p2) > ToleranceDistance * ToleranceDistance)
                        {
                            (geometries[i + 1] as Curve).Reverse();
                            pNext = p2;
                        }
                        else
                        {
                            pNext = p1;
                        }
                    }
                }
                else if (geometries[i + 1].GetType() == typeof(Point))
                    pNext = (geometries[i + 1] as Point).Location;
                else
                    throw new Exception("Only curves and points supported.");

                if (pEnd.DistanceToSquared(pNext) < ToleranceDistance * ToleranceDistance)
                    continue;

                double boundaryParamEnd, boundaryParamNext;
                boundary.ClosestPoint(pEnd, out boundaryParamEnd);
                boundary.ClosestPoint(pNext, out boundaryParamNext);

                Curve connector = null;
                if (Math.Pow((pEnd-pNext).X, 2) + Math.Pow((pEnd - pNext).Y, 2) < ToleranceDistance * ToleranceDistance)
                {
                    connector = new LineCurve(pEnd, pNext);
                }
                else
                {
                    connector = ShortestSubcurve(boundary, boundaryParamEnd, boundaryParamNext);
                }

                if (pEnd.DistanceToSquared(connector.PointAtStart) > ToleranceDistance * ToleranceDistance)
                {
                    segments.Add(new LineCurve(pEnd, connector.PointAtStart));
                    isConnectorSegment.Add(true);
                }
                if (connector.GetLength() > ToleranceDistance)
                {
                    segments.Add(connector);
                    isConnectorSegment.Add(true);
                }
                if (pNext.DistanceToSquared(connector.PointAtEnd) > ToleranceDistance * ToleranceDistance)
                {
                    segments.Add(new LineCurve(connector.PointAtEnd, pNext));
                    isConnectorSegment.Add(true);
                }
            }

            if (typeof(Curve).IsAssignableFrom(geometries.Last().GetType()))
            {
                segments.Add(geometries.Last() as Curve);
                isConnectorSegment.Add(isGeometryConnector.Last());
            }

            var connectedGeometries = new PolyCurve();
            foreach(var s in segments)
                connectedGeometries.Append(s);

            return connectedGeometries;
        }

        /// <summary>
        /// Connect geometries starting from the first using closest neighbour consecutively.
        /// </summary>
        /// <param name="geometries">Geometries to connect. Only curves and points.</param>
        /// <param name="isGeometryConnector">Flag to show whether original geometry is a connector. Should be same length as the geometries list.</param>
        /// <param name="segments">Curve segments exploded.</param>
        /// <param name="isConnectorSegment">Flags to show which segments are connectors.</param>
        /// <returns></returns>
        public static Curve ConnectGeometries(List<GeometryBase> geometries, List<bool> isGeometryConnector,
            out List<Curve> segments, out List<bool> isConnectorSegment)
        {
            isConnectorSegment = new List<bool>();
            segments = new List<Curve>();

            // Create map of key points
            // Skip fist one
            var positions = new PointCloud();
            var indexedGeometries = new List<int>();

            for (int i = 1; i < geometries.Count; i++)
            {
                if (typeof(Curve).IsAssignableFrom(geometries[i].GetType()))
                {
                    var curve = (geometries[i] as Curve);
                    positions.Add(curve.PointAtStart);
                    indexedGeometries.Add(i);
                    // Track a reversed curve by using negative indexes
                    positions.Add(curve.PointAtEnd);
                    indexedGeometries.Add(-i);
                }
                else if (geometries[i].GetType() == typeof(Point))
                {
                    positions.Add((geometries[i] as Point).Location);
                    indexedGeometries.Add(i);
                }
                else
                    throw new Exception("Only curves and points supported.");
            }

            // Loop through and find all connections
            Point3d currentPosition = new Point3d();

               
            if (typeof(Curve).IsAssignableFrom(geometries[0].GetType()))
            {
                currentPosition = (geometries[0] as Curve).PointAtEnd;
                segments.Add((geometries[0] as Curve));
                isConnectorSegment.Add(isGeometryConnector[0]);
            }
            else if (geometries[0].GetType() == typeof(Point))
            {
                currentPosition = (geometries[0] as Point).Location;
            }

            while (indexedGeometries.Count > 0)
            {
                var closestIndex = positions.ClosestPoint(currentPosition);
                var index = indexedGeometries[closestIndex];

                Point3d nextPosition = new Point3d();
                Point3d nextStart = new Point3d();
                Curve nextCurve = null;

                if (typeof(Curve).IsAssignableFrom(geometries[Math.Abs(index)].GetType()))
                {
                    var curve = geometries[Math.Abs(index)] as Curve;
                    nextCurve = curve.DuplicateCurve();

                    if (index < 0)
                    {
                        nextPosition = curve.PointAtStart;
                        nextStart = curve.PointAtEnd;
                        nextCurve.Reverse();
                    }
                    else
                    {
                        nextPosition = curve.PointAtEnd;
                        nextStart = curve.PointAtStart;
                    }
                }
                else if (geometries[Math.Abs(index)].GetType() == typeof(Point))
                {
                    nextPosition = (geometries[Math.Abs(index)] as Point).Location;
                    nextStart = nextPosition;
                }

                // Add connector
                if (nextStart.DistanceToSquared(currentPosition) > ToleranceDistance * ToleranceDistance)
                {
                    segments.Add(new LineCurve(currentPosition, nextStart));
                    isConnectorSegment.Add(true);
                }

                if (nextCurve != null)
                {
                    // Add curve
                    segments.Add(nextCurve);
                    isConnectorSegment.Add(isGeometryConnector[Math.Abs(index)]);

                    // Remove curves from map
                    if (index < 0)
                    {
                        positions.RemoveAt(closestIndex);
                        indexedGeometries.RemoveAt(closestIndex);
                        positions.RemoveAt(closestIndex - 1);
                        indexedGeometries.RemoveAt(closestIndex - 1);
                    }
                    else
                    {
                        positions.RemoveAt(closestIndex + 1);
                        indexedGeometries.RemoveAt(closestIndex + 1);
                        positions.RemoveAt(closestIndex);
                        indexedGeometries.RemoveAt(closestIndex);
                    }
                }
                else
                {
                    // Remove point from map
                    positions.RemoveAt(closestIndex);
                    indexedGeometries.RemoveAt(closestIndex);
                }

                currentPosition = nextPosition;
            }

            // Connect all segments
            var connectedGeometries = new PolyCurve();
            foreach (var s in segments)
                connectedGeometries.Append(s);

            return connectedGeometries;
        }

        public static Curve ConnectGeometriesSequential(List<GeometryBase> geometries, List<bool> isGeometryConnector,
            out List<Curve> segments, out List<bool> isConnectorSegment)
        {
            isConnectorSegment = new List<bool>();
            segments = new List<Curve>();

            // Loop through and find all connections
            Point3d currentPosition = new Point3d();

            if (typeof(Curve).IsAssignableFrom(geometries[0].GetType()))
            {
                currentPosition = (geometries[0] as Curve).PointAtEnd;
                segments.Add((geometries[0] as Curve));
                isConnectorSegment.Add(isGeometryConnector[0]);
            }
            else if (geometries[0].GetType() == typeof(Point))
            {
                currentPosition = (geometries[0] as Point).Location;
            }

            for (int i = 1; i < geometries.Count; i++)
            {
                Curve nextCurve = null;
                Point3d nextPosition = new Point3d();
                Point3d nextStart = new Point3d();

                if (typeof(Curve).IsAssignableFrom(geometries[i].GetType()))
                {
                    var curve = geometries[i] as Curve;
                    nextCurve = curve.DuplicateCurve();

                    nextPosition = curve.PointAtEnd;
                    nextStart = curve.PointAtStart;
                }
                else if (geometries[i].GetType() == typeof(Point))
                {
                    nextPosition = (geometries[i] as Point).Location;
                    nextStart = nextPosition;
                }

                // Add connector
                if (nextStart.DistanceToSquared(currentPosition) > ToleranceDistance * ToleranceDistance)
                {
                    segments.Add(new LineCurve(currentPosition, nextStart));
                    if (geometries[i].GetType() == typeof(Point))
                        isConnectorSegment.Add(isGeometryConnector[i]);
                    else
                        isConnectorSegment.Add(true);
                }

                if (nextCurve != null)
                {
                    // Add curve
                    segments.Add(nextCurve);
                    isConnectorSegment.Add(isGeometryConnector[i]);
                }   

                currentPosition = nextPosition;
            }

            var connectedGeometries = new PolyCurve();
            foreach (var s in segments)
                connectedGeometries.Append(s);

            return connectedGeometries;
        }
    }
}
