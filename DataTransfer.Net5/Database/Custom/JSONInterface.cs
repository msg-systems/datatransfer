using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using msa.Data.Transfer.Model;
using msa.Data.Transfer.SQL;
using msa.Logging;
using Newtonsoft.Json.Linq;

namespace msa.Data.Transfer.Database.Custom
{

    /// <summary>
    /// Spezifische Implementierung für einen JSON-Export. <br/>
    /// Als Treibertyp ist 'Custom.Export.JSON' anzugeben - Ein ConnectionString ist nicht nötig <br/>
    /// Transaktionen müssen deaktiviert sein - disableTransaction="true" <br/>
    /// Der targetTabellenname eines TableJobs ist der Dateiname der json-Datei inklusive Pfad - Der Pfad muss bereits existieren
    /// <br/> <br/>
    /// Nähere Informationen zu Connection Strings und FROM-Parametern sind unter RemoteRequest beschrieben.
    /// </summary>
    /// <see cref="msa.Data.Transfer.Model.RemoteRequest"/>
    public class JSONInterface : CustomInterfaceBase
	{

		/// <summary>
		/// Erzeugt einen neuen CSV-Export-Custom-Provider
		/// </summary>
		/// <param name="conString">Der ConnectionString für die Verbindung ( Passwörter evt. DPAPI-verschlüsselt)</param>
		/// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
		public JSONInterface(string conString, Logger logger = null) : base(conString, logger)
		{
			this.logSubject = String.Format("{0} > {1}: ", "Custom.JSON", this.conString);
			this.adoDriver = "Custom.JSON";
			this.dbTransaction = null;
			this.isTransactional = false;

			// Parsing aktivieren und . : \\ als Teil von Identifiern erlauben (für Protokolle und Aliase)
			this.parseFrom = true;
			this.parseAdditonalAllowedIdentifiers.AddRange(new char[] { '.', ':', '\\' });
			this.fillFromSQLParseTree += JSONInterface_fillFromSQLParseTree;

			this.updateBatchSize = 1;
			this.initFillContext += JSONInterface_initFillContext;
			this.commitFillContext += JSONInterface_commitFillContext;
			this.insertHandler += JSONInterface_insertHandler;
		}

		private StringBuilder result;
		bool firstRecord = true;

		/// <summary>
		/// <see cref="CustomInterfaceBase.fillFromSQLParseTree"/> 
		/// </summary>
		private async Task<Dictionary<string, DataTable>> JSONInterface_fillFromSQLParseTree(SqlParseTree parseTree)
		{
			// Zum Textparsen/ausgeben von Datentypen verwende en-US und nicht die Culture des ausführenden Rechners!
			System.Threading.Thread.CurrentThread.CurrentCulture = Program.defaultCulture;

			Dictionary<String, DataTable> files = new Dictionary<string, DataTable>();

			// gehe alle Tabellen durch
			foreach (SqlTableExpression tab in parseTree.tables.Values)
			{
				try
				{
					this.logger.logInfo(this.logSubject + "Load data from " + tab.expression);

					RemoteRequest request = new RemoteRequest(tab.expression); // JSON fragt immer Daten mittels Protokoll ab und sei es file://
					string content = await request.resolveRequest();

					// Ausgabetabelle erzeugen
					DataTable partial_dt = tab.createDataTable();
					List<SqlSelectExpression> directCols = tab.getDirectColReferences();

                    // Load JSON (erwarte Array mit [ 
                    JArray parsedContent = null;
                    if (request.parameter.ContainsKey("jsonpath"))
                    {
                        JToken jObj = JToken.Parse(content);
                        JToken jToken = jObj.SelectToken(request.parameter["jsonpath"][0]);
                        if (!(jToken is JArray)) { throw new ArgumentException("Selected jsonPath does not contain an array"); }
                        parsedContent = (JArray)jToken;
                    }
                    else
                    {
                        if (content.Trim().StartsWith("["))
                        {
                            parsedContent = JArray.Parse(content);
                        }
                        else
                        {
                            throw new ArgumentException("No Array in json-file");
                        }
                    }

                    HashSet<String> typeIsSet = new HashSet<string>();
					foreach (SqlSelectExpression selEx in directCols)
					{
						typeIsSet.Add(selEx.colName);
					}

					// 1. Schleife Datentyp festlegen
					foreach (JObject jobj in parsedContent)
					{
						if (typeIsSet.Count == 0) break;

						IEnumerable<JProperty> props = jobj.Properties();
						foreach (JProperty field in props)
						{
							if (!typeIsSet.Contains(field.Name)) continue;
							if (field.Value == null) continue;

							// date -> datum
							// JSON date
							// parseTree date as datum
							int colIndex = partial_dt.Columns.IndexOf(field.Name);
							if (colIndex == -1)
							{
								SqlSelectExpression selEx = directCols.FirstOrDefault((se) => se.colName == field.Name);
								colIndex = partial_dt.Columns.IndexOf(selEx.colNameResult);
							}
							if (colIndex != -1)
							{
								if (field.Value != null)
								{
									switch (field.Value.Type)
									{
										case JTokenType.String:
										case JTokenType.Uri:
											partial_dt.Columns[colIndex].DataType = typeof(String);
											break;
										case JTokenType.Date:
											partial_dt.Columns[colIndex].DataType = typeof(DateTime);
											break;
										case JTokenType.Integer:
										case JTokenType.Float:
											partial_dt.Columns[colIndex].DataType = typeof(Double);
											break;
										case JTokenType.Boolean:
											partial_dt.Columns[colIndex].DataType = typeof(Boolean);
											break;
									}
									typeIsSet.Remove(field.Name);
								}
							}
						}
					}

					// 2. Schleife Werte übernehmen
					foreach (JObject jobj in parsedContent)
					{
						try
						{
							DataRow dr = partial_dt.NewRow();
							foreach (SqlSelectExpression selEx in directCols)
							{
								object val = (jobj[selEx.colName] as JValue)?.Value;
								if (val == null)
									dr[selEx.colNameResult] = DBNull.Value;
								else
									dr[selEx.colNameResult] = val;
							}
							partial_dt.Rows.Add(dr);
						}
						catch (Exception e)
						{
							this.logger.logError(this.logSubject + "Error adding record " + jobj.ToString() + " : " + e.ToString());
							throw;
						}
					}

					files.Add(tab.alias, partial_dt);
					this.logger.logInfo(this.logSubject + "Load data from " + tab.expression + " done");
				}
				catch (Exception e)
				{
					this.logger.logError(this.logSubject + "Error loading table '" + tab.expression + "' : " + e.ToString());
					throw;
				}
			}

			return files;

		}


