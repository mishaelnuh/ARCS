from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_spraying.path_generation import connect_paths_through_bounds

class ConnectPathsThroughBounds(component):
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "ConnectPathsThroughBounds", "ACORN_Connect", """Connects spray paths and other points of interests through connectors made from a curve offset from the surface edges.""", "ACORN", "Spraying")
        return instance

    def get_ComponentGuid(self):
        return System.Guid("a2185e97-3871-44d0-ac9d-ba26998198ad")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Geometry()
        self.SetUpParam(p, "geometries", "geometries", "Geometries to connect. Only curves and points.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.list
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "surf", "surf", "Surface to get bounds from.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Surface()
        self.SetUpParam(p, "extended_surf", "extended_surf", "Extended surface. Use extend_surf or untrim the Brep.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Number()
        self.SetUpParam(p, "overspray_dist", "overspray_dist", "Length to extend path lines past surface bounds.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "connected_path", "connected_path", "Connected curve.")
        self.Params.Output.Add(p)
    
    def SolveInstance(self, DA):
        geometries = self.marshal.GetInput(DA, 0)
        surf = self.marshal.GetInput(DA, 1)
        extended_surf = self.marshal.GetInput(DA, 2)
        extended_surf = extended_surf.Surfaces[0] # Needed because the component automatically converts surface to brep
        overspray_dist = self.marshal.GetInput(DA, 3)
        
        result = connect_paths_through_bounds(geometries, surf, extended_surf, overspray_dist)

        if result is not None:
            self.marshal.SetOutput(result, DA, 0, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAAJxSURBVEhLxdS9S9VRGMDx26v0YmnvaVkmkklLBEG0NVoECToZiOGmkuCiNDg0BQ5BUlNz/4FQQw21NLi4OBhUYNqL2BAOptb3K8+5/PTertcKOvDB3/X3O+c55znPObn/3bZge/D5n7brmAnt2I0d2Iat+KuAdv74MxrPK5jGGzxGBw5iJwy26WZKZmJ8AxTzHSM4AgNtqhnAtHzCFwzBlHXjASaQAvl8AXtQdtr80JwfwymcDnU4Gf9rw3sY5B0asBdltxRkf6gM++L3YZzDFAzyFKarAmU3N9AON2E1zcLUGci8G+gKlrGEi/E/K23DdhWjGMdy7LczNYipc3UO5Iqew3f3cBQlV2GOX8IOeanx/BnugTNNh7AHvnsG+7sXRcvX6B/gx/N4hC4M4iusqF5kA7hXA7CPaboN98d3Be0h/NDSu4RG1Icz8dfBU4oc3JnOxgLta2mfQHqfbzfwA350H1aIyz0Aa9xlO2tlOzvTbID1KVxtfpy9GubQhEPw/vG97JDSkpq/O2E/r5R+FATwIXs1eA0YoDrelWoGuwb7vcV5uPI1KXIQ6/sb/HABLTDXG90zlmqqOi9C98oD58rzzUhGPA5LzY+toluoQkqTG+pk5LN9nsDvndxleJ14EAvK1AE8NN4pr2EnvYJ13gcPmVViWd5FKulFuA/2LXnQDGLF1MBKMlUpUOzQ6h5lTaIVlnQtnGQ+979ru+BhccOG8QJLMb6DeveMwVWdhXl3Uqaz6Aku1txcZ+OSrQpPqDXuab6DZnjwPFSWsysve/DU7GA+PWSuyEDWuLIDpyL442YgB7FqsqfZaioxcC73C5ndyX2PDBNxAAAAAElFTkSuQmCC"
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))