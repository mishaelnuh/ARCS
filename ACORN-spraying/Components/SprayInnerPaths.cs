using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;
using static ACORNSpraying.PathGeneration;

namespace ACORNSpraying
{
    public class SprayInnerPaths : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.primary; }

        private BoundingBox clippingBox;
        public override BoundingBox ClippingBox => clippingBox;
        
        public List<Curve> SurfEdges { get; set; }
        public List<double> EdgeId { get; set; }

        public SprayInnerPaths()
          : base("Spray Inner Paths", "ACORN_SprayInner",
              "Generates inner spray paths.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddBrepParameter("topSurf", "topSurf", "Top surface to spray to. Input as Brep in order to maintain trims.", GH_ParamAccess.item);

            pManager.AddBrepParameter("speedRegions", "speedRegions", "Speed regions defined by Breps. No holes accepted", GH_ParamAccess.list);
            pManager.AddNumberParameter("speeds", "speeds", "List of speeds associated with each speed region Brep.", GH_ParamAccess.list);
            pManager.AddNumberParameter("connSpeed", "connSpeed", "Off path spraying speed.", GH_ParamAccess.item);
            pManager.AddNumberParameter("flowRate", "flowRate", "Volumetric flow rate.", GH_ParamAccess.item);

            pManager.AddNumberParameter("dist", "dist", "Distance between path lines.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item, 0);

            pManager.AddNumberParameter("numGeo", "numGeo", "Number of geodesics to calculate paths from.", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("sourceEdges", "sourceEdges", "Edges to align to. If not set, all edges are used.", GH_ParamAccess.list);
            pManager.AddNumberParameter("pathRepeat", "pathRepeat", "Number of times to repeat each path.", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("thicknessFactor", "thicknessFactor", "Multiplicative factor for thickness.", GH_ParamAccess.item, 1);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
            pManager[10].Optional = true;
            pManager[11].Optional = true;
            pManager[12].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new Param_SprayPath(), "sprayPath", "sprayPath", "Spray paths", GH_ParamAccess.list);
            pManager.AddNumberParameter("thicknesses", "thicknesses", "Thicknesses of each layer", GH_ParamAccess.list);
            pManager.AddBrepParameter("slices", "slices", "Sprayed slices.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep surf = null;
            Surface extSurf = null;
            Brep topSurf = null;

            List<Brep> speedRegions = new List<Brep>();
            List<double> speeds = new List<double>();
            double connSpeed = 0;
            double flowRate = 0;

            double dist = 0;
            double expandDist = 0;

            double numGeo = 0;
            List<double> sourceEdges = new List<double>();
            double pathRepeat = 1;
            double thicknessFactor = 1.0;

            DA.GetData(0, ref surf);
            DA.GetData(1, ref extSurf);
            DA.GetData(2, ref topSurf);

            DA.GetDataList(3, speedRegions);
            DA.GetDataList(4, speeds);
            DA.GetData(5, ref connSpeed);
            DA.GetData(6, ref flowRate);

            DA.GetData(7, ref dist);
            DA.GetData(8, ref expandDist);

            DA.GetData(9, ref numGeo);
            DA.GetDataList(10, sourceEdges);
            DA.GetData(11, ref pathRepeat);
            DA.GetData(12, ref thicknessFactor);

            // If no speed regions given, use default values
            if (speedRegions.Count == 0)
            {
                speedRegions.Add(surf);
            }

            List<List<Curve>> baseSegments;
            List<List<bool>> baseIsConnector;
            List<Brep> slices = new List<Brep>();
            List<double> thicknesses = new List<double>();

            List<Curve> surfEdges;
            var basePaths = SprayInnerPaths(surf, extSurf, dist, expandDist, (int)numGeo,
                sourceEdges.Select(x => (int)x).ToList(),
                speedRegions, speeds, connSpeed, out surfEdges);
            
            SurfEdges = surfEdges;
            EdgeId = sourceEdges;

            List<SprayPath> paths = new List<SprayPath>();

            var baseArea = surf.GetArea();
            var currThickness = 0.0;

            // If no top surface given
            if (topSurf == null)
            {
                // Add first path
                for (int i = 0; i < pathRepeat; i++)
                {
                    var duplicatePath = basePaths.First().DeepClone();

                    if (i % 2 == 1)
                        duplicatePath.Reverse();

                    paths.Add(duplicatePath);
                }

                // Calculate base thickness
                foreach (var p in paths)
                    currThickness += p.GetDuration() * flowRate / baseArea;

                slices.Add(surf.Faces[0].CreateExtrusion(
                    new LineCurve(new Point3d(), (new Point3d()) + Vector3d.ZAxis * currThickness), true));
                thicknesses.Add(currThickness);

                DA.SetDataList(0, paths.Select(p => new GH_SprayPath(p)));
                DA.SetDataList(1, thicknesses);
                DA.SetDataList(2, slices);
                return;
            }

            // Extrude the top surface to create a cutter
            var bBoxHeight = surf.GetBoundingBox(false).Diagonal.Z;
            
            var topSurfaceCutter = Miscellaneous.ExtendSurf(topSurf, Plane.WorldYZ).ToBrep().Faces[0]
                .CreateExtrusion(
                    new LineCurve(new Point3d(0, 0, 0), new Point3d(0, 0, bBoxHeight * 10)),
                    true);

            // Loop through thickness
            bool loopFlag = true;
            int currSegmentUsed = Math.Min(0, basePaths.Count - 1);
            while(loopFlag)
            {
                // Get trimmed surface
                var shiftedCutter = topSurfaceCutter.DuplicateBrep();
                shiftedCutter.Translate(new Vector3d(0, 0, -currThickness / thicknessFactor));
                var splitSurface = surf.Split(shiftedCutter, Miscellaneous.ToleranceDistance).ToList();

                // Filter cutter by minimum area
                splitSurface = splitSurface.Where(s => s.GetArea() > Miscellaneous.ToleranceDistance).ToList();

                // No collision with cutter so just add the entire surface
                if (splitSurface.Count == 0)
                    splitSurface.Add(surf.DuplicateBrep());

                splitSurface = splitSurface.Where(s =>
                    {
                        var points = s.GetWireframe(2).Select(c => c.PointAtNormalizedLength(0.5)).ToList();
                        var closestPoints = shiftedCutter.Faces[0].PullPointsToFace(points, surf.GetBoundingBox(false).Diagonal.SquareLength * 10e9).ToList();

                        var p = points
                            .Zip(closestPoints, (p1, p2) => new
                            {
                                p1 = p1,
                                p2 = p2,
                            })
                            .OrderByDescending(item => item.p1.DistanceToSquared(item.p2))
                            .First();

                        return (p.p2.Z > p.p1.Z);
                    })
                    .ToList();

                // No surfaces above cutter so end loop
                if (splitSurface.Count == 0)
                {
                    loopFlag = false;
                    break;
                }

                // Loop through split surface
                var addedThickness = 0.0;
                var numAdded = 0;
                List<Brep> addedSlices = new List<Brep>();
                foreach (var splitSurf in splitSurface)
                {
                    List<Curve> trimmedPaths = new List<Curve>();
                    List<List<Curve>> trimmedSegments = new List<List<Curve>>();
                    List<List<bool>> trimmedConnector = new List<List<bool>>();

                    // Trim base segments to create new segments and path
                    var importantSegments = new List<SprayCurve>();

                    int trialSegment = currSegmentUsed;
                    do
                    {
                        var noConnectorPaths = basePaths[trialSegment].DeepClone();
                        noConnectorPaths.TrimConnectors();

                        // Trim segments with surface
                        foreach (var sprayCurve in noConnectorPaths)
                        {
                            List<Curve> insideCurves;
                            Miscellaneous.TrimCurveSurface(sprayCurve.Curve, splitSurf, out insideCurves, out _, out _);
                            var sprayCurves = insideCurves
                                .Select(c => {
                                    var tmp = sprayCurve.DeepClone();
                                    tmp.Curve = c;
                                    return tmp;
                                })
                                .ToList();
                            importantSegments.AddRange(sprayCurves);
                        }

                        // If no segments we try a different segment path
                        if (importantSegments.Count == 0)
                        {
                            trialSegment++;
                            if (trialSegment >= basePaths.Count)
                                trialSegment = 0;
                        }
                        // If total length is too short also try a different segment path
                        else if (importantSegments.Select(s => s.Curve.GetLength()).Sum() <= dist)
                        {
                            importantSegments = new List<SprayCurve>();
                            trialSegment++;
                            if (trialSegment >= basePaths.Count)
                                trialSegment = 0;
                        }
                    } while (trialSegment != currSegmentUsed && importantSegments.Count == 0);

                    // If no segments we continue. Other segments may still be useful.
                    if (importantSegments.Count == 0)
                        continue;

                    List <Curve> newTrimmedSegments;
                    List<bool> newTrimmedConnector;
                    var newPath = ConnectSprayObjs(importantSegments.Select(s => s as object).ToList(), connSpeed, false);

                    // If too short, we also continue
                    if (newPath.GetLength() <= dist)
                        continue;

                    // Shift segments and curve to current thickness
                    newPath.Translate(0, 0, currThickness);

                    // Repeat paths
                    PolyCurve repeatedJoinedCurve = new PolyCurve();
                    List<Curve> repeatedTrimmedSegments = new List<Curve>();
                    List<bool> repeatedTrimmedConnector = new List<bool>();

                    // Add first path
                    for (int i = 0; i < pathRepeat; i++)
                    {
                        var duplicatePath = newPath.DeepClone();

                        if (i % 2 == 1)
                            duplicatePath.Reverse();

                        paths.Add(duplicatePath);
                    }

                    addedSlices.Add(splitSurf);

                    // Calculate added thickness. Divide volume by total area at the end.
                    addedThickness += newPath.GetDuration() * flowRate * pathRepeat;

                    numAdded++;
                }

                // Nothing was added so lets end it
                if (numAdded == 0)
                {
                    loopFlag = false;
                    break;
                }

                // Divide added thickness by total area
                addedThickness /= addedSlices.Select(s => s.GetArea()).Sum();

                foreach (var splitSurf in addedSlices)
                {
                    var extrusion = splitSurf.Faces[0].CreateExtrusion(
                        new LineCurve(new Point3d(), (new Point3d()) + Vector3d.ZAxis * addedThickness), true);
                    extrusion.Translate(0, 0, currThickness);
                    slices.Add(extrusion);
                    thicknesses.Add(currThickness + addedThickness);
                }
                currThickness += addedThickness;

                // Shift the segment to be used
                currSegmentUsed++;
                if (currSegmentUsed >= basePaths.Count)
                    currSegmentUsed = 0;
            }

            DA.SetDataList(0, paths.Select(p => new GH_SprayPath(p)));
            DA.SetDataList(1, thicknesses);
            DA.SetDataList(2, slices);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("4b3c5e3c-3538-43dd-b924-c14b5f2c9294"); }
        }
    }
}
