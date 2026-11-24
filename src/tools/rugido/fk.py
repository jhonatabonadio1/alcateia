import numpy as np
from glb import Glb
def qmul(a,b):
    x1,y1,z1,w1=a; x2,y2,z2,w2=b
    return np.array([w1*x2+x1*w2+y1*z2-z1*y2, w1*y2-x1*z2+y1*w2+z1*x2, w1*z2+x1*y2-y1*x2+z1*w2, w1*w2-x1*x2-y1*y2-z1*z2])
def qinv(q): return np.array([-q[0],-q[1],-q[2],q[3]])/np.dot(q,q)
def qrot(q,v):
    u=np.array(q[:3]); w=q[3]; v=np.asarray(v,float)
    return v+2*np.cross(u,np.cross(u,v)+w*v)
def qnorm(q): return q/np.linalg.norm(q)
def qfrom(a,b):
    """menor rotação que leva o vetor a ao b"""
    a=a/np.linalg.norm(a); b=b/np.linalg.norm(b); d=np.dot(a,b)
    if d>0.999999: return np.array([0,0,0,1.0])
    if d<-0.999999:
        ax=np.cross(a,[1,0,0]); 
        if np.linalg.norm(ax)<1e-6: ax=np.cross(a,[0,1,0])
        ax/=np.linalg.norm(ax); return np.array([*ax,0.0])
    ax=np.cross(a,b); return qnorm(np.array([*ax,1+d]))
def qaxis(axis,ang):
    axis=np.asarray(axis,float); axis/=np.linalg.norm(axis); return np.array([*(axis*np.sin(ang/2)),np.cos(ang/2)])

class Rig:
    def __init__(self, path, clip):
        self.g=g=Glb(path); j=g.json; self.names=[n.get('name') for n in j['nodes']]
        self.parent={}
        for i,n in enumerate(j['nodes']):
            for c in n.get('children',[]): self.parent[c]=i
        self.rest_t=[np.array(n.get('translation',[0,0,0]),float) for n in j['nodes']]
        self.rest_r=[np.array(n.get('rotation',[0,0,0,1]),float) for n in j['nodes']]
        self.rest_s=[np.array(n.get('scale',[1,1,1]),float) for n in j['nodes']]
        self.anim=[a for a in j['animations'] if a['name']==clip][0]
        self.T={}; self.R={}; self.times=None; self.chan={}
        for c in self.anim['channels']:
            s=self.anim['samplers'][c['sampler']]; node=c['target']['node']; path=c['target']['path']
            self.chan[(node,path)]=s
            if self.times is None: self.times=g.acc(s['input'])[:,0]
            if path=='translation': self.T[node]=g.acc(s['output']).astype(float)
            if path=='rotation': self.R[node]=g.acc(s['output']).astype(float)
        self.n=len(self.times)
    def idx(self,name): return self.names.index(name if name in self.names else 'mixamorig:'+name)
    def local(self,node,f):
        t=self.T[node][f] if node in self.T else self.rest_t[node]
        r=self.R[node][f] if node in self.R else self.rest_r[node]
        return t,qnorm(r),self.rest_s[node]
    def world(self,node,f):
        """(posição, rotação, escala uniforme) no mundo"""
        chain=[]; i=node
        while True:
            chain.append(i)
            if i not in self.parent: break
            i=self.parent[i]
        p=np.zeros(3); q=np.array([0,0,0,1.0]); s=1.0
        for i in reversed(chain):
            t,r,sc=self.local(i,f)
            p=p+qrot(q,t*s); q=qmul(q,r); s=s*sc[0]
        return p,q,s
