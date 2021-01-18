from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_spraying.misc import trim_curve_boundary

class VisualiseWaste(component):
    @property
    def Exposure(self):
        return Grasshopper.Kernel.GH_Exposure.quarternary

    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "VisualiseWaste", "ACORN_VzSprayCirc", """Visualise the waste from spray path. Use inputs from SplitPathsOffOnSurf component.""", "ACORN", "Spraying")
        return instance
    
    def __init__(self):
        self.on_surf_waste = []
        self.off_surf_waste = []
        self.preview_text = ""
        self.preview_loc = Rhino.Geometry.Point3d(0, 0, 0)
        self.bounding_box = Rhino.Geometry.BoundingBox()

    def get_ComponentGuid(self):
        return System.Guid("4df5a622-599e-11eb-ae93-0242ac130002")
    
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
        p.Access = Grasshopper.Kernel.GH_ParamAccess.tree
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Boolean()
        self.SetUpParam(p, "on_surface", "on_surface", "Whether path is on or off the surface.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.tree
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        return
    
    def SolveInstance(self, DA):
        self.on_surf_waste = []
        self.off_surf_waste = []
        self.preview_text = ""
        self.preview_loc = Rhino.Geometry.Point3d(0, 0, 0)
        self.bounding_box = Rhino.Geometry.BoundingBox()

        surf = self.marshal.GetInput(DA, 0)
        path = self.marshal.GetInput(DA, 1)
        on_surface = self.marshal.GetInput(DA, 2)

        # Flatten trees
        path.Flatten()
        path = path.Branch(0)
        on_surface.Flatten()
        on_surface = on_surface.Branch(0)
        
        # Get bounds
        bounds = surf.GetWireframe(-1)
        bounds = Rhino.Geometry.Curve.JoinCurves(bounds)[0]

        # Separate path curves
        on_surf_length = 0.0
        off_surf_length = 0.0
        total_length = 0.0
        for i in range(len(path)):
            total_length += path[i].GetLength()
            if (on_surface[i]):
                inside, outside = trim_curve_boundary(path[i], bounds)
                self.on_surf_waste.extend(outside)
                for c in outside:
                    on_surf_length += c.GetLength()
            else:
                self.off_surf_waste.append(path[i])
                off_surf_length += path[i].GetLength()

        # Preview text
        self.preview_text = "Off surface waste = {0}\n".format(off_surf_length)
        self.preview_text += "On surface waste = {0}\n".format(on_surf_length)
        self.preview_text += "Total length = {0}".format(total_length)

        # Preview location
        area_mass_prop = Rhino.Geometry.AreaMassProperties.Compute(surf)
        self.preview_loc = area_mass_prop.Centroid

        # Get bounding box
        for c in path:
            self.bounding_box.Union(c.GetBoundingBox(False))
        
    def DrawViewportWires(self, args):
        for c in self.on_surf_waste:
            args.Display.DrawCurve(c, System.Drawing.Color.Blue)

        for c in self.off_surf_waste:
            args.Display.DrawCurve(c, System.Drawing.Color.Red)
        
        args.Display.Draw2dText(self.preview_text, System.Drawing.Color.Black, self.preview_loc, True, 20)

    def get_ClippingBox(self):
        return self.bounding_box
    
    def get_IsPreviewCapable(self):
        return True

    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAALtSURBVEhLzZTbSxRhGMaf2ZO6a6WJpph4qig620WgRf9G0B8QRJRa4GWXQgdadP0PhKAg6qqupC4MJCKiGwnURBN31101ohnt4ut5xh2dXdd2vOuFH7u8873Pe/pm8L9ZtPDrt6C+imZITQEJiEq+wKYgl/HxcZdKPnKIBEqiqnTQpNNpl0wm4yJfNpstQj5ykjSTwF3UqEK/uCgVX1lZ8Tq5SPaVIKrASuK+BGeJEqj7QOYur1RcPr+4kI/sO4EXWCTuUSLuUXZEFokUCMtB216yJ+5P4onncjkX+cjuHfQDPUMhzA4Am/0WnvMaNNBdpWfngbrhK33mfjRihjvbzdeJiW1hv7jYa8nWkIXZqQ6Yn+dgUrWwB0KYPAq0cpj194B3Y3Uxs9wN874JTNJRVjyfz++55Agr31g/A7PJBL9Pw4wl4ND3gUyO1sBe7YJZIz/a4HYiccYViQv5SJkEFl6kErB/nWICsn6CSeKwxyieY2erJEPx1MGYGe7r9YRcVHWhcj9FIwpr5qo2FYezdowJOI61Tph8O6uk8HIrzNMYnFvApwNAL2MuEAlp3kJVC/nKvslVmvkdjmW0GrYnnKVwpgVmZEf8Ms8eJ52EIWghEitl97dIC+Vtmhypgi3hNIWXj8AscbHJKOzbwEdG9fBoG2EuxIlEhObtXWmPHXsAxHRbkjHYS80F4UYutQFm4TDMXB3MkzAcdjjVBHQxJLEVubNIn+32DVp4JvFFVivheQonI3CSYdgzB5mgFuZbHOaRBYedvGZII3HfkyAW5mg2Zii6SL7Xb1WrmWssj0NwpquZIAbzJQJzF/jDmHaiMXlv/D8twvHMvUnATLNaCVLYXahmrrE8BJzPIZiXFswgsMgYLblesRKoZJEbwFUmmden4ibwlsKX6O8mbZo5E75S5Xy+cA24Tn8H0U0JlEAfOW1d10uBqk4j0Jy1TKH/8umZzuisYhQbyLwkqkqta77+Jeq/fHqmMwHEgb9XyQuMkWm54AAAAABJRU5ErkJggg=="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))