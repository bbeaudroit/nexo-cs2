using System.Numerics;
using System.Text.Json.Nodes;
using CsEsp;
internal static class CollisionGeometryChecks
{
 internal static void Run(){int passed=0;void Check(bool v,string m){if(!v)throw new Exception(m);passed++;}string dir=Path.Combine(Path.GetTempPath(),"nexo-collision-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);try{
 string file=Path.Combine(dir,"box.json");var data=JsonNode.Parse("""
 {"format":1,"map":"de_test","complete":false,"staticGeometryImported":true,"ballisticsAvailable":false,"surfaceHashes":[12,34],"collisionAttributes":[{"m_CollisionGroupString":"world"}],"counts":{"hulls":1,"planes":6,"meshes":1,"meshVertices":4,"triangles":2},"hulls":[{"bounds":{"min":[-1,-1,-1],"max":[1,1,1]},"planes":[[1,0,0,1],[-1,0,0,1],[0,1,0,1],[0,-1,0,1],[0,0,1,1],[0,0,-1,1]],"surfaceHash":12,"surfaceIndex":0,"collisionAttributeIndex":0}],"meshes":[{"bounds":{"min":[0,-1,-1],"max":[0,1,1]},"vertices":[[0,-1,-1],[0,1,-1],[0,1,1],[0,-1,1]],"triangles":[[0,1,2],[0,2,3]],"surfaceIndices":[0,0],"collisionAttributeIndex":0}]}
 """)!;
 void Save()=>File.WriteAllText(file,data.ToJsonString());Save();var geometry=CollisionGeometry.Load(file);var trace=geometry.TraceSegment(new(-2,0,0),new(2,0,0));Check(trace.HullIntervals.Count==1,"Box interval missing");Check(Math.Abs(trace.HullIntervals[0].Thickness-2)<1e-5,"Box thickness");Check(Math.Abs(trace.HullIntervals[0].EntryT-.25)<1e-5&&Math.Abs(trace.HullIntervals[0].ExitT-.75)<1e-5,"Box entry/exit");Check(trace.MeshHits.Count==1,"Shared-edge triangle hits must deduplicate");Check(geometry.TraceSegment(new(2,0,0),new(-2,0,0)).MeshHits.Count==1,"Double sided triangle");Check(geometry.TraceSegment(new(0,0,0),new(2,0,0)).HullIntervals[0].StartsInside,"Starts inside");Check(geometry.TraceSegment(new(-2,1,0),new(2,1,0)).HullIntervals.All(x=>x.Thickness==0),"Tangency cannot imply material thickness");Check(geometry.TraceSegment(new(-2,2,0),new(2,2,0)).HullIntervals.Count==0,"Out of bounds miss");Check(geometry.TraceSegment(Vector3.Zero,Vector3.Zero).HullIntervals.Count==0,"Zero segment");bool rejected=false;try{geometry.TraceSegment(new(float.NaN,0,0),Vector3.One);}catch(ArgumentException){rejected=true;}Check(rejected,"NaN query rejected");
 // Segment clipping must not manufacture a physical exit at its destination.
 var clipped=geometry.TraceSegment(new(-2,0,0),Vector3.Zero).HullIntervals.Single();
 Check(clipped.EndsInside&&!clipped.HasPhysicalExit,"Destination inside has no physical exit");
 Check(clipped.ExitT==1&&clipped.ExitNormal==Vector3.Zero&&Math.Abs(clipped.Thickness-1)<1e-6,"Clipped interval preserves segment length without an invented exit normal");
 var insideExit=geometry.TraceSegment(Vector3.Zero,new(2,0,0)).HullIntervals.Single();
 Check(insideExit.StartsInside&&!insideExit.EndsInside&&insideExit.HasPhysicalExit,"Starting inside can reach a physical exit");
 Check(insideExit.ExitNormal==Vector3.UnitX&&Math.Abs(insideExit.ExitT-.5)<1e-6,"Inside exit plane and time");
 var whollyInside=geometry.TraceSegment(new(-.5f,0,0),new(.5f,0,0)).HullIntervals.Single();
 Check(whollyInside.StartsInside&&whollyInside.EndsInside&&!whollyInside.HasPhysicalExit,"Wholly inside segment remains truncated");
 var boundaryExit=geometry.TraceSegment(new(-2,0,0),new(1,0,0)).HullIntervals.Single();
 Check(!boundaryExit.EndsInside&&boundaryExit.HasPhysicalExit&&boundaryExit.ExitT==1&&boundaryExit.ExitNormal==Vector3.UnitX,"Destination on outgoing boundary is a real exit");
 var boundaryEntry=geometry.TraceSegment(new(-2,0,0),new(-1,0,0)).HullIntervals.Single();
 Check(boundaryEntry.IsTangent&&!boundaryEntry.EndsInside&&!boundaryEntry.HasPhysicalExit,"Endpoint touches entry boundary without traversing material");
 var faceTangent=geometry.TraceSegment(new(-2,1,0),new(2,1,0)).HullIntervals.Single();
 Check(faceTangent.IsTangent&&!faceTangent.HasPhysicalExit,"Coplanar face trace is not a physical material exit");
 Check(!trace.HullIntervals.Single().EndsInside&&trace.HullIntervals.Single().HasPhysicalExit,"Complete traversal has a physical exit");
 Check(trace.HullIntervals.Single().HullIndex==clipped.HullIndex&&clipped.HullIndex==0,"Stable source hull identity across queries");
 var boundaryInside=geometry.TraceSegment(new(-1,0,0),Vector3.Zero).HullIntervals.Single();
 Check(boundaryInside.StartsInside&&boundaryInside.EndsInside&&!boundaryInside.HasPhysicalExit&&!boundaryInside.IsTangent,"Boundary origin moving inward ends with a truncated material interval");
 var boundaryOut=geometry.TraceSegment(new(1,0,0),new(2,0,0)).HullIntervals.Single();
 Check(boundaryOut.StartsInside&&boundaryOut.IsTangent&&!boundaryOut.HasPhysicalExit,"Boundary origin moving outward has zero material traversal");
 foreach(var mutate in new Action<JsonNode>[] {d=>d["hulls"]![0]!["planes"]![0]![0]=0,d=>d["meshes"]![0]!["triangles"]![0]![0]=99,d=>d["meshes"]![0]!["surfaceIndices"]![0]=9,d=>d["counts"]!["triangles"]=3,d=>d["meshes"]![0]!["vertices"]![0]![0]=100,d=>d["hulls"]![0]!["bounds"]!["min"]![0]=2}){var bad=data.DeepClone();mutate(bad);File.WriteAllText(file,bad.ToJsonString());rejected=false;try{CollisionGeometry.Load(file);}catch(InvalidDataException){rejected=true;}Check(rejected,"Malformed geometry must fail closed");}

 Save();File.WriteAllText(file,File.ReadAllText(file).Replace("\"format\":1", "\"format\":1,\"format\":1"));rejected=false;try{CollisionGeometry.Load(file);}catch(InvalidDataException){rejected=true;}Check(rejected,"Duplicate JSON keys rejected");
 Save();File.WriteAllText(file,File.ReadAllText(file).Replace("[-1,-1,-1]","[1e999,-1,-1]"));rejected=false;try{CollisionGeometry.Load(file);}catch(InvalidDataException){rejected=true;}Check(rejected,"Nonfinite geometry rejected");
 rejected=false;try{geometry.TraceSegment(new(1e10f,0,0),Vector3.Zero);}catch(ArgumentException){rejected=true;}Check(rejected,"Out of range query rejected");

 foreach(var mutate in new Action<JsonNode>[] {
 d=>{d["hulls"]![0]!["bounds"]!["min"]=JsonNode.Parse("[10,10,10]");d["hulls"]![0]!["bounds"]!["max"]=JsonNode.Parse("[12,12,12]");},
 d=>{d["hulls"]![0]!["planes"]![5]=d["hulls"]![0]!["planes"]![4]!.DeepClone();},
 d=>{d["hulls"]![0]!["planes"]![5]=JsonNode.Parse("[0,0,1,2]");},
 d=>{d["hulls"]![0]!["planes"]![1]=JsonNode.Parse("[-1,0,0,-2]");}
 }){var bad=data.DeepClone();mutate(bad);File.WriteAllText(file,bad.ToJsonString());rejected=false;try{CollisionGeometry.Load(file);}catch(InvalidDataException){rejected=true;}Check(rejected,"Incoherent, duplicate, unbounded or infeasible hull must fail closed");}
 var rounded=data.DeepClone();rounded["hulls"]![0]!["bounds"]!["min"]![0]=-.99;File.WriteAllText(file,rounded.ToJsonString());Check(CollisionGeometry.Load(file).TraceSegment(new(-.995f,-2,0),new(-.995f,2,0)).HullIntervals.Count==1,"BVH must use actual halfspace bounds rather than rounded metadata");
 data["meshes"]![0]!["surfaceIndices"]![1]=1;Save();Check(CollisionGeometry.Load(file).TraceSegment(new(-2,0,0),new(2,0,0)).MeshHits.Count==2,"Different materials at same t preserved");
 var opposed=data.DeepClone();opposed["meshes"]![0]!["surfaceIndices"]![1]=0;opposed["meshes"]![0]!["triangles"]![1]=JsonNode.Parse("[0,3,2]");File.WriteAllText(file,opposed.ToJsonString());
 var oppositeHits=CollisionGeometry.Load(file).TraceSegment(new(-2,0,0),new(2,0,0)).MeshHits;
 Check(oppositeHits.Count==2,"Coincident opposite-facing contacts must remain separate");
 Check(oppositeHits.Select(h=>h.FrontFacing).Distinct().Count()==2,"Entry and exit orientations retained at the same fraction");
 var separate=data.DeepClone();separate["meshes"]![0]!["surfaceIndices"]![1]=0;((JsonArray)separate["meshes"]!).Add(separate["meshes"]![0]!.DeepClone());separate["counts"]!["meshes"]=2;separate["counts"]!["meshVertices"]=8;separate["counts"]!["triangles"]=4;File.WriteAllText(file,separate.ToJsonString());
 Check(CollisionGeometry.Load(file).TraceSegment(new(-2,0,0),new(2,0,0)).MeshHits.Count==2,"Coincident contacts from independent source meshes must remain separate");
 string actual=Path.GetFullPath("local-assets/collision/de_mirage.json");if(File.Exists(actual)){var watch=System.Diagnostics.Stopwatch.StartNew();var mirage=CollisionGeometry.Load(actual);long loadMs=watch.ElapsedMilliseconds;Check(mirage.HullCount==1933&&mirage.TriangleCount==96357,"Mirage counts");watch.Restart();int candidates=0;for(int i=0;i<1000;i++)candidates+=mirage.TraceSegment(new(-1500+i%40*20,-1000,80),new(-900+i%40*20,-500,100)).CandidateTests;Console.WriteLine($"Mirage load {loadMs}ms; 1000 traces {watch.ElapsedMilliseconds}ms; candidates {candidates}");}
 }finally{Directory.Delete(dir,true);}Console.WriteLine($"{passed} collision geometry checks passed.");}
}
