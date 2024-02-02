using System;
using System.IO;
using System.Net.Http;
using System.ServiceModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using msa.Lotus;

namespace UnitTest
{
	[TestClass]
	public class UnitTest_LotusInterop
	{
		public static LotusSessionAuth auth = new LotusSessionAuth();

		[TestInitialize]
		public void init()
        {
			System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
		}

		[TestMethod]
		public void testCookieSSL()
		{
			System.Net.Cookie c = auth.getLotusSessionAuthCookie("dominoserver", "user", "password", "https");
			Console.WriteLine("{0} = {1}", c.Name, c.Value);
		}

		[TestMethod]
		public void testCookieSingleServer()
		{
			System.Net.Cookie c = auth.getLotusSessionAuthCookie("dominoserver", "user", "password", "https");
			Console.WriteLine("{0} = {1}", c.Name, c.Value);
		}

		[TestMethod]
		public void testCookieFail()
		{
			try
			{
				System.Net.Cookie c = auth.getLotusSessionAuthCookie("dominoserver", "user", "wrong Password", "https");
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
			
		}

		[TestMethod]
		public void testHttpClient()
        {
            System.Net.Cookie c = auth.getLotusSessionAuthCookie("dominoserver", "user", "password", "https");
            System.Net.CookieContainer cookieContainer = new System.Net.CookieContainer();
			cookieContainer.Add(c);
			HttpClient client = new HttpClient(new HttpClientHandler() { CookieContainer = cookieContainer });
						

			HttpResponseMessage result = client.GetAsync("https://dominoserver/names.nsf/($VimGroups)?OpenView&count=10").Result;
			Console.WriteLine(result.Content.ReadAsStringAsync().Result);
		}


		[TestMethod]
		public void testRequest()
		{
            System.Net.Cookie c = auth.getLotusSessionAuthCookie("dominoserver", "user", "password", "https");

            System.Net.WebClient webClient = new System.Net.WebClient();
			webClient.Headers.Add(System.Net.HttpRequestHeader.Cookie, c.ToString());

			string requestUrl = "http://dominoserver/names.nsf/($VimGroups)?OpenView&count=10";
			using (StreamReader reader = new StreamReader(webClient.OpenRead(requestUrl)))
			{
				Console.WriteLine(reader.ReadToEnd());
			}

			Console.WriteLine("\n******************************************\n");
			requestUrl = "http://dominoserver/names.nsf/($VimGroups)?OpenView&count=10";
			using (StreamReader reader = new StreamReader(webClient.OpenRead(requestUrl)))
			{
				Console.WriteLine(reader.ReadToEnd());
			}
		}

	}
}