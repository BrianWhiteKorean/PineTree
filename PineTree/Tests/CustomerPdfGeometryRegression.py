"""Compare generated cutting geometry against the customer PET reference PDF."""
from pathlib import Path
import math
import sys
from pypdf import PdfReader
from pypdf.generic import ContentStream
MM = 72 / 25.4

def deref(value):
    return value.get_object() if hasattr(value, "get_object") else value

def compose(m, n):
    a,b,c,d,e,f = m
    A,B,C,D,E,F = n
    return (a*A+b*C, a*B+b*D, c*A+d*C, c*B+d*D, e*A+f*C+E, e*B+f*D+F)

def inspect(path):
    reader = PdfReader(path)
    page = reader.pages[0]
    cuts, lines = [], []
    def walk(content, resources, initial, depth=0):
        assert depth < 30
        resources = deref(resources)
        colors = deref(resources.get("/ColorSpace", {}))
        forms = deref(resources.get("/XObject", {}))
        matrix, width, color = initial, 1., "black"
        stack, paths = [], []
        def point(x,y):
            a,b,c,d,e,f=matrix
            return ((x*a+y*c+e)/MM, (x*b+y*d+f)/MM)
        for args, op in ContentStream(content, reader).operations:
            if op == b"q": stack.append((matrix,width,color))
            elif op == b"Q" and stack: matrix,width,color=stack.pop()
            elif op == b"cm": matrix=compose(tuple(map(float,args)),matrix)
            elif op == b"w": width=float(args[0])
            elif op == b"CS":
                space=deref(colors.get(args[0], []))
                color=str(space[1]) if isinstance(space,list) and space and space[0]=="/Separation" else None
            elif op == b"K": color="black" if list(map(float,args))==[0.,0.,0.,1.] else None
            elif op == b"G": color="black" if float(args[0])==0 else None
            elif op == b"RG": color="black" if list(map(float,args))==[0.,0.,0.] else None
            elif op in (b"m",b"l"): paths.append((op,point(*map(float,args))))
            elif op == b"re":
                x,y,w,h=map(float,args)
                paths.append((op,[point(x,y),point(x+w,y),point(x+w,y+h),point(x,y+h)]))
            elif op in (b"c",b"v",b"y"): paths.append((op,None))
            elif op in (b"S",b"s",b"B",b"B*",b"b",b"b*"):
                physical_width=width*math.hypot(matrix[0],matrix[1])/MM
                if color=="/thru":
                    for kind, points in paths:
                        if kind==b"re":
                            xs,ys=zip(*points)
                            cuts.append((min(xs),min(ys),max(xs)-min(xs),max(ys)-min(ys),physical_width))
                if color=="black" and abs(physical_width-1/MM)<.002:
                    start=None
                    for kind, value in paths:
                        if kind==b"m": start=value
                        elif kind==b"l" and start:
                            if abs(math.dist(start,value)-5)<.03: lines.append((start,value))
                            start=value
                paths=[]
            elif op in (b"n",b"f",b"f*",b"F"): paths=[]
            elif op == b"Do":
                form=deref(forms.get(args[0]))
                if form and form.get("/Subtype")=="/Form":
                    walk(form,form.get("/Resources",resources),compose(tuple(map(float,form.get("/Matrix",[1,0,0,1,0,0]))),matrix),depth+1)
    walk(page.get_contents(),page.get("/Resources",{}),(1,0,0,1,0,0))
    centers=[]
    for a,b in lines:
        if abs(a[0]-b[0])>.02: continue
        x=(a[0]+b[0])/2
        for c,d in lines:
            if abs(c[1]-d[1])>.02: continue
            y=(c[1]+d[1])/2
            if min(c[0],d[0])<=x<=max(c[0],d[0]) and min(a[1],b[1])<=y<=max(a[1],b[1]):
                if not any(math.dist((x,y),p)<.1 for p in centers): centers.append((x,y))
    return {"page": (float(page.mediabox.width)/MM,float(page.mediabox.height)/MM), "cuts": cuts, "marks": sorted(centers)}

if __name__ == "__main__":
    reference = Path("../../1.요구분석/샘플파일/페트_재단선+사방타공마크_604X1804_1EA_별색.pdf")
    generated = Path(sys.argv[1]) if len(sys.argv)>1 else Path("bin/Debug/PostProcessRegression/customer-pet.pdf")
    a,b=inspect(reference),inspect(generated)
    for data in (a,b):
        assert len(data["cuts"])==1, data
        assert len(data["marks"])==4, data
        for actual,expected in zip(data["page"],(604,1804)): assert abs(actual-expected)<.01
        for actual,expected in zip(data["cuts"][0],(2,2,600,1800,.18)): assert abs(actual-expected)<.01
        for point,expected in zip(data["marks"],[(22,22),(22,1782),(582,22),(582,1782)]): assert math.dist(point,expected)<.05
    for actual,expected in zip(a["marks"],b["marks"]): assert math.dist(actual,expected)<.05
    print("PASS: customer reference and generated PDF: page 604x1804; thru cut 600x1800/0.18mm; four 5mm cross marks at (22,22), (22,1782), (582,22), (582,1782)")
