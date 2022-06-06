using Grasshopper.Kernel;
using System;

namespace ARCS
{
    public class PathProperties : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.secondary; }
        public PathProperties()
          : base("Path Properties", "ARCS_PathProp",
              "Get spray path properties.",
              "ARCS", "3 | Utilities")
        {
        }
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Param_SprayPath(), "sprayPath", "sprayPath", "Spray paths", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("length", "length", "Total spray length.", GH_ParamAccess.item);
            pManager.AddNumberParameter("duration", "duration", "Total spray duration.", GH_ParamAccess.item);
            pManager.AddNumberParameter("minSpeed", "minSpeed", "Minimum spray speed.", GH_ParamAccess.item);
            pManager.AddNumberParameter("maxSpeed", "maxSpeed", "Maximum spray speed.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object pathRaw = null;

            DA.GetData(0, ref pathRaw);

            var path = (pathRaw as GH_SprayPath).Value;

            DA.SetData(0, path.GetLength());
            DA.SetData(1, path.GetDuration());
            DA.SetData(2, path.MinSpeed);
            DA.SetData(3, path.MaxSpeed);
        }
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return Properties.Resources.properties;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("63585a98-89b3-43be-b69c-230cc4b5790c"); }
        }
    }
}
