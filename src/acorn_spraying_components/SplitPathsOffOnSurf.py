from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_spraying.path_processing import split_path_on_off_surf

class SplitPathsOffOnSurf(component):
    @property
    def Exposure(self):
        return Grasshopper.Kernel.GH_Exposure.secondary
    
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "SplitPathsOffOnSurf", "ACORN_SplitPathSurf", """Split the path into parts and flag each segment as either spraying on or off the surface. Set spray_diameter to 0 to split paths at surface edge.""", "ACORN", "Spraying")
        return instance

    def get_ComponentGuid(self):
        return System.Guid("4b05904f-e648-4070-aff3-016fc94f99bc")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "path", "path", "Spray path.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "surf", "surf", "Surface to get bounds from.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Surface()
        self.SetUpParam(p, "extended_surf", "extended_surf", "Extended surface.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "spray_diameter", "spray_diameter", "Distance from which a point is considered not to be spraying on the surface.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "paths", "paths", "Split paths.")
        self.Params.Output.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Boolean()
        self.SetUpParam(p, "on_surface", "on_surface", "Flag to tell if path is spraying on or off the surface.")
        self.Params.Output.Add(p)
    
    def SolveInstance(self, DA):
        path = self.marshal.GetInput(DA, 0)
        surf = self.marshal.GetInput(DA, 1)
        extended_surf = self.marshal.GetInput(DA, 2)
        extended_surf = extended_surf.Surfaces[0] # Needed because the component automatically converts surface to brep
        spray_diameter = self.marshal.GetInput(DA, 3)
        
        paths, on_surface = split_path_on_off_surf(path, surf, extended_surf, spray_diameter)

        if paths is not None:
            self.marshal.SetOutput(paths, DA, 0, True)
            self.marshal.SetOutput(on_surface, DA, 1, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAAIYSURBVEhL3ZVPS1RRGIdfaEYkISel0QakZRujVYtwpV9AZtEmcB9BixYFQYsgsK0USk2klo5aVvTnFkU06Gdo72eopJWL1+edc8/l6L2jR+6uAw8znJn7/O5573vukf9yVKBaEnPkR3tEfrfPiZZhbUT+oDoDuZCK/UG3YQs68BN+wHf4Bl8hgc/wET7Ae3gLm/DaheAag1xItRsQKU/ui54/60juMYdcN7KAizBsThP74QJ6yDuzojOTohfqov19on0V5tPRIMTkupYFjEOPgEPy3Xeizavdiw6QC0Cuq1nAJegREMj3KMfUZSesDYg+vC76a070X9uVpVuiGt/vOLmuxAQEZWndcvJRRDvPmDv0QH1ZvFxfxgSk8uSBaJUyMK93m8yZnHIdJdflmADrli+ijSE+02ErKJRTqlCuizEByK0VcwERcn0RE2B9/sn1eVaiaeYi5Po8JgC536GtGy5glE7ZeczcMXKlEY4P8Nufmu9xx1PjLqR2mja9Rps+ok2RJbdp0UFHQreZXJ/GBITd8oaNxl02r7iQkAMbjRWaXBdiAgJ5WPMOG2tmglfFMK+KKgGnmE9Hg1WYXOdjAkL5Oviav4Kg5laWrEQ3mUOuT2ICjpIvgX+gLUjL4uVKI6QBxS+71Xr5A2elLn9xFb6u7XCwQ8IOC/uD3YUt9STYNXZt4YFjw4dYehkK5X7YD7a0MqRykX0PYt7ajyvL3AAAAABJRU5ErkJggg=="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))