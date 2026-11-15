"""Tests of build/test_shards.py (run in CI with: python -m unittest discover -s build -p "test_*.py")."""

import tempfile
import unittest
from pathlib import Path

import test_shards


def feature(folder, name, scenarios):
    attributes = "\n".join("        [global::Xunit.SkippableFactAttribute(DisplayName=\"s\")]" for _ in range(scenarios))
    Path(folder, f"{name}.feature.cs").write_text(
        f"namespace ComplianceTracker.Specs.Features\n{{\n    public partial class {name}Feature\n    {{\n{attributes}\n    }}\n}}\n",
        encoding="utf-8")


class ShardTests(unittest.TestCase):
    def test_every_feature_is_in_exactly_one_shard(self):
        items = [(f"F{i}", i % 5 + 1) for i in range(23)]

        buckets = test_shards.split(items, 4)

        self.assertEqual(sorted(name for bucket in buckets for name in bucket), sorted(name for name, _ in items))

    def test_the_shards_have_about_the_same_number_of_scenarios(self):
        items = [(f"F{i}", i % 7 + 1) for i in range(40)]
        weight = dict(items)

        sums = [sum(weight[name] for name in bucket) for bucket in test_shards.split(items, 4)]

        self.assertLessEqual(max(sums) - min(sums), 7)

    def test_the_split_is_the_same_every_time(self):
        items = [("B", 3), ("A", 3), ("C", 1)]

        self.assertEqual(test_shards.split(items, 2), test_shards.split(list(reversed(items)), 2))

    def test_the_filter_ends_each_name_with_a_dot_and_can_leave_a_category_out(self):
        self.assertEqual("FullyQualifiedName~X.A.|FullyQualifiedName~X.B.", test_shards.expression(["X.B", "X.A"]))
        self.assertEqual("(FullyQualifiedName~X.A.)&Category!=keycloak", test_shards.expression(["X.A"], "keycloak"))

    def test_the_generated_classes_and_their_scenarios_are_read(self):
        with tempfile.TemporaryDirectory() as folder:
            feature(folder, "Login", 3)
            feature(folder, "Risks", 1)

            self.assertEqual([("ComplianceTracker.Specs.Features.LoginFeature", 3), ("ComplianceTracker.Specs.Features.RisksFeature", 1)],
                             test_shards.features(folder))

    def test_a_shard_outside_the_range_or_without_features_is_refused(self):
        with tempfile.TemporaryDirectory() as folder:
            feature(folder, "Login", 1)
            self.assertEqual(2, test_shards.main(["5", "4", "--features", folder]))
            self.assertEqual(2, test_shards.main(["2", "2", "--features", folder]))
            self.assertEqual(0, test_shards.main(["1", "2", "--features", folder]))


if __name__ == "__main__":
    unittest.main()
