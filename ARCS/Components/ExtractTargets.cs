using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static ARCS.Miscellaneous;

namespace ARCS
{
    public class ExtractTargets : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.secondary; }

        private bool UseDegrees { get; set; } = false;

        public ExtractTargets()
          : base("Extract Targets", "ARCS_Extract",
              "Extract robot targets, normals, and speed.",
              "ARCS", "3 | Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Param_SprayPath(), "sprayPath", "sprayPath", "Spray paths", GH_ParamAccess.item);
            pManager.AddBrepParameter("surf", "surf", "Spraying surfaces to align to.", GH_ParamAccess.list);
            pManager.AddNumberParameter("edgeDist", "edgeDist", "Distance to align edge to.", GH_ParamAccess.item);
            pManager.AddAngleParameter("edgeAngle", "edgeAngle", "Angle to apply at edge.", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("tolD", "tolD", "Tolerance distance for discretisation.", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("tolA", "tolA", "Tolerance angle for discretisation.", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("safeSpeed", "safeSpeed", "Speed for outside of surface.", GH_ParamAccess.item, 300);

            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("targets", "targets", "Spray targets.", GH_ParamAccess.list);
            pManager.AddVectorParameter("normals", "normals", "Spray normals.", GH_ParamAccess.list);
            pManager.AddNumberParameter("speeds", "speeds", "Spray speeds.", GH_ParamAccess.list);
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            UseDegrees = false;
            if (Params.Input[3] is Param_Number angleParameter)
                UseDegrees = angleParameter.UseDegrees;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object pathRaw = null;
            List<Brep> surfs = new List<Brep>();
            double edgeDist = 0;
            double angle = 0;
            double tolD = 0;
            double tolA = 0;
            double safeSpeed = 0;


            DA.GetData(0, ref pathRaw);
            DA.GetDataList(1, surfs);
            DA.GetData(2, ref edgeDist);
            DA.GetData(3, ref angle);
            DA.GetData(4, ref tolD);
            DA.GetData(5, ref tolA);
            DA.GetData(6, ref safeSpeed);

            if (UseDegrees)
                angle *= Math.PI / 180;

            var path = (pathRaw as GH_SprayPath).Value;

            List<Point3d> targets = new List<Point3d>();
            List<Vector3d> normals = new List<Vector3d>();
            List<double> speeds = new List<double>();

            var surfBoundaries = surfs.Select(s => s.Boundary()).ToList();
            var boundaries2d = surfBoundaries.Select(c => Curve.ProjectToPlane(c, Plane.WorldXY)).ToList();

            for (int i = 0; i < path.Count; i++)
            {
                var polylineCurve = path[i].Curve.ToPolyline(tolD, tolA, 0, path[i].Curve.GetLength());
                polylineCurve.RemoveShortSegments(ToleranceDistance * 10);

                var polyline = polylineCurve.ToPolyline();

                // Remove the first target you use
                if (i != 0)
                    polyline.RemoveAt(0);

                foreach (var target in polyline)
                {
                    targets.Add(target);

                    var addedNormal = false;
                    for (int j = 0; j < surfs.Count; j++)
                    {
                        var containment = boundaries2d[j].Contains(target, Plane.WorldXY, ToleranceDistance);
                        if (containment == PointContainment.Inside || containment == PointContainment.Coincident)
                        {
                            surfBoundaries[j].ClosestPoint(target, out double tmp);
                            surfs[j].Faces[0].ClosestPoint(surfBoundaries[j].PointAt(tmp),
                                out double borderU, out double borderV);
                            surfs[j].Faces[0].ClosestPoint(target,
                                out double targetU, out double targetV);

                            var distLength = surfs[j].Faces[0].ShortPath(
                                new Point2d(borderU, borderV),
                                new Point2d(targetU, targetV),
                                ToleranceDistance).GetLength();

                            normals.Add(AlignNormal(surfs[j], target, angle, path[i].IsEdge || distLength <= edgeDist + ToleranceDistance));

                            speeds.Add(path[i].Speed);

                            addedNormal = true;
                            break;
                        }
                    }

                    if (!addedNormal)
                    {
                        normals.Add(-Vector3d.ZAxis);
                        speeds.Add(safeSpeed);
                    }
                }
            }

            DA.SetDataList(0, targets);
            DA.SetDataList(1, normals);
            DA.SetDataList(2, speeds);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return Properties.Resources.extract;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("0edd9e29-8f5d-443d-93a1-879299a6676b"); }
        }
    }
}
