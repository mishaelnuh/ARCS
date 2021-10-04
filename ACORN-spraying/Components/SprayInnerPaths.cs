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

        public SprayInnerPaths()
          : base("Spray Inner Paths", "ACORN_SprayInner",
              "Generates inner spray paths.",
              "ACORN", "Spraying")
        {
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (SurfEdges != null && SurfEdges.Count > 0)
            {
                clippingBox = new BoundingBox();

                for (int i = 0; i < SurfEdges.Count; i++)
                {
                    args.Display.Draw2dText("Edge " + i.ToString(), System.Drawing.Color.Blue, SurfEdges[i].PointAtNormalizedLength(0.5), true);
                    clippingBox.Union(SurfEdges[i].PointAtNormalizedLength(0.5));
                }
            }
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddBrepParameter("topSurf", "topSurf", "Top surface to spray to. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddNumberParameter("dist", "dist", "Distance between path lines.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("flowRate", "flowRate", "Volumetric flow rate.", GH_ParamAccess.item, 100000);
            pManager.AddNumberParameter("spraySpeed", "spraySpeed", "On path spraying speed.", GH_ParamAccess.item, 350);
            pManager.AddNumberParameter("spraySpeedConn", "spraySpeedConn", "Off path spraying speed.", GH_ParamAccess.item, 700);
            pManager.AddNumberParameter("numGeo", "numGeo", "Number of geodesics to calculate paths from.", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("sourceEdges", "sourceEdges", "Edges to align to. If not set, all edges are used.", GH_ParamAccess.list);
            pManager.AddNumberParameter("pathRepeat", "pathRepeat", "Number of times to repeat each path.", GH_ParamAccess.item, 1);

            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
            pManager[10].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("paths", "paths", "Spray paths.", GH_ParamAccess.list);
            pManager.AddCurveParameter("segments", "segments", "Flattened list of curve segments.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if curve segment is a connector.", GH_ParamAccess.tree);
            pManager.AddNumberParameter("thicknesses", "thicknesses", "Thicknesses of each layer", GH_ParamAccess.list);
            pManager.AddBrepParameter("slices", "slices", "Sprayed slices.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep surf = null;
            Surface extSurf = null;
            Brep topSurf = null;
            double dist = 0;
            double expandDist = 0;
            double flowRate = 0;
            double spraySpeed = 0;
            double spraySpeedConn = 0;
            double numGeo = 0;
            List<double> sourceEdges = new List<double>();
            double pathRepeat = 1;

            DA.GetData(0, ref surf);
            DA.GetData(1, ref extSurf);
            DA.GetData(2, ref topSurf);
            DA.GetData(3, ref dist);
            DA.GetData(4, ref expandDist);
            DA.GetData(5, ref flowRate);
            DA.GetData(6, ref spraySpeed);
            DA.GetData(7, ref spraySpeedConn);
            DA.GetData(8, ref numGeo);
            DA.GetDataList(9, sourceEdges);
            DA.GetData(10, ref pathRepeat);

            List<List<Curve>> baseSegments;
            List<List<bool>> baseIsConnector;
            List<Brep> slices = new List<Brep>();
            List<double> thicknesses = new List<double>();

            List<Curve> surfEdges;
            var res = SprayInnerPaths(surf, extSurf, dist, expandDist, (int)numGeo, sourceEdges.Cast<int>().ToList(), out baseSegments, out baseIsConnector, out surfEdges);
            SurfEdges = surfEdges;

            List<Curve> paths = new List<Curve>();
            List<List<Curve>> segments = new List<List<Curve>>();
            List<List<bool>> isConnector = new List<List<bool>>();

            // Add first path
            paths.Add(res[0]);
            segments.Add(baseSegments[0]);
            isConnector.Add(baseIsConnector[0]);

            // Calculate base thickness
            var baseArea = surf.GetArea();

            var currThickness = 0.0;
            for (int i = 0; i < baseSegments[0].Count; i++)
            {
                currThickness += baseSegments[0][i].GetLength() /
                    (baseIsConnector[0][i] ? spraySpeedConn : spraySpeed) * flowRate / baseArea;
            }

            slices.Add(surf.Faces[0].CreateExtrusion(
                new LineCurve(new Point3d(), (new Point3d()) + Vector3d.ZAxis * currThickness), true));

            thicknesses.Add(currThickness);

            // Extrude the top surface to create a cutter
            var bBoxHeight = surf.GetBoundingBox(false).Diagonal.Z;
            
            var topSurfaceCutter = Miscellaneous.ExtendSurf(topSurf, Plane.WorldYZ).ToBrep().Faces[0]
                .CreateExtrusion(
                    new LineCurve(new Point3d(0, 0, 0), new Point3d(0, 0, bBoxHeight)),
                    true);

            // Loop through thickness
            bool loopFlag = true;
            int currSegmentUsed = 2;
            while(loopFlag)
            {
                // Get trimmed surface
                var shiftedCutter = topSurfaceCutter.DuplicateBrep();
                shiftedCutter.Translate(new Vector3d(0, 0, -currThickness));

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
                foreach (var splitSurf in splitSurface)
                {
                    List<Curve> trimmedPaths = new List<Curve>();
                    List<List<Curve>> trimmedSegments = new List<List<Curve>>();
                    List<List<bool>> trimmedConnector = new List<List<bool>>();

                    // Trim base segments to create new segments and path
                    var importantSegments = new List<Curve>();

                    var filteredSegments = baseSegments[currSegmentUsed]
                        .Zip(baseIsConnector[currSegmentUsed], (segment, flag) => new
                        {
                            segment = segment,
                            flag = flag,
                        })
                        .Where(item => !item.flag)
                        .Select(item => item.segment)
                        .ToList();

                    // Trim segments with surface
                    foreach (var c in filteredSegments)
                    {
                        List<Curve> insideCurves;
                        Miscellaneous.TrimCurveSurface(c, splitSurf, out insideCurves, out _);
                        importantSegments.AddRange(insideCurves);
                    }

                    // If no segments we continue. Other segments may still be useful.
                    if (importantSegments.Count == 0)
                        continue;

                    List<Curve> newTrimmedSegments;
                    List<bool> newTrimmedConnector;
                    var joinedCurve = ConnectGeometries(importantSegments.Select(s => s as GeometryBase).ToList(),
                        Enumerable.Repeat(true, importantSegments.Count).ToList(), out newTrimmedSegments, out newTrimmedConnector);

                    // If too short, we also continue
                    if (joinedCurve.GetLength() <= dist)
                        continue;

                    // Shift segments and curve to current thickness
                    joinedCurve.Translate(0, 0, currThickness);
                    foreach (var s in newTrimmedSegments)
                        s.Translate(0, 0, currThickness);

                    // Repeat paths
                    PolyCurve repeatedJoinedCurve = new PolyCurve();
                    List<Curve> repeatedTrimmedSegments = new List<Curve>();
                    List<bool> repeatedTrimmedConnector = new List<bool>();

                    for (int i = 0; i < pathRepeat; i++)
                    {
                        if (i % 2 == 0)
                        {
                            repeatedJoinedCurve.Append(joinedCurve);
                            repeatedTrimmedSegments.AddRange(newTrimmedSegments.Select(c => c.DuplicateCurve()));
                            repeatedTrimmedConnector.AddRange(newTrimmedConnector);
                        }
                        else
                        {
                            var duplicateCurve = joinedCurve.DuplicateCurve();
                            duplicateCurve.Reverse();
                            repeatedJoinedCurve.Append(duplicateCurve);
                            var tmpCurveList = newTrimmedSegments
                                .Select(c => {
                                    var newCurve = c.DuplicateCurve();
                                    newCurve.Reverse();
                                    return newCurve;
                                })
                                .ToList();
                            tmpCurveList.Reverse();
                            repeatedTrimmedSegments.AddRange(tmpCurveList);
                            var tmpBoolList = new List<bool>(newTrimmedConnector);
                            tmpBoolList.Reverse();
                            repeatedTrimmedConnector.AddRange(tmpBoolList);
                        }
                    }

                    paths.Add(repeatedJoinedCurve);
                    segments.Add(repeatedTrimmedSegments);
                    isConnector.Add(repeatedTrimmedConnector);

                    numAdded++;

                    // Calculate added thickness. Divide volume by total area at the end.
                    for (int i = 0; i < repeatedTrimmedSegments.Count; i++)
                    {
                        addedThickness += repeatedTrimmedSegments[i].GetLength() /
                            (repeatedTrimmedConnector[i] ? spraySpeedConn : spraySpeed) * flowRate;
                    }
                }

                // Nothing was added so lets end it
                if (numAdded == 0)
                {
                    loopFlag = false;
                    break;
                }

                // Divide added thickness by total area
                addedThickness /= splitSurface.Select(s => s.GetArea()).Sum();

                foreach (var splitSurf in splitSurface)
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
                if (currSegmentUsed >= baseSegments.Count)
                    currSegmentUsed = 0;
            }

            // Format into trees
            DataTree<Curve> pathTree = new DataTree<Curve>();
            DataTree<Curve> segmentsTree = new DataTree<Curve>();
            DataTree<bool> connectorTree = new DataTree<bool>();

            for (int i = 0; i < segments.Count; i++)
            {
                for (int j = 0; j < segments[i].Count; j++)
                {
                    segmentsTree.Insert(segments[i][j], new GH_Path(i), j);
                    connectorTree.Insert(isConnector[i][j], new GH_Path(i), j);
                }
            }

            DA.SetDataList(0, paths);
            DA.SetDataTree(1, segmentsTree);
            DA.SetDataTree(2, connectorTree);
            DA.SetDataList(3, thicknesses);
            DA.SetDataList(4, slices);
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
