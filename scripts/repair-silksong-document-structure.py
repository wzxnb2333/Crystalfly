"""Restore exact fenced code blocks in translated Markdown after local drafting."""
import pathlib
import re
import json

FENCE = re.compile(r"(?ms)^\s*(`{3,}|~{3,})[^\n]*\n.*?^\s*\1[^\n]*(?:\n|$)")

def main():
    root = pathlib.Path("catalog/silksong-mod-documents")
    fallback = []
    for original in sorted(root.rglob("*.en.md")):
        translated = original.with_name(original.name.replace(".en.md", ".zh-CN.md"))
        if not translated.exists(): continue
        source = original.read_text(encoding="utf-8-sig")
        target = translated.read_text(encoding="utf-8-sig")
        source_blocks = [m.group(0) for m in FENCE.finditer(source)]
        target_matches = list(FENCE.finditer(target))
        if len(source_blocks) != len(target_matches):
            translated.write_text(source, encoding="utf-8", newline="\n")
            fallback.append(str(original)); continue
        for match, block in reversed(list(zip(target_matches, source_blocks))):
            target = target[:match.start()] + block + target[match.end():]
        translated.write_text(target, encoding="utf-8", newline="\n")
    report = pathlib.Path("artifacts/silksong-translations-20261005/document-repair.json")
    report.write_text(json.dumps({"fencedBlocksRestored": True, "originalFallbackDocuments": fallback}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Fenced blocks restored; original fallback documents: {len(fallback)}")

if __name__ == "__main__": main()
