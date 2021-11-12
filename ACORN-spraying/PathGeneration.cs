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
        public static List<SprayPath> SprayInnerPaths(Brep surf, Surface extSurf, double dist, double expandDist, int numGeo,
            List<int> sourceEdges, List<Brep> speedRegions, List<double> spraySpeed, double connectorSpraySpeed,
            out List<Curve> boundaryEdges, out List<SprayPath> repeatPaths)
        {
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

            // Generate spray paths
            var paths = new List<SprayPath>();
            repeatPaths = new List<SprayPath>();

            var pathsDict = new Dictionary<int, SprayPath>();
            var repeatPathsDict = new Dictionary<int, SprayPath>();
            var boundaryEdgesDict = new Dictionary<int, Curve>();

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
                var path = OrthoGeodesics(geodesicsCurves, edges[i], dist / 2);

                // Pull paths to surface and trim
                path = path
                    .Select(p => extSurf.Pullback(p, ToleranceDistance))
                    .Select(p => extSurf.Pushup(p, ToleranceDistance))
                    .SelectMany(p =>
                    {
                        TrimCurveBoundary(p, boundary, out List<Curve> insideCurves, out _, out _);
                        return insideCurves;
                    })
                    .ToList();

                // Align path order
                edges[i].ClosestPoints(new List<GeometryBase>() { path[0] }, out Point3d pT1, out Point3d pT2, out _);
                var dist1 = pT1.DistanceToSquared(pT2);
                edges[i].ClosestPoints(new List<GeometryBase>() { path.Last() }, out pT1, out pT2, out _);
                var dist2 = pT1.DistanceToSquared(pT2);

                if (dist2 < dist1)
                    path.Reverse();

                // Split into base path and repeat paths
                var repeatPath = path
                    .Select((curve, index) => new { Index = index, Value = curve })
                    .Where(x => x.Index % 2 == 1)
                    .Select(x => x.Value)
                    .ToList();
                repeatPath.Reverse();
                path = path
                    .Select((curve, index) => new { Index = index, Value = curve })
                    .Where(x => x.Index % 2 == 0)
                    .Select(x => x.Value)
                    .ToList();

                // Split the paths for speed purposes
                if (speedRegions.Count > 0)
                {
                    foreach (var cutter in speedRegions)
                    {
                        for (int j = 0; j < path.Count; j++)
                        {
                            TrimCurveSurface(path[j], cutter, out _, out _, out List<Curve> subcurves);

                            path.RemoveAt(j);
                            path.InsertRange(j, subcurves);
                            j += subcurves.Count - 1;
                        }

                        for (int j = 0; j < repeatPath.Count; j++)
                        {
                            TrimCurveSurface(repeatPath[j], cutter, out _, out _, out List<Curve> subcurves);

                            repeatPath.RemoveAt(j);
                            repeatPath.InsertRange(j, subcurves);
                            j += subcurves.Count - 1;
                        }
                    }
                }

                // Connect paths together through the bounds
                var connectedPath = ConnectGeometries(
                    path.Cast<GeometryBase>().ToList(),
                    Enumerable.Repeat(false, path.Count).ToList(), false,
                    out List<Curve> tmp1, out List<bool> tmp2, out _);

                // Convert geometry to spray paths and associate with speed
                var sprayPath = new SprayPath();

                foreach(var segment in tmp1.Zip(tmp2, (c, f) => new {Curve = c, IsConnector = f}))
                {
                    var sprayCurve = new SprayCurve(segment.Curve) { IsConnector = segment.IsConnector };

                    if (sprayCurve.IsConnector)
                        sprayCurve.Speed = connectorSpraySpeed;
                    else
                    {
                        var midPoint = segment.Curve.PointAtNormalizedLength(0.5);
                        var distances = speedRegions.Select(s => midPoint.DistanceToSquared(s.ClosestPoint(midPoint))).ToList();
                        var minDistance = distances.Min();
                        var speedRegionIndex = distances
                            .Select((item, index) => new { Item = item, Index = index })
                            .Where(x => Math.Abs(x.Item - minDistance) < ToleranceDistance * ToleranceDistance * 100)
                            .First()
                            .Index;

                        sprayCurve.Speed = spraySpeed[speedRegionIndex];
                    }

                    sprayPath.Add(sprayCurve);
                }

                // Connect repeat paths together through the bounds
                var connectedRepeatPath = ConnectGeometries(
                    repeatPath.Cast<GeometryBase>().ToList(),
                    Enumerable.Repeat(false, repeatPath.Count).ToList(), false,
                    out tmp1, out tmp2, out _);

                // Convert geometry to spray paths and associate with speed
                var sprayRepeatPath = new SprayPath();

                foreach (var segment in tmp1.Zip(tmp2, (c, f) => new { Curve = c, IsConnector = f }))
                {
                    var sprayCurve = new SprayCurve(segment.Curve) { IsConnector = segment.IsConnector };

                    if (sprayCurve.IsConnector)
                        sprayCurve.Speed = connectorSpraySpeed;
                    else
                    {
                        var midPoint = segment.Curve.PointAtNormalizedLength(0.5);
                        var distances = speedRegions.Select(s => midPoint.DistanceToSquared(s.ClosestPoint(midPoint))).ToList();
                        var minDistance = distances.Min();
                        var speedRegionIndex = distances
                            .Select((item, index) => new { Item = item, Index = index })
                            .Where(x => Math.Abs(x.Item - minDistance) < ToleranceDistance * ToleranceDistance * 100)
                            .First()
                            .Index;

                        sprayCurve.Speed = spraySpeed[speedRegionIndex];
                    }

                    sprayRepeatPath.Add(sprayCurve);
                }

                pathsDict[edgeCounter] = sprayPath;
                repeatPathsDict[edgeCounter] = sprayRepeatPath;
                boundaryEdgesDict[edgeCounter] = edges[i];
            }

            foreach(var e in sourceEdges)
            {
                paths.Add(pathsDict[e]);
                repeatPaths.Add(repeatPathsDict[e]);
                boundaryEdges.Add(boundaryEdgesDict[e]);
            }

            return paths;
        }

        public static SprayPath SprayEdgePath(Brep surf, Surface extSurf, Point3d startP, double expandDist, double spraySpeed)
        {
            var boundary = OffsetSurfBoundary(surf, extSurf, expandDist);

            // Adjust seam
            boundary.ClosestPoint(startP, out double t);
            boundary.ChangeClosedCurveSeam(t);

            var sprayCurve = new SprayCurve(boundary)
            {
                IsEdge = true,
                Speed = spraySpeed
            };

            var sprayPath = new SprayPath() { sprayCurve };

            return sprayPath;
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
                    extSurf.ClosestPoint(p, out double u, out double v);
                    return new Point2d(u, v);
                })
                .ToList();
            var geodesicEndPoints = geodesicParam[1]
                .Select(t =>
                {
                    var p = geodesicOutline[1].PointAt(t);
                    extSurf.ClosestPoint(p, out double u, out double v);
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
                    g.ClosestPoints(guide, out Point3d p, out _);
                    g.ClosestPoint(p, out double t);
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

        public static SprayPath ConnectSprayObjsThroughBoundary(List<object> sprayObjs, double connectorSpraySpeed, Brep surf, Surface extSurf, double dist, bool maintainDir)
        {
            if (sprayObjs.Count == 0)
                return new SprayPath();

            var geometryObjects = sprayObjs
                .Select(o => {
                    if (o is SprayCurve)
                        return (o as SprayCurve).Curve as GeometryBase;
                    else if (o is Point)
                        return o as GeometryBase;
                    else
                        throw new Exception("Only accepts SprayCurve or Point objects.");
                }).ToList();
            var flags = Enumerable.Repeat(false, geometryObjects.Count).ToList();

            ConnectGeometriesThroughBoundary(geometryObjects, flags, surf, extSurf, dist, maintainDir,
                out List<Curve> segments, out List<bool> isConnector, out List<int> originalIndex);

            var sprayPath = new SprayPath();

            for (int i = 0; i < segments.Count; i++)
            {
                if (isConnector[i])
                {
                    var sprayCurve = new SprayCurve(segments[i])
                    {
                        Speed = connectorSpraySpeed,
                        IsConnector = true
                    };
                    
                    sprayPath.Add(sprayCurve);
                }
                else
                {
                    var sprayCurve = (sprayObjs[originalIndex[i]] as SprayCurve).DeepClone();
                    sprayCurve.Curve = segments[i];

                    sprayPath.Add(sprayCurve);
                }
            }

            return sprayPath;
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
            bool maintainDir, out List<Curve> segments, out List<bool> isConnectorSegment, out List<int> originalIndex)
        {
            var boundary = OffsetSurfBoundary(surf, extSurf, dist);
            return ConnectGeometriesThroughBoundary(geometries, isGeometryConnector, boundary, maintainDir, out segments, out isConnectorSegment, out originalIndex);
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
            out List<Curve> segments, out List<bool> isConnectorSegment, out List<int> originalIndex)
        {
            isConnectorSegment = new List<bool>();
            segments = new List<Curve>();
            originalIndex = new List<int>();

            for (int i = 0; i < geometries.Count - 1; i++)
            {
                Point3d pEnd, pNext;

                if (typeof(Curve).IsAssignableFrom(geometries[i].GetType()))
                {
                    segments.Add(geometries[i] as Curve);
                    originalIndex.Add(i);
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

                boundary.ClosestPoint(pEnd, out double boundaryParamEnd);
                boundary.ClosestPoint(pNext, out double boundaryParamNext);

                Curve connector;
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
                    originalIndex.Add(-1);
                    isConnectorSegment.Add(true);
                }
                if (connector.GetLength() > ToleranceDistance)
                {
                    segments.Add(connector);
                    originalIndex.Add(-1);
                    isConnectorSegment.Add(true);
                }
                if (pNext.DistanceToSquared(connector.PointAtEnd) > ToleranceDistance * ToleranceDistance)
                {
                    segments.Add(new LineCurve(connector.PointAtEnd, pNext));
                    originalIndex.Add(-1);
                    isConnectorSegment.Add(true);
                }
            }

            if (typeof(Curve).IsAssignableFrom(geometries.Last().GetType()))
            {
                segments.Add(geometries.Last() as Curve);
                originalIndex.Add(geometries.Count - 1);
                isConnectorSegment.Add(isGeometryConnector.Last());
            }

            var connectedGeometries = new PolyCurve();
            foreach(var s in segments)
                connectedGeometries.Append(s);

            return connectedGeometries;
        }

        public static SprayPath ConnectSprayObjs(List<object> sprayObjs, double connectorSpraySpeed, bool maintainDir)
        {
            if (sprayObjs.Count == 0)
                return new SprayPath();

            var geometryObjects = sprayObjs
                .Select(o => {
                    if (o is SprayCurve)
                        return (o as SprayCurve).Curve as GeometryBase;
                    else if (o is Point)
                        return o as GeometryBase;
                    else
                        throw new Exception("Only accepts SprayCurve or Point objects.");
                }).ToList();
            var flags = Enumerable.Repeat(false, geometryObjects.Count).ToList();

            ConnectGeometries(geometryObjects, flags, maintainDir,
                out List<Curve> segments, out List<bool> isConnector, out List<int> originalIndex);

            var sprayPath = new SprayPath();

            for(int i = 0; i < segments.Count; i++)
            {
                if (isConnector[i])
                {
                    var sprayCurve = new SprayCurve(segments[i])
                    {
                        Speed = connectorSpraySpeed,
                        IsConnector = true
                    };

                    sprayPath.Add(sprayCurve);
                }
                else
                {
                    var sprayCurve = (sprayObjs[originalIndex[i]] as SprayCurve).DeepClone();
                    sprayCurve.Curve = segments[i];

                    sprayPath.Add(sprayCurve);
                }
            }

            return sprayPath;
        }

        /// <summary>
        /// Connect geometries starting from the first using closest neighbour consecutively.
        /// </summary>
        /// <param name="geometries">Geometries to connect. Only curves and points.</param>
        /// <param name="isGeometryConnector">Flag to show whether original geometry is a connector. Should be same length as the geometries list.</param>
        /// <param name="segments">Curve segments exploded.</param>
        /// <param name="isConnectorSegment">Flags to show which segments are connectors.</param>
        /// <returns></returns>
        public static Curve ConnectGeometries(List<GeometryBase> geometries, List<bool> isGeometryConnector, bool maintainDir,
            out List<Curve> segments, out List<bool> isConnectorSegment, out List<int> originalIndex)
        {
            isConnectorSegment = new List<bool>();
            segments = new List<Curve>();
            originalIndex = new List<int>();

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
                    if (!maintainDir)
                    {
                        // Track a reversed curve by using negative indexes
                        positions.Add(curve.PointAtEnd);
                        indexedGeometries.Add(-i);
                    }
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
                originalIndex.Add(0);
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
                    originalIndex.Add(-1);
                    isConnectorSegment.Add(true);
                }

                if (nextCurve != null)
                {
                    // Add curve
                    segments.Add(nextCurve);
                    originalIndex.Add(Math.Abs(index));
                    isConnectorSegment.Add(isGeometryConnector[Math.Abs(index)]);

                    // Remove curves from map
                    if (maintainDir)
                    {
                        positions.RemoveAt(closestIndex);
                        indexedGeometries.RemoveAt(closestIndex);
                    }
                    else if (index < 0)
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

        public static SprayPath ConnectSprayObjsSequential(List<object> sprayObjs, double connectorSpraySpeed, bool maintainDir)
        {
            if (sprayObjs.Count == 0)
                return new SprayPath();

            var geometryObjects = sprayObjs
                .Select(o => {
                    if (o is SprayCurve)
                        return (o as SprayCurve).Curve as GeometryBase;
                    else if (o is Point)
                        return o as GeometryBase;
                    else
                        throw new Exception("Only accepts SprayCurve or Point objects.");
                }).ToList();
            var flags = Enumerable.Repeat(false, geometryObjects.Count).ToList();

            ConnectGeometriesSequential(geometryObjects, flags, maintainDir,
                out List<Curve> segments, out List<bool> isConnector, out List<int> originalIndex);

            var sprayPath = new SprayPath();

            for (int i = 0; i < segments.Count; i++)
            {
                if (isConnector[i])
                {
                    var sprayCurve = new SprayCurve(segments[i])
                    {
                        Speed = connectorSpraySpeed,
                        IsConnector = true
                    };
                    sprayPath.Add(sprayCurve);
                }
                else
                {
                    var sprayCurve = (sprayObjs[originalIndex[i]] as SprayCurve).DeepClone();
                    sprayCurve.Curve = segments[i];

                    sprayPath.Add(sprayCurve);
                }
            }

            return sprayPath;
        }

        public static Curve ConnectGeometriesSequential(List<GeometryBase> geometries, List<bool> isGeometryConnector,
            bool maintainDir, out List<Curve> segments, out List<bool> isConnectorSegment, out List<int> originalIndex)
        {
            isConnectorSegment = new List<bool>();
            segments = new List<Curve>();
            originalIndex = new List<int>();

            // Loop through and find all connections
            Point3d currentPosition = new Point3d();

            if (typeof(Curve).IsAssignableFrom(geometries[0].GetType()))
            {
                currentPosition = (geometries[0] as Curve).PointAtEnd;
                segments.Add((geometries[0] as Curve));
                originalIndex.Add(0);
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

                    if (maintainDir)
                    {
                        nextPosition = curve.PointAtEnd;
                        nextStart = curve.PointAtStart;
                    }
                    else
                    {
                        if (curve.PointAtEnd.DistanceToSquared(currentPosition) < curve.PointAtStart.DistanceToSquared(currentPosition))
                        {
                            nextCurve.Reverse();
                            nextPosition = curve.PointAtStart;
                            nextStart = curve.PointAtEnd;
                        }
                        else
                        {
                            nextPosition = curve.PointAtEnd;
                            nextStart = curve.PointAtStart;
                        }
                    }

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
                    originalIndex.Add(-1);
                    isConnectorSegment.Add(true);
                }

                if (nextCurve != null)
                {
                    // Add curve
                    segments.Add(nextCurve);
                    originalIndex.Add(i);
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
