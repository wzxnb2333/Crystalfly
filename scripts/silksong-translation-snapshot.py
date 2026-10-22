"""Freeze Thunderstore metadata and read documents from ZIP ranges without installing packages.

Uses curl already provided by Windows. Only trusted HTTPS Thunderstore URLs are read.
Downloaded document bytes are cached under artifacts; failures remain explicit in the report.
"""
import argparse
import concurrent.futures
import hashlib
import io
import json
import pathlib
import re
import subprocess
import tempfile
import zipfile
from datetime import datetime, timezone
from urllib.parse import urlparse

DOCUMENTS = ("README.md", "CHANGELOG.md")
MAX_DOCUMENT = 1024 * 1024


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def trusted(url):
    uri = urlparse(url)
    if uri.scheme != "https" or not (uri.hostname == "thunderstore.io" or (uri.hostname or "").endswith(".thunderstore.io")) or uri.username or uri.password:
        raise ValueError("Untrusted Thunderstore URL")
    return url


class RemoteZip(io.RawIOBase):
    def __init__(self, url, size, cache):
        self.url, self.size, self.cache, self.position = trusted(url), size, cache, 0
        self.windows = []
        self.fetch(max(0, size - 65536), size - 1)

    def seekable(self):
        return True

    def readable(self):
        return True

    def tell(self):
        return self.position

    def seek(self, offset, whence=0):
        self.position = offset if whence == 0 else self.position + offset if whence == 1 else self.size + offset
        return self.position

    def fetch(self, start, end):
        key = digest((self.url + f"/{start}-{end}").encode())
        target = self.cache / (key + ".bin")
        if not target.exists():
            self.cache.mkdir(parents=True, exist_ok=True)
            with tempfile.TemporaryDirectory(dir=self.cache) as temp:
                body, headers = pathlib.Path(temp) / "body", pathlib.Path(temp) / "headers"
                result = subprocess.run(["curl.exe", "--silent", "--show-error", "--fail", "--location", "--proto", "=https", "--proto-redir", "=https",
                    "--max-time", "45", "--retry", "2", "--retry-delay", "2", "--range", f"{start}-{end}",
                    "--max-filesize", str(max(MAX_DOCUMENT * 2, end - start + 1)), "--dump-header", str(headers),
                    "--output", str(body), self.url], capture_output=True, text=True)
                if result.returncode:
                    raise IOError(result.stderr.strip())
                text = headers.read_text(encoding="utf-8", errors="replace")
                locations = re.findall(r"(?im)^location:\s*(\S+)", text)
                for location in locations:
                    trusted(location)
                ranges = re.findall(r"(?im)^content-range:\s*bytes\s+(\d+)-(\d+)/(\d+)", text)
                data = body.read_bytes()
                if ranges:
                    actual_start, actual_end, actual_size = map(int, ranges[-1])
                    if (actual_start, actual_end, actual_size) != (start, end, self.size) or len(data) != end - start + 1:
                        raise IOError("Range or archive size changed")
                elif start != 0 or len(data) != self.size:
                    raise IOError("Server did not honor the ZIP range")
                target.write_bytes(data)
        data = target.read_bytes()
        if len(data) != end - start + 1:
            raise IOError("Corrupt cached range")
        self.windows.append((start, data))
        return data

    def read(self, size=-1):
        size = self.size - self.position if size < 0 else min(size, self.size - self.position)
        if size <= 0:
            return b""
        if size > MAX_DOCUMENT * 2:
            raise IOError("ZIP read exceeds document limit")
        for start, data in self.windows:
            offset = self.position - start
            if offset >= 0 and offset + size <= len(data):
                self.position += size
                return data[offset:offset + size]
        data = self.fetch(self.position, self.position + size - 1)
        self.position += size
        return data


def extract(package, cache, documents):
    identity, version = package["fullName"], package.get("version")
    if not re.fullmatch(r"[A-Za-z0-9_]+-[A-Za-z0-9_]+", identity) or (version and not re.fullmatch(r"\d+\.\d+\.\d+", version)):
        raise ValueError("Invalid package identity or version")
    report = {"fullName": identity, "version": version, "documents": {}}
    if not version:
        report["reason"] = "No active version"
        return report
    directory = documents / identity / version
    marker = directory / "source.json"
    if marker.exists():
        previous = json.loads(marker.read_text(encoding="utf-8"))
        if all(v.get("status") in ("present", "missing") for v in previous["documents"].values()):
            return previous
    try:
        with zipfile.ZipFile(RemoteZip(package["downloadUrl"], package["fileSize"], cache)) as archive:
            for name in DOCUMENTS:
                entries = [entry for entry in archive.infolist() if entry.filename.casefold() == name.casefold()]
                if not entries:
                    report["documents"][name] = {"status": "missing", "reason": "Absent in the official version ZIP"}
                    continue
                if len(entries) != 1 or entries[0].file_size > MAX_DOCUMENT:
                    raise IOError("Duplicate or oversized document")
                data = archive.read(entries[0])
                directory.mkdir(parents=True, exist_ok=True)
                (directory / (name[:-3] + ".en.md")).write_bytes(data)
                report["documents"][name] = {"status": "present", "sourceSha256": digest(data), "bytes": len(data)}
    except Exception as error:
        for name in DOCUMENTS:
            report["documents"].setdefault(name, {"status": "failed", "reason": str(error)[:300]})
    directory.mkdir(parents=True, exist_ok=True)
    marker.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--snapshot", type=pathlib.Path, required=True, help="Official v1 package JSON")
    parser.add_argument("--output", type=pathlib.Path, required=True)
    parser.add_argument("--extract", action="store_true")
    args = parser.parse_args()
    raw = args.snapshot.read_bytes()
    packages = json.loads(raw)
    baseline = []
    for package in packages:
        versions = [v for v in package["versions"] if v.get("is_active", True)]
        version = max(versions, key=lambda v: tuple(map(int, v["version_number"].split("."))), default=None)
        baseline.append({"fullName": package["full_name"], "name": package["name"], "owner": package["owner"],
            "categories": package.get("categories", []), "isDeprecated": package["is_deprecated"],
            "sourceUrl": package["package_url"], "version": version["version_number"] if version else None,
            "description": version["description"] if version else "", "descriptionSha256": digest(version["description"].encode()) if version else None,
            "downloadUrl": version["download_url"] if version else None, "fileSize": version["file_size"] if version else 0})
    args.output.mkdir(parents=True, exist_ok=True)
    frozen = args.output / "baseline.json"
    if frozen.exists():
        existing = json.loads(frozen.read_text(encoding="utf-8"))
        if existing["sourceSha256"] != digest(raw):
            raise ValueError("The input differs from this frozen snapshot. Use a new output directory.")
        baseline = existing["packages"]
    else:
        frozen.write_text(json.dumps({"capturedAt": datetime.now(timezone.utc).isoformat(), "sourceSha256": digest(raw),
            "community": "hollow-knight-silksong", "packages": baseline}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if args.extract:
        reports = []
        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as workers:
            futures = [workers.submit(extract, package, args.output / "range-cache", args.output / "documents") for package in baseline]
            for future in concurrent.futures.as_completed(futures):
                reports.append(future.result())
                if len(reports) % 25 == 0 or len(reports) == len(baseline):
                    print(f"Documents: {len(reports)}/{len(baseline)}; failed packages: {sum(any(x.get('status') == 'failed' for x in p['documents'].values()) for p in reports)}", flush=True)
        (args.output / "extraction-report.json").write_text(json.dumps(sorted(reports, key=lambda x: x["fullName"]), ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
