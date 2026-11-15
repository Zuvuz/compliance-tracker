"""CI: splits the Reqnroll features into shards that run in parallel jobs, with about the same number of scenarios each.

The generated feature classes (tests/ComplianceTracker.Specs/Features/*.feature.cs) are read; each class goes to the
shard with the fewest scenarios so far, biggest first, so the split is the same on every run. Only the standard library
is used.

    python build/test_shards.py <shard 1..n> <n> [--features <folder>] [--exclude-category keycloak]

Prints the `dotnet test --filter` expression for that shard.
"""

import argparse
import re
import sys
from pathlib import Path

CLASS = re.compile(r"public partial class (\w+)")
NAMESPACE = re.compile(r"namespace ([\w.]+)")
SCENARIO = re.compile(r"\[global::Xunit\.Skippable(?:Fact|Theory)Attribute|\[Xunit\.Skippable(?:Fact|Theory)")


def features(folder):
    """(full class name, number of scenarios) of every generated feature class"""
    found = []
    for file in sorted(Path(folder).glob("*.feature.cs")):
        text = file.read_text(encoding="utf-8")
        cls = CLASS.search(text)
        ns = NAMESPACE.search(text)
        if cls:
            found.append((f"{ns.group(1)}.{cls.group(1)}" if ns else cls.group(1), max(1, len(SCENARIO.findall(text)))))
    return found


def split(items, shards):
    """Greedy: the biggest feature first, each to the lightest shard; ties go to the lower shard"""
    buckets = [[] for _ in range(shards)]
    weights = [0] * shards
    for name, weight in sorted(items, key=lambda item: (-item[1], item[0])):
        lightest = min(range(shards), key=lambda i: (weights[i], i))
        buckets[lightest].append(name)
        weights[lightest] += weight
    return buckets


def expression(names, exclude_category=None):
    """'FullyQualifiedName~A.|FullyQualifiedName~B.' with a dot, so FeatureA does not also pick FeatureAB"""
    picked = "|".join(f"FullyQualifiedName~{name}." for name in sorted(names))
    if exclude_category:
        return f"({picked})&Category!={exclude_category}"
    return picked


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("shard", type=int)
    parser.add_argument("shards", type=int)
    parser.add_argument("--features", default="tests/ComplianceTracker.Specs/Features")
    parser.add_argument("--exclude-category")
    args = parser.parse_args(argv)
    if not 1 <= args.shard <= args.shards:
        print(f"The shard must be between 1 and {args.shards}", file=sys.stderr)
        return 2

    items = features(args.features)
    if not items:
        print(f"No generated features under {args.features}", file=sys.stderr)
        return 2

    names = split(items, args.shards)[args.shard - 1]
    if not names:
        print(f"Shard {args.shard} has no features", file=sys.stderr)
        return 2

    print(expression(names, args.exclude_category))
    return 0


if __name__ == "__main__":
    sys.exit(main())
