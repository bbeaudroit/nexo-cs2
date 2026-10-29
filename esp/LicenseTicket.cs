using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace CsEsp;
internal static class LicenseTicket
{
    static byte[] Decode(string value)=>Convert.FromBase64String(value.Replace('-','+').Replace('_','/').PadRight((value.Length+3)/4*4,'='));
    internal static bool Validate(string ticket,string publicKey,string device,long now)
    {
        try
        {
            if(ticket.Length>4096||device.Length!=64)return false;var parts=ticket.Split('.');if(parts.Length!=2)return false;
            using var key=ECDsa.Create();key.ImportFromPem(publicKey);
            if(!key.VerifyData(Encoding.ASCII.GetBytes(parts[0]),Decode(parts[1]),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation))return false;
            using var doc=JsonDocument.Parse(Decode(parts[0]));var p=doc.RootElement;
            long issued=p.GetProperty("iat").GetInt64(),expiry=p.GetProperty("exp").GetInt64(),licenseExpiry=p.GetProperty("licenseExpires").GetInt64();
            return p.GetProperty("v").GetInt32()==1&&p.GetProperty("iss").GetString()=="nexo-licensing"&&p.GetProperty("aud").GetString()=="nexo-desktop"&&p.GetProperty("device").GetString()==device&&issued<=now+30&&expiry>now&&expiry>issued&&expiry-issued<=600&&expiry<=licenseExpiry;
        }
        catch(Exception e) when(e is CryptographicException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException or ArgumentException or OverflowException){return false;}
    }
}
