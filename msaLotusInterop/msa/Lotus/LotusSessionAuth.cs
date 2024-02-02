using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Web;

namespace msa.Lotus
{
	/// <summary>
	/// Erlaubt eine Verwaltung von Lotus Notes SessionCookies und bietet Hilfsmethoden an, Cookies für WCF-OperationContext zu setzen (z.B. WebService-Clients)
	/// </summary>
	public class LotusSessionAuth
	{
		/// <summary> Cache für angeforderte Cookies </summary>
		protected Dictionary<String, Cookie> cookieCache = new Dictionary<string, Cookie>();

		/// <summary> Anzahl Minuten die ein Cookie standardmäßig seine Gültigkeit bewahren soll (Default 30) </summary>
		public int defaultExpiryMinutes { get; set; }

		/// <summary>
		/// Erstellt die ID für ein Cookie
		/// </summary>
		/// <param name="server">Der Server für den das Cookie ausgestellt ist</param>
		/// <param name="username">Der Nutzername den das Cookie authentifiziert</param>
		/// <returns>Ein String im Format [server]#[username]</returns>
		protected string getCookieIdentifier(string server, string username)
		{
			return server + "#" + username;
		}

		/// <summary> Erstellt eine neue LotusSessionAuth-Verwaltung mit eigenem CookieCache </summary>
		public LotusSessionAuth()
		{
			defaultExpiryMinutes = 30;
		}

		/// <summary>
		/// Aktualisiert das ExpiryDate eines gecachten Cookies, so dass er weiterhin verwendet werden kann ohne einen neuen anzufordern.
		/// Das ExpireDate wird auf [defaultExpiryMinutes] in die Zukunft gesetzt.
		/// </summary>
		/// <param name="server">Der Server für den das Cookie ausgestellt wurde</param>
		/// <param name="username">Der Nutzer für den das Cookie angefordert wurde</param>
		public void refreshExpireDate(string server, string username)
		{
			string cookieKey = this.getCookieIdentifier(server, username);
			if (this.cookieCache.ContainsKey(cookieKey))
			{
				this.cookieCache[cookieKey].Expires = DateTime.Now.AddMinutes(this.defaultExpiryMinutes);
			}
		}


		public HttpClient getHttpClient(string server, string username, string password, string protocol = "http", string cookiename = "DomAuthSessId")
        {
			HttpClient client = new HttpClient();
			string url = protocol + "://" + server + "/names.nsf?login";


			return null;

        }

		/// <summary>
		/// Gibt ein LotusSessionAuthCookie zurück. Die Methode ist auf die Lotus Forms-Authentication ausgerichtet um sich zu authentifizieren.
		/// Ist bereits ein Cookie für den selben Server, Nutzer und Cookiename im Cache vorhanden, welches noch nicht abgelaufen ist, wird dieses zurückgegeben
		/// </summary>
		/// <param name="server">Der Server der zur Authentifizierung verwendet werden soll im Format http://[server]/... z.B. dominoserverURL.domain.de</param>
		/// <param name="username">Der Nutzername zur Authentifizierung</param>
		/// <param name="password">Das Passwort zur Authentifizierung</param>
		/// <param name="protocol">Das zu verwendende Protokoll - Default http</param>
		/// <param name="cookiename">Der zu suchende/verwendende Cookie-Name - Default DomAuthSessId</param>
		/// <returns>Ein Domino Session-Cookie zur Authentifizierung weiterer Anfragen</returns>
		public Cookie getLotusSessionAuthCookie(string server, string username, string password, string protocol = "http", string cookiename = "DomAuthSessId")
		{
			string cookieKey = this.getCookieIdentifier(server, username);
			if (this.cookieCache.ContainsKey(cookieKey))
			{
				if (!this.cookieCache[cookieKey].Expired) return this.cookieCache[cookieKey];
			}

			// Http-Anfrage erstellen
			System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
			if (protocol.ToLower() != "http" && protocol.ToLower() != "https") throw new ArgumentException("Parameter protocol: Only http and https is allowd");

			HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(protocol + "://" + server + "/names.nsf?login");
			request.Method = "POST";
			request.AllowAutoRedirect = false;
			request.ContentType = "application/x-www-form-urlencoded";
			request.CookieContainer = new CookieContainer();

			// Postdaten vorbereiten und in Request-Stream schreiben
			string post = "Username=" + HttpUtility.UrlEncode(username) + "&Password=" + HttpUtility.UrlEncode(password);
			byte[] bytes = Encoding.ASCII.GetBytes(post);
			request.ContentLength = bytes.Length;
			using (Stream streamOut = request.GetRequestStream())
			{
				streamOut.Write(bytes, 0, bytes.Length);
			}

			// Antwort holen um Authcookie auszulesen
			Cookie c = null;
			using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
			{
				foreach (Cookie tempC in response.Cookies)
				{
					// siehe dazu http://www-01.ibm.com/support/docview.wss?uid=swg27003558
					if (tempC.Name == "LtpaToken" || tempC.Name == cookiename)
					{
						c = tempC;
						break;
					}
				}
				if (c == null)
				{
					throw new Exception("Weder LtpaToken noch " + cookiename + "-Cookie wurden zurückgeliefert. Nutzer/Passwort ungültig oder DOMINO-Standard Cookienames wurden durch neuere DOMINO-Version angepasst");
				}
			}

			// Setze Expire-Date und füge es dem CookieCache hinzu
			c.Expires = DateTime.Now.AddMinutes(this.defaultExpiryMinutes);
			this.cookieCache[cookieKey] = c;

			return c;
		}

