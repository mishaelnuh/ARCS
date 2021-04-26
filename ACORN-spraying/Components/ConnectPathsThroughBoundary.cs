using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using static ACORNSpraying.PathGeneration;

namespace ACORNSpraying
{
    public class ConnectGeometriesThroughBoundary : GH_Component
    {
        public ConnectGeometriesThroughBoundary()
          : base("ConnectGeometriesThroughBoundary", "ACORN_Connect",
              "Connects spray paths and other points of interests through connectors made from a curve offset from the surface edges.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("geometries", "geometries", "Geometries to connect. Only curves and points.", GH_ParamAccess.list);
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("connectedPath", "connectedPath", "Connected curve.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if curve segment is a connector.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GeometryBase> geometries = new List<GeometryBase>();
            Brep surf = null;
            Surface extSurf = null;
            double expandDist = 0;

            DA.GetDataList(0, geometries);
            DA.GetData(1, ref surf);
            DA.GetData(2, ref extSurf);
            DA.GetData(3, ref expandDist);

            List<bool> isConnector;
            var res = ConnectGeometriesThroughBoundary(geometries, surf, extSurf, expandDist, out isConnector);

            DA.SetData(0, res);
            DA.SetDataList(1, isConnector);
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
