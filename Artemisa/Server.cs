using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Web;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Artemisa {
    public class ServerOptions {
        private Server parent;
        public string logFile;
        public int port;
        public string configFile;

        public struct Module {
            private string _baseName;
            private string _path;
            private dynamic _reference;
            public string BaseName {
                get {
                    return _baseName;
                }
            }
            public string Path {
                get {
                    return _path;
                }
            }
            public dynamic Reference {
                get {
                    return _reference;
                }
            }

            public Module(string baseName, string path, dynamic reference) {
                _baseName = baseName;
                _path = path;
                _reference = reference;
            }
        }
        public Dictionary<string, Module> Modules;

        public ServerOptions(Server from) {
            Modules = new Dictionary<string, Module>();
            if (!File.Exists(from.localPath + "System/Startup.dll")) {
                from.log("Startup module not found.", Server.LogStatus.Error);
                throw new Exception("Startup module not found.");
            }
            parent = from;

            Assembly DLL = Assembly.LoadFile(from.localPath + "System/Startup.dll");
            Type startup = DLL.GetType("Artemisa.Startup");

            dynamic Startup = Activator.CreateInstance(startup);
            
            Modules.Add("Startup", new Module("Startup", from.localPath + "System/Startup.dll", Startup));
            Modules["Startup"].Reference.Parent = parent;
        }

        public bool AddModule(string name) {
            if (!File.Exists(parent.localPath + Modules["Startup"].Reference.ModulesDirectory + "/" + name + ".dll")) {
                parent.log(name + " module not found.", Server.LogStatus.Error);
                return false;
            }

            try {
                string mPath = parent.localPath + Modules["Startup"].Reference.ModulesDirectory + "/" + name + ".dll";

                Assembly DLL = Assembly.LoadFile(mPath);
                Type mType = DLL.GetType("Artemisa.Module");

                dynamic module = Activator.CreateInstance(mType);
                
                Modules.Add(name, new Module(name, mPath, module));
                Modules[name].Reference.Parent = parent;
                return Modules[name].Reference.Load();
            } catch (Exception e) {
                parent.log("Error loading module " + name + ". {\n" + e.Message + "\n}");
                return false;
            }
        }
        
        public void RemoveModule(string name) {
            if (Modules.ContainsKey(name)) {
                Modules[name].Reference.Dispose();
                Modules.Remove(name);
            }
        }

        public void ReloadModule(string name) {
            if (Modules.ContainsKey(name)) {
                string mPath = Modules[name].Path;
                Modules[name].Reference.Dispose();

                Assembly DLL = Assembly.LoadFile(mPath);
                Type mType = DLL.GetType("Artemisa.Module");

                dynamic module = Activator.CreateInstance(mType);
                Module newModule = new Module(name, mPath, module);
                
                Modules[name] = newModule;
                Modules[name].Reference.Parent = parent;
                Modules[name].Reference.Load();
            }
        }

        public dynamic GetModule(string name) {
            if (Modules.ContainsKey(name)) {
                return Modules[name].Reference;
            } else {
                return null;
            }
        }

        public bool isModule(dynamic module) {
            try {
                string mName = module.GetType().Name;
                return Modules.ContainsKey(mName);
            } catch (Exception e) {
                parent.log(e.Message, Server.LogStatus.Error);
                return false;
            }
        }
    }
    public class Server {
        public enum LogStatus {
            Default,
            System,
            Error,
            Success,
            Process
        };
        public readonly string defaultLogFile = DateTime.Now.ToString("AR-yyyy.MM.dd[HH-mm-ss]");
        public ServerOptions Options;

        public X509Certificate serverCertificate = null;

        public struct WebResponse {
            private string _status;
            private int _code;
            private dynamic _response;

            public string Status {
                get {
                    return _status;
                }
            }
            public int Code {
                get {
                    return _code;
                }
            }
            public dynamic Response {
                get {
                    return _response;
                }
            }

            public WebResponse(string status, int code, dynamic response) {
                _status = status;
                _code = code;
                _response = response;
            }
        }

        public string localPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/";

        public void log(string log, LogStatus status = LogStatus.Default) {
            string timeStamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            string statusText = "Normal";

            ConsoleColor timeColor = ConsoleColor.Gray;
            ConsoleColor statusColor = ConsoleColor.Green;
            ConsoleColor logColor = ConsoleColor.White;
            ConsoleColor simbolsColor = ConsoleColor.Blue;

            switch (status) {
                case LogStatus.Error:
                    statusText = "Error";
                    statusColor = ConsoleColor.Red;
                    logColor = ConsoleColor.Yellow;
                break;
                case LogStatus.System:
                    statusText = "System";
                    statusColor = ConsoleColor.Blue;
                    logColor = ConsoleColor.White;
                break;
                case LogStatus.Success:
                    statusText = "Success";
                    statusColor = ConsoleColor.Green;
                    logColor = ConsoleColor.Green;
                break;
                case LogStatus.Process:
                    statusText = "Process";
                    statusColor = ConsoleColor.Yellow;
                    logColor = ConsoleColor.White;
                break;
            }

            colorWrite("Log [", simbolsColor); colorWrite(timeStamp, timeColor); colorWrite("] <", simbolsColor); colorWrite(statusText, statusColor); colorWrite("> ", simbolsColor);
            colorWrite(log + "\n", logColor);

            if (!Directory.Exists(localPath + "Logs/")) Directory.CreateDirectory(localPath + "Logs/");
            File.AppendAllTextAsync(localPath + "Logs/" + Options.logFile + ".log", "Log [" + timeStamp + "] <" + statusText + "> " + log + "\n", Encoding.UTF8);
        }
        public void log(string log, dynamic module) {
            if (Options.isModule(module)) {
                string timeStamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                string statusText = module.ModuleName;

                ConsoleColor timeColor = ConsoleColor.Gray;
                ConsoleColor statusColor = ConsoleColor.Magenta;
                ConsoleColor logColor = ConsoleColor.White;
                ConsoleColor simbolsColor = ConsoleColor.Blue;
                
                colorWrite("Module [", simbolsColor); colorWrite(timeStamp, timeColor); colorWrite("] <", simbolsColor); colorWrite(statusText, statusColor); colorWrite("> ", simbolsColor);
                colorWrite(log + "\n", logColor);

                if (!Directory.Exists(localPath + "Logs/")) Directory.CreateDirectory(localPath + "Logs/");
                File.AppendAllTextAsync(localPath + "Logs/" + Options.logFile + ".log", "Log [" + timeStamp + "] <" + statusText + "> " + log + "\n", Encoding.UTF8);
            }
        }
        private void colorWrite(string text, ConsoleColor color) {
            ConsoleColor prevFore = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = prevFore;
        }

        public Server(ServerOptions options) {
            Options = options;
        }
        public Server() {
            ServerOptions defaultServerOptions = new ServerOptions(this);
            defaultServerOptions.configFile = "Artemisa";
            defaultServerOptions.logFile = defaultLogFile;
            defaultServerOptions.port = 3000;

            Options = defaultServerOptions;
        }

        public bool Startup() {
            try {
                Options.GetModule("Startup").Start();
                return true;
            } catch (Exception e) {
                log(e.Message, LogStatus.Error);
                return false;
            }
        }

        public async Task Listen() {
            string hostName = Dns.GetHostName();
            IPHostEntry ipHostInfo = Dns.GetHostEntry(hostName);
            IPAddress ip = null;
            
            for (int i = 0; i < ipHostInfo.AddressList.Length; ++i) {
                if (ipHostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork) {
                    ip = ipHostInfo.AddressList[i];
                    break;
                }
            }
            if (ip == null) {
                throw new Exception("No IPv4 address for server");
            }
            TcpListener listener = new TcpListener(ip, Options.port);

            listener.Start();
            log("Server started at " + ip.ToString() + ":" + Options.port.ToString(), LogStatus.Process);
            while (true) {
                try {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    log("Connection from " + client.Client.RemoteEndPoint.ToString(), LogStatus.Process);
                    Task t = acceptConnection(client);
                    await t;
                } catch (Exception ex) {
                    log(ex.Message, LogStatus.Error);
                }
            }
        }

        private async Task acceptConnection(TcpClient client) {
            // NetworkStream stream = client.GetStream();
            SslStream stream = new SslStream(client.GetStream());

            while (true) {
                stream.AuthenticateAsServer(serverCertificate);
                StreamReader reader = new StreamReader(stream);
                string request = await reader.ReadLineAsync();
                if (!String.IsNullOrEmpty(request)) {
                    StreamWriter writer = new StreamWriter(stream);

                    Handle(request, writer);

                    writer.Flush();
                } else {
                    break;
                }
            }
            client.Close();
        }

        private void Handle(string request, StreamWriter writer) {
            log(request, LogStatus.Process);

            string[] parts = request.Split(' ');
            string resource = parts[1];

            string instruction;
            Dictionary<string, string> par;

            WebResponse response;

            if (parts[0] == "GET" || parts[0] == "POST") {
                instruction = resource.Split('?', 2)[0];
                string[] tmpSpl = resource.Split('?', 2)[1].Split('&');

                par = new Dictionary<string, string>();
                for (int x = 0; x < tmpSpl.Length; x++) {
                    par.Add(tmpSpl[x].Split('=', 2)[0], HttpUtility.HtmlDecode(tmpSpl[x].Split('=', 2)[1]));
                }

                response = new WebResponse(instruction, 0, new WebResponse("instruction", 0, par));

                writer.Write("HTTP/1.1 200 OK\r\n\r\n");
                writer.Write(JsonConvert.SerializeObject(response, Formatting.Indented));
            }   
        }
    }
}