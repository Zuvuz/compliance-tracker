"""Edge-case unit tests of US-140's coverage gate. Run with: python -m unittest discover -s build -p "test_*.py" """

import json
import os
import tempfile
import unittest
from pathlib import Path

import coverage_gate


def cobertura(packages, source=None):
    """packages: {name: {filename: [(line, hits, condition or None)]}}"""
    parts = ['<?xml version="1.0"?><coverage>']
    if source:
        parts.append(f"<sources><source>{source}</source></sources>")
    parts.append("<packages>")
    for name, files in packages.items():
        parts.append(f'<package name="{name}"><classes>')
        for filename, lines in files.items():
            parts.append(f'<class name="C" filename="{filename}"><lines>')
            for number, hits, condition in lines:
                branch = f' branch="True" condition-coverage="{condition}"' if condition else ' branch="False"'
                parts.append(f'<line number="{number}" hits="{hits}"{branch}/>')
            parts.append("</lines></class>")
        parts.append("</classes></package>")
    parts.append("</packages></coverage>")
    return "".join(parts)


class CoverageGateTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = Path(self.folder.name)

    def write(self, run, packages, source=None):
        folder = self.root / run
        folder.mkdir()
        (folder / "coverage.cobertura.xml").write_text(cobertura(packages, source), encoding="utf-8")

    def merged(self):
        return coverage_gate.merge(sorted(self.root.rglob("coverage.cobertura.xml")))

    def test_a_line_run_by_any_test_project_counts(self):
        # Arrange: the unit tests run line 1, the scenarios line 2, nobody line 3
        self.write("unit", {"ComplianceTracker.Application": {"A.cs": [(1, 1, None), (2, 0, None), (3, 0, None)]}})
        self.write("specs", {"ComplianceTracker.Application": {"A.cs": [(1, 0, None), (2, 4, None), (3, 0, None)]}})

        # Act
        rate = self.merged()["Application"].line_rate()

        # Assert
        self.assertAlmostEqual(200 / 3, rate)

    def test_the_same_line_number_in_two_files_is_two_lines(self):
        # Arrange
        self.write("unit", {"ComplianceTracker.Domain": {"A.cs": [(1, 1, None)], "B.cs": [(1, 0, None)]}})

        # Act / Assert
        self.assertEqual(50.0, self.merged()["Domain"].line_rate())

    def test_windows_and_linux_paths_are_the_same_file(self):
        # Arrange
        self.write("unit", {"ComplianceTracker.Domain": {"src\\A.cs": [(1, 0, None)]}})
        self.write("specs", {"ComplianceTracker.Domain": {"src/A.cs": [(1, 1, None)]}})

        # Act / Assert
        self.assertEqual(100.0, self.merged()["Domain"].line_rate())

    def test_the_same_file_under_different_sources_is_one_file(self):
        # Arrange: one run names files from the project folder, another from src (as coverlet does)
        self.write("domain", {"ComplianceTracker.Domain": {"Security/Event.cs": [(1, 1, None), (2, 0, None)]}},
                   source="C:\\repo\\src\\ComplianceTracker.Domain\\")
        self.write("specs", {"ComplianceTracker.Domain": {"ComplianceTracker.Domain/Security/Event.cs": [(1, 0, None), (2, 1, None)]}},
                   source="C:/repo/src/")

        # Act
        project = self.merged()["Domain"]

        # Assert
        self.assertEqual(2, len(project.lines))
        self.assertEqual(100.0, project.line_rate())

    def test_a_path_with_dots_is_resolved(self):
        # Act / Assert
        self.assertEqual("/repo/src/a.cs", coverage_gate.full_path("../src/./A.cs", ["/repo/tests/"]))

    def test_branches_take_the_best_run_per_line(self):
        # Arrange
        self.write("unit", {"ComplianceTracker.Web": {"A.cs": [(1, 1, "50% (1/2)")]}})
        self.write("specs", {"ComplianceTracker.Web": {"A.cs": [(1, 1, "100% (2/2)")]}})

        # Act / Assert
        self.assertEqual(100.0, self.merged()["Web"].branch_rate())

    def test_a_project_without_lines_has_no_rate(self):
        # Act / Assert
        self.assertIsNone(coverage_gate.Project().line_rate())
        self.assertIsNone(coverage_gate.Project().branch_rate())

    def test_under_the_minimum_fails_and_names_the_project(self):
        # Arrange: 78 of 100 lines
        self.write("unit", {"ComplianceTracker.Application": {"A.cs": [(n, 1 if n <= 78 else 0, None) for n in range(1, 101)]}})

        # Act
        _, failures = coverage_gate.check(self.merged(), {"Application": 80})

        # Assert
        self.assertEqual(["Application: 78.0 % (minimum 80 %)"], failures)

    def test_exactly_the_minimum_passes(self):
        # Arrange
        self.write("unit", {"ComplianceTracker.Domain": {"A.cs": [(n, 1 if n <= 9 else 0, None) for n in range(1, 11)]}})

        # Act
        _, failures = coverage_gate.check(self.merged(), {"Domain": 90})

        # Assert
        self.assertEqual([], failures)

    def test_a_project_with_a_minimum_but_no_data_fails(self):
        # Act
        summary, failures = coverage_gate.check({}, {"Web": 60})

        # Assert
        self.assertEqual(["Web: no data (minimum 60 %)"], failures)
        self.assertIn("| Web | no data |", summary)

    def test_a_project_without_a_minimum_is_shown_but_never_fails(self):
        # Arrange
        self.write("unit", {"ComplianceTracker.Domain": {"A.cs": [(1, 0, None)]}})

        # Act
        summary, failures = coverage_gate.check(self.merged(), {})

        # Assert
        self.assertEqual([], failures)
        self.assertIn("| Domain | 0.0 % |", summary)

    def test_main_writes_the_job_summary_and_exits_with_1_under_a_minimum(self):
        # Arrange
        self.write("unit", {"ComplianceTracker.Domain": {"A.cs": [(1, 0, None)]}})
        minimums = self.root / "minimums.json"
        minimums.write_text(json.dumps({"Domain": 50}), encoding="utf-8")
        summary = self.root / "summary.md"
        os.environ["GITHUB_STEP_SUMMARY"] = str(summary)
        self.addCleanup(os.environ.pop, "GITHUB_STEP_SUMMARY", None)

        # Act
        code = coverage_gate.main([str(self.root), "--minimums", str(minimums)])

        # Assert
        self.assertEqual(1, code)
        self.assertIn("Domain: 0.0 % (minimum 50 %)", summary.read_text(encoding="utf-8"))

    def test_main_fails_without_coverage_files(self):
        # Act / Assert
        self.assertEqual(1, coverage_gate.main([str(self.root)]))


if __name__ == "__main__":
    unittest.main()
