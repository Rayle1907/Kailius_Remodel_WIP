import argparse
import json
from pathlib import Path

from .pipeline import build, candidate_rows
from .schema_contract import validate_contract


def main():
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    batch = commands.add_parser("build")
    batch.add_argument("--config", required=True, type=Path)
    candidate = commands.add_parser("candidate")
    candidate.add_argument("--catalogue", required=True, type=Path)
    candidate.add_argument("--dataset", required=True, type=Path)
    candidate.add_argument("--output", required=True, type=Path)
    schema = commands.add_parser("check-schema")
    schema.add_argument("--reference", type=Path, default=Path(__file__).resolve().parents[1] / "event-schema.reference.json")
    schema.add_argument("--events", type=Path, default=Path(__file__).resolve().parents[2] / "Assets/Scripts/Analytics/ResearchAnalyticsEvents.cs")
    args = parser.parse_args()
    try:
        if args.command == "build":
            print(json.dumps(build(args.config), indent=2))
        elif args.command == "candidate":
            candidate_rows(args.catalogue, args.dataset, args.output)
        else:
            validate_contract(args.reference, args.events)
            print("Unity Analytics event schema matches the C# event classes.")
    except (ValueError, KeyError, OSError, json.JSONDecodeError) as error:
        parser.exit(2, str(error) + "\n")


if __name__ == "__main__":
    main()
