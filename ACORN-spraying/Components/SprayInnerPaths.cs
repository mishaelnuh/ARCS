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
            pManager.AddBrepParameter("regionSurf", "regionSurf", "Region to spray on. Input as Brep in order to maintain trims.", GH_ParamAccess.item);
            pManager.AddNumberParameter("dist", "dist", "Distance between path lines.", GH_ParamAccess.item);
            pManager.AddNumberParameter("expandDist", "expandDist", "Length to extend path lines past surface bounds.", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("numGeo", "numGeo", "Number of geodesics to calculate paths from.", GH_ParamAccess.item, 10);

            pManager[2].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("paths", "paths", "Spray paths.", GH_ParamAccess.list);
            pManager.AddCurveParameter("segments", "segments", "Flattened list of curve segments.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("isConnector", "isConnector", "Flags to see if curve segment is a connector.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep surf = null;
            Surface extSurf = null;
            Brep regionSurf = null;
            double dist = 0;
            double expandDist = 0;
            double numGeo = 0;

            DA.GetData(0, ref surf);
            DA.GetData(1, ref extSurf);
            DA.GetData(2, ref regionSurf);
            DA.GetData(3, ref dist);
            DA.GetData(4, ref expandDist);
            DA.GetData(5, ref numGeo);

            List<List<Curve>> segments;
            List<List<bool>> isConnector;
            var res = SprayInnerPaths(surf, extSurf, dist, expandDist, (int)numGeo, out segments, out isConnector);

            if (regionSurf != null)
            {
                List<Curve> trimmedPaths = new List<Curve>();
                List<List<Curve>> trimmedSegments = new List<List<Curve>>();
                List<List<bool>> trimmedConnector = new List<List<bool>>();
                for (int i = 0; i < segments.Count; i++)
                {
                    var importantSegments = new List<Curve>();

                    var filteredSegments = segments[i]
                        .Zip(isConnector[i], (segment, flag) => new {
                            segment = segment,
                            flag = flag,
                        })
                        .Where(item => !item.flag)
                        .Select(item => item.segment)
                        .ToList();

                    foreach(var c in filteredSegments)
                    {
                        List<Curve> insideCurves;
                        Miscellaneous.TrimCurveSurface(c, regionSurf, out insideCurves, out _);
                        importantSegments.AddRange(insideCurves);
                    }

                    List<Curve> newTrimmedSegments;
                    List<bool> newTrimmedConnector;
                    var joinedCurve = ConnectGeometries(importantSegments.Select(s => s as GeometryBase).ToList(),
                        false, out newTrimmedSegments, out newTrimmedConnector);
                    trimmedPaths.Add(joinedCurve);
                    trimmedSegments.Add(newTrimmedSegments);
                    trimmedConnector.Add(newTrimmedConnector);
                }

                DA.SetDataList(0, trimmedPaths);
                DA.SetDataList(1, trimmedSegments.SelectMany(x => x).ToList());
                DA.SetDataList(2, trimmedConnector.SelectMany(x => x).ToList());
            }
            else
            {
                DA.SetDataList(0, res);
                DA.SetDataList(1, segments.SelectMany(x => x).ToList());
                DA.SetDataList(2, isConnector.SelectMany(x => x).ToList());
            }
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
