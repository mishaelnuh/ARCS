using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using static ACORNSpraying.PathGeneration;
using static ACORNSpraying.Miscellaneous;
using Grasshopper.Kernel.Parameters;

namespace ACORNSpraying
{
    public class AlignNormals : GH_Component
    {
        public override GH_Exposure Exposure { get => GH_Exposure.secondary; }

        private bool useDegrees { get; set; } = false;

        public AlignNormals()
          : base("Align Normals", "ACORN_Align",
              "Aligns spray normals to the target surface.",
              "ACORN", "Spraying")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("point", "point", "Location of frame.", GH_ParamAccess.item);
            pManager.AddBrepParameter("surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddAngleParameter("angle", "angle", "Angle to spray at in radians for the edges.", GH_ParamAccess.item, 0);

            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddVectorParameter("normal", "normal", "Spray normal to align to.", GH_ParamAccess.list);
        }

        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();

            useDegrees = false;
            Param_Number angleParameter = Params.Input[2] as Param_Number;
            if (angleParameter != null)
                useDegrees = angleParameter.UseDegrees;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Point3d point = new Point3d();
            Brep surf = null;
            double angle = 0;

            DA.GetData(0, ref point);
            DA.GetData(1, ref surf);
            DA.GetData(2, ref angle);

            if (useDegrees)
                angle *= Math.PI / 180;

            // Get surface boundary
            var pTest = surf.ClosestPoint(point);
            if (pTest.DistanceToSquared(point) > ToleranceDistance * ToleranceDistance * 1e6)
            {
                DA.SetData(0, -Vector3d.ZAxis);
                return;
            }

            // Get normal of surface
            double u, v;
            surf.Surfaces[0].ClosestPoint(point, out u, out v);
            var surfNorm = surf.Surfaces[0].NormalAt(u, v);

            if (surfNorm.Z > 0)
                surfNorm.Reverse();

            var boundarySegments = surf.Boundary().DuplicateSegments();
            var normalVecs = new List<Vector3d>();
            foreach(var seg in boundarySegments)
            {
                double t;
                if (seg.ClosestPoint(point, out t, ToleranceDistance))
                {
                    var curveTangent = seg.TangentAt(t);

                    // Check to make sure rotation will be in the right direction
                    var testVector = Vector3d.CrossProduct(-surfNorm, curveTangent);
                    var testPoint1 = point + testVector;
                    var testPoint2 = point - testVector;
                    var testDist1 = surf.ClosestPoint(testPoint1).DistanceToSquared(testPoint1);
                    var testDist2 = surf.ClosestPoint(testPoint2).DistanceToSquared(testPoint2);

                    if (testDist1 > testDist2)
                        curveTangent.Reverse();

                    // Rotate surfNorm
                    var norm = new Vector3d(surfNorm);
                    norm.Rotate(-angle, curveTangent);
                    norm.Unitize();

                    normalVecs.Add(norm);
                }
            }

            // Add all the norms from boundary curves
            var finalNorm = new Vector3d(0,0,0);
            if (normalVecs.Count == 0)
            {
                finalNorm = surfNorm;
            }
            else
            {
                foreach (var n in normalVecs)
                    finalNorm += n;
                finalNorm.Unitize();
            }

            DA.SetData(0, finalNorm);
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
            get { return new Guid("50e8c221-6b81-4910-b4d1-e0c986b7c9e3"); }
        }
    }
}
