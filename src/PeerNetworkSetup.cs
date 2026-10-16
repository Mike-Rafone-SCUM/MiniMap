using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Xml;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ScumMiniMap {
    // Read-only UPnP WAN discovery. Every HTTP endpoint must belong to the local
    // default gateway: no external IP service, proxy, redirect or XML entity.
    internal static class RouterAddressDiscovery {
        internal sealed class Service { internal string Type;internal Uri Control; }
        internal static string Find(IPAddress[] gateways,Func<IPAddress,string> nat,Func<IPAddress,string> upnp) {
            foreach(var lookup in new[]{nat,upnp})foreach(var gateway in gateways)try {
                string address=PublicPositionAddress.Parse(lookup(gateway));if(address!=null)return address;
            }catch(Exception) {}
            return null;
        }
        internal static bool LocalEndpoint(Uri uri,IPAddress gateway) {
            IPAddress address;return uri!=null&&uri.IsAbsoluteUri&&uri.Scheme=="http"&&string.IsNullOrEmpty(uri.UserInfo)&&string.IsNullOrEmpty(uri.Fragment)&&IPAddress.TryParse(uri.Host,out address)&&address.Equals(gateway);
        }
        internal static Uri Location(string response,IPAddress gateway) {
            if(response==null||response.Length>8192||!response.StartsWith("HTTP/1.1 200",StringComparison.OrdinalIgnoreCase))return null;
            foreach(string line in response.Split('\n'))if(line.StartsWith("LOCATION:",StringComparison.OrdinalIgnoreCase)) {
                Uri uri;if(Uri.TryCreate(line.Substring(9).Trim(),UriKind.Absolute,out uri)&&LocalEndpoint(uri,gateway))return uri;
            }
            return null;
        }
        static XmlDocument Xml(byte[] data) {
            if(data==null||data.Length>262144)throw new InvalidDataException();
            var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=262144 };
            var xml=new XmlDocument { XmlResolver=null };using(var stream=new MemoryStream(data))using(var reader=XmlReader.Create(stream,settings))xml.Load(reader);return xml;
        }
        internal static Service[] Services(byte[] data,Uri descriptor,IPAddress gateway) {
            if(!LocalEndpoint(descriptor,gateway))throw new InvalidDataException();var xml=Xml(data);Uri baseUri=descriptor;
            var root=xml.SelectSingleNode("/*[local-name()='root']/*[local-name()='URLBase']");
            if(root!=null&&!string.IsNullOrWhiteSpace(root.InnerText)) { Uri candidate;if(!Uri.TryCreate(root.InnerText.Trim(),UriKind.Absolute,out candidate)||!LocalEndpoint(candidate,gateway))throw new InvalidDataException();baseUri=candidate; }
            var services=new System.Collections.Generic.List<Service>();
            foreach(XmlNode node in xml.SelectNodes("//*[local-name()='service']")) {
                var type=node.SelectSingleNode("*[local-name()='serviceType']");var control=node.SelectSingleNode("*[local-name()='controlURL']");if(type==null||control==null)continue;
                string service=type.InnerText.Trim();if(service!="urn:schemas-upnp-org:service:WANIPConnection:1"&&service!="urn:schemas-upnp-org:service:WANIPConnection:2"&&service!="urn:schemas-upnp-org:service:WANPPPConnection:1")continue;
                Uri uri;if(Uri.TryCreate(baseUri,control.InnerText.Trim(),out uri)&&LocalEndpoint(uri,gateway))services.Add(new Service { Type=service,Control=uri });if(services.Count>=8)break;
            }
            return services.ToArray();
        }
        internal static string SoapAddress(byte[] data) {
            var value=Xml(data).SelectSingleNode("//*[local-name()='GetExternalIPAddressResponse']/*[local-name()='NewExternalIPAddress']");return value==null?null:PublicPositionAddress.Parse(value.InnerText);
        }
        static byte[] Fetch(Uri uri,IPAddress gateway,string service=null) {
            if(!LocalEndpoint(uri,gateway))throw new InvalidDataException();var request=(HttpWebRequest)WebRequest.Create(uri);
            request.Proxy=null;request.AllowAutoRedirect=false;request.Timeout=2000;request.ReadWriteTimeout=2000;request.MaximumResponseHeadersLength=16;
            request.KeepAlive=false;request.ServicePoint.Expect100Continue=false;
            if(service!=null) {
                request.Method="POST";request.ContentType="text/xml; charset=utf-8";request.Headers["SOAPAction"]="\""+service+"#GetExternalIPAddress\"";
                byte[] body=Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body><u:GetExternalIPAddress xmlns:u=\""+service+"\" /></s:Body></s:Envelope>");
                request.ContentLength=body.Length;using(var stream=request.GetRequestStream())stream.Write(body,0,body.Length);
            }
            using(var response=(HttpWebResponse)request.GetResponse()) {
                if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>262144)throw new InvalidDataException();
                using(var stream=response.GetResponseStream())using(var output=new MemoryStream()) {
                    byte[] buffer=new byte[4096];int count;while((count=stream.Read(buffer,0,buffer.Length))>0) { if(output.Length+count>262144)throw new InvalidDataException();output.Write(buffer,0,count); }return output.ToArray();
                }
            }
        }
        internal static string Upnp(IPAddress gateway) {
            IPAddress local;using(var route=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)) { route.Connect(gateway,9);local=((IPEndPoint)route.LocalEndPoint).Address; }
            var locations=new System.Collections.Generic.List<Uri>();
            using(var client=new UdpClient(new IPEndPoint(local,0))) {
                client.Client.SetSocketOption(SocketOptionLevel.IP,SocketOptionName.MulticastInterface,local.GetAddressBytes());client.Client.SetSocketOption(SocketOptionLevel.IP,SocketOptionName.MulticastTimeToLive,1);
                byte[] request=Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 1\r\nST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n\r\n");
                client.Send(request,request.Length,new IPEndPoint(IPAddress.Parse("239.255.255.250"),1900));var timer=System.Diagnostics.Stopwatch.StartNew();
                for(int count=0;count<16&&timer.ElapsedMilliseconds<1500;count++)try {
                    client.Client.ReceiveTimeout=(int)Math.Max(1,1500-timer.ElapsedMilliseconds);var sender=new IPEndPoint(IPAddress.Any,0);byte[] reply=client.Receive(ref sender);if(!sender.Address.Equals(gateway))continue;
                    Uri location=Location(Encoding.ASCII.GetString(reply),gateway);if(location!=null&&!locations.Contains(location))locations.Add(location);
                }catch(SocketException) { break; }
            }
            foreach(var location in locations.Take(4))try { foreach(var service in Services(Fetch(location,gateway),location,gateway))try { string address=SoapAddress(Fetch(service.Control,gateway,service.Type));if(address!=null)return address; }catch(Exception) {} }catch(Exception) {}
            return null;
        }
    }

    internal sealed class PeerFirewallRule {
        internal string Name,Description,Application,Ports,Interfaces;
        internal int Protocol,Direction,Action,Profiles;
        internal bool Enabled,EdgeTraversal;
    }
    internal interface IPeerFirewallPolicy:IDisposable {
        PeerFirewallRule Get(string name);
        void Add(PeerFirewallRule rule);
        void Remove(string name);
    }
    internal static class PeerFirewall {
        internal static PeerFirewallRule Expected(string application,int port) {
            if(string.IsNullOrWhiteSpace(application)||!Path.IsPathRooted(application)||port<1024||port>65535)throw new ArgumentException();application=Path.GetFullPath(application);
            string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(application.ToUpperInvariant()+"|"+port))).Replace("-","").Substring(0,16);
            return new PeerFirewallRule { Name="SCUM MiniMap private TCP "+port+" ["+hash+"]",Description="SCUM MiniMap managed host rule v1",Application=application,Ports=port.ToString(System.Globalization.CultureInfo.InvariantCulture),Protocol=6,Direction=1,Action=1,Profiles=7,Enabled=true,EdgeTraversal=false,Interfaces="All" };
        }
        static bool Owned(PeerFirewallRule rule,PeerFirewallRule expected) { return rule!=null&&rule.Name==expected.Name&&rule.Description==expected.Description&&string.Equals(rule.Application,expected.Application,StringComparison.OrdinalIgnoreCase)&&rule.Ports==expected.Ports; }
        static bool Matches(PeerFirewallRule rule,PeerFirewallRule expected) { return Owned(rule,expected)&&rule.Protocol==6&&rule.Direction==1&&rule.Action==1&&rule.Profiles==7&&rule.Enabled&&!rule.EdgeTraversal&&rule.Interfaces=="All"; }
        internal static bool Ensure(string application,int port,IPeerFirewallPolicy policy) {
            var expected=Expected(application,port);var existing=policy.Get(expected.Name);if(existing!=null)return Matches(existing,expected);
            try { policy.Add(expected);if(Matches(policy.Get(expected.Name),expected))return true; }
            catch(Exception) { if(Owned(policy.Get(expected.Name),expected))policy.Remove(expected.Name);throw; }
            if(Owned(policy.Get(expected.Name),expected))policy.Remove(expected.Name);return false;
        }
        internal static bool Ensure(string application,int port) {
            try { using(var policy=new WindowsPolicy())return Ensure(application,port,policy); }catch(Exception) { return false; }
        }
        sealed class WindowsPolicy:IPeerFirewallPolicy {
            object policy,rules;
            static object GetValue(object target,string property) { return target.GetType().InvokeMember(property,BindingFlags.GetProperty,null,target,null); }
            static void SetValue(object target,string property,object value) { target.GetType().InvokeMember(property,BindingFlags.SetProperty,null,target,new[]{value}); }
            static object Invoke(object target,string method,params object[] args) { return target.GetType().InvokeMember(method,BindingFlags.InvokeMethod,null,target,args); }
            static void Release(object value) { if(value!=null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value); }
            internal WindowsPolicy() { try { policy=Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2",true));if((int)GetValue(policy,"LocalPolicyModifyState")!=0)throw new UnauthorizedAccessException();rules=GetValue(policy,"Rules"); }catch { Dispose();throw; } }
            public PeerFirewallRule Get(string name) {
                object rule=null;try { rule=Invoke(rules,"Item",name);return new PeerFirewallRule { Name=(string)GetValue(rule,"Name"),Description=(string)GetValue(rule,"Description"),Application=(string)GetValue(rule,"ApplicationName"),Ports=(string)GetValue(rule,"LocalPorts"),Interfaces=(string)GetValue(rule,"InterfaceTypes"),Protocol=(int)GetValue(rule,"Protocol"),Direction=(int)GetValue(rule,"Direction"),Action=(int)GetValue(rule,"Action"),Profiles=(int)GetValue(rule,"Profiles"),Enabled=(bool)GetValue(rule,"Enabled"),EdgeTraversal=(bool)GetValue(rule,"EdgeTraversal") }; }
                catch(Exception e) { var wrapped=e as TargetInvocationException;var error=wrapped==null?e:wrapped.InnerException;if(error!=null&&(error.HResult==unchecked((int)0x80070002)||error.HResult==unchecked((int)0x80070490)))return null;throw; }
                finally { Release(rule); }
            }
            public void Add(PeerFirewallRule expected) {
                object rule=null;try {
                    rule=Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule",true));SetValue(rule,"Name",expected.Name);SetValue(rule,"Description",expected.Description);SetValue(rule,"ApplicationName",expected.Application);SetValue(rule,"Protocol",expected.Protocol);SetValue(rule,"LocalPorts",expected.Ports);SetValue(rule,"Direction",expected.Direction);SetValue(rule,"Action",expected.Action);SetValue(rule,"Profiles",expected.Profiles);SetValue(rule,"InterfaceTypes",expected.Interfaces);SetValue(rule,"EdgeTraversal",expected.EdgeTraversal);SetValue(rule,"Enabled",expected.Enabled);Invoke(rules,"Add",rule);
                }finally { Release(rule); }
            }
            public void Remove(string name) { Invoke(rules,"Remove",name); }
            public void Dispose() { Release(rules);Release(policy);rules=policy=null; }
        }
    }
}
