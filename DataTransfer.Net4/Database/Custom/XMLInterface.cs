using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using msa.Data.Transfer.Model;
using msa.Data.Transfer.SQL;
using msa.Logging;
using msa.Lotus;

namespace msa.Data.Transfer.Database.Custom
{
	/// <summary>
	/// Spezifische Implementierung für einen JSON-Export. <br/>
	/// Als Treibertyp ist 'Custom.XML' anzugeben - Ein ConnectionString ist nicht nötig <br/>
	/// Transaktionen müssen deaktiviert sein - disableTransaction="true" <br/>
	/// Der targetTabellenname eines TableJobs ist der Dateiname der json-Datei inklusive Pfad - Der Pfad muss bereits existieren
	/// <br/> <br/>
	/// Nähere Informationen zu Connection Strings und FROM-Parametern sind unter RemoteRequest beschrieben.
	/// <br/><br/>
	/// Variante Notes-Authentifizierung: Wenn man im connectionString AuthType=LotusNotes;Server=notesserverURL;User=support;Password=geheim;Protocol=https;CookieName=SessionCookie angibt, kann man sich gegen Notes authentifizieren <br/>
	/// <list type="bullet">
	///		<item>AuthType: Gibt aktuell nur LotusNotes um ein Session-Cookie von Notes Domino zu ermitteln</item>
	///		<item>Server: Pflicht - Domino web servername</item>
	///		<item>User: Pflicht - Notes-Webusername</item>
	///		<item>Password: Pflicht - Notes Webuser Passwort</item>
	///		<item>Protocol: Optional - Im Standard https. Gültig http/https</item>
	///		<item>CookieName: Optional - Im Standard DomSessAuthId - Cookiename welches bei der Auth geladen werden soll</item>
	/// </list>
	/// </summary>
	/// <see cref="msa.Data.Transfer.Model.RemoteRequest"/>
	public class XMLInterface : CustomInterfaceBase
	{

		Cookie authCookie = null;
		const int notesPageSize = 2000;

		/// <summary>
		/// Erzeugt einen neuen CSV-Export-Custom-Provider
		/// </summary>
		/// <param name="conString">Der ConnectionString für die Verbindung ( Passwörter evt. DPAPI-verschlüsselt)</param>
		/// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
		public XMLInterface(string conString, Logger logger = null) : base(conString, logger)
		{
			this.logSubject = String.Format("{0} > {1}: ", "Custom.XML", this.conString);
			this.adoDriver = "Custom.XML";
			this.dbTransaction = null;
			this.isTransactional = false;

			// Parsing aktivieren und . : \\ als Teil von Identifiern erlauben (für Protokolle und Aliase)
			this.parseFrom = true;
			this.parseAdditonalAllowedIdentifiers.AddRange(new char[] { '.', ':', '\\' });
			this.fillFromSQLParseTree += XMLInterface_fillFromSQLParseTree;

			this.updateBatchSize = 1;
			this.initFillContext += XMLInterface_initFillContext;
			this.commitFillContext += XMLInterface_commitFillContext;
			this.insertHandler += XMLInterface_insertHandler;


		}

		private StringBuilder result;
		bool firstRecord = true;

		/// <summary>Führt Connect-Logik für Abfrage durch</summary>
		/// <returns>Nichts</returns>
        public override async Task connect()
        {
			
			DbConnectionStringBuilder builder = new DbConnectionStringBuilder();
			builder.ConnectionString = this.getDecryptedConString(this.conString);
			// Notes-Anmeldung
			if (builder.ContainsKey("AuthType") && builder["AuthType"].ToString() == "LotusNotes")
			{
				this.logger.logInfo(this.logSubject + "Connecting");
				string server = builder["Server"].ToString();
				string user = builder["User"].ToString();
				string password = builder["Password"].ToString();
				string protocol = "https";
				if (builder.ContainsKey("Protocol")) protocol = builder["Protocol"].ToString();
				if (String.IsNullOrWhiteSpace(server)) throw new ArgumentException("Server param is needed for SpecialAuth Notes");
				if (String.IsNullOrWhiteSpace(user)) throw new ArgumentException("user param is needed for SpecialAuth Notes");
				if (String.IsNullOrWhiteSpace(password)) throw new ArgumentException("password param is needed for SpecialAuth Notes");

				LotusSessionAuth auth = new LotusSessionAuth();
				if (builder.ContainsKey("CookieName"))
					this.authCookie = auth.getLotusSessionAuthCookie(server, user, password, protocol, builder["CookieName"].ToString());
				else
					this.authCookie = auth.getLotusSessionAuthCookie(server, user, password, protocol);
				this.logger.logInfo(this.logSubject + "Connected");
			}
			
			await Task.Delay(0);
        }


