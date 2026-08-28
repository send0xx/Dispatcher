# CI and release guide

## GitHub Actions

The [CI workflow](.github/workflows/ci.yml) builds the complete solution, tests the .NET 8 and .NET 10 targets, publishes the Native AOT sample, packs every library, and retains the packages as workflow artifacts for seven days. Packing runs only after the Native AOT publish succeeds, and each package is unzipped afterwards to confirm it contains the assemblies it is supposed to ship.

NuGet publishing runs only for version tags in the `v<version>` form. Before publishing, the workflow verifies that the tag matches the central `Version` in [`Directory.Build.props`](Directory.Build.props) and pushes an explicit list of package files.

## Documentation site

The [docs workflow](.github/workflows/docs.yml) builds the DocFX site in [`docs/`](docs) and deploys it to GitHub Pages at <https://send0xx.github.io/Dispatcher/>. It builds on every pull request to catch broken links and cross-references, and deploys only from `main`.

The site is built with `--warningsAsErrors`, so an invalid link, an unresolved cross-reference, or a missing table of contents entry fails the build rather than shipping a broken page.

DocFX is pinned in [`.config/dotnet-tools.json`](.config/dotnet-tools.json). Build the site locally with:

```bash
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

That serves the site on <http://localhost:8080>. Drop `--serve` to build only. Generated output is not committed: `docs/_site/` and the generated API metadata under `docs/api/` are ignored.

### One-time Pages setup

In **Settings → Pages**, set the source to **GitHub Actions**. The workflow deploys through the `github-pages` environment, which GitHub creates on the first successful run.

### Keeping the API reference accurate

The API reference is generated from the XML documentation comments of the packages listed under `metadata` in [`docs/docfx.json`](docs/docfx.json). A new package with public API must be added to that list, or its types will be missing from the site. The `Send0xx.Dispatcher.SourceGeneration` packaging project is deliberately excluded because it ships the analyzer rather than public API.

## NuGet trusted publishing

Publishing uses NuGet trusted publishing. The workflow obtains a short-lived API key through GitHub OIDC instead of storing a long-lived NuGet API key.

In the NuGet.org account that owns the packages, create a trusted publishing policy with:

- Repository owner: `send0xx`
- Repository: `Dispatcher`
- Workflow file: `ci.yml`
- Environment: `nuget`

Create a GitHub environment named `nuget` and add an environment secret named `NUGET_USER` containing the NuGet.org profile name, not an email address. Add required reviewers or deployment-branch restrictions when release approval is required.

## Publish a release

Update the central `Version` in [`Directory.Build.props`](Directory.Build.props), then build, test, and pack all libraries before creating a tag. Inspect the generated package dependencies and XML documentation.

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
