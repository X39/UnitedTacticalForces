"""Regenerate Kiota using nullable schemas compatible with the pinned Kiota 1.32.2 release."""

import json
from pathlib import Path
import subprocess


WEBAPP = Path(__file__).resolve().parent


def normalize(value):
    """Represent OpenAPI 3.1 nullable unions as the equivalent OpenAPI 3.0 schema."""
    if isinstance(value, list):
        return [normalize(item) for item in value]
    if not isinstance(value, dict):
        return value.replace("\r\n", "\n") if isinstance(value, str) else value
    result = {key: normalize(item) for key, item in value.items() if key != "$comment"}
    for keyword in ("oneOf", "anyOf"):
        alternatives = result.get(keyword)
        if isinstance(alternatives, list) and len(alternatives) == 2:
            non_null = [item for item in alternatives if item != {"type": "null"}]
            if len(non_null) == 1:
                del result[keyword]
                result.update(non_null[0])
                result["nullable"] = True
    types = result.get("type")
    if isinstance(types, list) and "null" in types and len(types) == 2:
        result["type"] = next(item for item in types if item != "null")
        result["nullable"] = True
    return result


def main():
    """Write a disposable generator input and regenerate the existing client in place."""
    source = WEBAPP.parent / "ReactApp" / "openapi" / "utf-api.json"
    schema = normalize(json.loads(source.read_text(encoding="utf-8")))
    schema["openapi"] = "3.0.3"
    generator_input = WEBAPP / "obj" / "kiota-openapi.json"
    generator_input.parent.mkdir(parents=True, exist_ok=True)
    generator_input.write_text(json.dumps(schema, indent=2) + "\n", encoding="utf-8")
    previous = {path: path.read_bytes() for path in (WEBAPP / "UtfApi").rglob("*.cs")}
    subprocess.run([
        "kiota", "generate", "-d", str(generator_input), "-o", str(WEBAPP / "UtfApi"),
        "-l", "CSharp", "-c", "ApiClient", "-n", "X39.UnitedTacticalForces.WebApp.UtfApi",
    ], check=True)
    # Preserve pre-existing formatting when the generator made no semantic changes.
    for generated, previous_bytes in previous.items():
        before = previous_bytes.decode("utf-8")
        after = generated.read_text(encoding="utf-8")
        if [line.rstrip() for line in before.splitlines()] == [line.rstrip() for line in after.splitlines()]:
            generated.write_bytes(previous_bytes)



if __name__ == "__main__":
    main()
