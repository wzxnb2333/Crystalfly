"""Validate the frozen Silksong translation baseline; no dependencies or network.

--update-hashes records reviewed document edits only after preservation checks pass.
Missing upstream documents are reported separately and are never counted as translated.
"""
import argparse
import collections
import hashlib
import json
import pathlib
import re
import sys


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def technical(text):
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    # Verify independently of the translator: code, fenced blocks, links, HTML and files.
    patterns = [r"(?ms)^[ \t]*(`{3,}|~{3,})[^\n]*\n.*?^[ \t]*\1[^\n]*(?:\n|$)",
        r"`+[^`\n]*`+", r"https?://[^\s<>\"')\]]+", r"<[A-Za-z/!][^>]*>",
        r"!?\[(?:[^\]\n]|`[^`]*`)*\]\([^\n]*?\)",
        r"\b[\w./-]+\.(?:dll|json|cfg|exe|zip|md|cs|png|wav|sh|txt|bat|yml|yaml)\b",
        r"\b[A-Za-z]*[a-z][A-Z][A-Za-z0-9]*\b|\b[A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)+\b",
        r"\b\d+\.\d+(?:\.\d+)+\b",
        r"\b[A-Za-z]+_[A-Za-z_0-9]+\b",
        r"\[!(?:NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]",
        r"(?im)^[^\n]*\b(?:copyright|licen[cs]e|all rights reserved)\b[^\n]*"]
    values = []
    for pattern in patterns:
        values.extend(match[0].strip().rstrip(".,;:!?") if match[0].startswith("http") else match[0].strip()
            for match in re.finditer(pattern, text))
    return collections.Counter(values)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--catalog", type=pathlib.Path, default=pathlib.Path("catalog"))
    parser.add_argument("--report", type=pathlib.Path, default=pathlib.Path("artifacts/silksong-translations-20261005/validation.json"))
    parser.add_argument("--update-hashes", action="store_true")
    args = parser.parse_args()
    baseline = json.loads((args.catalog / "silksong-translation-baseline.v1.json").read_text(encoding="utf-8"))
    index_path = args.catalog / "silksong-mod-translations.zh-CN.v1.json"
    index = json.loads(index_path.read_text(encoding="utf-8"))
    entries = {item["fullName"]: item for item in index["mods"]}
    errors, missing, empty, docs, files = [], [], [], collections.Counter(), collections.Counter()
    original_fallback = []
    if len(entries) != len(index["mods"]): errors.append("Duplicate package identities")
    if not {p["fullName"] for p in baseline["packages"]}.issubset(entries): errors.append("Frozen package coverage differs")
    categories = {name for p in baseline["packages"] for name in p["categories"]}
    for name in categories:
        if not re.search(r"[\u3400-\u9fff]", index["categoryNames"].get(name, "")): errors.append("Missing category: " + name)
    for package in baseline["packages"]:
        identity = package["fullName"]
        entry = entries.get(identity)
        if entry is None: continue
        if not re.search(r"[\u3400-\u9fff]", entry["displayName"]): errors.append("Missing Chinese name: " + identity)
        if entry["sourceUrl"] != package["sourceUrl"]: errors.append("Changed source identity: " + identity)
        if package["version"] is None: continue
        version = next((v for v in entry["versions"] if v["version"] == package["version"]), None)
        if version is None:
            errors.append("Missing latest version: " + identity); continue
        if package["description"].strip():
            if version["descriptionSha256"] != package["descriptionSha256"]: errors.append("Changed description digest: " + identity)
            if not re.search(r"[\u3400-\u9fff]", version.get("description") or ""): errors.append("Missing Chinese description: " + identity)
        for filename, source in package["documents"].items():
            metadata = version["documents"].get(filename)
            if source["status"] == "missing":
                missing.append({"fullName": identity, "version": package["version"], "document": filename, "reason": "Absent in the official version ZIP"})
                if metadata != {"missing": True}: errors.append("Missing document incorrectly counted: " + identity + "/" + filename)
                continue
            if source["status"] != "present":
                errors.append("Unresolved source download: " + identity + "/" + filename); continue
            directory = args.catalog / "silksong-mod-documents" / identity / package["version"]
            original, chinese = directory / (filename[:-3] + ".en.md"), directory / (filename[:-3] + ".zh-CN.md")
            if not original.exists() or not chinese.exists() or metadata is None or metadata.get("missing"):
                errors.append("Missing translation file: " + identity + "/" + filename); continue
            raw, translated = original.read_bytes(), chinese.read_bytes()
            if sha(raw) != source["sourceSha256"] or metadata["sourceSha256"] != source["sourceSha256"]:
                errors.append("Original bytes changed: " + identity + "/" + filename)
            text, zh = raw.decode("utf-8-sig"), translated.decode("utf-8-sig")
            normalized_original = text.replace("\r\n", "\n").replace("\r", "\n")
            normalized_chinese = zh.replace("\r\n", "\n").replace("\r", "\n")
            # Latin identifiers touching Chinese prose have no Unicode word boundary;
            # compare literal spans instead of reparsing translated prose as English.
            lost = {token for token in technical(text) if normalized_chinese.count(token) < normalized_original.count(token)}
            if lost: errors.append({"document": identity + "/" + filename, "changedTechnicalTokens": list(lost)[:8]})
            if re.search(r"⟦(?:K|BLOCK)\d+⟧|ZXXZ|ZX\d+XZ", zh): errors.append("Unrestored tokens: " + identity + "/" + filename)
            if not zh.strip() and text.strip(): errors.append("Empty translation: " + identity + "/" + filename)
            if zh == text and text.strip(): original_fallback.append({"fullName": identity, "version": package["version"], "document": filename})
            if args.update_hashes: metadata["translationSha256"] = sha(translated)
            elif metadata["translationSha256"] != sha(translated): errors.append("Translation digest differs: " + identity + "/" + filename)
            files[filename] += 1
            if text.strip(): docs[filename] += 1
            else: empty.append({"fullName": identity, "version": package["version"], "document": filename, "reason": "Empty document in the official version ZIP"})
    report = {"capturedAt": baseline["capturedAt"], "sourceSha256": baseline["sourceSha256"],
        "packages": len(baseline["packages"]), "catalogPackages": len(entries), "categories": len(categories), "documents": dict(docs), "documentFiles": dict(files),
        "translatedDocuments": sum(docs.values()), "upstreamMissingDocuments": missing,
        "upstreamEmptyDocuments": empty, "originalFallbackDocuments": original_fallback,
        "errors": errors, "quality": "Repository-maintained, locally assisted drafts; not all documents have been manually reviewed"}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k not in ("upstreamMissingDocuments", "upstreamEmptyDocuments", "errors")}, ensure_ascii=False))
    print(f"Upstream missing: {len(missing)}; empty: {len(empty)}; errors: {len(errors)}")
    if errors:
        print(json.dumps(errors[:12], ensure_ascii=False, indent=2)); raise SystemExit(1)
    if args.update_hashes: index_path.write_text(json.dumps(index, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
