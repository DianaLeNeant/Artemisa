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

namespace Artemisa {

    public class ServerOptions {
        private Server parent;

        public string logFile;
        public FileStream logStream;

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

        public ServerOptions(Server from, string logPath) {
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

            logFile = logPath;
            logStream = File.OpenWrite(logPath);
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
            } catch (Exception) {
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
    #region Definitions
        public enum LogStatus {
            Default,
            System,
            Error,
            Success,
            Process
        };

        public string localPath {
            get {
                return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/";
            }
        }
        public string defaultLogFile {
            get {
                return localPath + "/Logs/" + DateTime.Now.ToString("AR-yyyy.MM.dd[HH-mm-ss]") + ".log";
            }
        }
        public ServerOptions Options;

        public X509Certificate serverCertificate = null;
    #endregion

    #region Log
        private void log(string log, LogStatus status = LogStatus.Default) {
            string timeStamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            string statusText = "Normal";

            ConsoleColor timeColor = ConsoleColor.Gray;
            ConsoleColor statusColor = ConsoleColor.White;
            ConsoleColor logColor = ConsoleColor.Gray;
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
                    logColor = ConsoleColor.Gray;
                break;
                case LogStatus.Success:
                    statusText = "Success";
                    statusColor = ConsoleColor.Green;
                    logColor = ConsoleColor.White;
                break;
                case LogStatus.Process:
                    statusText = "Process";
                    statusColor = ConsoleColor.Yellow;
                    logColor = ConsoleColor.White;
                break;
            }

            colorWrite("Log [", simbolsColor);
            colorWrite(timeStamp, timeColor);
            colorWrite("] <", simbolsColor);
            colorWrite(statusText, statusColor);
            colorWrite("> ", simbolsColor);
            
            beautyWrite(log + "\n", logColor);
        }
        public void log(string log, dynamic module) {
            if (Options.isModule(module)) {
                string timeStamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                string statusText = module.ModuleName;

                ConsoleColor timeColor = ConsoleColor.Yellow;
                ConsoleColor statusColor = ConsoleColor.Magenta;
                ConsoleColor logColor = ConsoleColor.White;
                ConsoleColor simbolsColor = ConsoleColor.Blue;
                
                colorWrite("Module [", simbolsColor);
                colorWrite(timeStamp, timeColor);
                colorWrite("] <", simbolsColor);
                colorWrite(statusText, statusColor);
                colorWrite("> ", simbolsColor);

                beautyWrite(log + "\n", logColor);
            }
        }
        private void colorWrite(string text, ConsoleColor color, bool logOut = true) {
            ConsoleColor prevFore = Console.ForegroundColor;

            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = prevFore;

            if (logOut) Options.logStream.Write(new UTF8Encoding(true).GetBytes(text), 0, text.Length);
        }
        private void beautyWrite(string str, ConsoleColor defaultColor = ConsoleColor.White) {
            for (int x = 0; x < str.Length; x++) {
                char tc = str.ToCharArray()[x];
                ConsoleColor c = defaultColor;

                switch (tc)
                {
                    case '{':
                        c = ConsoleColor.Magenta;
                        colorWrite(tc.ToString(), c); x++; tc = str.ToCharArray()[x];
                        while (tc != '}') {
                            colorWrite(tc.ToString(), ConsoleColor.DarkMagenta);
                            x++; tc = str.ToCharArray()[x];
                        }
                        colorWrite(tc.ToString(), c); continue;
                    
                    case '[':
                        c = ConsoleColor.Cyan;
                        colorWrite(tc.ToString(), c); x++; tc = str.ToCharArray()[x];
                        while (tc != ']') {
                            colorWrite(tc.ToString(), ConsoleColor.DarkCyan);
                            x++; tc = str.ToCharArray()[x];
                        }
                        colorWrite(tc.ToString(), c); continue;
                    
                    case '\'':
                        c = ConsoleColor.Yellow;
                        colorWrite(tc.ToString(), c); x++; tc = str.ToCharArray()[x];
                        while (tc != '\'') {
                            colorWrite(tc.ToString(), ConsoleColor.Gray);
                            x++; tc = str.ToCharArray()[x];
                        }
                        colorWrite(tc.ToString(), c); continue;

                    case '.': case ',':
                        c = ConsoleColor.DarkBlue;
                        break;
                }

                int r;
                if (Int32.TryParse(tc.ToString(), out r)) {
                    colorWrite(tc.ToString(), ConsoleColor.Blue);
                } else {
                    colorWrite(tc.ToString(), c);
                }
            }
        }
    #endregion

