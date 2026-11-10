"""US-140: merges the coverage of all test projects and fails when a project falls under its minimum.

Each test project (Domain.Tests, UnitTests, Specs) writes its own Cobertura file. A line counts as covered when any of
them ran it (R2), so code that only the UI scenarios reach counts too. Only the standard library is used (R1).

    python build/coverage_gate.py <folder with coverage.cobertura.xml files> [--minimums build/minimum-coverage.json]

Writes a Markdown table to $GITHUB_STEP_SUMMARY when set (else to stdout) and exits with 1 under a minimum (R3).
"""

import argparse
import json
import os
import re
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

PREFIX = "ComplianceTracker."
CONDITION = re.compile(r"\((\d+)/(\d+)\)")


class Project:
    """Merged lines of one project: covered when any run hit them; branches as the best (covered, total) per line."""

    def __init__(self):
        self.lines = {}
        self.branches = {}

    def add_line(self, key, hits, covered_branches=None, total_branches=None):
        self.lines[key] = self.lines.get(key, False) or hits > 0
        if total_branches:
            best = self.branches.get(key, (0, total_branches))
            self.branches[key] = (max(best[0], covered_branches), max(best[1], total_branches))

    def line_rate(self):
        return percent(sum(1 for hit in self.lines.values() if hit), len(self.lines))

    def branch_rate(self):
        return percent(sum(c for c, _ in self.branches.values()), sum(t for _, t in self.branches.values()))


def percent(part, whole):
    return None if whole == 0 else 100.0 * part / whole


def full_path(filename, sources):
    """'Security/Event.cs' under the source 'C:\\repo\\src\\Domain\\' → 'c:/repo/src/domain/security/event.cs'. Windows
    and Linux separators and case are made the same, so one file is one file."""
    name = filename.replace("\\", "/")
    is_absolute = name.startswith("/") or (len(name) > 1 and name[1] == ":")
    if not is_absolute and sources:
        name = sources[0].replace("\\", "/").rstrip("/") + "/" + name
    parts = []
    for part in name.split("/"):
        if part == "..":
            if parts:
                parts.pop()
        elif part not in ("", "."):
            parts.append(part)
    return ("/" if name.startswith("/") else "") + "/".join(parts).lower()


def short_name(package):
    """'ComplianceTracker.Application' -> 'Application'"""
    return package[len(PREFIX):] if package.startswith(PREFIX) else package


def merge(files):
    projects = {}
    for file in files:
        root = ElementTree.parse(file).getroot()
        # File names are relative to the run's <sources>, which differ between test projects; the full path is the key
        sources = [s.text.strip() for s in root.iter("source") if s.text and s.text.strip()]
        for package in root.iter("package"):
            project = projects.setdefault(short_name(package.get("name", "")), Project())
            for cls in package.iter("class"):
                filename = full_path(cls.get("filename", ""), sources)
                for line in cls.iter("line"):
                    key = (filename, int(line.get("number", "0")))
                    hits = int(line.get("hits", "0"))
                    match = CONDITION.search(line.get("condition-coverage", "")) if line.get("branch") == "True" else None
                    if match:
                        project.add_line(key, hits, int(match.group(1)), int(match.group(2)))
                    else:
                        project.add_line(key, hits)
    return projects


def check(projects, minimums):
    """Returns the Markdown summary and the failures, e.g. 'Application: 78.0 % (minimum 80 %)'."""
    rows = ["| Project | Lines | Branches | Minimum (lines) |", "|---|---:|---:|---:|"]
    failures = []
    for name in sorted(set(projects) | set(minimums)):
        project = projects.get(name)
        lines = project.line_rate() if project else None
        branches = project.branch_rate() if project else None
        minimum = minimums.get(name)
        shown = "no data" if lines is None else f"{lines:.1f} %"
        rows.append(f"| {name} | {shown} | {'-' if branches is None else f'{branches:.1f} %'} | "
                    f"{'-' if minimum is None else f'{minimum} %'} |")
        if minimum is not None and (lines is None or lines + 1e-9 < minimum):
            failures.append(f"{name}: {shown} (minimum {minimum} %)")
    return "\n".join(["## Test coverage", "", *rows, ""]), failures


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("folder")
    parser.add_argument("--minimums", default=str(Path(__file__).with_name("minimum-coverage.json")))
    args = parser.parse_args(argv)

    files = sorted(Path(args.folder).rglob("coverage.cobertura.xml"))
    if not files:
        print(f"No coverage.cobertura.xml under {args.folder}", file=sys.stderr)
        return 1

    minimums = json.loads(Path(args.minimums).read_text(encoding="utf-8"))
    summary, failures = check(merge(files), minimums)
    if failures:
        summary += "\n**Under the minimum:**\n\n" + "\n".join(f"- {f}" for f in failures) + "\n"

    target = os.environ.get("GITHUB_STEP_SUMMARY")
    if target:
        with open(target, "a", encoding="utf-8") as out:
            out.write(summary + "\n")
    print(summary)
    for failure in failures:
        print(f"::error::{failure}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
