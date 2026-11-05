# Security policy

Thank you for helping keep Compliance Tracker and its users safe. This is the coordinated vulnerability disclosure policy for the tracker (Cyber Resilience Act, Annex I, Part II, points 5 and 6).

## Reporting a vulnerability

Please report it privately. Do **not** open a public issue.

- **Preferred:** a private security advisory on GitHub: <https://github.com/Zuvuz/compliance-tracker/security/advisories/new>
- **E-mail:** runen8800@gmail.com

Please include:

- what is affected (the page, the request, or the file)
- how to reproduce it
- what an attacker could do with it
- the version (the commit or the image tag) you tested

## What you can expect

| When | What |
|---|---|
| Within 3 working days | We confirm that we have received the report |
| Within 10 working days | We tell you whether we see it as a vulnerability, and how serious it is |
| When it is fixed | We publish a security advisory and credit you, unless you prefer not to be named |

We aim to fix critical and high vulnerabilities within 30 days. If a vulnerability is actively exploited, we follow the CRA's reporting deadlines (Article 14).

Please give us a reasonable time to fix the vulnerability before you tell others about it. We will not take legal action against anyone who reports in good faith and follows this policy.

## Supported versions

Security updates are made for the newest version on the `main` branch and the newest published image (`ghcr.io/zuvuz/compliance-tracker:latest`). Please update to it before you report.

## What we do ourselves

- Every pull request is scanned by CodeQL (static analysis). Known vulnerable NuGet packages stop the build.
- The images are scanned with Trivy, and every image published to GHCR gets a software bill of materials (SBOM, CycloneDX).
- Each installation serves this contact at `/.well-known/security.txt` (RFC 9116).
