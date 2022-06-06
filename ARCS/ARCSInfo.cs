using Grasshopper.Kernel;
using System;
using System.Drawing;
using static ARCS.Miscellaneous;

namespace ARCS
{
    public class ARCSInfo : GH_AssemblyInfo
    {
        public ARCSInfo()
        {
            ToleranceDistance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            ToleranceAngle = Rhino.RhinoDoc.ActiveDoc.ModelAngleToleranceRadians;
            Rhino.RhinoDoc.DocumentPropertiesChanged += DocumentPropertiesChanged;
        }

        private void DocumentPropertiesChanged(object sender, Rhino.DocumentEventArgs e)
        {
            ToleranceDistance = e.Document.ModelAbsoluteTolerance;
            ToleranceAngle = e.Document.ModelAngleToleranceRadians;
        }

        public override string Name
        {
            get
            {
                return "ARCS";
            }
        }
    public override Bitmap Icon
    {
        get
        {
            //Return a 24x24 pixel bitmap to represent this GHA library.
            return null;
        }
    }
    public override string Description
    {
        get
        {
            //Return a short string describing the purpose of this GHA library.
            return "";
        }
    }
    public override Guid Id
    {
        get
        {
            return new Guid("5f75c9b2-d1eb-4cd5-8fcf-5361763f611a");
        }
    }

    public override string AuthorName
    {
        get
        {
            //Return a string identifying you or your company.
            return "";
        }
    }
    public override string AuthorContact
    {
        get
        {
            //Return a string representing your preferred contact details.
            return "";
        }
    }
}
}