    #region Server startup
        public Server(ServerOptions options) {
            Options = options;
        }
        public Server() {
            try {
                ServerOptions defaultServerOptions = new ServerOptions(this, defaultLogFile);

                defaultServerOptions.configFile = "Artemisa";
                defaultServerOptions.port = 3000;

                Options = defaultServerOptions;
            } catch (System.Exception) {
                throw;
            }
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
    #endregion

    #region Server listening
        public async Task Listen() {
            IPHostEntry ipHostInfo = Dns.GetHostEntry("localhost"/* Dns.GetHostName() */);
            IPAddress ip = null;

            for (int i = 0; i < ipHostInfo.AddressList.Length; ++i) {
                if (ipHostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork) {
                    ip = ipHostInfo.AddressList[i];
                    //break;
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
                    string remoteIP = client.Client.RemoteEndPoint.ToString();

                    log("Connection from " + remoteIP , LogStatus.Process);
                    Task t = acceptConnection(client);
                    await t;
                } catch (Exception ex) {
                    log(ex.Message, LogStatus.Error);
                }
            }
        }
        private async Task acceptConnection(TcpClient client) {
            // NetworkStream stream = null;
            // SslStream sstream = null;
            dynamic stream = null;
            string remoteIPStr = "{" + client.Client.RemoteEndPoint.ToString() + "} ";

            if (Options.GetModule("Startup").certificateExists) {
                stream = new SslStream(client.GetStream());
                //log("HTTPS mode.", LogStatus.Process);
            } else {
                stream = client.GetStream();
                //log("HTTP mode.", LogStatus.Process);
            }

            while (true) {
                if (stream != null) {
                    if (!stream.CanRead) break;
                    StreamReader reader = new StreamReader(stream);

                    if (!reader.EndOfStream) {
                        string request;
                        
                        try {
                            request = await reader.ReadLineAsync();
                            log(remoteIPStr + request, LogStatus.Process);
                        } catch (System.Exception) {
                            break;
                        }

                        if (!String.IsNullOrEmpty(request)) {
                            using (StreamWriter writer = new StreamWriter(stream)) {
                                string response = Handle(request, writer);

                                if (response != null) {
                                    log(remoteIPStr + "Sent: [\n" + response + "\n]", LogStatus.Success);
                                    writer.Write(response);
                                } else {
                                    log(remoteIPStr + "Invalid request", LogStatus.Error);
                                    writer.Write(WebHandle.WebResponseStringify(new WebHandle.WebResponse(null, 404, null)));
                                }
                                writer.Flush();
                            }
                        } else {
                            break;
                        }
                    } else {
                        break;
                    }
                } else {
                    log(remoteIPStr + "Failed to initialize reading stream.", LogStatus.Error);
                    break;
                }
            }
            client.Close();
            log(remoteIPStr + "Stream end.", LogStatus.Process);
        }
        private String Handle(string request, StreamWriter writer) {
            string method = request.Split(' ')[0];
            string resource = request.Split(' ')[1];

            string instruction;
            string[] tmpSpl;
            Dictionary<string, string> par;

            string responseString;

            if (!resource.Contains("?")) {
                instruction = resource;
                tmpSpl = new string[] {};
            } else {
                instruction = resource.Split('?', 2)[0];
                tmpSpl = resource.Split('?', 2)[1].Split('&');
            }

            par = new Dictionary<string, string>();
            for (int x = 0; x < tmpSpl.Length; x++) {
                par.Add(tmpSpl[x].Split('=', 2)[0], HttpUtility.HtmlDecode(tmpSpl[x].Split('=', 2)[1]));
            }

            switch (method) {
                case "GET": case "POST":
                    if (instruction == "/favicon.ico") { 
                        //writer.Write("HTTP/1.1 404 NOT FOUND\r\n\r\n");
                        return null;
                    }

                    

                    responseString = WebHandle.WebResponseStringify(new WebHandle.WebResponse(instruction, 200, new WebHandle.WebResponse("instruction", 0, par)));

                    //writer.Write("HTTP/1.1 200 OK\r\n\r\n");
                    return responseString;
                default:
                    //writer.Write("HTTP/1.1 404 NOT FOUND\r\n\r\n");
                    return null;
            }
        }
    #endregion
    }

}