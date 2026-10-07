#!/usr/bin/env python3
"""Render every PlantUML (.puml) file under the given paths to a sibling .png.

The detailed-design docs reference each diagram as an inline image
(``![caption](diagrams/foo.png)``), so a design is only finished once every
``.puml`` source has a matching ``.png`` next to it. This script makes that
pairing deterministic instead of asking each run to reinvent the render step.

Usage
-----
    python render_puml.py [PATH ...]

Each PATH may be a ``.puml`` file or a directory (searched recursively).
With no PATH, it searches ``docs/detailed-designs`` under the current
directory, falling back to the current directory itself.

Finding PlantUML
----------------
Checked in order; the first that works is used:
  1. ``$PLANTUML_JAR``            -> ``java -jar $PLANTUML_JAR``
  2. ``plantuml`` on ``PATH``     -> ``plantuml``
  3. Common jar locations (see ``JAR_CANDIDATES`` below)

C4 diagrams that ``!include <C4/C4_Component>`` (and friends) render offline:
the C4-PlantUML standard library ships inside a recent ``plantuml.jar``. No
network is required.

Graphviz
--------
C4 and class diagrams are laid out by Graphviz ``dot``. Without it PlantUML
does not fail: it writes a "Cannot find Graphviz" error image and exits 0, so
every such diagram would silently ship broken. The script therefore runs
``plantuml -testdot`` first and refuses to render if Graphviz is missing.
Install it (``apt-get install graphviz``, ``brew install graphviz``,
``choco install graphviz``) or point ``GRAPHVIZ_DOT`` at the ``dot`` binary.

Exit status is non-zero if any diagram fails to render, so a caller can tell
whether the design's images are complete.
"""
from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

# Common places a plantuml.jar lands across machines. Ordered most- to
# least-likely; the first that exists wins.
JAR_CANDIDATES = [
    "C:/tools/plantuml.jar",
    "C:/ProgramData/chocolatey/lib/plantuml/tools/plantuml.jar",
    str(Path.home() / "plantuml.jar"),
    "/usr/local/bin/plantuml.jar",
    "/opt/plantuml/plantuml.jar",
    "/opt/homebrew/opt/plantuml/libexec/plantuml.jar",
    "/usr/share/plantuml/plantuml.jar",
]


def find_runner():
    """Return the argv prefix that runs PlantUML, or None if none is found."""
    jar = os.environ.get("PLANTUML_JAR")
    if jar and Path(jar).is_file():
        return ["java", "-jar", jar]

    cli = shutil.which("plantuml")
    if cli:
        return [cli]

    for candidate in JAR_CANDIDATES:
        if Path(candidate).is_file():
            return ["java", "-jar", candidate]

    return None


def check_graphviz(runner):
    """Return None if PlantUML can run Graphviz dot, else the diagnostic text."""
    proc = subprocess.run(runner + ["-testdot"], capture_output=True, text=True)
    output = ((proc.stdout or "") + (proc.stderr or "")).strip()
    if proc.returncode == 0 and "Installation seems OK" in output:
        return None
    return output or f"plantuml -testdot exited with {proc.returncode}"


def collect_puml(paths):
    """Expand files/directories into a sorted, de-duplicated list of .puml files."""
    found = []
    for raw in paths:
        p = Path(raw)
        if p.is_dir():
            found.extend(p.rglob("*.puml"))
        elif p.is_file() and p.suffix.lower() == ".puml":
            found.append(p)
        else:
            print(f"  ! skipped (not a .puml or directory): {p}")
    # De-dupe while preserving order.
    seen, unique = set(), []
    for f in found:
        key = f.resolve()
        if key not in seen:
            seen.add(key)
            unique.append(f)
    return sorted(unique)


def render(runner, files):
    """Render all files in one batch, then retry any missing PNG individually
    so a single broken diagram cannot hide the others and its error is shown."""
    # PlantUML writes <name>.png next to <name>.puml by default.
    subprocess.run(
        runner + ["-tpng", "-charset", "UTF-8"] + [str(f) for f in files],
        capture_output=True, text=True,
    )

    ok, failed = [], []
    for f in files:
        png = f.with_suffix(".png")
        if png.is_file() and png.stat().st_size > 0:
            ok.append(f)
            continue
        # Re-render this one alone to surface the specific error.
        proc = subprocess.run(
            runner + ["-tpng", "-charset", "UTF-8", str(f)],
            capture_output=True, text=True,
        )
        if png.is_file() and png.stat().st_size > 0:
            ok.append(f)
        else:
            err = (proc.stderr or proc.stdout or "no output").strip()
            failed.append((f, err))
    return ok, failed


def main(argv):
    paths = argv[1:]
    if not paths:
        default = Path("docs/detailed-designs")
        paths = [str(default)] if default.is_dir() else ["."]

    runner = find_runner()
    if runner is None:
        print(
            "ERROR: PlantUML not found.\n"
            "  Set PLANTUML_JAR to a plantuml.jar, put `plantuml` on PATH,\n"
            "  or place plantuml.jar at one of:\n    "
            + "\n    ".join(JAR_CANDIDATES),
            file=sys.stderr,
        )
        return 2

    dot_error = check_graphviz(runner)
    if dot_error is not None:
        print(
            "ERROR: PlantUML cannot run Graphviz dot, so C4 and class diagrams\n"
            "  would render as error images. Install Graphviz or set\n"
            "  GRAPHVIZ_DOT to the dot executable.\n\n  "
            + "\n  ".join(dot_error.splitlines()),
            file=sys.stderr,
        )
        return 2

    files = collect_puml(paths)
    if not files:
        print(f"No .puml files found under: {', '.join(paths)}")
        return 0

    print(f"Rendering {len(files)} diagram(s) with: {' '.join(runner)}")
    ok, failed = render(runner, files)

    for f in ok:
        print(f"  ok   {f.with_suffix('.png')}")
    for f, err in failed:
        first_line = err.splitlines()[0] if err else "unknown error"
        print(f"  FAIL {f}  --  {first_line}")

    print(f"\n{len(ok)} rendered, {len(failed)} failed.")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
