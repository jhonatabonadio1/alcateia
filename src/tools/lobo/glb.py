"""Leitura e regravação mínima de GLB (glTF binário), só com numpy."""
import json, struct
import numpy as np

CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


class Glb:
    def __init__(self, path):
        data = open(path, "rb").read()
        magic, version, length = struct.unpack_from("<III", data, 0)
        assert magic == 0x46546C67
        off = 12
        self.bin = b""
        while off < length:
            clen, ctype = struct.unpack_from("<II", data, off)
            chunk = data[off + 8: off + 8 + clen]
            if ctype == 0x4E4F534A:
                self.doc = json.loads(chunk.decode("utf-8"))
            elif ctype == 0x004E4942:
                self.bin = bytes(chunk)
            off += 8 + clen
        # cada bufferView vira um pedaço próprio, para poder trocar e somar
        self.views = []
        for v in self.doc["bufferViews"]:
            o = v.get("byteOffset", 0)
            self.views.append(self.bin[o: o + v["byteLength"]])

    def accessor(self, index):
        a = self.doc["accessors"][index]
        v = self.doc["bufferViews"][a["bufferView"]]
        dt = CT[a["componentType"]]
        n = NC[a["type"]]
        raw = self.views[a["bufferView"]]
        o = a.get("byteOffset", 0)
        stride = v.get("byteStride", 0)
        size = np.dtype(dt).itemsize * n
        if stride and stride != size:
            out = np.zeros((a["count"], n), dtype=dt)
            for i in range(a["count"]):
                out[i] = np.frombuffer(raw, dtype=dt, count=n, offset=o + i * stride)
        else:
            out = np.frombuffer(raw, dtype=dt, count=a["count"] * n, offset=o).reshape(a["count"], n)
        if a.get("normalized"):
            out = out.astype(np.float32) / np.iinfo(dt).max
        return out

    def add_view(self, data, target=None):
        while len(data) % 4:
            data += b"\0"
        self.views.append(data)
        v = {"buffer": 0, "byteLength": len(data)}
        if target:
            v["target"] = target
        self.doc["bufferViews"].append(v)
        return len(self.views) - 1

    def add_accessor(self, array, kind, normalized=False):
        array = np.ascontiguousarray(array)
        ct = {v: k for k, v in CT.items()}[array.dtype.type]
        view = self.add_view(array.tobytes(), 34962)
        a = {"bufferView": view, "componentType": ct, "count": int(array.shape[0]), "type": kind}
        if normalized:
            a["normalized"] = True
        self.doc["accessors"].append(a)
        return len(self.doc["accessors"]) - 1

    def image_bytes(self, index):
        return self.views[self.doc["images"][index]["bufferView"]]

    def set_image(self, index, data, mime):
        img = self.doc["images"][index]
        self.views[img["bufferView"]] = data
        img["mimeType"] = mime

    def add_image(self, name, data, mime):
        view = self.add_view(data)
        self.doc["images"].append({"name": name, "mimeType": mime, "bufferView": view})
        self.doc.setdefault("textures", []).append({"source": len(self.doc["images"]) - 1,
                                                    "sampler": self.doc["textures"][0].get("sampler", 0)} if self.doc.get("textures") and "sampler" in self.doc["textures"][0] else {"source": len(self.doc["images"]) - 1})
        return len(self.doc["textures"]) - 1

    def save(self, path):
        blob = bytearray()
        for v, data in zip(self.doc["bufferViews"], self.views):
            while len(blob) % 4:
                blob += b"\0"
            v["byteOffset"] = len(blob)
            v["byteLength"] = len(data)
            blob += data
        while len(blob) % 4:
            blob += b"\0"
        self.doc["buffers"] = [{"byteLength": len(blob)}]
        js = json.dumps(self.doc, separators=(",", ":"), ensure_ascii=False).encode("utf-8")
        while len(js) % 4:
            js += b" "
        total = 12 + 8 + len(js) + 8 + len(blob)
        with open(path, "wb") as f:
            f.write(struct.pack("<III", 0x46546C67, 2, total))
            f.write(struct.pack("<II", len(js), 0x4E4F534A))
            f.write(js)
            f.write(struct.pack("<II", len(blob), 0x004E4942))
            f.write(blob)
