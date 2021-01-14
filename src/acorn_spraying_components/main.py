import clr
from os import listdir
from os.path import isfile, join

py_files = [f for f in listdir('./') if (isfile(join('./', f)) and f.endswith('.py') and not f == 'main.py')]

print 'Modules to compile:'
print py_files

clr.CompileModules("ACORNSpraying.ghpy", *py_files)

print 'Complete!'