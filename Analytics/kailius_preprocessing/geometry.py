import math
import json
from pathlib import Path


def load_catalogue(paths):
    segments = {}
    for path in paths:
        source = json.loads(Path(path).read_text(encoding="utf-8-sig"))
        if source.get("schema_version") != 1:
            raise ValueError("Unsupported catalogue schema")
        for segment in source["segments"]:
            identity = (segment["segment_id"], segment["segment_version"], segment["geometry_hash"])
            if not all(isinstance(value, str) and value for value in identity):
                raise ValueError("Missing catalogue identity")
            if segment.get("has_difficulty_target"):
                minimum = float(segment["target_failure_min"])
                maximum = float(segment["target_failure_max"])
                if not 0 <= minimum <= maximum <= 1:
                    raise ValueError("Invalid intended failure interval")
            comparable = {key: value for key, value in segment.items() if key != "derived_geometry_features"}
            existing = segments.get(identity)
            if existing is not None and {key: value for key, value in existing.items()
                                          if key != "derived_geometry_features"} != comparable:
                raise ValueError("Conflicting catalogue version: " + repr(identity))
            if existing is not None:
                continue
            comparable["derived_geometry_features"] = geometry_features(comparable)
            segments[identity] = comparable
    return segments


def validate_segment(segment):
    return geometry_features(segment)


def geometry_features(segment):
    geometry = segment["geometry"]
    platforms = geometry["platforms"]
    if not platforms:
        raise ValueError("A route needs ordered platform surfaces")
    for surface in platforms + geometry.get("hazards", []):
        for field in ("x", "y", "width", "height"):
            if not math.isfinite(float(surface[field])):
                raise ValueError("Nonfinite geometry")
        if surface["width"] <= 0 or surface["height"] < 0:
            raise ValueError("Invalid collider size")
    widths = [p["width"] for p in platforms]
    gaps, rises, drops = [], [], []
    distance = 0.0
    for left, right in zip(platforms, platforms[1:]):
        gaps.append(max(0, abs(right["x"] - left["x"]) - (left["width"] + right["width"]) / 2))
        height = right["y"] - left["y"]
        rises.append(max(0, height))
        drops.append(max(0, -height))
        distance += math.hypot(right["x"] - left["x"], height)
    start, target = geometry["start"], geometry["target"]
    distance += math.hypot(platforms[0]["x"] - start["x"], platforms[0]["y"] - start["y"])
    distance += math.hypot(target["x"] - platforms[-1]["x"], target["y"] - platforms[-1]["y"])
    result = {
        "route_length": distance, "platform_count": len(platforms),
        "platform_width_min": min(widths), "platform_width_mean": sum(widths) / len(widths),
        "gap_max": max(gaps, default=0), "gap_mean": sum(gaps) / max(1, len(gaps)),
        "rise_max": max(rises, default=0), "drop_max": max(drops, default=0),
        "hazard_count": len(geometry.get("hazards", [])),
        "death_wall_speed": float(geometry.get("death_wall_speed", 0)),
        "death_wall_head_start": float(geometry.get("death_wall_head_start", 0)),
    }
    clearances = []
    for surface in platforms:
        for hazard in geometry.get("hazards", []):
            horizontal = max(0, abs(hazard["x"] - surface["x"]) - (hazard["width"] + surface["width"]) / 2)
            surface_mid = surface["y"] - surface["height"] / 2
            hazard_mid = hazard["y"] - hazard["height"] / 2
            vertical = max(0, abs(hazard_mid - surface_mid) - (hazard["height"] + surface["height"]) / 2)
            clearances.append(math.hypot(horizontal, vertical))
    result["hazard_clearance_min"] = min(clearances, default=0)
    if not all(math.isfinite(float(value)) and value >= 0 for value in result.values()):
        raise ValueError("Invalid derived geometry feature")
    return result
