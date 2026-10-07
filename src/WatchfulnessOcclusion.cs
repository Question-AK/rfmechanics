using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace rfmechanics;

internal sealed partial class WatchfulnessRenderer
{
    private int volumeCells, peakVolumeCells;
    // Conservative voxel coverage of the convex hull(camera, clip box). For each slab on
    // the longest axis, intersect its boundaries with all eight camera-to-box-corner edges.
    // Their orthogonal rectangle encloses the full hull section. No sampled-ray gaps.
    // False obstruction is permitted; a missed solid voxel is not. Budget exhaustion fails closed.
    private bool ClearVolume(double minX,double minY,double minZ,double maxX,double maxY,double maxZ,out string reason)
    {
        reason="solid viewing volume";
        Span<double> lo=stackalloc double[] {minX,minY,minZ};
        Span<double> hi=stackalloc double[] {maxX,maxY,maxZ};
        Span<double> eye=stackalloc double[] {camera.X,camera.Y,camera.Z};
        int axis=0;
        for(int a=1;a<3;a++)
            if(Math.Abs((lo[a]+hi[a])/2-eye[a])>Math.Abs((lo[axis]+hi[axis])/2-eye[axis])) axis=a;
        int u=(axis+1)%3,v=(axis+2)%3, visits=0;
        int first=(int)Math.Floor(Math.Min(eye[axis],lo[axis]));
        int last=(int)Math.Floor(Math.Max(eye[axis],hi[axis]));
        if(last-first>132) {reason="occlusion extent budget";return false;}
        Span<int> cell=stackalloc int[3];
        for(int slab=first;slab<=last;slab++)
        {
            double umin=double.PositiveInfinity,umax=double.NegativeInfinity;
            double vmin=double.PositiveInfinity,vmax=double.NegativeInfinity;
            for(int corner=0;corner<8;corner++)
            {
                double endA=(corner&(1<<axis))==0?lo[axis]:hi[axis];
                double endU=(corner&(1<<u))==0?lo[u]:hi[u];
                double endV=(corner&(1<<v))==0?lo[v]:hi[v];
                double d=endA-eye[axis];
                double t0,t1;
                if(Math.Abs(d)<1e-9)
                {
                    if(eye[axis]<slab || eye[axis]>slab+1) continue;
                    t0=0;t1=1;
                }
                else
                {
                    double ta=(slab-eye[axis])/d,tb=(slab+1-eye[axis])/d;
                    t0=Math.Max(0,Math.Min(ta,tb));t1=Math.Min(1,Math.Max(ta,tb));
                    if(t0>t1) continue;
                }
                double ua=eye[u]+(endU-eye[u])*t0,ub=eye[u]+(endU-eye[u])*t1;
                double va=eye[v]+(endV-eye[v])*t0,vb=eye[v]+(endV-eye[v])*t1;
                umin=Math.Min(umin,Math.Min(ua,ub));umax=Math.Max(umax,Math.Max(ua,ub));
                vmin=Math.Min(vmin,Math.Min(va,vb));vmax=Math.Max(vmax,Math.Max(va,vb));
            }
            // Box edges also bound hull sections within the target's own axial extent.
            if(slab+1>=lo[axis] && slab<=hi[axis])
            {
                umin=Math.Min(umin,lo[u]);umax=Math.Max(umax,hi[u]);
                vmin=Math.Min(vmin,lo[v]);vmax=Math.Max(vmax,hi[v]);
            }
            if(!double.IsFinite(umin)) continue;
            cell[axis]=slab;
            for(int iu=(int)Math.Floor(umin);iu<=(int)Math.Floor(umax);iu++)
            for(int iv=(int)Math.Floor(vmin);iv<=(int)Math.Floor(vmax);iv++)
            {
                if(++visits>8192 || ++volumeCells>8192) {reason="occlusion voxel budget";return false;}
                peakVolumeCells=Math.Max(peakVolumeCells,volumeCells);
                cell[u]=iu;cell[v]=iv;
                blockPos.Set(cell[0],cell[1]%BlockPos.DimensionBoundary,cell[2]);blockPos.dimension=dimension;
                var access=api.World.BlockAccessor;
                if(access.GetChunkAtBlockPos(blockPos)==null) {reason="missing chunk";return false;}
                var block=access.GetBlock(blockPos,BlockLayersAccess.Solid);
                var m=block.BlockMaterial;
                if(block.Id!=0 && m!=EnumBlockMaterial.Air && m!=EnumBlockMaterial.Leaves && m!=EnumBlockMaterial.Plant
                    && m!=EnumBlockMaterial.Water && m!=EnumBlockMaterial.Lava && m!=EnumBlockMaterial.Fire) return false;
            }
        }
        reason="clear";return true;
    }
}
