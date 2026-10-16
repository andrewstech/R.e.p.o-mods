#!/usr/bin/env python3
"""Bundle the official x64 Logitech LED SDK runtime with the lighting mod."""
import hashlib
import io
import pathlib
import sys
import urllib.request
import zipfile

URL = "https://www.logitechg.com/sdk/LED_SDK_9.00.zip"
ZIP_SHA256 = "72bff162eea84915699d4e902153b60bd85755c68f8a2a845c6ae6e81c98204d"
DLL_SHA256 = "46a0773e5ae6ef5b24557f3051e18a62527c7b2c133360dfb21522cbfe9cbdd1"
MEMBER = "LED/Lib/LogitechLedEnginesWrapper/x64/LogitechLedEnginesWrapper.dll"


def main():
    target = pathlib.Path(sys.argv[1])
    license_path = target.with_name("LogitechGamingLEDSDK.pdf")
    if (target.is_file() and license_path.is_file() and
            hashlib.sha256(target.read_bytes()).hexdigest() == DLL_SHA256):
        print("Verified bundled Logitech SDK DLL:", target)
        return
    with urllib.request.urlopen(URL, timeout=60) as response:
        archive = response.read()
    if hashlib.sha256(archive).hexdigest() != ZIP_SHA256:
        raise RuntimeError("Logitech SDK ZIP hash changed; review the official download before updating the pinned hash")
    with zipfile.ZipFile(io.BytesIO(archive)) as bundle:
        dll = bundle.read(MEMBER)
        license_pdf = bundle.read("LED/Doc/LogitechGamingLEDSDK.pdf")
    if hashlib.sha256(dll).hexdigest() != DLL_SHA256:
        raise RuntimeError("Logitech SDK DLL hash mismatch")
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(dll)
    license_path.write_bytes(license_pdf)
    print("Bundled Logitech LED SDK 9.00 x64 and license:", target)


if __name__ == "__main__":
    main()
