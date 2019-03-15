using Newtonsoft.Json;

namespace Artemisa {
    public static class WebHandle {
        public interface iWebRequest {

        }
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
        public static string WebResponseStringify(WebResponse t) {
            string header = 
                "HTTP/1.1 " + t.Code + "\r\n" +
                    "Connection: close" + "\r\n\r\n";

            return header + JsonConvert.SerializeObject(t, Formatting.Indented);
        }
    }
}