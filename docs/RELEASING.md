# CI and release guide

## GitHub Actions

The [CI workflow](../.github/workflows/ci.yml) builds the complete solution, tests the .NET 8 and .NET 10 targets, publishes the Native AOT sample, packs every library, and retains the packages as workflow artifacts for seven days. Packing runs only after the Native AOT publish succeeds, and each package is unzipped afterwards to confirm it contains the assemblies it is supposed to ship.

NuGet publishing runs only for version tags in the `v<version>` form. Before publishing, the workflow verifies that the tag matches the central `Version` in [`Directory.Build.props`](../Directory.Build.props) and pushes an explicit list of package files.

## NuGet trusted publishing

Publishing uses NuGet trusted publishing. The workflow obtains a short-lived API key through GitHub OIDC instead of storing a long-lived NuGet API key.

In the NuGet.org account that owns the packages, create a trusted publishing policy with:

- Repository owner: `send0xx`
- Repository: `Dispatcher`
- Workflow file: `ci.yml`
- Environment: `nuget`

Create a GitHub environment named `nuget` and add an environment secret named `NUGET_USER` containing the NuGet.org profile name, not an email address. Add required reviewers or deployment-branch restrictions when release approval is required.

## Publish a release

Update the central `Version` in [`Directory.Build.props`](../Directory.Build.props), then build, test, and pack all libraries before creating a tag. Inspect the generated package dependencies and XML documentation.

After the version change has been reviewed, create and push a matching release tag:

```bash
git tag v<version>
git push origin v<version>
```

The tag must use the `v<version>` form and exactly match the package version. The workflow publishes these packages:

- `Send0xx.Dispatcher.Abstractions`
- `Send0xx.Dispatcher`
- `Send0xx.Dispatcher.DependencyInjection`
- `Send0xx.Dispatcher.SourceGeneration`
