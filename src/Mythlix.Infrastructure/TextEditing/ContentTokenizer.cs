using System.Globalization;
using System.Text;
namespace Mythlix.Infrastructure.TextEditing;
internal sealed record Token(int Start,int Length,string Value,byte[]? Bytes=null) {
 internal bool Number(out double value) => double.TryParse(Value,NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out value) && double.IsFinite(value);
}

/// <summary>Byte-offset lexer: replacements never reserialize unrelated PDF operators.</summary>
internal static class ContentTokenizer {
 static bool White(byte b)=>b is 0 or 9 or 10 or 12 or 13 or 32;
 static bool Delimiter(byte b)=>White(b) || b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';
 public static IReadOnlyList<Token> Read(byte[] bytes) {
  if(bytes.Length>8*1024*1024)throw new InvalidOperationException("Stream de contenido superior a 8 MB.");
  var tokens=new List<Token>();var p=0;
  while(p<bytes.Length) {
   if(White(bytes[p])) {p++;continue;}
   if(bytes[p]=='%') {while(p<bytes.Length && bytes[p]!=10 && bytes[p]!=13)p++;continue;}
   var start=p;
   if(bytes[p]=='(') {
    p++;var depth=1;using var value=new MemoryStream();
    while(p<bytes.Length && depth>0) {
     var b=bytes[p++];
     if(b=='\\') {
      if(p>=bytes.Length)throw new InvalidOperationException("Cadena PDF incompleta.");
      b=bytes[p++];
      if(b==13) {if(p<bytes.Length && bytes[p]==10)p++;continue;}
      if(b==10)continue;
      if(b>='0' && b<='7') {
       var oct=b-'0';for(var n=0;n<2 && p<bytes.Length && bytes[p]>='0' && bytes[p]<='7';n++)oct=oct*8+bytes[p++]-'0';
       value.WriteByte((byte)oct);continue;
      }
      value.WriteByte(b switch {(byte)'n'=>10,(byte)'r'=>13,(byte)'t'=>9,(byte)'b'=>8,(byte)'f'=>12,_=>b});continue;
     }
     if(b=='(')depth++;
     if(b==')' && --depth==0)break;
     if(b!=')') {if(b==13){if(p<bytes.Length && bytes[p]==10)p++;b=10;}value.WriteByte(b);}
     else value.WriteByte(b);
    }
    if(depth!=0)throw new InvalidOperationException("Cadena PDF sin cierre.");
    tokens.Add(new(start,p-start,"string",value.ToArray()));
   } else if(bytes[p]=='<' && p+1<bytes.Length && bytes[p+1]!='<') {
    p++;var hex=new StringBuilder();while(p<bytes.Length && bytes[p]!='>'){if(!White(bytes[p]))hex.Append((char)bytes[p]);p++;}
    if(p>=bytes.Length)throw new InvalidOperationException("Cadena hexadecimal sin cierre.");p++;
    if(hex.Length%2!=0)hex.Append('0');tokens.Add(new(start,p-start,"string",Convert.FromHexString(hex.ToString())));
   } else {
    if(bytes[p]=='/') {p++;while(p<bytes.Length && !Delimiter(bytes[p]))p++;}
    else if(Delimiter(bytes[p])) {p++;if(p<bytes.Length && (bytes[start]=='<' && bytes[p]=='<' || bytes[start]=='>' && bytes[p]=='>'))p++;}
    else while(p<bytes.Length && !Delimiter(bytes[p]))p++;
    tokens.Add(new(start,p-start,Encoding.ASCII.GetString(bytes,start,p-start)));
   }
   if(tokens.Count>100000)throw new InvalidOperationException("Demasiados operadores en el stream.");
  }
  return tokens;
 }
 public static byte[] Literal(byte[] value) {
  var result=new StringBuilder("(");
  foreach(var b in value) {
   if(b is (byte)'(' or (byte)')' or (byte)'\\')result.Append('\\').Append((char)b);
   else if(b<32 || b>126)result.Append('\\').Append(Convert.ToString(b,8).PadLeft(3,'0'));
   else result.Append((char)b);
  }
  return Encoding.ASCII.GetBytes(result.Append(')').ToString());
 }
}
