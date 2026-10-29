using System.Media;
using System.Text;
namespace CsEsp;
internal sealed class HitSound : IDisposable
{
    SoundPlayer? player;MemoryStream? stream;int type=-1,volume=-1;
    internal void Play(int selected,int level)
    {
        level=Math.Clamp(level,0,100);if(level==0)return;selected=Math.Clamp(selected,0,2);
        if(player is null||type!=selected||volume!=level)
        {
            Dispose();type=selected;volume=level;const int rate=22050,count=1764;stream=new MemoryStream();using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                for(int i=0;i<count;i++){double t=(double)i/rate,f=selected==0?1800:selected==1?880:220;double sample=(Math.Sin(2*Math.PI*f*t)+.25*Math.Sin(4*Math.PI*f*t))*Math.Exp(-t/(selected==1?.03:.012));writer.Write((short)(sample*15000*level/100));}
            }
            stream.Position=0;player=new SoundPlayer(stream);player.Load();
        }
        player.Play();
    }
    public void Dispose(){player?.Stop();player?.Dispose();player=null;stream?.Dispose();stream=null;}
}
