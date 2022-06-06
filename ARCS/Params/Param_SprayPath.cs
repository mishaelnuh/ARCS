using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace ARCS
{
    public class Param_SprayPath : GH_PersistentParam<GH_SprayPath>, IGH_PreviewObject
    {
        private BoundingBox clippingBox;

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        //protected override Bitmap Icon => null;
        public bool Hidden { get; set; }
        public bool IsPreviewCapable => true;
        public BoundingBox ClippingBox => clippingBox;


        public Param_SprayPath() :
          base(new GH_InstanceDescription("SprayPath", "ARCS_SprayPath", "Spray path", "ARCS", "3 | Utilities"))
        { }

        public override System.Guid ComponentGuid
        {
            get { return new Guid("60a23022-1575-4bbb-8795-96d71618999b"); }
        }

        protected override GH_GetterResult Prompt_Singular(ref GH_SprayPath value)
        {
            throw new NotImplementedException();
        }

        protected override GH_GetterResult Prompt_Plural(ref List<GH_SprayPath> values)
        {
            throw new NotImplementedException();
        }

        public void DrawViewportWires(IGH_PreviewArgs args)
        {
            var enumerator = this.m_data.GetEnumerator();
            clippingBox = new BoundingBox();

            while (enumerator.MoveNext())
            {
                var curr = (GH_SprayPath)enumerator.Current;
                if (curr != null)
                    clippingBox.Union(curr.Preview(args, this.Attributes.Selected));
            }
        }

        public void DrawViewportMeshes(IGH_PreviewArgs args)
        {
        }
    }

    public class GH_SprayPath : GH_Goo<SprayPath>
    {
        public GH_SprayPath()
        {
            Value = new SprayPath();
        }

        public GH_SprayPath(SprayPath path)
        {
            Value = path.DeepClone();
        }
        public GH_SprayPath(GH_Goo<SprayPath> path)
        {
            Value = path.Value.DeepClone();
        }

        public override bool IsValid => Value != null;

        public override string IsValidWhyNot => throw new NotImplementedException();

        public override string TypeName => "Spray path";

        public override string TypeDescription => "Collection of spray curves";

        public override IGH_Goo Duplicate()
        {
            return new GH_SprayPath(Value.DeepClone());
        }

        public override string ToString()
        {
            return "Spray path: " + Value.Count() + " curves";
        }

        public BoundingBox Preview(IGH_PreviewArgs args, bool selected)
        {
            var selectedColor = Color.Green;

            if (Value == null)
                return new BoundingBox();

            var boundingBox = new BoundingBox();

            // Draw edges in the viewport.
            foreach (var sprayCurve in Value)
            {
                boundingBox.Union(sprayCurve.Curve.GetBoundingBox(false));

                Color color;
                if (selected)
                    color = selectedColor;
                else
                {
                    double percentageSpeed;
                    if (SprayPath.GlobalMaxSpeed == SprayPath.GlobalMinSpeed)
                        percentageSpeed = 0.5;
                    else
                    {
                        percentageSpeed = (sprayCurve.Speed - SprayPath.GlobalMinSpeed) / (SprayPath.GlobalMaxSpeed - SprayPath.GlobalMinSpeed);
                        percentageSpeed = Math.Min(Math.Max(percentageSpeed, 0), 1);
                    }

                    color = Color.FromArgb(
                        (int)(255 * Math.Min(1, percentageSpeed)),
                        0,
                        (int)(255 * (Math.Min(1, 1 - percentageSpeed)))
                    );
                }

                args.Display.DrawCurve(sprayCurve.Curve, color);
                args.Display.Draw2dText(sprayCurve.Speed.ToString(), color, sprayCurve.Curve.PointAtNormalizedLength(0), true);
            }

            return boundingBox;
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetByteArray("value", Value.Serialise());
            return base.Write(writer);
        }
        public override bool Read(GH_IReader reader)
        {
            var valArray = reader.GetByteArray("value");
            Value = valArray.Deserialise() as SprayPath;
            return base.Read(reader);
        }
    }

}
