#!/usr/bin/env bash
# Full verification: dotnet build of all assemblies, then EditMode and PlayMode suites.
set -u
D="$(cd "$(dirname "$0")" && pwd)"
python -I "$D/build.py" || exit 1
bash "$D/run-unity-tests.sh" EditMode || exit 2
bash "$D/run-unity-tests.sh" PlayMode || exit 3
