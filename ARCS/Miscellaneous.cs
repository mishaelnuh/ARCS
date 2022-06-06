using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using static ARCS.PathGeneration;

namespace ARCS
{
    public static class Miscellaneous
    {
        public static double ToleranceMath = 1e-12;
        public static double ToleranceDistance = 1e-12;
        public static double ToleranceAngle = 0.01 / 180 * Math.PI;

        /// <summary>
        /// Extends a surface using consecutive bounding boxes.
        /// </summary>
        /// <param name="surf">Surface to extend. Input as Brep in order to maintain trims.</param>
        /// <param name="plane">Plane to orient extended surface borders to.</param>
        /// <returns>Extended surface.</returns>
        public static Surface ExtendSurf(Brep surf, Plane plane)
        {
            // Get rotated plane
            var rotatedPlane = plane.Clone();
            rotatedPlane.Rotate(Math.PI / 4, Vector3d.ZAxis);

            // Calculate large boundary to extend to using consecutive bounding boxes
            var bounds = new Box(plane, surf.GetBoundingBox(plane));
            bounds = new Box(rotatedPlane, bounds.GetCorners());
            bounds = new Box(plane, bounds.GetCorners());
            bounds = new Box(rotatedPlane, bounds.GetCorners());
            bounds = new Box(plane, bounds.GetCorners());
            var boundCorners = bounds.GetCorners();

            // Find a boundary polyline curve to extend to
            var extendedOutline = new Polyline(new List<Point3d>()
            {
                boundCorners[0],
                boundCorners[4],
                boundCorners[5],
                boundCorners[1],
                boundCorners[0],
            }).ToPolylineCurve();

            // Populate the Brep with 10000 points
            var patchPoints = new List<Point3d>();
            var boundingBox = surf.GetBoundingBox(false);
            var domU = surf.Surfaces[0].Domain(0);
            var domV = surf.Surfaces[0].Domain(1);
            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < 100; j++)
                {
                    patchPoints.Add(surf.Surfaces[0].PointAt(
                        domU.Min + (domU.Max - domU.Min) / 99 * i,
                        domV.Min + (domV.Max - domV.Min) / 99 * j));
                }
            }
            var patchGeometries = Rhino.Geometry.Intersect.Intersection.ProjectPointsToBreps(
                new List<Brep>() { surf },
                patchPoints,
                Vector3d.ZAxis,
                ToleranceDistance)
                .Where(p => p != null)
                .Select(p => new Point(p) as GeometryBase)
                .ToList();

