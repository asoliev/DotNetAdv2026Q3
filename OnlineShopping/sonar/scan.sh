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
# Coverage goes to a fresh folder per scan; reports left in tests/**/TestResults by earlier runs
# describe old line numbers and make the import fail or skew coverage.
results_dir=$(mktemp -d)
trap 'rm -rf "$results_dir"' EXIT
coverage_reports_path="$results_dir/**/coverage.opencover.xml"

dotnet tool restore --tool-manifest "$script_dir/dotnet-tools.json"

SONAR_TOKEN=${SONAR_TOKEN:-}
if [ -z "$SONAR_TOKEN" ]; then
  echo "SONAR_TOKEN is required." >&2
  exit 1
fi

# Defaults to the Docker instance; override to scan another server, e.g. a local install.
SONAR_HOST_URL=${SONAR_HOST_URL:-http://localhost:9000}

cd "$script_dir"
dotnet tool run dotnet-sonarscanner begin \
  /k:"$project_key" \
  /d:sonar.host.url="$SONAR_HOST_URL" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.cs.opencover.reportsPaths="$coverage_reports_path" \
  /d:sonar.coverage.exclusions="**/Messaging/RabbitMq*.cs"

if [ -d "$solution_dir/tests" ]; then
  dotnet build "$repo_root/$solution_path" --no-incremental
  dotnet test "$repo_root/$solution_path" --no-build --collect "XPlat Code Coverage;Format=opencover" --results-directory "$results_dir"
else
  dotnet build "$repo_root/$solution_path"
fi

cd "$script_dir"
dotnet tool run dotnet-sonarscanner end /d:sonar.token="$SONAR_TOKEN"