from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr

class VisualiseSprayCircles(component):
    @property
    def Exposure(self):
        return Grasshopper.Kernel.GH_Exposure.quarternary

    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "VisualiseSprayCircles", "ACORN_VzSprayCirc", """Draw circles along the spray path to show spraying.""", "ACORN", "Spraying")
        return instance
    
    def __init__(self):
        self.spray_circles = []
        self.bounding_box = Rhino.Geometry.BoundingBox()

    def get_ComponentGuid(self):
        return System.Guid("be98312d-72fd-4b62-aaa3-dd1f30f75e95")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "surf", "surf", "Surface to spray on.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "path", "path", "Spray path.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.list
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "spray_diameter", "spray_diameter", "Diameter of circles drawn.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "circles", "circles", "Spray circles.")
        self.Params.Output.Add(p)
    
    def SolveInstance(self, DA):
        self.spray_circles = Grasshopper.DataTree[Rhino.Geometry.Circle]()
        self.bounding_box = Rhino.Geometry.BoundingBox()

        surf = self.marshal.GetInput(DA, 0)
        surf_surf = surf.Surfaces[0]
        path = self.marshal.GetInput(DA, 1)
        spray_diameter = self.marshal.GetInput(DA, 2)
        
        # Divide path to lengths of 10% of diameter
        for i,p in enumerate(path):
            params = p.DivideByLength(spray_diameter * 0.1, True)
            points = [p.PointAt(t) for t in params]
            brep_point = [surf.ClosestPoint(p) for p in points]
            surf_point = [surf_surf.ClosestPoint(p) for p in brep_point]
            normals = [surf_surf.NormalAt(p[1], p[2]) for p in surf_point]

            # Get circles
            circles = [Rhino.Geometry.Circle(
                Rhino.Geometry.Plane(z[0], z[1]), spray_diameter / 2) for z in zip(points, normals)]
            
            self.spray_circles.AddRange(circles, Grasshopper.Kernel.Data.GH_Path(i))

            # Get bounding box
            for c in circles:
                self.bounding_box.Union(c.BoundingBox)
        
        self.marshal.SetOutput(self.spray_circles, DA, 0, True)
        
    def DrawViewportWires(self, args):
        circle = self.spray_circles
        circle.Flatten()
        circle = circle.Branch(0)
        for c in circle:
            args.Display.DrawCircle(c, System.Drawing.Color.Cyan)

    def get_ClippingBox(self):
        return self.bounding_box
    
    def get_IsPreviewCapable(self):
        return True

    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAAMKSURBVEhLxZXfS1NhGMe/uYGCZJaGQT/MLFwmlNmPTcs0584ksrwI/Au6qstI+hO6EqJRpk4lwqIIqS5UzKAiNfwVEVREYJIWRMdtupWbp+8zdG1n77S7Hviws/O+5/O853mfcw7+Z1iINQ75b440stacxNCrXxSGtNc3w9rYl4g2HlnUxqb9zldtg4daSzicQdJ6gIJewDMATA0CEf5OPwbamoHYHJIcQdewO+IeDxh1k4aZsDY+P+LobuwCzj4FfBQbZpho3gM0UpVFEpPoNc93pZKvIEkoWVDJV5AkFwE7lYlJQu7RWyqpGZXUzAOgm8otRMoVDYvUXCU0oxKa6QNm6NxJ1kftDKtsqEpoRiU0wz0K07mHbIzaGVZ2y5p3sOQaMYaRqZTGww6bpbOIbIraGVa/a8irksZwTxjBzeeNACoMHUeMKRQZb5FnvER6UoLbwEM6E+7AMnC4ZZ90iVJOQtubonIVP1BmfEahMYlc4xkswTPAaToLSGwPJDKkz1VJfhddVYpV6LAHG5Bzkr4d4oyal0N6Nuuarcnxtar/Dp/m2Yg2EV4oua77ULGkkqViFKUtdCUlkJAkG8hWIjUs/gb7B5VkNWZ5Da+VNs0kf8MLl430dMDt74Dm86Cm9yccIZVkNXQ4gtTtJtlRsUQrtKJOuPUu1BnxvEflL5VkNeZQvkCljcTaVFbfY5YLT+D0qySrMYOjH6lMSGBhWQKqBF3QFudwTClKxQhKO+hMKJE1dYI64x2O//M+sP6BU8ipojOfxDbZ2o7aRyq50InKIR/KIyphPJyzdA/FTfTJayKPpItcwnIF9oNeaHNmeTs0vRb5Dfex99IcV6cSC7Lyu7BdpquYbCNJH52MC9hfcQPOPi/LxWR6M6r761FYzzFZUeE55Ja9QannOzeRnTIvzMD+aRgH2p3IPrE8T+TyDkr6Pq8j8u6QD4W8R2STBHlg5HZlRfIQyrHUV8akUwQ5lnMr81J+/CWJPN4ySVYhSFI5J2Nyy1JX2TzpEGlDQY7lnIzFlQX4A9+MMHXQlVT1AAAAAElFTkSuQmCC"
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))