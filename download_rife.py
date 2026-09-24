"""Download the official 20221029 RIFE Windows release for offline evaluation."""
from pathlib import Path
from urllib.request import Request, urlopen
import time

url = "https://github.com/nihui/rife-ncnn-vulkan/releases/download/20221029/rife-ncnn-vulkan-20221029-windows.zip"
destination = Path(__file__).resolve().parents[1] / "work" / "rife" / "rife-windows.zip"
destination.parent.mkdir(parents=True, exist_ok=True)
for attempt in range(8):
    existing = destination.stat().st_size if destination.exists() else 0
    headers = {"User-Agent": "Codex-Desktop-Pet"}
    if existing:
        headers["Range"] = f"bytes={existing}-"
    try:
        with urlopen(Request(url, headers=headers), timeout=120) as response:
            if response.status == 206:
                mode = "ab"
                total = existing
            elif response.status == 200:
                mode = "wb"
                total = 0
            else:
                raise RuntimeError(f"Unexpected HTTP {response.status}")
            expected = int(response.headers.get("Content-Length", "0")) + total
            mark = total // (25 * 1024 * 1024)
            with destination.open(mode) as output:
                while chunk := response.read(1024 * 1024):
                    output.write(chunk)
                    total += len(chunk)
                    if total // (25 * 1024 * 1024) > mark:
                        mark = total // (25 * 1024 * 1024)
                        print(f"Downloaded {total // (1024 * 1024)} MiB", flush=True)
            if total == expected:
                print(f"Saved {destination}: {total // (1024 * 1024)} MiB")
                break
            print(f"Incomplete transfer: {total}/{expected}", flush=True)
    except TimeoutError:
        print(f"Timed out after {existing // (1024 * 1024)} MiB; retrying", flush=True)
        time.sleep(1)
else:
    raise RuntimeError("Download did not complete after 8 attempts")
