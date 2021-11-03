using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static ACORNSpraying.PathGeneration;
using Grasshopper.Kernel.Types;

namespace ACORNSpraying
{
    public class ConnectGeometriesSequential : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.primary; }

        public ConnectGeometriesSequential()
          : base("Connect Geometries", "ACORN_Connect2",
              "Connect geometries in sequence through shortest connections.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("paths", "paths", "Spray paths or points to be connected.", GH_ParamAccess.list);
            pManager.AddNumberParameter("connSpeed", "connSpeed", "Off path spraying speed.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new Param_SprayPath(), "sprayPath", "sprayPath", "Spray paths", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<object> paths = new List<object>();
            double connSpeed = 0;

            DA.GetDataList(0, paths);
            DA.GetData(1, ref connSpeed);

            if (paths.Count == 0)
                return;

            var sprayObjs = paths
                .SelectMany(g => {
                    if (g.GetType() == typeof(GH_ObjectWrapper))
                        return ((g as GH_ObjectWrapper).Value as SprayPath).Select(c => c as object).ToList();
                    else if (g.GetType() == typeof(GH_SprayPath))
                        return ((g as GH_SprayPath).Value).Select(c => c as object).ToList();
                    else if (g.GetType() == typeof(Grasshopper.Kernel.Types.GH_Point))
                        return new List<object>() { new Point((g as Grasshopper.Kernel.Types.GH_Point).Value) as object };
                    else
                        return null;
                })
                .Where(x => x != null)
                .ToList();

            var path = ConnectSprayObjsSequential(sprayObjs, connSpeed);

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
