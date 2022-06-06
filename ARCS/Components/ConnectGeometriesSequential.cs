using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static ARCS.PathGeneration;

namespace ARCS
{
    public class ConnectGeometriesSequential : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.primary; }

        public ConnectGeometriesSequential()
          : base("Connect Geometries", "ARCS_ConnectSimple",
              "Connect geometries in sequence through shortest connections.",
              "ARCS", "2 | Connection")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("paths", "paths", "Spray paths or points to be connected.", GH_ParamAccess.list);
            pManager.AddNumberParameter("connSpeed", "connSpeed", "Off path spraying speed.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("maintainDir", "maintainDir", "Maintain connection direction or not.", GH_ParamAccess.item, false);
            pManager.AddBooleanParameter("keepConn", "keepConn", "Keep the old connectors or not.", GH_ParamAccess.item, true);

            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new Param_SprayPath(), "sprayPath", "sprayPath", "Spray paths", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<object> paths = new List<object>();
            double connSpeed = 0;
            bool maintainDir = false;
            bool keepConn = false;

            DA.GetDataList(0, paths);
            DA.GetData(1, ref connSpeed);
            DA.GetData(2, ref maintainDir);
            DA.GetData(3, ref keepConn);

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

            var path = ConnectSprayObjsSequential(sprayObjs, connSpeed, maintainDir);

            DA.SetData(0, new GH_SprayPath(path));
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
