import json, struct, numpy as np
CT = {5120:('b',1),5121:('B',1),5122:('h',2),5123:('H',2),5125:('I',4),5126:('f',4)}
NC = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}
class Glb:
    def __init__(self, path):
        d = open(path,'rb').read()
        magic, ver, length = struct.unpack('<III', d[:12])
        l0, t0 = struct.unpack('<II', d[12:20]); self.json = json.loads(d[20:20+l0]); o = 20+l0
        l1, t1 = struct.unpack('<II', d[o:o+8]); self.bin = bytearray(d[o+8:o+8+l1])
    def acc(self, i):
        a = self.json['accessors'][i]; bv = self.json['bufferViews'][a['bufferView']]
        fmt, size = CT[a['componentType']]; n = NC[a['type']]
        off = bv.get('byteOffset',0) + a.get('byteOffset',0)
        arr = np.frombuffer(bytes(self.bin[off:off+a['count']*n*size]), dtype='<'+fmt).reshape(a['count'], n)
        return arr.copy()
    def add_acc(self, arr, type_, minmax=False):
        arr = np.asarray(arr, dtype='<f4')
        while len(self.bin) % 4: self.bin.append(0)
        off = len(self.bin); self.bin += arr.tobytes()
        self.json['bufferViews'].append({'buffer':0,'byteOffset':off,'byteLength':arr.nbytes})
        a = {'bufferView':len(self.json['bufferViews'])-1,'componentType':5126,'count':int(arr.shape[0]),'type':type_}
        if minmax: a['min']=[float(arr.min())]; a['max']=[float(arr.max())]
        self.json['accessors'].append(a); return len(self.json['accessors'])-1
    def save(self, path):
        while len(self.bin) % 4: self.bin.append(0)
        self.json['buffers'][0]['byteLength'] = len(self.bin)
        j = json.dumps(self.json, separators=(',',':')).encode()
        while len(j) % 4: j += b' '
        out = struct.pack('<III', 0x46546C67, 2, 12+8+len(j)+8+len(self.bin)) + struct.pack('<II', len(j), 0x4E4F534A) + j + struct.pack('<II', len(self.bin), 0x004E4942) + bytes(self.bin)
        open(path,'wb').write(out)
