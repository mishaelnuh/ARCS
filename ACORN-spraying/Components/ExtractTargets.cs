﻿using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static ACORNSpraying.PathGeneration;
using static ACORNSpraying.Miscellaneous;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;

namespace ACORNSpraying
{
    public class ExtractTargets : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.secondary; }

        private bool UseDegrees { get; set; } = false;

        public ExtractTargets()
          : base("Extract Targets", "ACORN_Extract",
              "Extract robot targets, normals, and speed.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddParameter(new Param_SprayPath(), "sprayPath", "sprayPath", "Spray paths", GH_ParamAccess.item);
            pManager.AddBrepParameter("surf", "surf", "Spraying surface to align to.", GH_ParamAccess.item);
            pManager.AddNumberParameter("edgeDist", "edgeDist", "Distance to align edge to.", GH_ParamAccess.item);
            pManager.AddAngleParameter("edgeAngle", "edgeAngle", "Angle to apply at edge.", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("tolD", "tolD", "Tolerance distance for discretisation.", GH_ParamAccess.item, 10);
            pManager.AddNumberParameter("tolA", "tolA", "Tolerance angle for discretisation.", GH_ParamAccess.item, 10);

            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
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
            Brep surf = null;
            double edgeDist = 0;
            double angle = 0;
            double tolD = 0;
            double tolA = 0;

            DA.GetData(0, ref pathRaw);
            DA.GetData(1, ref surf);
            DA.GetData(2, ref edgeDist);
            DA.GetData(3, ref angle);
            DA.GetData(4, ref tolD);
            DA.GetData(5, ref tolA);

            if (UseDegrees)
                angle *= Math.PI / 180;

            var path = (pathRaw as GH_SprayPath).Value;

            List<Point3d> targets = new List<Point3d>();
            List<Vector3d> normals = new List<Vector3d>();
            List<double> speeds = new List<double>();

            var surfBoundary = surf.Boundary();

            for(int i = 0; i < path.Count; i++)
            {
                var polylineCurve = path[i].Curve.ToPolyline(tolD, tolA, 0, path[i].Curve.GetLength());
                polylineCurve.RemoveShortSegments(ToleranceDistance * 10);

                var polyline = polylineCurve.ToPolyline();

                // Remove the first target you use
                if (i != 0)
                    polyline.RemoveAt(0);

                foreach(var target in polyline)
                {
                    surfBoundary.ClosestPoint(target, out double tmp);
                    surf.Faces[0].ClosestPoint(surfBoundary.PointAt(tmp),
                        out double borderU, out double borderV);
                    surf.Faces[0].ClosestPoint(target,
                        out double targetU, out double targetV);

                    var distLength = surf.Faces[0].ShortPath(
                        new Point2d(borderU, borderV),
                        new Point2d(targetU, targetV),
                        ToleranceDistance).GetLength();

                    targets.Add(target);
                    normals.Add(AlignNormal(surf, target, angle, path[i].IsEdge || distLength <= edgeDist + ToleranceDistance));
                    speeds.Add(path[i].Speed);
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
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("0edd9e29-8f5d-443d-93a1-879299a6676b"); }
        }
    }
}