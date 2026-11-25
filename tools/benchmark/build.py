"""Build the project's assemblies with dotnet even when Unity has not regenerated the csproj files.

For each target, write `_v.<name>.csproj` next to the original: the same project, but its
<Compile> list is synced with the .cs files actually on disk under the assembly's root folder,
and ProjectReferences to the other targets point at their `_v.` copies.
Usage: python build.py [target ...]   (default: all)
"""
import os
import re
import subprocess
import sys

ROOT = r"C:\Users\Admin\Desktop\Manh\UnityProjects\Game"
TARGETS = {
    "ChaseGame": ["Assets\\Scripts"],
    "ChaseGame.Tests.EditMode": ["Assets\\Tests\\EditMode"],
    "ChaseGame.Tests.PlayMode": ["Assets\\Tests\\PlayMode"],
    "Assembly-CSharp": [],
    "Assembly-CSharp-Editor": ["Assets\\Editor"],
}


def disk_files(roots):
    found = []
    for r in roots:
        for dirpath, _, files in os.walk(os.path.join(ROOT, r)):
            for f in files:
                if f.endswith(".cs"):
                    found.append(os.path.relpath(os.path.join(dirpath, f), ROOT))
    return found


def make(name):
    src = os.path.join(ROOT, name + ".csproj")
    text = open(src, encoding="utf-8-sig").read()
    roots = TARGETS[name]
    items = re.findall(r'<Compile Include="([^"]+)" />', text)
    in_roots = lambda p: any(p.lower().startswith(r.lower() + "\\") for r in roots)
    keep = [p for p in items if not in_roots(p) or os.path.exists(os.path.join(ROOT, p))]
    known = {p.lower() for p in keep}
    keep += [p for p in disk_files(roots) if p.lower() not in known]
    text = re.sub(r'\s*<Compile Include="[^"]+" />', "", text)
    block = "\n  <ItemGroup>\n" + "".join(f'    <Compile Include="{p}" />\n' for p in keep) + "  </ItemGroup>\n"
    text = text.replace("</Project>", block + "</Project>")
    for other in TARGETS:
        text = text.replace(f'Include="{other}.csproj"', f'Include="_v.{other}.csproj"')
    dst = os.path.join(ROOT, "_v." + name + ".csproj")
    open(dst, "w", encoding="utf-8").write(text)
    return dst


def main():
    names = sys.argv[1:] or [t for t in TARGETS if t != "Assembly-CSharp"]
    for n in TARGETS:
        make(n)
    ok = True
    for n in names:
        r = subprocess.run(["dotnet", "build", "_v." + n + ".csproj", "-nologo", "-v:q", "-clp:ErrorsOnly"],
                           cwd=ROOT, capture_output=True, text=True)
        errs = sorted({l.strip() for l in r.stdout.splitlines() if ": error " in l})
        print(f"{n}: {'OK' if r.returncode == 0 else 'FAILED'}")
        for e in errs[:25]:
            print("  " + re.sub(r"\s*\[[^\]]+\]$", "", e.replace(ROOT + "\\", "")))
        ok &= r.returncode == 0
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
