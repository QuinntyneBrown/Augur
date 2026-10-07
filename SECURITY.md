# Security Policy

## Supported versions

Augur has not had its first release yet. Until it does, security fixes are
made on the `main` branch only. After the first release, fixes will be made
for the latest released version.

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues,
discussions or pull requests.**

Report them privately through
[GitHub private vulnerability reporting](https://github.com/QuinntyneBrown/Augur/security/advisories/new).

Please include as much of the following as you can:

- The type of issue, for example API key exposure, path traversal or content
  injection into generated code
- The affected command, option or source file
- Step-by-step instructions to reproduce the issue
- A proof of concept, if you have one
- The impact of the issue and how an attacker might exploit it

You should receive an acknowledgement within 5 business days. We will keep you
informed while the issue is investigated and fixed, and will credit you in the
advisory unless you ask us not to.

## Scope

The following are especially relevant to Augur:

- Exposure of the `OPENAI_API_KEY` value in output, logs, lockfiles, plans or
  generated files
- Writing files outside the output directory, including through symbolic links
  or junctions
- Text from a specification or from the model reaching generated files or paths
- Insecure defaults in generated .NET or Angular code
- Bypassing HTTPS or certificate validation for Decisions API traffic

Vulnerabilities in the OpenAI API itself should be reported to OpenAI.
