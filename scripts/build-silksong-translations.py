"""Assemble repository-maintained Silksong translations without any translation service.

Preserves existing historical translations and untouched original Markdown bytes.
New names/descriptions must be explicitly maintained in the override file first.
"""
import argparse
import hashlib
import json
import pathlib
import re
import shutil


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--snapshot", type=pathlib.Path, required=True)
    parser.add_argument("--catalog", type=pathlib.Path, default=pathlib.Path("catalog"))
    args = parser.parse_args()
    baseline = json.loads((args.snapshot / "baseline.json").read_text(encoding="utf-8"))
    reports = {p["fullName"]: p for p in json.loads((args.snapshot / "extraction-report.json").read_text(encoding="utf-8"))}
    overrides = json.loads((args.catalog / "silksong-translation-overrides.zh-CN.json").read_text(encoding="utf-8"))
    index = args.catalog / "silksong-mod-translations.zh-CN.v1.json"
    previous = json.loads(index.read_text(encoding="utf-8")) if index.exists() else {"mods": []}
    entries = {mod["fullName"]: mod for mod in previous["mods"]}
    categories = {"(Deprecated category) Audio": "音频（旧分类）", "(Deprecated category) Misc": "其他（旧分类）",
        "(Deprecated category) Tools": "工具（旧分类）", "AI Generated": "AI 生成", "Accessibility": "无障碍",
        "Boss": "首领", "Cosmetic": "外观", "Custom Crests": "自定义纹章", "Custom Tools": "自定义工具",
        "Expansion": "内容扩展", "Libraries": "依赖库", "Modpacks": "整合包", "Mods": "模组", "Utility": "实用工具"}
    for package in baseline["packages"]:
        identity = package["fullName"]
        if not re.fullmatch(r"[A-Za-z0-9_]+-[A-Za-z0-9_]+", identity) or (package["version"] and not re.fullmatch(r"\d+\.\d+\.\d+", package["version"])):
            raise ValueError("Invalid package identity or version")
        override = overrides.get(identity)
        if not isinstance(override, dict) or not override.get("displayName") or (package["description"].strip() and not override.get("description")):
            raise ValueError("Maintain this package's Chinese metadata first: " + identity)
        entry = {"fullName": identity, "displayName": override["displayName"], "sourceUrl": package["sourceUrl"],
            "versions": [v for v in entries.get(identity, {}).get("versions", []) if v["version"] != package["version"]]}
        if package["version"]:
            version = {"version": package["version"], "description": override["description"] if package["description"].strip() else None,
                "descriptionSha256": package["descriptionSha256"], "documents": {}}
            for filename, document in reports[identity]["documents"].items():
                if document["status"] == "failed": raise ValueError("Unresolved document download: " + identity + "/" + filename)
                if document["status"] == "missing":
                    version["documents"][filename] = {"missing": True}; continue
                directory = args.catalog / "silksong-mod-documents" / identity / package["version"]
                directory.mkdir(parents=True, exist_ok=True)
                stem = filename[:-3]
                original = args.snapshot / "documents" / identity / package["version"] / (stem + ".en.md")
                if sha(original.read_bytes()) != document["sourceSha256"]: raise ValueError("Changed source: " + identity + "/" + filename)
                shutil.copyfile(original, directory / original.name)
                chinese = directory / (stem + ".zh-CN.md")
                version["documents"][filename] = {"missing": False, "sourceSha256": document["sourceSha256"],
                    "translationSha256": sha(chinese.read_bytes()) if chinese.exists() else None}
            entry["versions"].append(version)
        entries[identity] = entry
        package["documents"] = reports[identity]["documents"]
    categories = {**previous.get("categoryNames", {}), **categories}
    value = {"schemaVersion": 1, "language": "zh-CN", "community": baseline["community"], "updatedAt": baseline["capturedAt"],
        "categoryNames": categories, "mods": list(entries.values())}
    index.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (args.catalog / "silksong-translation-baseline.v1.json").write_text(json.dumps(baseline, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Catalog assembled: {len(entries)} packages")


if __name__ == "__main__":
    main()
