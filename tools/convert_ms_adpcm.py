"""Convert extracted Microsoft ADPCM WAV files into Unity-importable PCM WAV files.

The originals are copied to TempInspection/AudioOriginal before replacement.
Run from the project root with Python and NumPy.
"""

from __future__ import annotations

import collections
import pathlib
import shutil
import struct
import sys
import wave

import numpy as np


ROOT = pathlib.Path(__file__).resolve().parents[1]
AUDIO = ROOT / "Assets" / "Audio"
BACKUP = ROOT / "TempInspection" / "AudioOriginal"
ADAPTATION = np.asarray(
    [230, 230, 230, 230, 307, 409, 512, 614, 768, 614, 512, 409, 307, 230, 230, 230],
    dtype=np.int32,
)


def chunks(blob: bytes):
    offset = 12
    while offset + 8 <= len(blob):
        kind = blob[offset : offset + 4]
        length = struct.unpack_from("<I", blob, offset + 4)[0]
        start = offset + 8
        if start + length > len(blob):
            raise ValueError(f"Truncated RIFF chunk {kind!r}")
        yield kind, blob[start : start + length]
        offset = start + length + (length & 1)


def decode_batch(blocks: np.ndarray, channels: int, samples_per_block: int, coefficients):
    count = len(blocks)
    predictors = blocks[:, :channels].astype(np.int32)
    if np.any(predictors >= len(coefficients)):
        raise ValueError("Invalid ADPCM predictor")
    coefficient_a = coefficients[predictors, 0]
    coefficient_b = coefficients[predictors, 1]
    offset = channels
    delta = blocks[:, offset : offset + channels * 2].copy().view("<i2").reshape(count, channels).astype(np.int32)
    offset += channels * 2
    sample1 = blocks[:, offset : offset + channels * 2].copy().view("<i2").reshape(count, channels).astype(np.int32)
    offset += channels * 2
    sample2 = blocks[:, offset : offset + channels * 2].copy().view("<i2").reshape(count, channels).astype(np.int32)
    offset += channels * 2
    payload = blocks[:, offset:]
    output = np.empty((count, samples_per_block, channels), dtype="<i2")
    output[:, 0, :] = sample2
    output[:, 1, :] = sample1
    for sample_index in range(2, samples_per_block):
        if channels == 2:
            byte = payload[:, sample_index - 2]
            nibbles = np.stack((byte >> 4, byte & 15), axis=1).astype(np.int32)
        else:
            byte = payload[:, (sample_index - 2) // 2]
            nibble = byte >> 4 if sample_index % 2 == 0 else byte & 15
            nibbles = nibble[:, None].astype(np.int32)
        signed = np.where(nibbles >= 8, nibbles - 16, nibbles)
        weighted = sample1 * coefficient_a + sample2 * coefficient_b
        prediction = np.where(weighted >= 0, weighted // 256, -((-weighted) // 256))
        decoded = np.clip(prediction + signed * delta, -32768, 32767)
        output[:, sample_index, :] = decoded
        sample2, sample1 = sample1, decoded
        delta = np.maximum(16, (delta * ADAPTATION[nibbles]) // 256)
    return output


def convert(path: pathlib.Path):
    source = path.read_bytes()
    if source[:4] != b"RIFF" or source[8:12] != b"WAVE":
        raise ValueError("Not RIFF/WAVE")
    parts = dict(chunks(source))
    fmt = parts[b"fmt "]
    format_tag, channels, sample_rate, _, block_align, bits = struct.unpack_from("<HHIIHH", fmt)
    if format_tag == 1:
        return False
    if format_tag != 2 or channels not in (1, 2) or bits != 4:
        raise ValueError(f"Unsupported WAV format {format_tag}, {channels} channels, {bits} bits")
    samples_per_block, coefficient_count = struct.unpack_from("<HH", fmt, 18)
    coefficients = np.array(
        [struct.unpack_from("<hh", fmt, 22 + i * 4) for i in range(coefficient_count)], dtype=np.int32
    )
    data = parts[b"data"]
    if len(data) % block_align:
        raise ValueError("Partial ADPCM block")
    block_count = len(data) // block_align
    samples_total = struct.unpack_from("<I", parts.get(b"fact", b"\x00" * 4))[0]
    if not samples_total:
        samples_total = block_count * samples_per_block

    relative = path.relative_to(AUDIO)
    backup = BACKUP / relative
    backup.parent.mkdir(parents=True, exist_ok=True)
    if not backup.exists():
        shutil.copy2(path, backup)
    temporary = path.with_suffix(".pcm.tmp")
    blocks = np.frombuffer(data, dtype=np.uint8).reshape(block_count, block_align)
    written = 0
    with wave.open(str(temporary), "wb") as wav:
        wav.setnchannels(channels)
        wav.setsampwidth(2)
        wav.setframerate(sample_rate)
        for start in range(0, block_count, 2048):
            decoded = decode_batch(blocks[start : start + 2048], channels, samples_per_block, coefficients)
            remaining = max(0, samples_total - written)
            frame_count = min(len(decoded) * samples_per_block, remaining)
            wav.writeframesraw(decoded.reshape(-1, channels)[:frame_count].tobytes())
            written += frame_count
    if written != samples_total:
        temporary.unlink(missing_ok=True)
        raise ValueError(f"Expected {samples_total} samples; decoded {written}")
    temporary.replace(path)
    return True


def main():
    selected = set(sys.argv[1:])
    result = collections.Counter()
    paths = sorted(AUDIO.rglob("*.wav"))
    for index, path in enumerate(paths, 1):
        if selected and path.stem not in selected:
            continue
        try:
            result["converted" if convert(path) else "pcm_already"] += 1
        except Exception as error:
            result["failed"] += 1
            print(f"FAILED {path.relative_to(AUDIO)}: {error}", flush=True)
        if index % 25 == 0:
            print(f"{index}/{len(paths)} {dict(result)}", flush=True)
    print(dict(result), flush=True)
    return 1 if result["failed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
