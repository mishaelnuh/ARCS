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
            pManager.AddCurveParameter("segments", "segments", "Spray segments.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if segment is a connector.", GH_ParamAccess.list);
            pManager.AddPointParameter("endP", "endP", "End point of spray path.", GH_ParamAccess.item, Point3d.Unset);
            
            pManager.AddBooleanParameter("maintainDir", "maintainDir", "Maintain or flip curve directions.", GH_ParamAccess.item, false);

            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item);

            pManager.AddNumberParameter("numLayer", "numLayer", "Number of repeats for the path.", GH_ParamAccess.item, 1);

            pManager[0].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[8].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("segments", "segments", "Segmented spray path.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if curve segment is a connector.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Point3d startP = new Point3d();
            List<Curve> segments = new List<Curve>();
            List<bool> isConnector = new List<bool>();
            Point3d endP = new Point3d();

            bool maintainDir = false;

            Brep surf = null;
            Surface extSurf = null;
            double expandDist = 0;

            double numLayer = 1;

            DA.GetData(0, ref startP);
            DA.GetDataList(1, segments);
            DA.GetDataList(2, isConnector);
            DA.GetData(3, ref endP);

            DA.GetData(4, ref maintainDir);

            DA.GetData(5, ref surf);
            DA.GetData(6, ref extSurf);
            DA.GetData(7, ref expandDist);

            DA.GetData(8, ref numLayer);

            var outputSegments = new List<Curve>();
            var outputFlags = new List<bool>();

            if (segments.Count == 0)
                return;

            // Add line from start point to edge path
            if (startP.IsValid)
            {
                outputSegments.Add(new LineCurve(startP, segments.First().PointAtStart));
                outputFlags.Add(true);
            }

            // Add connected path
            List<Curve> tmpSegments;
            List<bool> tmpFlags;

            var connectedPath = ConnectGeometriesThroughBoundary(
                segments.Cast<GeometryBase>().ToList(),
                isConnector,
                surf, extSurf, expandDist,
                maintainDir,
                out tmpSegments, out tmpFlags);

            List<Curve> tmpConnector;
            List<bool> tmpConnectorFlag;

            var layerConnector = ConnectGeometriesThroughBoundary(
                new List<GeometryBase>()
                {
                    new Point(connectedPath.PointAtEnd),
                    new Point(connectedPath.PointAtStart)
                },
                isConnector,
                surf, extSurf, expandDist,
                maintainDir,
                out tmpConnector, out tmpConnectorFlag);

            for (int i = 0; i < numLayer; i++)
            {
                outputSegments.AddRange(tmpSegments);
                outputFlags.AddRange(tmpFlags);
                if (i < numLayer - 1)
                {
                    outputSegments.AddRange(tmpConnector);
                    outputFlags.AddRange(tmpConnectorFlag);
                }
            }

            // Add path from end of inner path to end point
            if (!endP.IsValid)
            {
                if (startP.IsValid)
                    endP = startP;
                else
                    endP = segments.First().PointAtStart;
            }

            tmpSegments.Clear();
            tmpFlags.Clear();

            ConnectGeometriesThroughBoundary(
                new List<GeometryBase>()
                {
                    new Point(connectedPath.PointAtEnd),
                    new Point(endP)
                },
                Enumerable.Repeat(true, 2).ToList(),
                surf, extSurf, 0,
                maintainDir,
                out tmpSegments, out tmpFlags);;

            outputSegments.AddRange(tmpSegments);
            outputFlags.AddRange(tmpFlags);

            DA.SetDataList(0, outputSegments);
            DA.SetDataList(1, outputFlags);
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
