# Summary

<!-- What changes, and why. Link the issue this closes, if there is one. -->

## Checklist

- [ ] Tests cover the observable behavior change, or the change has none.
- [ ] Both registration modes stay equivalent, or the difference is explained below.
- [ ] Documentation is updated when public behavior changes.
- [ ] `dotnet build Dispatcher.slnx -c Release` succeeds with no new warnings.
- [ ] `dotnet format --verify-no-changes` reports no changes.

## Public API

<!--
List any added, changed, or removed public type or member. Changes to the command, query,
notification, handler, and pipeline contracts are breaking and need design discussion first.
Write "none" if the public API is untouched.
-->

## Performance

<!--
Required only for changes to dispatch, registry creation, scanning, or the pipeline. Paste
BenchmarkDotNet results from a Release build, and say whether the numbers are warmed-scope or
fresh-scope-per-request. Dry jobs are not evidence. Write "not applicable" otherwise.
-->
