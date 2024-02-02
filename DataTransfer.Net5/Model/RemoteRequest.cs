using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace msa.Data.Transfer.Model
{
    /// <summary>
    /// <para>Model und Logik zur Durchführung von Datenermittlungen auf Textbasis von Fremdquellen etwa, Dateien/ URLs</para>
    /// <para>Format param1:paramVal1:::param2:paramVal2:::URL</para>
    /// <para>Je nach Protokoll sind verschiedene Parameter erlaubt
    /// <list type="bullet">
    ///     <item>file
    ///         <list type="bullet">
    ///             <item>file-server : Der Server auf dem sich der Share befindet - Default localhost</item>
    ///         </list>
    ///     </item>
    ///     <item>file csv
    ///         <list type="bullet">
    ///             <item>delimiter : Trennzeichen für Token im CSV - Default ConnectionString delimiter-Wert für CSV-Connector oder wenn da nicht angegeben dann ; </item>
    ///             <item>enclose : Klammerung für Token im CSV - Default ConnectionString enclose-Wert für CSV-Connector oder wenn da nicht angegeben dann " </item>
    ///         </list>
    ///     </item>
    ///     <item>file xml
    ///         <list type="bullet">
    ///             <item>xpath : XPath Ausdruck für das einzulesende XML-Array - ohne Angabe wird das array von Elementen unter dem root-Element verwendet </item>
    ///         </list>
    ///     </item>
    ///      <item>file json
    ///         <list type="bullet">
    ///             <item>jsonpath : jsonpath Ausdruck für das einzulesende JSON-Array - ohne Angabe wird angenommen das die oberste Ebene des JSON ein Array ist </item>
    ///         </list>
    ///     </item>
    ///     <item>http/https
    ///         <list type="bullet">
    ///             <item>http-method : HTTP-Methode GET, POST, PUT, DELETE - Default GET</item>
    ///             <item>http-postdata : Daten die bei einem POST, PUT oder DELETE gesendet werden sollen </item>
    ///             <item>http-timeout : Timeout für die Anfrage in Sekunden - Default 120 </item>
    ///             <item>http-header : Mehrfach-Verwendung möglich, einmal pro HTTP-Header - Header im Format Headername:Headerwert  </item>
    ///             <item>http-SecProtType : Security Protokollversion für HTTPs z.B. Tls12 (Gültig sind die Enum-Werte von System.Net.SecurityProtocolType)</item>
    ///         </list>
    ///     </item>
    /// </list>
    /// </para>
    /// </summary>
    public class RemoteRequest
    {
        /// <summary>Liste von Konfigurationsparametern</summary>
        public Dictionary<String, List<String>> parameter { get; set; } = new Dictionary<string, List<string>>();
        /// <summary>URL welche aufgelöst werden soll</summary>
        public string url { get; set; }
        /// <summary>Zu verwendendes Protokoll für die Auflösung</summary>
        public string protocol { get; set; }

        /// <summary>Cookies für Webrequests um etwa Authentifizierungsangaben nachträglich in Folgerequests weiter zu verwenden</summary>
        public List<Cookie> cookies = new List<Cookie>();

        /// <summary> Erstellt einen neuen RemoteRequest aus dem übergebenen Info-String </summary>
        /// <param name="inputformat">Info String für Initialisierung des RemoteRequests <para></para>
        /// Format param1:paramVal1:::param2:paramVal2:::URL</param>
        public RemoteRequest(string inputformat)
        {
            // METHOD=XXX:::HEADER=XXX:::Auth=XXX:::Message=XXX:::ANY:::protocol://URL
            // METHOD=GET:::http://google.de
            // METHOD=POST:::Message=123:::http://google.de
            // METHOD=GET:::Header=key1=val1;key2=val2:::http://google.de
            Match data = Regex.Match(inputformat, @"([\w-]+=.*:::)*([a-zA-Z]+://)?(.+)");

            if (!data.Success) throw new ArgumentException($"Remote-Specification '{inputformat}' is malformed");
            if (data.Groups[2].Value == "") // Sicherheit, wenn kein Protokoll angegeben ist, gehe davon aus das es ein Netzwerkpfad ist
                this.protocol = "file";
            else
                this.protocol = data.Groups[2].Value.Replace("://", "");

            this.url = this.protocol + "://" + data.Groups[3].Value;

            // Rückwärts auflösen um : selbst als ende eines Parameters zuzulassen z.B. Param=:::: -> Param = :
            string lParam = data.Groups[1].Value;
            List<String> pairs = new List<string>();
            while( lParam.LastIndexOf(":::") != -1)
            {
                string pair = lParam.Substring(lParam.LastIndexOf(":::") + 3);
                if (pair != "") pairs.Add(pair);
                lParam = lParam.Substring(0, lParam.LastIndexOf(":::"));
            }
            if (lParam != "") pairs.Add(lParam);

            foreach (string pair in pairs)
            {
                int firstEqual = pair.IndexOf("=");
                if (firstEqual == -1 || firstEqual == 0) throw new ArgumentException($"Remote-Specification '{inputformat}' is malformed. Parameter-Value is malformed {pair}");

                string paramName = pair.Substring(0, firstEqual).ToLower().Trim();
                string paramValue = pair.Substring(firstEqual+1 ).Trim();
                if (parameter.ContainsKey(paramName))
                {
                    parameter[paramName].Add(paramValue);
                }
                else
                {
                    List<String> paramValues = new List<string>() { paramValue };
                    parameter.Add(paramName, paramValues);
                }
                
            }
        }


        /// <summary>Ausgabe von Informationen für den RemoteRequest</summary>
        /// <returns>Informationstext zum Request</returns>
        public override string ToString()
        {
            return $"Protocol: {this.protocol}\nUrl: {this.url}\nParameters:\n{String.Join("\n", parameter.Select((kv) => kv.Key + " = " + kv.Value ))}";
        }


        /// <summary>
        /// Führt den Request aus und gibt die erhaltenen Daten als Klartext zurück
        /// </summary>
        /// <returns>Das Ergebnis des Requests</returns>
        public async Task<String> resolveRequest()
        {
            switch (protocol)
            {
                case "http":
                case "https":
                    return await this.resolveHttp();
                case "file":
                    return await this.resolveFile();
                default:
                    throw new ArgumentException("Protocol " + this.protocol + " is not supported for remote specification");
            }
        }

        /// <summary>
        /// Löst einen File-Request auf (lokal oder remote) und gibt den Dateiinhalt zurück
        /// </summary>
        /// <returns>Der Inhalt der Datei als Text (Standardkodierung)</returns>
        protected async Task<String> resolveFile()
        {
            string server = (this.parameter.ContainsKey("file-server") ? this.parameter["file-server"][0].ToLower() : "localhost");
            //  C:\ wird zu \\localhost\c$\
            string correctURL = this.url.Replace("file:", "").Replace('/', '\\').Replace("%20", " ");
            Match m = Regex.Match(correctURL, "([a-zA-Z0-9]+):");
            if (m.Success)
            {
                correctURL = correctURL.Replace(m.Groups[0].Value, server + "\\" + m.Groups[1].Value + "$");
            }
            using (StreamReader reader = new StreamReader(correctURL))
            {
                return await reader.ReadToEndAsync();
            }
        }

        /// <summary>
        /// Löst eine HTTP/REST-Anfrage auf
        /// </summary>
        /// <returns>Das Ergebnis der HTTP/REST-Anfrage als String</returns>
        protected async Task<String> resolveHttp()
        {
            CookieContainer cookieContainer = new CookieContainer();
            using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookieContainer })
            using (HttpClient client = new HttpClient(handler))
            {
                // Protokoll-Variante für HTTPS
                if (this.parameter.ContainsKey("http-SecProtType"))
                {
                    System.Net.SecurityProtocolType secType = System.Net.SecurityProtocolType.Tls12;
                    if (Enum.TryParse<System.Net.SecurityProtocolType>(this.parameter["http-SecProtType"][0], out secType))
                    {
                        System.Net.ServicePointManager.SecurityProtocol = secType;
                    }
                    else
                    {
                        throw new ArgumentException($"Parameter http-SecProtType of value {this.parameter["http - SecProtType"][0]} is not a valid member of System.Net.SecurityProtocolType");
                    }
                }

                string method = (this.parameter.ContainsKey("http-method") ? this.parameter["http-method"][0].ToLower() : "get");
                string postData = (this.parameter.ContainsKey("http-postdata") ? this.parameter["http-postdata"][0] : "");
                client.Timeout = TimeSpan.FromSeconds((this.parameter.ContainsKey("http-timeout") ? Convert.ToInt32(this.parameter["http-timeout"][0]) : 1200));

                // Header einlesen/konfigurieren
                if (this.parameter.ContainsKey("http-header"))
                {
                    foreach (string header in this.parameter["http-header"])
                    {
                        int firstEqual = header.IndexOf(":");
                        if (firstEqual == -1 || firstEqual == 0) throw new ArgumentException($"Remote-Specification for {this.url} is malformed. Parameter-Value http-header is malformed {header}");

                        string paramName = header.Substring(0, firstEqual).Trim();
                        string paramValue = header.Substring(firstEqual + 1).Trim();

                        if (client.DefaultRequestHeaders.Contains(paramName))
                        {
                            client.DefaultRequestHeaders.Remove(paramName);
                        }
                        client.DefaultRequestHeaders.Add(paramName, paramValue);
                    }
                }

                // Add Cookies if needed
                foreach (Cookie c in this.cookies)
                {
                    cookieContainer.Add(c);
                }

                // Request ausführen
                HttpResponseMessage response = null;
                switch (method)
                {
                    case "get": return await client.GetStringAsync(this.url);
                    case "post":
                        response = await client.PostAsync(this.url, new StringContent(postData));
                        return await response.Content.ReadAsStringAsync();
                    case "put":
                        response = await client.PutAsync(this.url, new StringContent(postData));
                        return await response.Content.ReadAsStringAsync();
                    case "delete":
                        response = await client.DeleteAsync(this.url);
                        return await response.Content.ReadAsStringAsync();
                    default:
                        throw new ArgumentException($"Remote - Specification for {this.url} is malformed. Parameter - Value http-method is unknown {method}");
                }
            }
        }
    }
}
