from ghpythonlib.componentbase import dotnetcompiledcomponent as component
import Grasshopper, GhPython
import System
import Rhino
import clr
from acorn_spraying.misc import trim_curve_boundary

class TrimCurveBoundary(component):
    @property
    def Exposure(self):
        return Grasshopper.Kernel.GH_Exposure.tertiary
    
    def __new__(cls):
        instance = Grasshopper.Kernel.GH_Component.__new__(cls,
            "TrimCurveBoundary", "ACORN_TrimCurveBoundary", """Trim curve using a closed boundary curve.""", "ACORN", "Spraying")
        return instance

    def get_ComponentGuid(self):
        return System.Guid("360c94ba-97ee-47da-9cde-83e03266d614")
    
    def SetUpParam(self, p, name, nickname, description):
        p.Name = name
        p.NickName = nickname
        p.Description = description
        p.Optional = True
    
    def RegisterInputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "curve", "curve", "Curve to trim.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "bounds", "bounds", "Closed curve to trim curve with.")
        p.Access = Grasshopper.Kernel.GH_ParamAccess.item
        self.Params.Input.Add(p)
    
    def RegisterOutputParams(self, pManager):
        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "inside_curves", "inside_curves", "Curves inside the bounds.")
        self.Params.Output.Add(p)

        p = Grasshopper.Kernel.Parameters.Param_Curve()
        self.SetUpParam(p, "outside_curves", "outside_curves", "Curves outside the bounds.")
        self.Params.Output.Add(p)
    
    def SolveInstance(self, DA):
        curve = self.marshal.GetInput(DA, 0)
        bounds = self.marshal.GetInput(DA, 1)

        inside_curves, outside_curves = trim_curve_boundary(curve, bounds)

        if inside_curves is not None and outside_curves is not None:
            self.marshal.SetOutput(inside_curves, DA, 0, True)
            self.marshal.SetOutput(outside_curves, DA, 1, True)
        
    def get_Internal_Icon_24x24(self):
        o = "iVBORw0KGgoAAAANSUhEUgAAABgAAAAYCAYAAADgdz34AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAAHYcAAB2HAY/l8WUAAANZSURBVEhLrZVdSJNRGMc3tzVdmSur9UXkoNaIhpab0UXgRd3UdY1dlmisizEopBurCyMXfRBZFFlCF0EwbzSY62bELmSgos258GpQF7r2xT7sIrH/f+zImu/7tqADP959vOf/P+d5nvMcVZ2jofL872M3uAn0oNaE3wVq/vCv4yRYAT/BcdAIKGYD19Vq9V08b4ALoAVoQN1DB76BGDgFdoJt4JzNZhuKRCIThUJhLh6Pf/Z6vS90Oh3NjoEtoK7dnAXr4BI4APaALqvVej+ZTE6trq7Ol0qlL8ViMUpCoVDAaDT68I4FcKeKJvzzNKCBCxwGZq1WOxCLxT5BeJ6i+Xx+AcT4xG4WAoHAR7x3G+wC3InsoEET+A5yoAi+ut3uDxTnyoV4Lpcrw880cTqdr/Euc7IVKOaEOVjw+XzrmLzOp91uh0ZBrHpDvNokHA6HMO8WYM6oIRsqLShQnIPPpsbGtWw2uwgxssmAZDKZGBL+FHOZM0ahtrQ3Bg0i1Ttob2n5lVlejiuZLC0tzWLeENgLDEDWwNrc3PzcYrGkuXKKv0PSp7q785mVlTjEJA08Hs97zO0BJiC7gyMmk2lweno6yGrhRK6c4pMVk6yEid/vDyA8DzGfpdoKJM9EK1Z+JxqNTrLWkdSyAcPClcuYLI6NjQUMBsMTzO8CDA8PpWQVOYeHh0chPifKsbLCshBFKc5wMWwMX1tb2w+ckbeYewbsB2wbkueA2xlIpVIRiouSrBgIyiYUry4AzGNyGRoeMtmT3KDX6wchMls5qbXigkWunOKihDG3BI4Cxp4VKDmY8d5gMDjxNwOHw1Gs2QEbI5udogG31dHZ2fkIIvOYLOK/ifHxcYaEolw5W/oauAxYnjzBsoPJ6e3v7x9V2oXL5UKeVVfBCdAOEoBtgp2X9S+ZAw6GiX3E29fX9yadTrOS/ug9MzMzYdT7Y7zDS+ggoCi/82I6BLYDxUbHGNLkitlsfjYyMuJPJBKzNMFFE0ZZvsR/7Jj7AEuSndMJmAuW6g6gGCYOvsDD0gGuaTSaBziAr/D5HrgIuHIjEHf1eUCDbkADxftADO6E8eQqWd88oVw1OyXFxR3NeNOIv7GKuLC6DDgoQCMKsDsyHDSt7fXChP9XiatUvwGdUptC6Eoz2gAAAABJRU5ErkJggg=="
        return System.Drawing.Bitmap(System.IO.MemoryStream(System.Convert.FromBase64String(o)))