		/// <summary>
		/// <see cref="CustomInterfaceBase.fillFromSQLParseTree"/> 
		/// </summary>
		/// <param name="parseTree">siehe Definition CustomInterfaceBase</param>
		/// <returns>siehe Definition CustomInterfaceBase</returns>
		private async Task<Dictionary<string, DataTable>> XMLInterface_fillFromSQLParseTree(SqlParseTree parseTree)
		{	
			Dictionary<String, DataTable> files = new Dictionary<string, DataTable>();

			// gehe alle Tabellen durch
			foreach (SqlTableExpression tab in parseTree.tables.Values)
			{
				// Ausgabetabelle erzeugen
				this.logger.logInfo(this.logSubject + "Load data from " + tab.expression);
				try
				{
					DataTable partial_dt = tab.createDataTable();
					List<SqlSelectExpression> directCols = tab.getDirectColReferences();

					// XML kennt nur String (ohne Annotations)
					foreach (DataColumn dc in partial_dt.Columns)
					{
						dc.DataType = typeof(Object);
					}

					RemoteRequest request = new RemoteRequest(tab.expression); // JSON fragt immer Daten mittels Protokoll ab und sei es file://
					if (authCookie != null) // Hook für Auth-Cookies die durch Connect reinkommen
					{
						request.cookies.Add(authCookie);
					}

					// Daten abfragen
					// Wenn Notes - Dann Paging, da man sonst Out Of Memory-Exceptions seitens Domino HTTP bekommt
					if (tab.expression.ToLower().EndsWith("?readviewentries"))
					{

						int curEntry = 1;
						string baseUrl = request.url;

						request.url = baseUrl + "&start=" + curEntry.ToString() + "&count=" + notesPageSize;
						string content = await request.resolveRequest();

						while (loadDataIntoTable(content, partial_dt, directCols, tab))
						{
							curEntry += notesPageSize;
							request.url = baseUrl + "&start=" + curEntry + "&count=" + notesPageSize;
							content = await request.resolveRequest();
						}
					}
					else
					{
						string content = await request.resolveRequest();
						if (request.parameter.ContainsKey("xpath"))
						{
							XmlDocument doc = new XmlDocument();
							doc.LoadXml(content);
							XmlNode root = doc.DocumentElement;
							XmlNode node = root.SelectSingleNode(request.parameter["xpath"][0]);
							content = node.InnerXml;
						}
						loadDataIntoTable(content, partial_dt, directCols, tab);
					}

					files.Add(tab.alias, partial_dt);
					this.logger.logInfo(this.logSubject + "Load data from " + tab.expression + " done");
				}
				catch(Exception e)
                {
					this.logger.logError(this.logSubject + "Error loading table '" + tab.expression + "' : " + e.ToString());
					throw;
				}
			}

			return files;

		}

