namespace CsEsp;
public sealed class OwnedInt(int original)
{
    bool written;int last;
    public void Written(int value){written=true;last=value;}
    public int? RestoreValue(int current)=>written&&current==last?original:null;
}
