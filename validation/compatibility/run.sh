#!/usr/bin/env bash
# Measures the working tree against every published package and writes
# results/COMPATIBILITY.md. Usage: run.sh [processRounds]
set -euo pipefail

process_rounds="${1:-5}"
script_directory=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
artifacts=artifacts
results=results
versions=(1.0.0 1.1.0 1.2.0 1.2.1 1.3.0 1.4.0 2.0.0 3.0.0 local)
payload_sizes=(4096 16777216 440401920)
operations=(managed-encode managed-decode2 reuse-encode reuse-decode2)

cd -- "$script_directory"
# Git Bash on Windows needs a native path for the MSBuild property below.
absolute_results="$(pwd -W 2>/dev/null || pwd)/$results"
rm -rf "$results"
mkdir -p "$results"

probe() {
    local version="$1"
    shift
    dotnet "$artifacts/$version/bin/Compatibility/release/Compatibility.dll" "$@"
}

for version in "${versions[@]}"; do
    dotnet build Compatibility.csproj -c Release "-p:CodecVersion=$version" \
        --artifacts-path "$artifacts/$version" --nologo --verbosity quiet
    probe "$version" report "$results/report-$version.json"
done

measured_versions=(2.0.0 3.0.0 local)
reversed_versions=(local 3.0.0 2.0.0)

for ((round = 0; round < process_rounds; round++)); do
    for payload_size in "${payload_sizes[@]}"; do
        for operation in "${operations[@]}"; do
            # Alternating the order keeps slow drift from favoring one version.
            if ((round % 2 == 0)); then
                order=("${measured_versions[@]}")
            else
                order=("${reversed_versions[@]}")
            fi

            for version in "${order[@]}"; do
                probe "$version" measure "$operation" "$payload_size" >> "$results/performance.jsonl"
            done
        done
    done
done

# The SDK's package validation lists every public API difference to a published package.
# No file is written when there is no difference.
for baseline_version in 2.0.0 3.0.0; do
    dotnet pack ../../ReedSolomon/ReedSolomon/ReedSolomon.csproj -c Release --nologo --verbosity quiet \
        -o "$artifacts/package-$baseline_version" -p:GeneratePackageOnBuild=false \
        -p:EnablePackageValidation=true "-p:PackageValidationBaselineVersion=$baseline_version" \
        -p:GenerateCompatibilitySuppressionFile=true \
        "-p:ApiCompatSuppressionOutputFile=$absolute_results/api-differences-to-$baseline_version.xml"
done

probe local compare "$results" "$results/COMPATIBILITY.md"
