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