# Package content verification

## Status

Not implemented. This document records the problem and the intended fix so that it can be applied
to [the CI workflow](../.github/workflows/ci.yml) before it is needed.

## Problem

`Send0xx.Dispatcher.SourceGeneration` does not ship a library. It sets `IncludeBuildOutput=false`
and instead globs one specific build output into the analyzer path:

```xml
<None Include="$(BaseOutputPath)$(Configuration)/netstandard2.0/$(AssemblyName).dll"
      Pack="true"
      PackagePath="analyzers/dotnet/cs"
      Visible="false" />
```

An `Include` glob that matches nothing is not an error. If the `netstandard2.0` build output is ever
absent when `dotnet pack` runs, the command still succeeds and still produces a `.nupkg`. That
package contains a valid nuspec and a README, and no generator.

Nothing downstream catches it:

- `NU5128`, which warns about a package with no lib and no dependency for a target framework, is
  suppressed in the project file because the package is legitimately analyzer-only.
- The `pack` job's `if-no-files-found: error` only asserts that the `artifacts/packages` directory is
  non-empty, not what any package contains.
- The `publish_nuget` job checks that each expected `.nupkg` path exists, not its contents.

A consumer installing the package would get no diagnostics, no generated dispatcher, and no
registration extension methods — with a compile error about a missing method rather than anything
naming the generator. This is the failure mode the library rejects everywhere else: a build problem
that hides instead of failing loudly.

## Fix

Add a verification step to the `pack` job, after the four `dotnet pack` steps and before
`Upload packages`. It asserts the analyzer is present in the package, and that every package built
for a framework carries the library it claims to.

```yaml
      - name: Verify package contents
        shell: bash
        run: |
          set -euo pipefail

          PACKAGE_VERSION="$(dotnet msbuild src/Dispatcher.Abstractions/Dispatcher.Abstractions.csproj -nologo -getProperty:Version | tail -n 1 | tr -d '\r')"

          assert_entry() {
            local PACKAGE_ID="$1"
            local ENTRY="$2"
            local PACKAGE_PATH="artifacts/packages/${PACKAGE_ID}.${PACKAGE_VERSION}.nupkg"

            if ! unzip -l "$PACKAGE_PATH" | grep -qF "$ENTRY"; then
              echo "Package '$PACKAGE_ID' is missing '$ENTRY'."
              exit 1
            fi
          }

          assert_entry Send0xx.Dispatcher.SourceGeneration analyzers/dotnet/cs/Dispatcher.SourceGeneration.dll

          for TARGET_FRAMEWORK in net8.0 net10.0
          do
            assert_entry Send0xx.Dispatcher.Abstractions "lib/${TARGET_FRAMEWORK}/Dispatcher.Abstractions.dll"
            assert_entry Send0xx.Dispatcher "lib/${TARGET_FRAMEWORK}/Dispatcher.dll"
            assert_entry Send0xx.Dispatcher.DependencyInjection "lib/${TARGET_FRAMEWORK}/Dispatcher.DependencyInjection.dll"
          done
```

The step reuses the same `Version` lookup the publish job already performs, so the two stay
consistent, and it needs no tooling beyond `unzip`, which is present on `ubuntu-latest`.

This is a packaging assertion, not a code-style check. CI must not reformat or rewrite sources;
formatting is run locally before committing.

## Verify locally

The same assertion by hand, after packing into `artifacts/packages`:

```bash
unzip -l artifacts/packages/Send0xx.Dispatcher.SourceGeneration.*.nupkg
```

The listing must contain `analyzers/dotnet/cs/Dispatcher.SourceGeneration.dll`. Checking the nuspec
dependency groups at the same time is worthwhile, because the source-generated package must not
acquire a dependency on the reflection-based implementation:

```bash
unzip -p artifacts/packages/Send0xx.Dispatcher.SourceGeneration.*.nupkg \
  Send0xx.Dispatcher.SourceGeneration.nuspec
```

## Related

`docs/RELEASING.md` asks the maintainer to inspect package dependencies and XML documentation before
tagging. That inspection is manual and easy to skip; the CI step above makes the most damaging case
impossible to miss.