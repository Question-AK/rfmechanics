#!/usr/bin/env python3
import argparse
import copy
import hashlib
import json
import sys
import zipfile
from pathlib import Path

EXPECTED_TCM_SHA256 = "1b5cd5354ef1679c29453dc2c34e3db120836601726d699ea7a9193db62e910c"
RF_BEHAVIOR = "RfGoblinCropStunt"
TCM_BEHAVIOR = "AlmanacNitrogenFixing"


def load_jsonc(text):
    lines = []
    for line in text.splitlines():
        line = line.strip()
        if not line.startswith("//"):
            lines.append(line)
    return json.loads("\n".join(lines))


def load_tcm_patches(path):
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    require(digest == EXPECTED_TCM_SHA256, f"unexpected TCM SHA-256: {digest}")
    with zipfile.ZipFile(path) as archive:
        data = archive.read("assets/almanactcm/patches/far-crop-behaviors.json").decode("utf-8")
    return load_jsonc(data)


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def native_addmerge(document, patch):
    require(patch["op"] == "addmerge", "test only models addmerge")
    require(patch["path"] == "/cropProps/behaviors", "unexpected behavior path")
    crop_props = document["cropProps"]
    incoming = copy.deepcopy(patch["value"])
    existing = crop_props.get("behaviors")
    if existing is None:
        crop_props["behaviors"] = incoming
    else:
        require(isinstance(existing, list), "behavior target must be an array")
        existing.extend(incoming)


def behavior_names(document):
    return [behavior["name"] for behavior in document["cropProps"]["behaviors"]]


def document_with(behaviors=None):
    document = {"cropProps": {}}
    if behaviors is not None:
        document["cropProps"]["behaviors"] = copy.deepcopy(behaviors)
    return document


def rfm_patch_for(patches, filename):
    matches = [patch for patch in patches if patch["file"] == filename]
    require(len(matches) == 1, f"expected one RFM patch for {filename}")
    return matches[0]


def tcm_patch_for(patches, filename):
    matches = [patch for patch in patches if patch["file"] == filename]
    require(len(matches) == 1, f"expected one TCM patch for {filename}")
    return matches[0]


def test_rfm_alone(rfm_patches):
    for patch in rfm_patches:
        require(patch["op"] == "addmerge", f"{patch['file']} is not addmerge")
        require(patch["path"] == "/cropProps/behaviors", f"{patch['file']} has a non-merge-safe path")
        require(patch["value"] == [{"name": RF_BEHAVIOR}], f"{patch['file']} has an unexpected behavior value")

        absent = document_with()
        native_addmerge(absent, patch)
        require(behavior_names(absent) == [RF_BEHAVIOR], f"{patch['file']} did not create an absent array")

        existing = document_with([{"name": "UnrelatedBehavior"}])
        native_addmerge(existing, patch)
        require(behavior_names(existing) == ["UnrelatedBehavior", RF_BEHAVIOR], f"{patch['file']} replaced an existing array")


def test_tcm_coexistence(rfm_patches, tcm_patches):
    filename = "game:blocktypes/plant/crop/soybean.json"
    rfm_patch = rfm_patch_for(rfm_patches, filename)
    tcm_patch = tcm_patch_for(tcm_patches, filename)
    require(tcm_patch["op"] == "addmerge", "TCM soybean patch is no longer addmerge")

    for order in ((rfm_patch, tcm_patch), (tcm_patch, rfm_patch)):
        for initial in (None, [{"name": "UnrelatedBehavior"}]):
            document = document_with(initial)
            for patch in order:
                native_addmerge(document, patch)
            names = behavior_names(document)
            if initial is not None:
                require(names[0] == "UnrelatedBehavior", "combined patches replaced an unrelated behavior")
            require(names.count(RF_BEHAVIOR) == 1, "combined patches duplicated the RFM behavior")
            require(names.count(TCM_BEHAVIOR) == 1, "combined patches duplicated the TCM behavior")


def test_reapplication_boundary(rfm_patches):
    patch = rfm_patch_for(rfm_patches, "game:blocktypes/plant/crop/soybean.json")
    first_load = document_with()
    second_load = document_with()
    native_addmerge(first_load, patch)
    native_addmerge(second_load, patch)
    require(behavior_names(first_load) == [RF_BEHAVIOR], "first fresh load was not singular")
    require(behavior_names(second_load) == [RF_BEHAVIOR], "second fresh load was not singular")

    direct_reapplication = document_with()
    native_addmerge(direct_reapplication, patch)
    native_addmerge(direct_reapplication, patch)
    require(behavior_names(direct_reapplication).count(RF_BEHAVIOR) == 2, "native addmerge unexpectedly deduplicated direct reapplication")


def main():
    parser = argparse.ArgumentParser(description="Verify RFM crop patches coexist with pinned Almanac TCM 0.5.14.")
    parser.add_argument("--tcm-zip", type=Path, required=True)
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[1]
    rfm_patches = json.loads((root / "assets/rfmechanics/patches/goblin-crop-stunt.json").read_text(encoding="utf-8"))
    tcm_patches = load_tcm_patches(args.tcm_zip)

    test_rfm_alone(rfm_patches)
    test_tcm_coexistence(rfm_patches, tcm_patches)
    test_reapplication_boundary(rfm_patches)
    print("PASS: RFM addmerge preserves absent and unrelated crop behavior arrays; soybean coexists with pinned TCM in either order.")
    print("BOUNDARY: the model matches native addmerge for one load; direct reapplication to an already patched array appends a duplicate.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, OSError, ValueError, zipfile.BadZipFile) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
