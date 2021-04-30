using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static ACORNSpraying.PathGeneration;

namespace ACORNSpraying
{
    public class PathCombiner : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.primary; }

        public PathCombiner()
          : base("Combine Paths", "ACORN_Combine",
              "Combine spray paths along wth start and end points.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("startP", "startP", "Starting point of spray path.", GH_ParamAccess.item, Point3d.Unset);
            pManager.AddCurveParameter("ePath", "ePath", "Spray path of edge.", GH_ParamAccess.item);
            pManager.AddCurveParameter("iSegments", "iSegments", "Spray path of inner.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("iIsConnector", "iIsConnector", "Flags to see if inner curve segment is a connector.", GH_ParamAccess.list);
            pManager.AddPointParameter("endP", "endP", "End point of spray path.", GH_ParamAccess.item, Point3d.Unset);

            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item);

            pManager.AddNumberParameter("numELayer", "numELayer", "Number of repeats of edge path.", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("numILayer", "numILayer", "Number of repeats of inner path.", GH_ParamAccess.item, 1);

            pManager[0].Optional = true;
            pManager[4].Optional = true;
            pManager[8].Optional = true;
            pManager[9].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("segments", "segments", "Segmented spray path.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if curve segment is a connector.", GH_ParamAccess.list);
            pManager.AddNumberParameter("edgeIndices", "edgeIndices", "Indices of edge path segment.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Point3d startP = new Point3d();
            Curve ePath = null;
            List<Curve> iSegment = new List<Curve>();
            List<bool> iIsConnector = new List<bool>();
            Point3d endP = new Point3d();

            Brep surf = null;
            Surface extSurf = null;
            double expandDist = 0;

            double numELayer = 1;
            double numILayer = 1;

            DA.GetData(0, ref startP);
            DA.GetData(1, ref ePath);
            DA.GetDataList(2, iSegment);
            DA.GetDataList(3, iIsConnector);
            DA.GetData(4, ref endP);

            DA.GetData(5, ref surf);
            DA.GetData(6, ref extSurf);
            DA.GetData(7, ref expandDist);

            DA.GetData(8, ref numELayer);
            DA.GetData(9, ref numILayer);

            var segments = new List<Curve>();
            var isConnector = new List<bool>();

            // Add line from start point to edge path
            if (startP.IsValid)
            {
                segments.Add(new LineCurve(startP, ePath.PointAtStart));
                isConnector.Add(true);
            }

            // Add edge path
            for (int i = 0; i < numELayer; i++)
            {
                segments.Add(ePath);
                isConnector.Add(false);
            }
            var edgeIndices = Enumerable.Range(1, (int)numELayer).ToList();

            // Add connector from edge path to inner path
            List<Curve> tmpSegments;
            List<bool> tmpFlags;
            ConnectGeometriesThroughBoundary(
                new List<GeometryBase>()
                {
                    new Point(ePath.PointAtEnd),
                    new Point(iSegment.First().PointAtStart)
                },
                Enumerable.Repeat(true, 2).ToList(),
                surf, extSurf, expandDist,
                out tmpSegments, out tmpFlags);

            segments.AddRange(tmpSegments);
            isConnector.AddRange(tmpFlags);

            // Add connected inner path
            tmpSegments.Clear();
            tmpFlags.Clear();

            var connectedInnerPath = ConnectGeometriesThroughBoundary(
                iSegment.Cast<GeometryBase>().ToList(),
                iIsConnector,
                surf, extSurf, expandDist,
                out tmpSegments, out tmpFlags);

            for (int i = 0; i < numILayer; i++)
            {
                segments.AddRange(tmpSegments);
                isConnector.AddRange(tmpFlags);
            }

            // Add path from end of inner path to end point
            if (!endP.IsValid)
            {
                if (startP.IsValid)
                    endP = startP;
                else
                    endP = ePath.PointAtStart;
            }

            tmpSegments.Clear();
            tmpFlags.Clear();

            ConnectGeometriesThroughBoundary(
                new List<GeometryBase>()
                {
                    new Point(connectedInnerPath.PointAtEnd),
                    new Point(endP)
                },
                Enumerable.Repeat(true, 2).ToList(),
                surf, extSurf, 0,
                out tmpSegments, out tmpFlags);

            segments.AddRange(tmpSegments);
            isConnector.AddRange(tmpFlags);

            DA.SetDataList(0, segments);
            DA.SetDataList(1, isConnector);
            DA.SetDataList(2, edgeIndices);
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
            get { return new Guid("32a2306c-172c-4c14-97e6-9884678764a8"); }
        }
    }
}
