using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using static ACORNSpraying.PathGeneration;

namespace ACORNSpraying
{
    public class SprayEdgePath : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.primary; }

        public SprayEdgePath()
          : base("Spray Edge Path", "ACORN_SprayEdge",
              "Generates edge spray path.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddPointParameter("startP", "startP", "Starting point of spray to align seam.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("path", "path", "Spray path.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep surf = null;
            Point3d startP = new Point3d();

            DA.GetData(0, ref surf);
            DA.GetData(1, ref startP);

            var res = SprayEdgePath(surf, startP);

            DA.SetData(0, res);
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
            get { return new Guid("cf0ca6d2-4ab7-45d3-a58f-1b742dc61cb7"); }
        }
    }
}
