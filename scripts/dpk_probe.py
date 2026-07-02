#!/usr/bin/env python3
"""List and extract files from Xunxian's ``whpackage1.0`` DPK archives.

The client uses 1024-byte linked blocks.  Blocks 1 and 2 contain the current
path tree and its string table; regular files are WHSC streams compressed with
zlib and optionally protected by the client's DES-based transform.
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import struct
import zlib
from dataclasses import dataclass
from pathlib import Path, PurePosixPath

try:
    from Crypto.Cipher import DES
except ImportError as error:  # pragma: no cover - environment guidance
    raise SystemExit("Install the extractor dependency with: pip install pycryptodome") from error


BLOCK_SIZE = 1024
ROOT_PAYLOAD_OFFSET = 32
CHAIN_PAYLOAD_OFFSET = 8


@dataclass(frozen=True)
class DpkEntry:
    path: str
    root_block: int


class DpkReader:
    def __init__(
        self,
        path: Path,
        password: bytes = b"tianxiawudi",
        content_password: bytes = b"gandiaosifu",
    ):
        self.path = path
        self.password = password
        self.content_password = content_password

    def read_header(self) -> bytes:
        with self.path.open("rb") as stream:
            header = stream.read(BLOCK_SIZE)
        if header[:12] != b"whpackage1.0":
            raise ValueError("not a whpackage1.0 archive")
        if hashlib.md5(self.password).digest() != header[32:48]:
            raise ValueError("package password does not match the archive header")
        return header

    def read_virtual_file(self, root_block: int) -> bytes:
        with self.path.open("rb") as stream:
            stream.seek(root_block * BLOCK_SIZE)
            block = stream.read(BLOCK_SIZE)
            if len(block) != BLOCK_SIZE:
                raise ValueError(f"invalid root block: {root_block}")

            previous, next_block, tail_block, file_size = struct.unpack_from("<4I", block)
            if previous != 0:
                raise ValueError(f"block {root_block} is not a root block")

            result = bytearray(block[ROOT_PAYLOAD_OFFSET:])
            seen = {root_block}
            current = next_block
            last = root_block

            while current and len(result) < file_size:
                if current in seen:
                    raise ValueError(f"cycle detected at block {current}")
                seen.add(current)
                stream.seek(current * BLOCK_SIZE)
                block = stream.read(BLOCK_SIZE)
                if len(block) != BLOCK_SIZE:
                    raise ValueError(f"invalid chain block: {current}")
                block_previous, current_next = struct.unpack_from("<2I", block)
                if block_previous != last:
                    raise ValueError(f"broken chain: {last} -> {current}")
                result.extend(block[CHAIN_PAYLOAD_OFFSET:])
                last = current
                current = current_next

            if last != tail_block:
                raise ValueError(f"tail mismatch: expected {tail_block}, got {last}")
            return bytes(result[:file_size])

    @staticmethod
    def _decrypt_residual(ciphertext: bytes, previous: bytes, key: bytes) -> bytes:
        """Mirror the client's short-block transform at Patcher 0x42428f."""
        data = bytearray(ciphertext)
        parity = (data[-1] + data[0]) & 1
        for left in range(len(data) // 2):
            right = len(data) - left - 1
            if ((data[right] + data[left]) & 1) == parity:
                data[left], data[right] = data[right], data[left]

        prior_cipher_byte = 0
        for index, value in enumerate(data):
            data[index] = value ^ key[index] ^ prior_cipher_byte
            prior_cipher_byte = value
        return bytes(value ^ previous[index] for index, value in enumerate(data))

    def decode_whsc(self, data: bytes) -> bytes:
        if data[:7] != b"whsc1.0" or len(data) < 48:
            raise ValueError("not a WHSC stream")
        flags = data[15]
        expected_digest = data[16:32]
        reserved, plain_size, stored_size, _timestamp = struct.unpack_from("<4I", data, 32)
        if reserved != 0 or stored_size + 48 > len(data):
            raise ValueError("invalid WHSC header")
        payload = data[48:48 + stored_size]

        if flags & 2:
            # Bit 4 selects the per-file asset key.  The index streams omit it
            # and use the package password that is authenticated in the DPK header.
            source_key = self.content_password if flags & 4 else self.password
            key = source_key[:8].ljust(8, b"\0")
            cipher = DES.new(key, DES.MODE_ECB)
            previous = bytes(8)
            decoded = bytearray()
            full_size = len(payload) // 8 * 8
            for offset in range(0, full_size, 8):
                block = payload[offset:offset + 8]
                plain_block = cipher.decrypt(block)
                decoded.extend(left ^ right for left, right in zip(plain_block, previous))
                previous = block
            if full_size != len(payload):
                decoded.extend(self._decrypt_residual(payload[full_size:], previous, key))
            payload = bytes(decoded)

        if flags & 1:
            payload = zlib.decompress(payload)
        if len(payload) != plain_size:
            raise ValueError(f"plain-size mismatch: expected {plain_size}, got {len(payload)}")
        if hashlib.md5(payload).digest() != expected_digest:
            raise ValueError("decoded WHSC MD5 mismatch")
        return payload

    def entries(self) -> list[DpkEntry]:
        header = self.read_header()
        tree_root, names_root = struct.unpack_from("<2I", header, 0x84)
        tree = self.decode_whsc(self.read_virtual_file(tree_root))
        names = self.decode_whsc(self.read_virtual_file(names_root))

        name_by_offset: dict[int, str] = {}
        offset = 0
        while offset < len(names):
            end = names.find(b"\0", offset)
            if end < 0:
                raise ValueError("unterminated path string table")
            name_by_offset[offset] = names[offset:end].decode("ascii")
            offset = end + 1

        entries: list[DpkEntry] = []
        directories: list[str] = []
        offset = 0
        while offset < len(tree):
            record_type = tree[offset]
            offset += 1
            if record_type == 1:
                name_offset, root_block = struct.unpack_from("<2I", tree, offset)
                offset += 8
                path = PurePosixPath(*directories, name_by_offset[name_offset]).as_posix()
                entries.append(DpkEntry(path, root_block))
            elif record_type == 2:
                (name_offset,) = struct.unpack_from("<I", tree, offset)
                offset += 4
                directories.append(name_by_offset[name_offset])
            elif record_type == 3:
                if not directories:
                    raise ValueError("unbalanced directory tree")
                directories.pop()
            else:
                raise ValueError(f"unknown path-tree record {record_type} at {offset - 1}")
        return entries

    def extract(self, entry: DpkEntry) -> bytes:
        return self.decode_whsc(self.read_virtual_file(entry.root_block))


def matches(path: str, pattern: str) -> bool:
    lowered = path.lower()
    lowered_pattern = pattern.lower()
    return fnmatch.fnmatch(lowered, lowered_pattern) or lowered_pattern in lowered


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("dpk", type=Path)
    parser.add_argument("--password", default="tianxiawudi")
    parser.add_argument("--content-password", default="gandiaosifu")
    parser.add_argument("--list", metavar="PATTERN", help="list matching archive paths")
    parser.add_argument("--extract", metavar="PATTERN", help="extract matching archive paths")
    parser.add_argument("--output", type=Path, default=Path("dpk-output"))
    args = parser.parse_args()

    if not args.list and not args.extract:
        parser.error("use --list PATTERN or --extract PATTERN")

    reader = DpkReader(
        args.dpk,
        args.password.encode("ascii"),
        args.content_password.encode("ascii"),
    )
    selected = [entry for entry in reader.entries() if matches(entry.path, args.list or args.extract)]
    if args.list:
        for entry in selected:
            print(f"{entry.root_block:>8}  {entry.path}")
        print(f"{len(selected)} matching files")
        return

    for entry in selected:
        target = args.output.joinpath(*PurePosixPath(entry.path).parts)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(reader.extract(entry))
        print(target)
    print(f"{len(selected)} files extracted to {args.output}")


if __name__ == "__main__":
    main()
