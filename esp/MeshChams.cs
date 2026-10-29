using System.Buffers.Binary;
using System.Drawing.Drawing2D;
using System.Numerics;
using System.Text.Json;

namespace CsEsp;

// External SAS proxy, not a material override inside CS2. Retargeting uses joint
// positions, so twist, fingers, clothing and other agents are approximate.
internal sealed class MeshChams(string file) : IDisposable
{
    sealed record Vertex(Vector3 Position,int[] Joints,float[] Weights);
    sealed record Part(Vertex[] Vertices,int[] Indices);
    sealed record Joint(string Name,int Parent,Vector3 Position);
    Part[] parts=[];
    Joint[] joints=[];
    bool attempted;
    internal string Status {get;private set;}="External mesh not loaded";

    void Load()
    {
        if(attempted)return;attempted=true;
        try
        {
            var info=new FileInfo(file);if(!info.Exists||info.Length>64*1024*1024)throw new InvalidDataException("Local SAS model missing or too large");
            byte[] bytes=File.ReadAllBytes(file);
            if(bytes.Length<28||BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=0x46546c67||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))!=2||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8))!=bytes.Length)throw new InvalidDataException("Invalid GLB header");
            int jsonLength=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)));
            if(jsonLength<=0||jsonLength>bytes.Length-28||BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16))!=0x4e4f534a)throw new InvalidDataException("Invalid GLB JSON");
            using var document=JsonDocument.Parse(bytes.AsMemory(20,jsonLength));var root=document.RootElement;
            int binHeader=20+jsonLength,binLength=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(binHeader)));
            if(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(binHeader+4))!=0x004e4942||binLength<0||binHeader+8L+binLength>bytes.Length)throw new InvalidDataException("GLB binary data unavailable");
            int binStart=binHeader+8;var nodes=root.GetProperty("nodes").EnumerateArray().ToArray();if(nodes.Length>4096)throw new InvalidDataException("Too many model nodes");
            var parents=Enumerable.Repeat(-1,nodes.Length).ToArray();
            for(int i=0;i<nodes.Length;i++)if(nodes[i].TryGetProperty("children",out var children))foreach(var child in children.EnumerateArray()){int n=child.GetInt32();if(n<0||n>=nodes.Length||parents[n]!=-1)throw new InvalidDataException("Invalid model hierarchy");parents[n]=i;}
            var globals=new Matrix4x4[nodes.Length];var resolved=new byte[nodes.Length];
            Matrix4x4 Global(int i)
            {
                if(resolved[i]==2)return globals[i];if(resolved[i]==1)throw new InvalidDataException("Cyclic model hierarchy");resolved[i]=1;
                var n=nodes[i];Matrix4x4 local;
                if(n.TryGetProperty("matrix",out var matrix)){float[] a=matrix.EnumerateArray().Select(x=>x.GetSingle()).ToArray();if(a.Length!=16)throw new InvalidDataException("Invalid model transform");local=new(a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7],a[8],a[9],a[10],a[11],a[12],a[13],a[14],a[15]);}
                else
                {
                    Vector3 V(string key,Vector3 fallback){if(!n.TryGetProperty(key,out var v))return fallback;float[] a=v.EnumerateArray().Select(x=>x.GetSingle()).ToArray();if(a.Length!=3)throw new InvalidDataException("Invalid model vector");return new(a[0],a[1],a[2]);}
                    Quaternion rotation=Quaternion.Identity;if(n.TryGetProperty("rotation",out var r)){float[] a=r.EnumerateArray().Select(x=>x.GetSingle()).ToArray();if(a.Length!=4)throw new InvalidDataException("Invalid model rotation");rotation=new(a[0],a[1],a[2],a[3]);}
                    local=Matrix4x4.CreateScale(V("scale",Vector3.One))*Matrix4x4.CreateFromQuaternion(rotation)*Matrix4x4.CreateTranslation(V("translation",Vector3.Zero));
                }
                globals[i]=parents[i]<0?local:local*Global(parents[i]);resolved[i]=2;return globals[i];
            }
            for(int i=0;i<nodes.Length;i++)Global(i);
            joints=nodes.Select((n,i)=>new Joint(n.TryGetProperty("name",out var name)?name.GetString()??"":"",parents[i],Vector3.Transform(Vector3.Zero,globals[i]))).ToArray();
            int meshNode=Array.FindIndex(nodes,n=>n.TryGetProperty("name",out var name)&&name.GetString()?.Contains("thirdperson_body",StringComparison.OrdinalIgnoreCase)==true&&n.TryGetProperty("mesh",out _));
            if(meshNode<0)throw new InvalidDataException("SAS body mesh unavailable");
            var mesh=root.GetProperty("meshes")[nodes[meshNode].GetProperty("mesh").GetInt32()];var skin=root.GetProperty("skins")[nodes[meshNode].GetProperty("skin").GetInt32()];int[] jointNodes=skin.GetProperty("joints").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(jointNodes.Any(n=>n<0||n>=nodes.Length))throw new InvalidDataException("Invalid skin joints");
            float[][] Accessor(int index,int components)
            {
                var accessor=root.GetProperty("accessors")[index];if(accessor.TryGetProperty("sparse",out _))throw new InvalidDataException("Sparse mesh data unsupported");
                string type=accessor.GetProperty("type").GetString()??"";if(type!=(components==1?"SCALAR":"VEC"+components))throw new InvalidDataException("Unexpected mesh attribute type");
                int count=accessor.GetProperty("count").GetInt32(),component=accessor.GetProperty("componentType").GetInt32();if(count<0||count>200000)throw new InvalidDataException("Mesh exceeds vertex limit");
                int width=component switch{5121=>1,5123=>2,5125 or 5126=>4,_=>throw new InvalidDataException("Unsupported mesh component")};
                var view=root.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];if(view.TryGetProperty("buffer",out var buffer)&&buffer.GetInt32()!=0)throw new InvalidDataException("External model buffers unsupported");
                int viewOffset=view.TryGetProperty("byteOffset",out var vo)?vo.GetInt32():0,offset=accessor.TryGetProperty("byteOffset",out var ao)?ao.GetInt32():0;
                int length=view.GetProperty("byteLength").GetInt32(),stride=view.TryGetProperty("byteStride",out var st)?st.GetInt32():components*width;
                if(viewOffset<0||offset<0||length<0||stride<components*width||viewOffset+(long)length>binLength||(count>0&&offset+(long)(count-1)*stride+components*width>length))throw new InvalidDataException("Mesh buffer out of bounds");
                bool normalized=accessor.TryGetProperty("normalized",out var normal)&&normal.GetBoolean();var result=new float[count][];
                for(int i=0;i<count;i++)
                {
                    result[i]=new float[components];for(int c=0;c<components;c++)
                    {
                        int p=checked(binStart+viewOffset+offset+i*stride+c*width);float value=component switch{5121=>bytes[p],5123=>BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(p)),5125=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p)),_=>BitConverter.ToSingle(bytes,p)};
                        if(normalized&&component!=5126)value/=component==5121?255:component==5123?65535:4294967295f;
                        if(!float.IsFinite(value))throw new InvalidDataException("Nonfinite model attribute");result[i][c]=value;
                    }
                }
                return result;
            }
            var result=new List<Part>();int totalFaces=0;var vertexCache=new Dictionary<string,Vertex[]>();
            foreach(var primitive in mesh.GetProperty("primitives").EnumerateArray())
            {
                if(primitive.TryGetProperty("mode",out var mode)&&mode.GetInt32()!=4)throw new InvalidDataException("Mesh must use triangles");
                var attributes=primitive.GetProperty("attributes");string cacheKey=$"{attributes.GetProperty("POSITION").GetInt32()}:{attributes.GetProperty("JOINTS_0").GetInt32()}:{attributes.GetProperty("WEIGHTS_0").GetInt32()}";
                if(!vertexCache.TryGetValue(cacheKey,out var vertices))
                {
                    var positions=Accessor(attributes.GetProperty("POSITION").GetInt32(),3);var weights=Accessor(attributes.GetProperty("WEIGHTS_0").GetInt32(),4);var indices=Accessor(attributes.GetProperty("JOINTS_0").GetInt32(),4);
                    if(weights.Length!=positions.Length||indices.Length!=positions.Length)throw new InvalidDataException("Mismatched skin attributes");
                    vertices=new Vertex[positions.Length];
                    for(int i=0;i<vertices.Length;i++)
                    {
                        int[] refs=indices[i].Select(x=>checked((int)x)).ToArray();if(refs.Any(n=>n<0||n>=jointNodes.Length))throw new InvalidDataException("Invalid vertex joint");
                        float sum=weights[i].Sum();if(sum<=0||weights[i].Any(w=>w<0))throw new InvalidDataException("Invalid skin weights");
                        var v=positions[i];vertices[i]=new(Vector3.Transform(new(v[0],v[1],v[2]),globals[meshNode]),refs.Select(n=>jointNodes[n]).ToArray(),weights[i].Select(w=>w/sum).ToArray());
                    }
                    vertexCache[cacheKey]=vertices;
                }
                int[] triangles=Accessor(primitive.GetProperty("indices").GetInt32(),1).Select(a=>checked((int)a[0])).ToArray();totalFaces+=triangles.Length/3;
                if(triangles.Length%3!=0||totalFaces>100000||triangles.Any(i=>i<0||i>=vertices.Length))throw new InvalidDataException("Invalid triangle indices");
                result.Add(new(vertices,triangles));
            }
            parts=result.ToArray();Status="External SAS mesh · approximate pose";
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or OverflowException or InvalidOperationException or IndexOutOfRangeException or KeyNotFoundException or ArgumentException)
        {parts=[];joints=[];Status="External mesh unavailable: "+e.Message;}
    }

    static Vector3 Unit(Vector3 v,Vector3 fallback)=>v.LengthSquared()>.000001f?Vector3.Normalize(v):fallback;
    static (Vector3 Right,Vector3 Up,Vector3 Forward) Basis(Vector3 direction,Vector3 lateral)
    {
        var up=Unit(direction,Vector3.UnitY);var side=lateral-up*Vector3.Dot(lateral,up);
        if(side.LengthSquared()<.000001f)side=Vector3.Cross(up,Math.Abs(up.Z)<.8f?Vector3.UnitZ:Vector3.UnitX);
        var right=Unit(side,Vector3.UnitX);return(right,up,Unit(Vector3.Cross(right,up),Vector3.UnitZ));
    }

    readonly SilhouetteRaster raster=new();
    readonly List<(PointF A,PointF B,PointF C)> triangles=[];
    readonly Dictionary<Vertex[],(PointF[] Screen,bool[] Valid)> projected=[];
    readonly HashSet<Vertex[]> updated=[];
    Matrix4x4[] transforms=[];
    sealed record Rig(int LiveIndex,int LiveNext,Vector3 Position,Vector3 Right,Vector3 Up,Vector3 Forward,float Length);
    Rig[] rig=[];
    bool? compactRig;
    public void Dispose()=>raster.Dispose();
    bool renderFailed;
    internal void Draw(Graphics graphics,Player player,float[] matrix,int width,int height,EspSettings settings)
    {
        if(renderFailed||settings.ChamsOpacity==0&&(!settings.ChamsOutline||settings.ChamsOutlineOpacity==0))return;
        var state=graphics.Save();
        try{DrawMesh(graphics,player,matrix,width,height,settings);}
        catch(Exception e) when(e is System.Runtime.InteropServices.ExternalException or InvalidOperationException or ArgumentException or OutOfMemoryException)
        {renderFailed=true;Status="External mesh disabled after rendering error: "+e.Message;}
        finally{graphics.Restore(state);}
    }
    void DrawMesh(Graphics graphics,Player player,float[] matrix,int width,int height,EspSettings settings)
    {
        Load();if(parts.Length==0||player.Bones.Length is not(23 or 28))return;
        bool compact=player.Bones.Length==23;
        Vector3 liveLateral=player.Bones[compact?12:13]-player.Bones[8];
        if(compactRig!=compact)
        {
        var map=new Dictionary<string,int>{{"pelvis",compact?1:0},{"spine_1",2},{"spine_3",4},{"neck_0",5},{"head_0",6},{"arm_upper_L",8},{"arm_lower_L",9},{"hand_L",10},{"arm_upper_R",compact?12:13},{"arm_lower_R",compact?13:14},{"hand_R",compact?14:15},{"leg_upper_L",compact?17:22},{"leg_lower_L",compact?18:23},{"ankle_L",compact?19:24},{"leg_upper_R",compact?20:25},{"leg_lower_R",compact?21:26},{"ankle_R",compact?22:27}};
        var children=new Dictionary<string,string>{{"pelvis","spine_1"},{"spine_1","spine_3"},{"spine_3","neck_0"},{"neck_0","head_0"},{"arm_upper_L","arm_lower_L"},{"arm_lower_L","hand_L"},{"arm_upper_R","arm_lower_R"},{"arm_lower_R","hand_R"},{"leg_upper_L","leg_lower_L"},{"leg_lower_L","ankle_L"},{"leg_upper_R","leg_lower_R"},{"leg_lower_R","ankle_R"}};
        var named=joints.Select((j,i)=>(j.Name,i)).Where(p=>map.ContainsKey(p.Name)).GroupBy(p=>p.Name).ToDictionary(g=>g.Key,g=>g.First().i);
        if(!named.TryGetValue("pelvis",out int pelvis)||!named.TryGetValue("arm_upper_L",out int shoulderL)||!named.TryGetValue("arm_upper_R",out int shoulderR))return;
        Vector3 restLateral=joints[shoulderR].Position-joints[shoulderL].Position;
        rig=new Rig[joints.Length];transforms=new Matrix4x4[joints.Length];
        for(int i=0;i<joints.Length;i++)
        {
            int anchor=i;while(anchor>=0&&!map.ContainsKey(joints[anchor].Name))anchor=joints[anchor].Parent;if(anchor<0)anchor=pelvis;
            var joint=joints[anchor];int liveIndex=map[joint.Name],liveNext;Vector3 restUp;
            if(children.TryGetValue(joint.Name,out string? child)&&named.TryGetValue(child,out int next)){restUp=joints[next].Position-joint.Position;liveNext=map[child];}
            else{restUp=joints[named.GetValueOrDefault("head_0",pelvis)].Position-joints[pelvis].Position;liveNext=-1;}
            var rest=Basis(restUp,restLateral);
            rig[i]=new(map[joint.Name],liveNext,joint.Position,rest.Right,rest.Up,rest.Forward,restUp.Length());
            // For terminal joints the up vector follows pelvis -> head, but the origin stays at its own live joint.
        }
        compactRig=compact;
        }
        for(int i=0;i<rig.Length;i++)
        {
            var joint=rig[i];Vector3 liveUp=joint.LiveNext<0?player.Bones[6]-player.Bones[compact?1:0]:player.Bones[joint.LiveNext]-player.Bones[joint.LiveIndex];
            var live=Basis(liveUp,liveLateral);
            const float scale=1/.0254f;float stretch=joint.Length>.0001f?Math.Clamp(liveUp.Length()/joint.Length,scale*.55f,scale*1.6f):scale;
            Vector3 Convert(Vector3 d)=>live.Right*Vector3.Dot(d,joint.Right)*scale+live.Up*Vector3.Dot(d,joint.Up)*stretch+live.Forward*Vector3.Dot(d,joint.Forward)*scale;
            Vector3 x=Convert(Vector3.UnitX),y=Convert(Vector3.UnitY),z=Convert(Vector3.UnitZ),origin=player.Bones[joint.LiveIndex]-Convert(joint.Position);
            transforms[i]=new(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,origin.X,origin.Y,origin.Z,1);
        }
        triangles.Clear();
        // Shared material primitives reference the same vertices. Skin each buffer once per draw.
        updated.Clear();
        foreach(var part in parts)
        {
            if(!projected.TryGetValue(part.Vertices,out var projection))
            {
                var pixels=new PointF[part.Vertices.Length];var accepted=new bool[pixels.Length];
                projection=(pixels,accepted);projected[part.Vertices]=projection;
            }
            if(updated.Add(part.Vertices))
            {
                var pixels=projection.Screen;var accepted=projection.Valid;
                for(int i=0;i<part.Vertices.Length;i++)
                {
                    var vertex=part.Vertices[i];Vector3 posed=default;for(int j=0;j<4;j++)if(vertex.Weights[j]>0)posed+=Vector3.Transform(vertex.Position,transforms[vertex.Joints[j]])*vertex.Weights[j];
                    accepted[i]=Projection.ToScreen(matrix,posed,width,height,out var p)&&float.IsFinite(p.X)&&float.IsFinite(p.Y)&&Math.Abs(p.X)<width*4&&Math.Abs(p.Y)<height*4;if(accepted[i])pixels[i]=new(p.X,p.Y);
                }
            }
            var screen=projection.Screen;var valid=projection.Valid;
            for(int t=0;t<part.Indices.Length;t+=3)
            {
                int a=part.Indices[t],b=part.Indices[t+1],c=part.Indices[t+2];if(!valid[a]||!valid[b]||!valid[c])continue;var pa=screen[a];var pb=screen[b];var pc=screen[c];float area=(pb.X-pa.X)*(pc.Y-pa.Y)-(pb.Y-pa.Y)*(pc.X-pa.X);if(Math.Abs(area)<.08f)continue;
                if(Math.Abs(pa.X-pb.X)+Math.Abs(pa.Y-pb.Y)>height)continue;
                triangles.Add((pa,pb,pc));
            }
        }
        raster.Draw(graphics,triangles,width,height,settings);
    }
}
