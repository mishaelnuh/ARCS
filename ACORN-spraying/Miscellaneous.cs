using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace ACORNSpraying
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

            var perimLength = perim.GetLength();

            // Offset curve
            var bounds1 = perim.OffsetOnSurface(extSurf, dist, ToleranceDistance)[0];
            var bounds2 = perim.OffsetOnSurface(extSurf, -dist, ToleranceDistance)[0];

            if (AreaMassProperties.Compute(Curve.ProjectToPlane(bounds1, Plane.WorldXY)).Area >
                AreaMassProperties.Compute(Curve.ProjectToPlane(bounds2, Plane.WorldXY)).Area)
                return bounds2;
            else
                return bounds1;
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
                Point3d p1, p2;
                c.ClosestPoints(new List<GeometryBase>() { surf }, out p1, out p2, out _);
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
        public static void TrimCurveBoundary(Curve curve, Curve boundary, out List<Curve> insideCurves, out List<Curve> outsideCurves)
        {
            var toleranceVec = new Vector3d(0, 0, ToleranceDistance);

            // Get an extrusion from the bounds to use as intersection Brep
            var curveBounds = curve.GetBoundingBox(false);
            var basePlane = new Plane(curveBounds.Min - 2 * toleranceVec, Vector3d.ZAxis);
            var projectedBounds = Curve.ProjectToPlane(boundary, basePlane);
            var extrudeDir = new Vector3d(0, 0, curveBounds.Max.Z - curveBounds.Min.Z + 4 * ToleranceDistance);
            var extrusion = Surface.CreateExtrusion(projectedBounds, extrudeDir).ToBrep().CapPlanarHoles(ToleranceDistance);

            // Get intersection parameters
            var intersections = new List<double> { curve.Domain.Min };
            var tmp = new double[0];
            Rhino.Geometry.Intersect.Intersection.CurveBrep(
                curve, extrusion, ToleranceDistance, ToleranceAngle, out tmp);
            intersections.AddRange(tmp);
            intersections.Add(curve.Domain.Max);
            intersections = intersections.Distinct().ToList();

            // Determine if inside of outside extrusion
            insideCurves = new List<Curve>();
            outsideCurves = new List<Curve>();

            for (int i = 0; i < intersections.Count - 1; i++)
            {
                var c = curve.Trim(intersections[i], intersections[i + 1]);
                if (extrusion.IsPointInside(c.PointAtNormalizedLength(0.5), ToleranceDistance, false))
                    insideCurves.Add(c);
                else
                    outsideCurves.Add(c);
            }

            // Do check with a point outside extrusion and flip inside and outside depending on result
            var checkPoint = new Point3d(curveBounds.Min.X - ToleranceDistance * 1e6, curveBounds.Min.Y - ToleranceDistance * 1e6, curveBounds.Min.Z - ToleranceDistance * 1e6);
            if (extrusion.IsPointInside(checkPoint, ToleranceDistance, true))
            {
                var tmpList = outsideCurves;
                outsideCurves = insideCurves;
                insideCurves = tmpList;
            }

            outsideCurves = Curve.JoinCurves(outsideCurves).ToList();
            insideCurves = Curve.JoinCurves(insideCurves).ToList();
        }

        /// <summary>
        /// Trim curve by a Brep surface.
        /// </summary>
        /// <param name="curve">Curve to trim.</param>
        /// <param name="boundary">Brep surface to trim with.</param>
        /// <param name="insideCurves">Curves inside the boundary.</param>
        /// <param name="outsideCurves">Curves outside the boundary.</param>
        public static void TrimCurveSurface(Curve curve, Brep brep, out List<Curve> insideCurves, out List<Curve> outsideCurves)
        {
            var toleranceVec = new Vector3d(0, 0, ToleranceDistance);

            // Get an extrusion to use as intersection Brep
            var curveBounds = curve.GetBoundingBox(true);
            var extrusionCurve = new LineCurve(new Point3d(), (new Point3d()) + Vector3d.ZAxis * ((curveBounds.Max.Z - curveBounds.Min.Z) * 10 + 4 * ToleranceDistance));
            var extrusion = brep.Faces[0].CreateExtrusion(extrusionCurve, true);
            extrusion.Translate(new Vector3d(0, 0, - (curveBounds.Max.Z - curveBounds.Min.Z) * 5 - 2 * ToleranceDistance));

            // Get intersection parameters
            var intersections = new List<double> { curve.Domain.Min };
            var tmp = new double[0];
            Rhino.Geometry.Intersect.Intersection.CurveBrep(
                curve, extrusion, ToleranceDistance, ToleranceAngle, out tmp);
            intersections.AddRange(tmp);
            intersections.Add(curve.Domain.Max);
            intersections = intersections.Distinct().ToList();
            intersections.Sort();
            
            // Determine if inside of outside extrusion
            insideCurves = new List<Curve>();
            outsideCurves = new List<Curve>();

            for (int i = 0; i < intersections.Count - 1; i++)
            {
                var c = curve.Trim(intersections[i], intersections[i + 1]);
                if (extrusion.IsPointInside(c.PointAtNormalizedLength(0.5), ToleranceDistance, false))
                    insideCurves.Add(c);
                else
                    outsideCurves.Add(c);
            }

            // Do check with a point outside extrusion and flip inside and outside depending on result
            var checkPoint = new Point3d(curveBounds.Min.X - ToleranceDistance * 1e6, curveBounds.Min.Y - ToleranceDistance * 1e6, curveBounds.Min.Z - ToleranceDistance * 1e6);
            if (extrusion.IsPointInside(checkPoint, ToleranceDistance, true))
            {
                var tmpList = outsideCurves;
                outsideCurves = insideCurves;
                insideCurves = tmpList;
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
        /// Get boundary outline of Brep surface. Assumes no holes.
        /// </summary>
        /// <param name="brep">Brep to find outline of.</param>
        /// <returns>Outline curve.</returns>
        public static Curve Boundary(this Brep brep)
        {
            var loops = brep.GetWireframe(-1).Where(l => l != null).ToList();

            if (loops.Count == 0)
                return null;

            // Join curves in a loop increasing the tolerance if the curve isn't closed until it is
            double factor = 1.0;
            var curves = new Curve[0];
            while (factor <= 100)
            {
                curves = Curve.JoinCurves(loops, ToleranceDistance * factor, false);
                if (curves.Length == 1)
                {
                    if (curves[0].IsClosed)
                        break;
                }
                factor += 1;
            }
            return curves[0];
        }
    }
}