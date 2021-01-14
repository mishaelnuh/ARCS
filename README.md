# acorn_spraying

Concrete spraying path generation.

This package allows users to generate concrete spraying paths for use in robotic concrete spraying.

> **The Grasshopper components are incompatible with Rhino 5.** The underlying python library is. Users can simply import the modules in a `GhPython` component.

## Dependencies

* RhinoCommon 5.0+
* HAL Robotics (*optional* – for post path generation. All components and the module should work without it.)

## Installation

An installer script is included in `src\install.bat`. This will install the compiled Grasshopper components as well as allow access to the `acorn_spraying` modules within Rhino/Grasshopper.

### Manual installation
Copy the `src\acorn_spraying` folder to Rhino's IronPython library folder. The directories for each version of Rhino can be found at:
- Rhino 5: `%AppData%\McNeel\Rhinoceros\5.0\Plug-ins\IronPython (814d908a-e25c-493d-97e9-ee3861957f49)\settings\lib`
- Rhino 6: `%AppData%\McNeel\Rhinoceros\6.0\Plug-ins\IronPython (814d908a-e25c-493d-97e9-ee3861957f49)\settings\lib`

For the Grasshopper components, copy `src\acorn_spraying_components\ACORNSpraying.ghpy` file to you Grasshopper components folder at `%AppData%\Grasshopper\Libraries`.

## Building
The Grasshopper components should already be prebuilt. If you wish to compile the components yourself, run the `src\acorn_spraying_components\main.py` file using the `RunPythonFile` command in Rhino 6. Alternatively, install IronPython and simply run the python file.

## Contact

Mishael Nuh - men30@cam.ac.uk