from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_spraying.misc import extend_surf

class ExtendSurf(component):
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "ExtendSurf", "ACORN_ExtendSurf", """Extends a surface using consecutive bounding boxes.""", "ACORN", "Spraying")
        return instance
    
    def get_ComponentGuid(self):
        return System.Guid("e75cbe13-b20c-4cc5-9109-17a60ba4dea8")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Brep()
        self.SetUpParam(p, "surf", "surf", "Surface to extend. Input as Brep in order to maintain trims.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Plane()
        self.SetUpParam(p, "plane", "plane", "Plane to orient extended surface borders to.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Surface()
        self.SetUpParam(p, "extended_surf", "extended_surf", "Extended surface.")
        self.Params.Output.Add(p)
    
    def SolveInstance(self, DA):
        surf = self.marshal.GetInput(DA, 0)
        plane = self.marshal.GetInput(DA, 1)
        
        result = extend_surf(surf, plane)

        if result is not None:
            self.marshal.SetOutput(result, DA, 0, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAAFiSURBVEhL7dVNSsNAGMbxEa2foKKUivixESqopbhW1BPoQly5d+/aC7gVr6An8ACi+LEQvIUirjzB+H/ijMR2ZtKWFhF84UdDMnmfdJJMzG9Wn9OT2sQOBtH1EDW8wwMmMOT2da22YZ09TGMEpRYMIFm60ntkAdWSsRfl1lxWzAfnjCMZkr/6zO25sfammEIYP49oiJ/7HwEbtXDDRi6gCk2ppqup+qG51oCseahRjAtYdecHA1QK0ZPTacAakgGqYfQ0QAf/A/5IwFbd2Ouzr+12AnaRBVydNjfN8+MUdDL5HVCBVuFg6W1+sa7YbtcT9jGG4AqsNeTV9Q81KKKAA+hlDX5LFKABb3jHMdZRg/5+I9/4GUdYwSyiAdoxihksOgvQSWXoBnoa84hDLGMJc5hC8iPlQ3QVom1djZ6MPC0pOq4w3Vj9au51LNrclwZouiQ12K/A/otX2LiTUkiksTGfoePClZYQbhwAAAAASUVORK5CYII="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))