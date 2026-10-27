"""Build a relocatable Windows x64 worker runtime from an installed Python 3.12.

Usage: python build_runtime.py <python-install-directory> [ghostscript-native.nupkg]
Requires pypdf, pypdfium2 and Pillow in that installation. Copies their licenses.
Does not install or modify anything outside this project's runtime directory.
"""
from pathlib import Path
import shutil
import sys
import zipfile

source = Path(sys.argv[1]).resolve()
target = Path(__file__).parent / "runtime"
target.mkdir(exist_ok=True)
if not (source / "python312.dll").is_file():
    raise ValueError("A Windows x64 Python 3.12 installation is required.")
for file in source.iterdir():
    if file.is_file() and file.name in ("python.exe", "python3.dll", "python312.dll", "vcruntime140.dll", "vcruntime140_1.dll", "LICENSE.txt"):
        shutil.copy2(file, target / file.name)
ignore = shutil.ignore_patterns("__pycache__", "*.pyc", "site-packages", "test", "tests", "idlelib", "tkinter", "ensurepip", "venv")
shutil.copytree(source / "Lib", target / "Lib", ignore=ignore, dirs_exist_ok=True)
shutil.copytree(source / "DLLs", target / "DLLs", ignore=ignore, dirs_exist_ok=True)
packages = source / "Lib" / "site-packages"
names = ("pypdf", "pypdfium2", "pypdfium2_raw", "pypdfium2_cfg", "PIL")
for package in packages.iterdir():
    if package.name in names or (package.name.endswith(".dist-info") and package.name.startswith(("pypdf-", "pypdfium2-", "pillow-"))):
        shutil.copytree(package, target / "Lib" / "site-packages" / package.name, ignore=ignore, dirs_exist_ok=True)
if len(sys.argv) > 2:
    gs = target / "ghostscript"
    gs.mkdir(exist_ok=True)
    with zipfile.ZipFile(sys.argv[2]) as archive:
        for name in ("runtimes/win-x64/native/gsdll64.dll", "runtimes/win-x64/native/ghostscript.version", "LICENSE.md", "README.md"):
            (gs / Path(name).name).write_bytes(archive.read(name))
(target / "python312._pth").write_text(".\nLib\nDLLs\nLib/site-packages\nimport site\n", encoding="ascii")
print("Built runtime:", target)
