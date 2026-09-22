"""Build a source-only publication folder; never copies local/private artifacts."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
ROOT_FILES = {"README.md", ".gitignore", ".env.example", "docker-compose.yml"}
FOLDERS = {"front", "ai-gateway", "ml-service", "ocr-service", "trino", "docs", "scripts"}
SKIP = {"node_modules", "bin", "obj", "dist", ".vs", ".vscode", "__pycache__", "models", "data"}
SUFFIXES = {".cs", ".csproj", ".sln", ".ts", ".tsx", ".js", ".jsx", ".css", ".html",
            ".json", ".lock", ".md", ".txt", ".sql", ".properties", ".py", ".conf", ".yml"}
NAMES = {"Dockerfile", ".dockerignore", ".env.example"}
# Known legacy identifiers plus common credential/internal-network signatures.
PATTERNS = [
    re.compile(r"con" + r"veste|cloud" + r"bi|csf" + r"cloud|csf" + r"audicon", re.I),
    re.compile(r"\bsk-(?:proj-)?[A-Za-z0-9_-]{20,}"),
    re.compile(r"\b(?:10\.\d{1,3}|192\.168|172\.(?:1[6-9]|2\d|3[01]))\.\d{1,3}\.\d{1,3}\b"),
    re.compile(r"(?im)(?:[;\"']password|^connection-password|^ADMIN_PASSWORD|^JWT_KEY|^DEEPSEEK_API_KEY)[ \t]*=[ \t]*(?![ \t]*(?:<|\$|$))[^;\s\"']+"),
]

def candidates():
    for name in sorted(ROOT_FILES):
        yield ROOT / name
    for folder in sorted(FOLDERS):
        for p in sorted((ROOT / folder).rglob("*")):
            rel = p.relative_to(ROOT)
            if any(part in SKIP for part in rel.parts) or not p.is_file():
                continue
            if p.name.startswith(".env") and p.name != ".env.example":
                continue
            if p.name.startswith("appsettings.") and p.name != "appsettings.json":
                continue
            if p.suffix in SUFFIXES or p.name in NAMES:
                yield p

def main():
    files = list(candidates())
    issues = []
    for p in files:
        if p.is_symlink() or not p.resolve().is_relative_to(ROOT):
            raise SystemExit(f"Unsafe path: {p.relative_to(ROOT)}")
        content = p.read_text(encoding="utf-8-sig")
        if p.resolve() == Path(__file__).resolve():
            continue
        if any(pattern.search(content) for pattern in PATTERNS):
            issues.append(str(p.relative_to(ROOT)))
    if issues:
        raise SystemExit("Review required (values withheld):\n" + "\n".join(issues))
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    destination = ROOT / ".publication" / stamp
    destination.mkdir(parents=True, exist_ok=False)
    manifest = {}
    for p in files:
        relative = p.relative_to(ROOT)
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(p, target)
        manifest[relative.as_posix()] = hashlib.sha256(target.read_bytes()).hexdigest()
    (destination / "MANIFEST.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Exported {len(files)} source files to {destination}")
    print("Automated checks passed. Review the exported folder before publishing.")

if __name__ == "__main__":
    main()
