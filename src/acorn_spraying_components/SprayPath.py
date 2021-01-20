from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_spraying.path_generation import spray_path

class SprayPath(component):
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "SprayPath", "ACORN_Spray", """Generates spray path""", "ACORN", "Spraying")
        return instance
    
    def get_ComponentGuid(self):
        return System.Guid("1c28dafc-d26a-4b80-af41-c71a9c756a5b")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "surf", "surf", "Surface to spray. Input as Brep in order to maintain trims.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Surface()
        self.SetUpParam(p, "extended_surf", "extended_surf", "Extended surface. Use extend_surf or untrim the Brep.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "angle", "angle", "Angle to generate paths at in radians.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
        
        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "dist", "dist", "Distance between path lines.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "overspray_dist", "overspray_dist", "Length to extend path lines past surface bounds.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "path", "path", "Spray path.")
        self.Params.Output.Add(p)
    
    def SolveInstance(self, DA):
        surf = self.marshal.GetInput(DA, 0)
        extended_surf = self.marshal.GetInput(DA, 1)
        extended_surf = extended_surf.Surfaces[0] # Needed because the component automatically converts surface to brep
        angle = self.marshal.GetInput(DA, 2)
        dist = self.marshal.GetInput(DA, 3)
        overspray_dist = self.marshal.GetInput(DA, 4)
        
        result = spray_path(surf, extended_surf, angle, dist, overspray_dist)

        if result is not None:
            self.marshal.SetOutput(result, DA, 0, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAAQ8SURBVEhLrVZrbJNlFH7atWXt6GAwLmNc5CrIJCOYKMIPSAgQCAFiJIQRAwSBiIJkLlxcWtaB26DjzmALjMkgIjDKVDYuASMQbz8kBjXRmYAaMOE2sLRry7rjc75iNoTCBpzkSZr3e9/nnO95n3O+og1haYEEwkQ8n3ADjavNaPDYcGmlCXuXAtO6AUl8ZCWeOZGFCSR4DHKlDHJ2AfzerrjlsqDuXWAOn7fXPcbOp4mFQHq+DWE5C2mJ028h6jIhmm1CCbd1ItoZB9oa7wNDvakI/D9BzRuQU7Mg2/rh7qpEVFOvrtyeGDvVhlgOTNmVgVBL8uiXkHUpkBv7IeEayJbeCOTYsI3bU4k2vYkpzwIf30DOzIHUH4wl+DkfsnsYfzORnID4P4Z85EBoLjCbZ1SuVt9JQkEy/jy3EFL7JqSoA+QkZdn5IuQXD8mPE58Rh7k+BeI2IUR39eU5J/Fkd30AZBY6EVZJtPJ/fJCywZC1iZBQFdeqiUOUaTfE2xGyoxfC75mQz6N6H0+Wao0DF79bFiNXhGohG7tDKkcQwyFNKhnvoWYcxDcS8vdySJ4Vt5OB/jyub2E2iB4VLjOO8SIjwS+aE/jGQ6on8JJZfUUG5PxMyOUVrJ7SNRRxjxdS0h1BNgdbBF0Im0HWMpbxklZbUMVmCvgmQjZ0izXY6Sxq359yHCARK79JsgIHZH0ypG4R19YTayEXpkJcNnxPql6EwyDVWMmMHwK5HivqqybhTuRkrOqLbggbTTb3gAToFvmUqGSiElrVCdnDpLKOWEO4IJdm87I5WkjZh+hgkGtwMVKeibtXd3HjfUn+2gEpHRSrfHMa5N4+ru8l+XbalGsHaNWiJEqmjiK5fwmk0I5IlhkrSPlAAgs7dqrHgsDlrYj+lAfZ/xqkmP6/wEtuouaHR0G+YvfWU+vSPpCjr3K9kM5h4j/YIw3ct6UTojl2VJIvg0gndCAaoZ51LAbmuoBoBR3yQzYrVisqPoFcK6A927Hi9pCvqbPopbLhTrCQM0xeloao24nj5HmZeIHoTDzQcDrjU7KBsooRqJfPY+ShciabD9mezm6l/7+ZxnVWLipLLp9NZmIbmjzJOMfZnUmOfkTckWEdDKRx9vt9Y2jFl0hqp9Z8o9/eoS3ZrbWjm8mvUBqvE425iYZrhhHqf7WnnYgbdt5QVWlf3KujTMFNJNtKFEN+nwcpp/6N9P75sazcisjiJKNzVRYdEUrebM04kfA2MHZDKvxS2kwuvIOr9Hsxrcmqw+zyX1/hpOX+IYQ6RmVp1bg2ZQE9860I+9k413IgP86AHMlEWNfcZtyeDizhPq1aJelBdCQe7trHhE2bJS8BQX6Hr6+y4dv5wKbXaWU+G0oMILRTtWqVJP7MiRP6MdeqlGQgwbs3qu1NpBE68//76D916GEORsPPKYR2pZKq/Vr5bwL4F4JwGNvr19PiAAAAAElFTkSuQmCC"
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))

import GhPython
import System

class AssemblyInfo(GhPython.Assemblies.PythonAssemblyInfo):
    def get_AssemblyName(self):
        return "AcornSpraying"
    
    def get_AssemblyDescription(self):
        return """Robotic concrete spraying path generation."""

    def get_AssemblyVersion(self):
        return "0.1"

    def get_AuthorName(self):
        return "Mishael Nuh"
    
    def get_Id(self):
        return System.Guid("e11977f6-c9f2-467f-a890-1a202e80a286")