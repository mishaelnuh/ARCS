using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using static ARCS.Miscellaneous;

namespace ARCS
{
    public class ExtendSurf : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.secondary; }

        public ExtendSurf()
          : base("Extend Surface", "ARCS_ExtendSurf",
              "Extends a surface using consecutive bounding boxes.",
              "ARCS", "3 | Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddPlaneParameter("plane", "plane", "Plane to orient extended surface borders to.", GH_ParamAccess.item, Plane.WorldYZ);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep surf = null;
            Plane plane = new Plane();

            DA.GetData(0, ref surf);
            DA.GetData(1, ref plane);

            var res = ExtendSurf(surf, plane);

            DA.SetData(0, res);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return Properties.Resources.extend;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("db5fc8f7-9114-4b2a-9448-d72ba86cff35"); }
        }
    }
}
