using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using static ACORNSpraying.PathGeneration;

namespace ACORNSpraying
{
    public class SprayInnerPaths : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.primary; }

        public SprayInnerPaths()
          : base("Spray Inner Paths", "ACORN_SprayInner",
              "Generates inner spray paths.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddSurfaceParameter("extSurf", "extSurf", "Extended surface. Use ExtendSurf or untrim the Brep.", GH_ParamAccess.item);
            pManager.AddNumberParameter("dist", "dist", "Distance between path lines.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("numGeo", "numGeo", "Number of geodesics to calculate paths from.", GH_ParamAccess.item, 10);

            pManager[3].Optional = true;
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("paths", "paths", "Spray paths.", GH_ParamAccess.list);
            pManager.AddCurveParameter("segments", "segments", "Curve segments.", GH_ParamAccess.tree);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if curve segment is a connector.", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep surf = null;
            Surface extSurf = null;
            double dist = 0;
            double expandDist = 0;
            double numGeo = 0;

            DA.GetData(0, ref surf);
            DA.GetData(1, ref extSurf);
            DA.GetData(2, ref dist);
            DA.GetData(3, ref expandDist);
            DA.GetData(4, ref numGeo);

            List<List<Curve>> segments;
            List<List<bool>> isConnector;
            var res = SprayInnerPaths(surf, extSurf, dist, expandDist, (int)numGeo, out segments, out isConnector);

            var branchIndex = DA.ParameterTargetIndex(0);

            var segmentTree = new DataTree<Curve>();
            
            for (int i = 0; i < segments.Count; i++)
            {
                segmentTree.AddRange(segments[i], DA.ParameterTargetPath(1).AppendElement(branchIndex).AppendElement(i));
            }

            var isConnectorTree = new DataTree<bool>();
            for (int i = 0; i < isConnector.Count; i++)
                isConnectorTree.AddRange(isConnector[i], DA.ParameterTargetPath(2).AppendElement(branchIndex).AppendElement(i));

            DA.SetDataList(0, res);
            DA.SetDataTree(1, segmentTree);
            DA.SetDataTree(2, isConnectorTree);
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
