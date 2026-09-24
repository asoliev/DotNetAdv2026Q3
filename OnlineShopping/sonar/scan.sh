#!/bin/sh

set -eu

if [ "$#" -lt 2 ]; then
  echo "Usage: $0 <project-key> <solution-path>" >&2
  exit 1
fi

project_key=$1
solution_path=$2
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(CDPATH= cd -- "$script_dir/../.." && pwd)
solution_dir="$repo_root/$(dirname -- "$solution_path")"
coverage_reports_path="$solution_dir/tests/**/TestResults/**/coverage.cobertura.xml"

dotnet tool restore --tool-manifest "$script_dir/dotnet-tools.json"

SONAR_TOKEN=${SONAR_TOKEN:-}
if [ -z "$SONAR_TOKEN" ]; then
  echo "SONAR_TOKEN is required." >&2
  exit 1
fi

cd "$script_dir"
dotnet tool run dotnet-sonarscanner begin \
  /k:"$project_key" \
  /d:sonar.host.url="http://localhost:9000" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.cs.cobertura.reportsPaths="$coverage_reports_path"

if [ -d "$solution_dir/tests" ]; then
  dotnet test "$repo_root/$solution_path" --collect "XPlat Code Coverage"
else
  dotnet build "$repo_root/$solution_path"
fi

cd "$script_dir"
dotnet tool run dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"