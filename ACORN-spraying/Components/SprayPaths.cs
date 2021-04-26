using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using static ACORNSpraying.PathGeneration;

namespace ACORNSpraying
{
    public class SprayPaths : GH_Component
    {
        public SprayPaths()
          : base("SprayPaths", "ACORN_SprayPaths",
              "Generates spray paths.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("dist", "dist", "Distance between path lines.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("paths", "paths", "Spray paths.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep surf = null;
            Surface extSurf = null;
            double dist = 0;
            double expandDist = 0;

            DA.GetData(0, ref surf);
            DA.GetData(1, ref extSurf);
            DA.GetData(2, ref dist);
            DA.GetData(3, ref expandDist);

            var res = SprayPaths(surf, extSurf, dist, expandDist);

            DA.SetDataList(0, res);
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
            get { return new Guid("4b3c5e3c-3538-43dd-b924-c14b5f2c9294"); }
        }
    }
}
