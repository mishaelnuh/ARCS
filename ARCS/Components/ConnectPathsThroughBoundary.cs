﻿using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static ARCS.PathGeneration;

namespace ARCS
{
    public class ConnectGeometriesThroughBoundary : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.primary; }

        public ConnectGeometriesThroughBoundary()
          : base("Connect Geometries Through Boundary", "ACORN_ConnectBoundary",
              "Connects spray paths and other points of interests through connectors made from a curve offset from the surface edges.",
              "ARCS", "2 | Connection")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("paths", "paths", "Geometries to connect. Only curves and points.", GH_ParamAccess.list);
            pManager.AddNumberParameter("connSpeed", "connSpeed", "Off path spraying speed.", GH_ParamAccess.item);
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item, 0);
            pManager.AddBooleanParameter("maintainDir", "maintainDir", "Maintain connection direction or not.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("keepConn", "keepConn", "Keep the old connectors or not.", GH_ParamAccess.item, true);

            pManager[3].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new Param_SprayPath(), "sprayPath", "sprayPath", "Spray paths", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<object> paths = new List<object>();
            double connSpeed = 0;
            Brep surf = null;
            Surface extSurf = null;
            double expandDist = 0;
            bool maintainDir = false;
            bool keepConn = false;

            DA.GetDataList(0, paths);
            DA.GetData(1, ref connSpeed);
            DA.GetData(2, ref surf);
            DA.GetData(3, ref extSurf);
            DA.GetData(4, ref expandDist);
            DA.GetData(5, ref maintainDir);
            DA.GetData(6, ref keepConn);

            if (paths.Count == 0)
                return;

            var sprayObjs = paths
                .SelectMany(g =>
                {
                    if (g.GetType() == typeof(GH_ObjectWrapper))
                    {
                        var sprayPaths = ((g as GH_ObjectWrapper).Value as SprayPath).Select(x => x).ToList();
                        if (!keepConn)
                            sprayPaths = sprayPaths.Where(x => !x.IsConnector).ToList();

                        return sprayPaths.Select(c => c as object).ToList();
                    }
                    else if (g.GetType() == typeof(GH_SprayPath))
                    {
                        var sprayPaths = (g as GH_SprayPath).Value.Select(x => x).ToList();
                        if (!keepConn)
                            sprayPaths = sprayPaths.Where(x => !x.IsConnector).ToList();

                        return sprayPaths.Select(c => c as object).ToList();
                    }
                    else if (g.GetType() == typeof(Grasshopper.Kernel.Types.GH_Point))
                        return new List<object>() { new Point((g as Grasshopper.Kernel.Types.GH_Point).Value) as object };
                    else
                        return new List<object>();
                })
                .Where(x => x != null)
                .ToList();


            var path = ConnectSprayObjsThroughBoundary(sprayObjs, connSpeed, surf, extSurf, expandDist, maintainDir);
            path.AvoidHoles(surf, extSurf, expandDist);

            DA.SetData(0, new GH_SprayPath(path));
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return Properties.Resources.connect;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("d2d29166-b2f1-4eab-be2d-031b5c85bdc4"); }
        }
    }
}
