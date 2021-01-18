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
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAAKBSURBVEhLzZU5axZRFIaP+5K4o4j7viDBxkZbK7EQlAgqIm6JcSuUYCHYRIyNiEUKF7TwL9iIhZWIuBQWCv4BQRsXUBCMPs/k3jDJzPcxYOOBh5lvlvOec+975ov/PSbA5ITnjcOHJyYmJXKiKYk98DGxH2aC1+uFLkZ0nI7YcjZi15mIHhiAB1x7wvE917+dj/iT6YAcvD4ML+AwcKsoZGyQqPNkRFdfxO5TEX29EYMcH/L7Kfc+nIv43kagzB2YD3YzJmzNpZgKtjsHFsBiWAarYR1sgI2AfnxOXIEb8AsUodZCpNJJFlF9GsyATpgNc8GXFF0IS2AFrII1sB4GQYFXsBQstDaabHK504ydKfATFPbaP4WFZGGLWQ5ZwK7suG2UE3g+Pkya79vlJVDgNbQVMFmdz12WvEQD8DtxCy5D3mSc3XqJ9sIzGE4uLCxZtqhQQbpbsel9cMNrN/kmjD6co6HAG2Aei+S6q2LTfeCDtnkN+iH7/AJshc2wKTEEeYnuQResBWfG5C7jaLjmLosCJrcKLefRzXI9rUrvOwOLwCXIMyArwevOzZjKDR3wAxTYDk6ticoeH7/JDqLJ5iU8936d2wrFLLAD9LMJyzase7F83/OW4QPPQYHr0FSgcbhEByFv8lXogXZzIFnc9y1GLKRSjBdMcBsUKchRZ1M+31/w5Fs+qY/4nA9x7Kei7mMR27pH9qwiYiXa6wS8hLaDxqh+JfE7/jce822+e5RJPhJx6AAm2TnyxbWriohtK6KXj8MnqJsDLez/gr7XqlpWi2Y36bBaAcNOKLqwqZutv6U8B1YoFuP/xCzwnengPrVMnsObduO+tJqDBpsc8ReUXtz4EN4XjAAAAABJRU5ErkJggg=="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))