		/// <summary>
		/// Lädt den übergebenen XML-Text in die Quelltabelle ein
		/// </summary>
		/// <param name="content">XML-Text mit Daten</param>
		/// <param name="tab">Die Quelltabelle die befüllt werden soll</param>
		/// <param name="directCols">Die Spalten die befüllt werden sollen, laut dem SQL-ParseTree</param>
		/// <param name="sqlTab">Infos zur geparsten SQL-Tabelle</param>
		/// <returns>true wenn noch mehr Daten vorhanden sind (Paging), false wenn alles geladen wurde</returns>
		public bool loadDataIntoTable(string content, DataTable tab, List<SqlSelectExpression> directCols, SqlTableExpression sqlTab)
        {
			// Zum Textparsen von Datentypen verwende en-US und nicht die Culture des ausführenden Rechners!
			System.Threading.Thread.CurrentThread.CurrentCulture = Program.defaultCulture;

			XDocument parsedContent = XDocument.Parse(content);
			// Lotus Notes
			if (parsedContent.Element("viewentries") != null)
			{
				IEnumerable<XElement> entries = parsedContent.Descendants("viewentry");

				// Prüfe Schema
				if (tab.Rows.Count == 0)
				{

					HashSet<String> toCheck = new HashSet<string>();
					foreach (DataColumn dc in tab.Columns)
					{
						toCheck.Add(dc.ColumnName);
					}
					foreach (XElement el in entries)
					{
						if (toCheck.Count == 0) break;

						foreach (SqlSelectExpression selEx in directCols)
						{
							if (toCheck.Contains(selEx.colNameResult))
							{
								XElement valEl = el.Elements("entrydata").Where((XElement ed) => ed.Attribute("name").Value.ToLower() == selEx.colName.ToLower()).FirstOrDefault();
								// Notes Views haben ein festes Schema, weswegen die Spalten immer oder nie vorkommen
								if (valEl == null) throw new ArgumentException("Column " + selEx.colName + " does not exist in Notes View " + sqlTab.expression);
								XElement contentEl = ((XElement)valEl.FirstNode);
								if (contentEl == null)
								{
									continue;
								}
								if (contentEl.Name.ToString().EndsWith("list")) contentEl = (XElement)contentEl.FirstNode;
								string val = contentEl.Value;

								if (contentEl.Name == "datetime")
								{
									tab.Columns[selEx.colNameResult].DataType = typeof(DateTime);
									toCheck.Remove(selEx.colNameResult);
								}
								else if (contentEl.Name == "number")
								{
									tab.Columns[selEx.colNameResult].DataType = typeof(Double);
									toCheck.Remove(selEx.colNameResult);
								}
								else if (contentEl.Name == "text" && val != "")
								{
									tab.Columns[selEx.colNameResult].DataType = typeof(String);
									toCheck.Remove(selEx.colNameResult);
								}
							}
						}
					}
				}
				//Parallel.ForEach(entries, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, (XElement el) =>
				foreach (XElement el in entries)
				{
					try
					{
						DataRow dr = tab.NewRow();
						foreach (SqlSelectExpression selEx in directCols)
						{
							XElement valEl = el.Elements("entrydata").Where((XElement ed) => ed.Attribute("name").Value.ToLower() == selEx.colName.ToLower()).FirstOrDefault();
							// Notes Views haben ein festes Schema, weswegen die Spalten immer oder nie vorkommen
							if (valEl == null)
							{
								this.logger.logError(this.logSubject + "Column " + selEx.colName + " does not exist in " + el.ToString());
								throw new ArgumentException("Column " + selEx.colName + " does not exist in Notes View " + sqlTab.expression);
							}
							XElement contentEl = ((XElement)valEl.FirstNode);
							if (contentEl == null)
							{
								dr[selEx.colNameResult] = DBNull.Value;
								continue;
							}
							if (contentEl.Name.ToString().EndsWith("list")) contentEl = (XElement)contentEl.FirstNode;
							string val = contentEl.Value; // Element("text")
							if (contentEl.Name == "datetime")
							{
								if (val != "")
								{
									// 20030727T000000,00+02
									if (val.Length == 8)
									{

										//if (tab.Columns[selEx.colNameResult].DataType == typeof(String))
										//{
										dr[selEx.colNameResult] = DateTime.ParseExact(val,
										"yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None);
										//.ToString("yyyy-MM-dd"); ;
										/*}
										else
										{ 
											dr[selEx.colNameResult] = DateTime.ParseExact(val,
											"yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None);
										}*/
									}
									else
									{
										//if (tab.Columns[selEx.colNameResult].DataType == typeof(String))
										//{
										dr[selEx.colNameResult] = DateTime.ParseExact(val.Substring(0, 15),
										"yyyyMMddTHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);
										//.ToString("yyyy-MM-dd HH:mm:ss");
										/*}
										else
										{
											dr[selEx.colNameResult] = DateTime.ParseExact(val.Substring(0, 15),
											"yyyyMMddTHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);
										}*/
									}
								}
								else
								{
									dr[selEx.colNameResult] = DBNull.Value;
								}
							}
							else if (contentEl.Name == "number")
								if (val == "")
								{
									dr[selEx.colNameResult] = DBNull.Value;
								}
								else
								{
									dr[selEx.colNameResult] = Double.Parse(val);
								}
							else
							{
								if (val == "")
									dr[selEx.colNameResult] = DBNull.Value;
								else
									dr[selEx.colNameResult] = val;
							}
						}
						tab.Rows.Add(dr);
					}
					catch (Exception e)
                    {
						this.logger.logError(this.logSubject + "Error adding record " + el.ToString() + " : " + e.ToString());
						throw;
                    }
				//});
				}
				return (entries.Count() == notesPageSize);
			}
			else // Standardfall
			{
				IEnumerable<XElement> entries = parsedContent.Root.Elements();
				foreach (XElement el in entries)
				{
					try
					{
						DataRow dr = tab.NewRow();
						foreach (SqlSelectExpression selEx in directCols)
						{
							XElement valEl = el.Element(selEx.colName);
							if (valEl == null || valEl.Value == "") // null Spalten sind erlaut - da XML nur Semistrukturiert
							{
								dr[selEx.colNameResult] = DBNull.Value;
							}
							else
							{
								dr[selEx.colNameResult] = valEl.Value;
							}
						}
					}
					catch (Exception e)
					{
						this.logger.logError(this.logSubject + "Error adding record " + el.ToString() + " : " + e.ToString());
						throw;
					}
				}
				return false;
			}
		}



		/// <summary>
		/// <see cref="CustomInterfaceBase.initFillContext"/> 
		/// </summary>
		/// <param name="dt">siehe Definition CustomInterfaceBase</param>
		/// <param name="columnMap">siehe Definition CustomInterfaceBase</param>
		/// <returns>siehe Definition CustomInterfaceBase</returns>
		protected async Task XMLInterface_initFillContext(DataTable dt, TransferTableColumnList columnMap)
		{
			this.logger.logInfo(this.logSubject + "Check path '" + dt.TableName + "'");
			string path = Path.GetDirectoryName(dt.TableName);
			if (!Directory.Exists(path)) throw new ArgumentException(String.Format("Path {0} does not exist", path));
			result = new StringBuilder();
			result.AppendLine("<results>");
			firstRecord = true;
			await Task.Delay(0);
		}



		/// <summary>
		/// <see cref="CustomInterfaceBase.insertHandler"/> 
		/// </summary>
		/// <param name="dt">siehe Definition CustomInterfaceBase</param>
		/// <param name="dr">siehe Definition CustomInterfaceBase</param>
		/// <param name="columnMap">siehe Definition CustomInterfaceBase</param>
		/// <returns>siehe Definition CustomInterfaceBase</returns>
		protected async Task XMLInterface_insertHandler(DataTable dt, DataRow dr, TransferTableColumnList columnMap)
		{
			// Zum Textparsen/ausgeben von Datentypen verwende en-US und nicht die Culture des ausführenden Rechners!
			System.Threading.Thread.CurrentThread.CurrentCulture = Program.defaultCulture;
			if (firstRecord)
			{
				firstRecord = false;
			}
			else
			{
				result.AppendLine(", ");
			}
			result.AppendLine(this.transformToXmlValue(dr));
			await Task.Delay(0);
		}



		/// <summary>
		/// <see cref="CustomInterfaceBase.commitFillContext"/> 
		/// </summary>
		/// <param name="dt">siehe Definition CustomInterfaceBase</param>
		/// <returns>siehe Definition CustomInterfaceBase</returns>
		protected async Task XMLInterface_commitFillContext(DataTable dt)
		{
			result.AppendLine("</results>");
			using (StreamWriter sw = new StreamWriter(dt.TableName, false, Encoding.UTF8))
			{
				await sw.WriteLineAsync(result.ToString());
				await sw.FlushAsync();
			}
			this.logger.logInfo(this.logSubject + "File written");
		}
		

		/// <summary> Transformiert einen Einzelwert in einen CSV-Wert </summary>
		/// <param name="input">Eingabetoken</param>
		/// <returns>CSV-kodierter Wert</returns>
		public string transformToXmlValue(string input)
		{
			return System.Security.SecurityElement.Escape(input);
		}

		/// <summary> Transformiert die Daten einer Datentabelle in CSV-Zeilen </summary>
		/// <param name="input">Eine DataTable</param>
		/// <returns>CSV-kodierte Daten</returns>
		public string transformToXmlValue(DataRow input)
		{
			// Zum Textparsen/ausgeben von Datentypen verwende en-US und nicht die Culture des ausführenden Rechners!
			System.Threading.Thread.CurrentThread.CurrentCulture = Program.defaultCulture;

			int max = input.Table.Columns.Count;
			StringBuilder result = new StringBuilder();
			result.Append("<record>");
			for (int i = 0; i < max; i++)
			{
				//if (i > 0) result.Append(", ");
				result.Append("<" + input.Table.Columns[i].ColumnName + ">");
				if (input.Table.Columns[i].DataType == typeof(string) || input.Table.Columns[i].DataType == typeof(DateTime) )
				{
					result.Append(this.transformToXmlValue(input[i].ToString()));
				}
				else
				{
					result.Append(input[i].ToString());
				}
				result.Append("</" + input.Table.Columns[i].ColumnName + ">");
			}
			result.Append("</record>");

			return result.ToString();
		}

	}
}