        /// <summary>
        /// <see cref="CustomInterfaceBase.initFillContext"/> 
        /// </summary>
        /// <param name="dt">siehe Definition CustomInterfaceBase</param>
        /// <param name="columnMap">siehe Definition CustomInterfaceBase</param>
        /// <returns>siehe Definition CustomInterfaceBase</returns>
        protected async Task JSONInterface_initFillContext(DataTable dt, TransferTableColumnList columnMap)
		{
			this.logger.logInfo(this.logSubject + "Check path '" + dt.TableName + "'");
			string path = Path.GetDirectoryName(dt.TableName);
			if (!Directory.Exists(path)) throw new ArgumentException(String.Format("Path {0} does not exist", path));
			result = new StringBuilder();
			result.AppendLine("{ results: [");
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
        protected async Task JSONInterface_insertHandler(DataTable dt, DataRow dr, TransferTableColumnList columnMap)
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
			result.AppendLine(this.transformToJsonValue(dr));
			await Task.Delay(0);
		}


        /// <summary>
        /// <see cref="CustomInterfaceBase.commitFillContext"/> 
        /// </summary>
        /// <param name="dt">siehe Definition CustomInterfaceBase</param>
        /// <returns>siehe Definition CustomInterfaceBase</returns>
        protected async Task JSONInterface_commitFillContext(DataTable dt)
		{
			result.AppendLine("]}");
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
		public string transformToJsonValue(string input)
		{
			return "\"" + input.Replace("\"", "\\\"") + "\"";
		}

		/// <summary> Transformiert die Daten einer Datentabelle in CSV-Zeilen </summary>
		/// <param name="input">Eine DataTable</param>
		/// <returns>CSV-kodierte Daten</returns>
		public string transformToJsonValue(DataRow input)
		{
			// Zum Textparsen/ausgeben von Datentypen verwende en-US und nicht die Culture des ausführenden Rechners!
			System.Threading.Thread.CurrentThread.CurrentCulture = Program.defaultCulture;
			int max = input.Table.Columns.Count;
			StringBuilder result = new StringBuilder();
			result.Append("{");
			for (int i = 0; i < max; i++)
			{
				if (i > 0) result.Append(", ");
				result.Append(input.Table.Columns[i].ColumnName + ": ");
				if (input.Table.Columns[i].DataType == typeof(string) || input.Table.Columns[i].DataType == typeof(DateTime) )
				{
					result.Append(this.transformToJsonValue(input[i].ToString()));
				}
				else
				{
					result.Append(input[i].ToString());
				}
			}
			result.Append("}");

			return result.ToString();
		}

	}
}