		/// <summary>
		/// Setzt im aktuellen OperationContextScope das angegebene Cookie z.B. für einen WebService.
		/// Beispiel:
		/// using(OperationContextScope scope = new OperationContextScope(myServiceClient.InnerChannel))
		/// {
		///     instance.setOpContextCookie(cookie)
		/// }
		/// </summary>
		/// <param name="cookie">Das zu setzende Cookie</param>
		/// <exception cref="System.Exception"> Tritt auf wenn die Methode außerhalb eines OperationContextScope´s verwendet wird </exception>
		/// <see cref="System.ServiceModel.OperationContextScope"/>
		public void setOpContextCookie(Cookie cookie)
		{
			// Prüfe ob ein OperationContext existiert, sonst Fehler
			if (OperationContext.Current == null) throw new Exception("setWebServiceCookie darf nur innerhalb eines OperationContextScopes verwendet werden");

			// Ermittel die HttpRequestMessageProperties
			HttpRequestMessageProperty httpRequestProperty = null;
			bool httpPropsExist = OperationContext.Current.OutgoingMessageProperties.ContainsKey(HttpRequestMessageProperty.Name);

			if (httpPropsExist)
			{
				httpRequestProperty = (HttpRequestMessageProperty)OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name];
			}
			else
			{
				httpRequestProperty = new HttpRequestMessageProperty();
			}

			// Setze das Cookie
			httpRequestProperty.Headers.Set(HttpRequestHeader.Cookie, cookie.ToString());

			// Setze die Properties falls eine neue Instanz der HttpRequestMessageProperties-Eigenschaften verwendet wurde
			if (!httpPropsExist)
			{
				OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = httpRequestProperty;
			}
		}

		/// <summary>
		/// Setzt im aktuellen OperationContextScope ein ermitteltes Session-AuthCookie
		/// </summary>
		/// <param name="server">Der Server der zur Authentifizierung verwendet werden soll im Format http://[server]/... z.B. dominoServerURL.domain.de</param>
		/// <param name="username">Der Nutzername zur Authentifizierung</param>
		/// <param name="password">Das Passwort zur Authentifizierung</param>
		/// <exception cref="System.Exception"> Tritt auf wenn die Methode außerhalb eines OperationContextScope´s verwendet wird </exception>
		/// <see cref="msa.Lotus.LotusSessionAuth"/>
		public void setOpContextSessionCookie(string server, string username, string password)
		{
			setOpContextCookie(getLotusSessionAuthCookie(server, username, password));
		}
	}
}