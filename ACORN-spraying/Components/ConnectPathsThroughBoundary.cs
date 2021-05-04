using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static ACORNSpraying.PathGeneration;

namespace ACORNSpraying
{
    public class ConnectGeometriesThroughBoundary : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.secondary; }

        public ConnectGeometriesThroughBoundary()
          : base("Connect Geometries Through Boundary", "ACORN_Connect",
              "Connects spray paths and other points of interests through connectors made from a curve offset from the surface edges.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("geometries", "geometries", "Geometries to connect. Only curves and points.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flag to show whether geometry is a connector. Should be same length as the geometries list.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("maintainDir", "maintainDir", "Maintain or flip curve directions.", GH_ParamAccess.item, false);
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item, 0);

            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("connectedPath", "connectedPath", "Connected curve.", GH_ParamAccess.item);
            pManager.AddCurveParameter("segments", "segments", "Curve segments.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if curve segment is a connector.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<object> geometries = new List<object>();
            List<bool> isGeoConnector = new List<bool>();
            bool maintainDir = false;
            Brep surf = null;
            Surface extSurf = null;
            double expandDist = 0;

            DA.GetDataList(0, geometries);
            DA.GetDataList(1, isGeoConnector);
            DA.GetData(2, ref maintainDir);
            DA.GetData(3, ref surf);
            DA.GetData(4, ref extSurf);
            DA.GetData(5, ref expandDist);

            var castedGeometries = new List<GeometryBase>();
            foreach (var g in geometries)
            {
                if (g == null)
                    continue;
                if (g.GetType() == typeof(GH_Point))
                    castedGeometries.Add(new Point((g as GH_Point).Value));
                else if (g.GetType() == typeof(GH_Curve))
                    castedGeometries.Add((g as GH_Curve).Value);
            }

            if (castedGeometries.Count != isGeoConnector.Count)
            {
                // If no connector list passed that all geometries are not connectors
                if (isGeoConnector.Count == 0)
                    isGeoConnector = Enumerable.Repeat(false, castedGeometries.Count).ToList();
                else
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Connector flag list length does not match geometry list length.");
                    return;
                }
            }

            List<Curve> segments;
            List<bool> isResConnector;
            var res = ConnectGeometriesThroughBoundary(castedGeometries, isGeoConnector, surf, extSurf, expandDist, maintainDir, out segments, out isResConnector);

            DA.SetData(0, res);
            DA.SetDataList(1, segments);
            DA.SetDataList(2, isResConnector);
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
            get { return new Guid("d2d29166-b2f1-4eab-be2d-031b5c85bdc4"); }
        }
    }
}
