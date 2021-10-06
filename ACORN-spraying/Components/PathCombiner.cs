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
              "Combine spray paths geometries.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("geometries", "geometries", "Path geometries to connect", GH_ParamAccess.list);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if geometry is a connector. Same length as geometries list.", GH_ParamAccess.list);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("segments", "segments", "Segmented spray path.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if curve segment is a connector.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<object> geometries = new List<object>();
            List<bool> isConnector = new List<bool>();

            DA.GetDataList(0, geometries);
            DA.GetDataList(1, isConnector);

            if (geometries.Count == 0)
                return;

            List<Curve> segments;
            List<bool> connector;

            ConnectGeometriesSequential(
                geometries.Select(g =>
                {
                    if (g.GetType() == typeof(Grasshopper.Kernel.Types.GH_Curve))
                        return (g as Grasshopper.Kernel.Types.GH_Curve).Value as GeometryBase;  
                    else if (g.GetType() == typeof(Grasshopper.Kernel.Types.GH_Point))
                        return new Point((g as Grasshopper.Kernel.Types.GH_Point).Value) as GeometryBase;
                    else
                        return null;
                }).ToList()
                , isConnector, out segments, out connector);

            DA.SetDataList(0, segments);
            DA.SetDataList(1, connector);
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
