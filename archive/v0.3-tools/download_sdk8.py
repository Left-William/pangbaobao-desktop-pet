"""Fetch the official portable .NET 8 SDK for building this project on Windows."""
from pathlib import Path
from urllib.request import Request, urlopen
import json
import time

root = Path(__file__).resolve().parents[1]
metadata = "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/8.0/releases.json"
data = json.load(urlopen(metadata, timeout=30))
sdk = next(item["sdk"] for item in data["releases"] if "sdk" in item)
url = next(item["url"] for item in sdk["files"]
           if item["rid"] == "win-x64" and item["name"].endswith(".zip"))
destination = root / "work" / "dotnet8" / f"dotnet-sdk-{sdk['version']}-win-x64.zip"
destination.parent.mkdir(parents=True, exist_ok=True)

for attempt in range(8):
    existing = destination.stat().st_size if destination.exists() else 0
    headers = {"User-Agent": "Codex-Desktop-Pet"}
    if existing:
        headers["Range"] = f"bytes={existing}-"
    try:
        with urlopen(Request(url, headers=headers), timeout=120) as response:
            total = existing if response.status == 206 else 0
            mode = "ab" if response.status == 206 else "wb"
            expected = total + int(response.headers.get("Content-Length", "0"))
            mark = total // (25 * 1024 * 1024)
            with destination.open(mode) as output:
                while chunk := response.read(1024 * 1024):
                    output.write(chunk)
                    total += len(chunk)
                    if total // (25 * 1024 * 1024) > mark:
                        mark = total // (25 * 1024 * 1024)
                        print(f"Downloaded {total // (1024 * 1024)} MiB", flush=True)
            if total == expected:
                print(f"Saved SDK {sdk['version']}: {destination}")
                break
    except TimeoutError:
        print(f"Timeout, retry {attempt + 1}", flush=True)
        time.sleep(1)
else:
    raise RuntimeError("SDK download did not complete")