            // Create surface using Brep patch
            patchGeometries.Add(extendedOutline);
            var extSurf = Brep.CreatePatch(patchGeometries, null, 20, 20, false, true,
                extendedOutline.GetLength() / 100, 50, 10,
                new bool[] { true, true, true, true, true, true },
                ToleranceDistance);
            return extSurf.Surfaces[0];
        }

        /// <summary>
        /// Offset the surface boundary outwards by a specified distance.
        /// </summary>
        /// <param name="surf">Surface to offset border from. Input as Brep in order to maintain trims.</param>
        /// <param name="extSurf">Surface to extend border on.</param>
        /// <param name="dist">Distance to offset by.</param>
        /// <returns>Offset surface bound curve.</returns>
        public static Curve OffsetSurfBoundary(Brep surf, Surface extSurf, double dist)
        {
            var perim = surf.Boundary();

            if (dist == 0)
                return perim;

            // Offset curve
            var bounds1 = perim.OffsetOnSurface(extSurf, dist, ToleranceDistance)[0];
            bounds1.MakeClosed(ToleranceDistance);
            var bounds2 = perim.OffsetOnSurface(extSurf, -dist, ToleranceDistance)[0];
            bounds2.MakeClosed(ToleranceDistance);

            if (dist < 0)
            {
                if (AreaMassProperties.Compute(Curve.ProjectToPlane(bounds1, Plane.WorldXY)).Area >
                    AreaMassProperties.Compute(Curve.ProjectToPlane(bounds2, Plane.WorldXY)).Area)
                    return bounds2;
                else
                    return bounds1;
            }
            else
            {
                if (AreaMassProperties.Compute(Curve.ProjectToPlane(bounds1, Plane.WorldXY)).Area >
                    AreaMassProperties.Compute(Curve.ProjectToPlane(bounds2, Plane.WorldXY)).Area)
                    return bounds1;
                else
                    return bounds2;
            }
        }

        /// <summary>
        /// Offset the surface holes outwards by a specified distance.
        /// </summary>
        /// <param name="surf">Surface to offset holes from. Input as Brep in order to maintain trims.</param>
        /// <param name="extSurf">Surface to extend border on.</param>
        /// <param name="dist">Distance to offset by.</param>
        /// <returns>Offset surface bound curve.</returns>
        public static List<Curve> OffsetSurfHoles(Brep surf, Surface extSurf, double dist)
        {
            var holes = surf.Holes();

            if (dist == 0)
                return holes;

            // Offset curve
            var offsetHoles = new List<Curve>();
            foreach (var h in holes)
            {
                var bounds1 = h.OffsetOnSurface(extSurf, dist, ToleranceDistance)[0];
                bounds1.MakeClosed(ToleranceDistance);
                var bounds2 = h.OffsetOnSurface(extSurf, -dist, ToleranceDistance)[0];
                bounds2.MakeClosed(ToleranceDistance);

                if (dist < 0)
                {
                    if (AreaMassProperties.Compute(Curve.ProjectToPlane(bounds1, Plane.WorldXY)).Area <
                        AreaMassProperties.Compute(Curve.ProjectToPlane(bounds2, Plane.WorldXY)).Area)
                        offsetHoles.Add(bounds2);
                    else
                        offsetHoles.Add(bounds1);
                }
                else
                {
                    if (AreaMassProperties.Compute(Curve.ProjectToPlane(bounds1, Plane.WorldXY)).Area <
                        AreaMassProperties.Compute(Curve.ProjectToPlane(bounds2, Plane.WorldXY)).Area)
                        offsetHoles.Add(bounds1);
                    else
                        offsetHoles.Add(bounds2);
                }

            }

            return offsetHoles;
        }

        /// <summary>
        /// Filters curves to be within a specified distance to surface.
        /// </summary>
        /// <param name="curves">Curves to filter.</param>
        /// <param name="surf">Surface to measure to.</param>
        /// <param name="dist">Max allowable distance from curve to surface.</param>
        /// <returns>Filtered curves.</returns>
        public static List<Curve> FilterCurvesByDistToSurf(List<Curve> curves, Brep surf, double dist)
        {
            var filteredCurves = new List<Curve>();

            foreach (var c in curves)
            {
                c.ClosestPoints(new List<GeometryBase>() { surf }, out Point3d p1, out Point3d p2, out _);
                if (p1.DistanceToSquared(p2) <= dist * dist)
                    filteredCurves.Add(c);
            }

            return filteredCurves;
        }

        /// <summary>
        /// Trim curve using a closed boundary curve.
        /// </summary>
        /// <param name="curve">Curve to trim.</param>
        /// <param name="boundary">Closed curve to trim curve with.</param>
        /// <param name="insideCurves">Curves inside the boundary.</param>
        /// <param name="outsideCurves">Curves outside the boundary.</param>
        public static void TrimCurveBoundary(Curve curve, Curve boundary, out List<Curve> insideCurves, out List<Curve> outsideCurves, out List<Curve> subcurves)
        {
            var toleranceVec = new Vector3d(0, 0, ToleranceDistance);

            // Get an extrusion from the bounds to use as intersection Brep
            var curveBounds = curve.GetBoundingBox(false);
            var basePlane = new Plane(curveBounds.Min - 2 * toleranceVec, Vector3d.ZAxis);
            var projectedBounds = Curve.ProjectToPlane(boundary, basePlane);
            var extrudeDir = new Vector3d(0, 0, curveBounds.Max.Z - curveBounds.Min.Z + 4 * ToleranceDistance);
            var extrusion = Surface.CreateExtrusion(projectedBounds, extrudeDir).ToBrep().CapPlanarHoles(ToleranceDistance);
            if (!extrusion.IsValid)
                extrusion.Repair(ToleranceDistance);

            // Do check with a point outside extrusion and flip inside and outside depending on result
            var checkPoint = new Point3d(curveBounds.Min.X - ToleranceDistance * 1e6, curveBounds.Min.Y - ToleranceDistance * 1e6, curveBounds.Min.Z - ToleranceDistance * 1e6);
            if (extrusion.IsPointInside(checkPoint, ToleranceDistance, true))
                extrusion.Flip();

            // Get intersection parameters
            var intersections = new List<double> { curve.Domain.Min };
            Rhino.Geometry.Intersect.Intersection.CurveBrep(
                curve, extrusion, ToleranceDistance, ToleranceAngle, out double[] tmp);
            intersections.AddRange(tmp);
            intersections.Add(curve.Domain.Max);
            intersections = intersections.Distinct().ToList();

            // Determine if inside of outside extrusion
            insideCurves = new List<Curve>();
            outsideCurves = new List<Curve>();
            subcurves = new List<Curve>();

            for (int i = 0; i < intersections.Count - 1; i++)
            {
                var c = curve.Trim(intersections[i], intersections[i + 1]);
                if (c == null)
                    continue;

                if (extrusion.IsPointInside(c.PointAtNormalizedLength(0.5), ToleranceDistance, false))
                    insideCurves.Add(c);
                else
                    outsideCurves.Add(c);
                subcurves.Add(c);
            }

            outsideCurves = Curve.JoinCurves(outsideCurves).ToList();
            insideCurves = Curve.JoinCurves(insideCurves).ToList();
        }

        /// <summary>
        /// Trim curve by a Brep surface.
        /// </summary>
        /// <param name="curve">Curve to trim.</param>
        /// <param name="brep">Brep surface to trim with.</param>
        /// <param name="expandDist">Extra distance to trim the inner surface to.</param>
        /// <param name="insideCurves">Curves inside the boundary.</param>
        /// <param name="outsideCurves">Curves outside the boundary.</param>
        public static void TrimCurveSurface(Curve curve, Brep brep, double expandDist, out List<Curve> insideCurves, out List<Curve> outsideCurves, out List<Curve> subcurves)
        {
            // Get an extrusion to use as intersection Brep
            var curveBounds = curve.GetBoundingBox(true);
            var duplicateBrep = brep.DuplicateBrep();
            var surfBounds = duplicateBrep.GetBoundingBox(true);
            duplicateBrep.Translate(new Vector3d(0, 0, (curveBounds.Min.Z - surfBounds.Max.Z)) * 3);
            surfBounds = duplicateBrep.GetBoundingBox(true);
            var extrusionCurve = new LineCurve(new Point3d(), (new Point3d()) + Vector3d.ZAxis * (curveBounds.Max.Z - surfBounds.Min.Z) * 6);
            var extrusion = duplicateBrep.Faces[0].CreateExtrusion(extrusionCurve, true);
            if (!extrusion.IsValid)
                extrusion.Repair(ToleranceDistance);

            // Do check with a point outside extrusion and flip inside and outside depending on result
            var checkPoint = new Point3d(curveBounds.Min.X - ToleranceDistance * 1e6, curveBounds.Min.Y - ToleranceDistance * 1e6, curveBounds.Min.Z - ToleranceDistance * 1e6);
            if (extrusion.IsPointInside(checkPoint, ToleranceDistance, true))
                extrusion.Flip();

            // Get intersection parameters
            var intersections = new List<double> { curve.Domain.Min };
            Rhino.Geometry.Intersect.Intersection.CurveBrep(
                curve, extrusion, ToleranceDistance, ToleranceAngle, out double[] tmp);
            var shiftedParams = new List<double>();
            var sortedTmp = tmp.Distinct().ToList();
            sortedTmp.Sort();
            if (expandDist == 0.0)
                shiftedParams.AddRange(sortedTmp);
            else
            {
                foreach (var t in sortedTmp)
                {
                    var currLength = curve.GetLength(new Interval(curve.Domain.T0, t));
                    var cLength = curve.GetLength();

                    var tangent = curve.TangentAt(t);
                    tangent.Z = 0;
                    tangent.Unitize();
                    var testPoint = curve.PointAt(t) + tangent;

                    var success1 = curve.LengthParameter(currLength + Math.Abs(expandDist), out double p1Param);
                    var success2 = curve.LengthParameter(currLength - Math.Abs(expandDist), out double p2Param);
                    if (!success1 || currLength + Math.Abs(expandDist) >= cLength || p1Param < t)
                    {
                        p1Param = curve.Domain.T1;
                    }
                    if (!success2 || currLength - Math.Abs(expandDist) <= 0.0 || p2Param > t)
                    {
                        p2Param = curve.Domain.T0;
                    }

                    if (extrusion.IsPointInside(testPoint, 0.0, false))
                    {
                        shiftedParams.Add(expandDist > 0.0 ? p2Param : p1Param);
                    }
                    else
                    {
                        shiftedParams.Add(expandDist > 0.0 ? p1Param : p2Param);
                    }
                }
            }
            intersections.AddRange(shiftedParams);
            intersections.Add(curve.Domain.Max);
            intersections = intersections.Distinct().ToList();

            // Determine if inside of outside extrusion
            insideCurves = new List<Curve>();
            outsideCurves = new List<Curve>();
            subcurves = new List<Curve>();

            for (int i = 0; i < intersections.Count - 1; i++)
            {
                var c = curve.Trim(intersections[i], intersections[i + 1]);
                if (c != null)
                {
                    if (expandDist == 0)
                    {
                        if (extrusion.IsPointInside(c.PointAtNormalizedLength(0.5), ToleranceDistance, false))
                            insideCurves.Add(c);
                        else
                            outsideCurves.Add(c);
                    }
                    else if (expandDist < 0)
                    {
                        if (extrusion.IsPointInside(c.PointAtStart, ToleranceDistance, false) && extrusion.IsPointInside(c.PointAtEnd, ToleranceDistance, false) && extrusion.IsPointInside(c.PointAtNormalizedLength(0.5), ToleranceDistance, false))
                            insideCurves.Add(c);
                        else
                            outsideCurves.Add(c);
                    }
                    else if (expandDist > 0)
                    {
                        if (!extrusion.IsPointInside(c.PointAtStart, ToleranceDistance, false) && !extrusion.IsPointInside(c.PointAtEnd, ToleranceDistance, false) && !extrusion.IsPointInside(c.PointAtNormalizedLength(0.5), ToleranceDistance, false))
                            outsideCurves.Add(c);
                        else
                            insideCurves.Add(c);
                    }
                    subcurves.Add(c);
                }
            }

            outsideCurves = Curve.JoinCurves(outsideCurves).ToList();
            insideCurves = Curve.JoinCurves(insideCurves).ToList();
        }

        /// <summary>
        /// Finds shortest subcurve in closed curve between parameters.
        /// </summary>
        /// <param name="curve">Closed curve.</param>
        /// <param name="t0">Start parameter.</param>
        /// <param name="t1">End parameter.</param>
        /// <returns>Subcurve.</returns>
        public static Curve ShortestSubcurve(Curve curve, double t0, double t1)
        {
            if (curve.PointAt(t0).DistanceToSquared(curve.PointAt(t1)) < ToleranceDistance * ToleranceDistance)
                return new LineCurve(curve.PointAt(t0), curve.PointAt(t1));

            var c0 = GetSubcurve(curve, t0, t1);
            var c1 = GetSubcurve(curve, t1, t0);
            c1.Reverse();

            if (c0.PointAtStart.DistanceToSquared(c0.PointAtEnd) < ToleranceDistance * ToleranceDistance)
                return new LineCurve(c0.PointAtStart, c0.PointAtEnd);
            else if (c1.PointAtStart.DistanceToSquared(c1.PointAtEnd) < ToleranceDistance * ToleranceDistance)
                return new LineCurve(c1.PointAtStart, c1.PointAtEnd);

            if (c0.GetLength() < c1.GetLength())
                return c0;
            else
                return c1;
        }

        /// <summary>
        /// Finds the subcurve between two parameters.
        /// </summary>
        /// <param name="curve">Closed curve.</param>
        /// <param name="t0">Start parameter.</param>
        /// <param name="t1">End parameter.</param>
        /// <returns>Subcurve.</returns>
        public static Curve GetSubcurve(Curve curve, double t0, double t1)
        {
            if (t0 < t1)
                return curve.Trim(t0, t1);
            else
            {
                var domain = curve.Domain;
                var c0 = curve.Trim(t0, domain.T1);
                var c1 = curve.Trim(domain.T0, t1);
                var joined = Curve.JoinCurves(new List<Curve>() { c0, c1 }, ToleranceDistance)[0];
                return joined;
            }
        }

        /// <summary>
        /// Get boundary outline of Brep surface.
        /// </summary>
        /// <param name="brep">Brep to find outline of.</param>
        /// <returns>Outline curve.</returns>
        public static Curve Boundary(this Brep brep)
        {
            return brep.Faces[0].OuterLoop.To3dCurve();
        }

        /// <summary>
        /// Get hole outlines of Brep surface.
        /// </summary>
        /// <param name="brep">Brep to find outline of.</param>
        /// <returns>Outline curve.</returns>
        public static List<Curve> Holes(this Brep brep)
        {
            var loops = brep.Faces[0].Loops
                .Where(x => x.LoopType != BrepLoopType.Outer)
                .Select(x => x.To3dCurve())
                .ToList();

            return loops;
        }

        /// <summary>
        /// Modify curve to go around holes.
        /// </summary>
        /// <param name="curve">Curve to modify./param>
        /// <param name="holes">List of curves defining holes to go around.</param>
        /// <returns>Modified curve.</returns>
        public static Curve AvoidHoles(this Curve curve, List<Curve> holes)
        {
            foreach (var hole in holes)
            {
                TrimCurveBoundary(curve, hole, out List<Curve> insideCurves, out List<Curve> outsideCurves, out _);

                // Assume only intersection with one hole
                if (insideCurves.Count > 0 && outsideCurves.Count > 0)
                {
                    return ConnectGeometriesThroughBoundary(
                        new List<GeometryBase>()
                        {
                            new Point(curve.PointAtStart),
                            new Point(curve.PointAtEnd),
                        },
                        new List<bool>()
                        {
                            true,
                            true
                        },
                        hole,
                        false,
                        out _,
                        out _,
                        out _
                    );
                }
            }

            return curve;
        }

        /// <summary>
        /// Return normal vector defining the TCP orientation.
        /// </summary>
        /// <param name="surf">Target surface.</param>
        /// <param name="point">Point to compute normal for.</param>
        /// <param name="angle">Additional angle for boundary. Only used if isOnEdge is set to true.</param>
        /// <param name="isOnEdge">Whether to consider the edges separately or not.</param>
        /// <returns>Normal vector defining the TCP orientation.</returns>
        public static Vector3d AlignNormal(Brep surf, Point3d point, double angle, bool isOnEdge)
        {
            var edgeSprayPath = surf.Boundary();

            // Check if not on surface boundary
            var boundary2d = Curve.ProjectToPlane(edgeSprayPath, Plane.WorldXY);
            var point2d = new Point3d(point) { Z = 0 };

            var containmentTest = boundary2d.Contains(point2d, Plane.WorldXY, ToleranceDistance);

            if (!isOnEdge)
            {
                if (containmentTest == PointContainment.Outside)
                    return -Vector3d.ZAxis;
                else
                {
                    var p = Rhino.Geometry.Intersect.Intersection.ProjectPointsToBreps(new List<Brep>() { surf }, new List<Point3d>() { point }, Vector3d.ZAxis, ToleranceDistance);

                    if (p.Length == 0)
                        return -Vector3d.ZAxis;

                    surf.Faces[0].ClosestPoint(p[0], out double surfU, out double surfV);
                    var norm = surf.Faces[0].NormalAt(surfU, surfV);
                    if (norm.Z > 0)
                        norm.Reverse();

                    return norm;
                }
            }
            else
            {
                // Get normal of surface
                surf.Surfaces[0].ClosestPoint(point, out double u, out double v);
                var surfNorm = surf.Surfaces[0].NormalAt(u, v);
                surfNorm.Unitize();

                if (surfNorm.Z > 0)
                    surfNorm.Reverse();

                var boundarySegments = edgeSprayPath.DuplicateSegments();

                var normalVecs = new List<Vector3d>();

                edgeSprayPath.ClosestPoint(point, out double tmp);
                var distanceToEdge = edgeSprayPath.PointAt(tmp).DistanceTo(point);

                foreach (var seg in boundarySegments)
                {
                    if (seg.ClosestPoint(point, out double t, distanceToEdge + ToleranceDistance))
                    {
                        var curveTangent = seg.TangentAt(t);

                        // Check to make sure rotation will be in the right direction
                        var testVector = Vector3d.CrossProduct(-surfNorm, curveTangent);
                        testVector.Unitize();

                        var testPoint1 = point + testVector * Math.Max(distanceToEdge * 2, ToleranceDistance * 10);

                        testPoint1.Z = 0;

                        if (boundary2d.Contains(testPoint1, Plane.WorldXY, ToleranceDistance) == PointContainment.Outside)
                            curveTangent.Reverse();

                        // Rotate surfNorm
                        var norm = new Vector3d(surfNorm);
                        norm.Rotate(-angle, curveTangent);
                        norm.Unitize();

                        normalVecs.Add(norm);
                    }
                }

                // Add all the norms from boundary curves
                var finalNorm = new Vector3d(0, 0, 0);
                if (normalVecs.Count == 0)
                {
                    finalNorm = surfNorm;
                }
                else
                {
                    foreach (var n in normalVecs)
                    {
                        finalNorm += n;
                    }
                    finalNorm.Unitize();
                }

                finalNorm.Unitize();

                return finalNorm;
            }
        }

        /// <summary>
        /// Deep clone a generic object using serialisation.
        /// </summary>
        /// <typeparam name="T">Object type.</typeparam>
        /// <param name="obj">Object.</param>
        /// <returns>Cloned object.</returns>
        public static T DeepClone<T>(this T obj)
        {
            if (obj == null)
                return default;

            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }

        /// <summary>
        /// Serialise an object into a byte stream.
        /// </summary>
        /// <param name="obj">Object to serialise</param>
        /// <returns>Byte stream</returns>
        public static byte[] Serialise(this object obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Deserialise a byte stream into an object.
        /// </summary>
        /// <param name="stream">Byte stream</param>
        /// <returns>Deserialised object</returns>
        public static object Deserialise(this byte[] stream)
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(stream, 0, stream.Length);
                ms.Seek(0, SeekOrigin.Begin);
                var formatter = new BinaryFormatter();
                return formatter.Deserialize(ms);
            }
        }
    }
}