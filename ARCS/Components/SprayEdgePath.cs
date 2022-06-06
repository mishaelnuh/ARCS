using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using static ARCS.PathGeneration;

namespace ARCS
{
    public class SprayEdgePath : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.primary; }

        public SprayEdgePath()
          : base("Spray Edge Path", "ARCS_SprayEdge",
              "Generates edge spray path.",
              "ARCS", "1 | Generation")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddPointParameter("startP", "startP", "Starting point of spray to align seam.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("speed", "speed", "Spraying speed.", GH_ParamAccess.item);

            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new Param_SprayPath(), "sprayPath", "sprayPath", "Spray paths", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep surf = null;
            Surface extSurf = null;
            Point3d startP = new Point3d();
            double expandDist = 0;
            double speed = 0;

            DA.GetData(0, ref surf);
            DA.GetData(1, ref extSurf);
            DA.GetData(2, ref startP);
            DA.GetData(3, ref expandDist);
            DA.GetData(4, ref speed);

            var res = SprayEdgePath(surf, extSurf, startP, expandDist, speed);

            DA.SetData(0, new GH_SprayPath(res));
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
