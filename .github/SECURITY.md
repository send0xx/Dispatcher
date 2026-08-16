# Security policy

## Supported versions

Dispatcher is published as a single release line. Security fixes are made against the latest released version of the `Send0xx.Dispatcher.*` packages, and older versions are not patched. Upgrade to the latest release before reporting a problem.

## Reporting a vulnerability

Report vulnerabilities privately through GitHub, not in a public issue:

1. Open the [Security tab](https://github.com/send0xx/Dispatcher/security/advisories) of the repository.
2. Choose **Report a vulnerability** to open a private security advisory.

Please include the affected package and version, the target framework and registration mode, and the smallest reproduction you can produce. A draft advisory keeps the discussion private until a fix is released.

Expect an acknowledgement within seven days. If a report is accepted, the fix and its advisory are published together, and the reporter is credited unless they ask otherwise.

## Scope

Dispatcher is a library with no network, file system, or process boundary of its own: it routes in-process messages to handlers the application registers. Reports that describe a real security impact on an application using the library are in scope, for example a handler being invoked for a message it was never registered for, or a registration path that resolves a type the application did not configure.

The following are not vulnerabilities:

- Behavior of application handlers, pipeline behaviors, or validation written on top of Dispatcher.
- Untrusted input passed straight to `dotnet` assembly scanning APIs. `AddDispatcherHandlers` loads and inspects the assemblies it is given, so give it only assemblies the application already trusts.
- Denial of service caused by an application dispatching unbounded work, since Dispatcher imposes no concurrency limit of its own.
