using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using msa.Data.Transfer.Model;
using msa.Logging;
using msa.Data.Transfer.SQL;
using DevLib.Csv;
using msa.DSL.CodeParser;

namespace msa.Data.Transfer.Database.Custom
{
	/// <summary>
	/// Spezifische Implementierung für einen CSV-Export. <br/>
	/// Als Treibertyp ist 'Custom.Export.CSV' anzugeben - Ein ConnectionString ist nicht nötig <br/>
	/// Transaktionen müssen deaktiviert sein - disableTransaction="true" <br/>
	/// Der targetTabellenname eines TableJobs ist der Dateiname der csv-Datei inklusive Pfad - Der Pfad muss bereits existieren. 
	/// <br/> <br/>
	/// Nähere Informationen zu Connection Strings und FROM-Parametern sind unter RemoteRequest beschrieben.
	/// </summary>
	/// <see cref="msa.Data.Transfer.Model.RemoteRequest"/>
	public class CSVInterface : CustomInterfaceBase
	{

		/// <summary>Trennzeichen zwischen den Tokens</summary>
		public string delimiter { get; set; } = ";";
		/// <summary>Klammerungszeichen für einen Token</summary>
		public string enclose { get; set; } = "\"";

		/// <summary>
		/// Erzeugt einen neuen CSV-Export-Custom-Provider
		/// </summary>
		/// <param name="conString">Der ConnectionString für die Verbindung ( Passwörter evt. DPAPI-verschlüsselt)</param>
		/// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
		public CSVInterface(string conString, Logger logger = null) : base(conString, logger)
		{
			this.adoDriver = "Custom.Export.CSV";

			this.isTransactional = false;
			this.updateBatchSize = 1;

			this.initFillContext += CSVInterface_initFillContext;
			this.commitFillContext += CSVInterface_commitFillContext;
			this.rollbackFillContext += CSVInterface_rollbackFillContext;
			this.insertHandler += CSVInterface_insertHandler;

			this.parseFrom = true;
			this.parseAdditonalAllowedIdentifiers.AddRange(new char[] { '.', ':', '\\' });

			this.fillFromSQLParseTree += CSVInterface_fillFromSQLParseTree;

			DbConnectionStringBuilder conStr = new DbConnectionStringBuilder(false);
			conStr.ConnectionString = conString;
			foreach( string conKey in conStr.Keys)
			{
				switch (conKey.ToLower())
				{
					case "delimiter": delimiter = conStr[conKey].ToString(); break;
					case "enclose": enclose = conStr[conKey].ToString(); break;
				}
			}
		}

		/// <summary>
		/// Liest CSV-Tabellen laut Angaben aus dem ParseTree ein und initialisiert Datentypen
		/// </summary>
		/// <param name="parseTree">Das geparste SQL mit Angaben zu Tabellen die eingelesen werden sollen</param>
		/// <returns>Die erstellten und befüllten Tabellenkonstrukte</returns>
		private async Task<Dictionary<string, DataTable>> CSVInterface_fillFromSQLParseTree(SqlParseTree parseTree)
		{

			Dictionary<String, DataTable> files = new Dictionary<string, DataTable>();

			//DataTableDSL dataTableDsl = new DataTableDSL();
			//CodeEvaluator codeEvaluator = new CodeEvaluator();

			foreach ( SqlTableExpression tab in parseTree.tables.Values )
			{
				try
				{
					RemoteRequest request = new RemoteRequest(tab.expression); // CSV fragt immer Daten mittels Protokoll ab und sei es file://

					using (CsvDocument csv = new CsvDocument())
					{
						this.logger.logInfo(this.logSubject + "Load data from " + tab.expression);
						string content = await request.resolveRequest();

						// Mit oder ohne enclose-Zeichen
						string delimiterLocal = this.delimiter;
						if (request.parameter.ContainsKey("delimiter")) delimiterLocal = request.parameter["delimiter"][0];
						string encloseLocal = this.enclose;
						if (request.parameter.ContainsKey("enclose")) encloseLocal = request.parameter["enclose"][0];

						if (String.IsNullOrWhiteSpace(this.enclose))
							csv.LoadCsv(content, true, delimiterLocal.ToCharArray()[0]);
						else
							csv.LoadCsv(content, true, delimiterLocal.ToCharArray()[0], encloseLocal.ToCharArray()[0]);

						Dictionary<string, int> colMap = new Dictionary<string, int>();

						DataTable partial_dt = tab.createDataTable();
						List<SqlSelectExpression> directCols = tab.getDirectColReferences();

						// Codereferenzen Spaltenmapping aufbauen 
						foreach (SqlSelectExpression selEx in directCols)
						{
							if (!colMap.ContainsKey(selEx.colName))
							{
								int index = csv.HeaderColumns.IndexOf(selEx.colName);
								if (index == -1) throw new ArgumentException($"Column {selEx.expression} does not exist in base table {tab.expression}");
								colMap.Add(selEx.colName, index);
							}
						}

						foreach (DataColumn dc in partial_dt.Columns)
						{
							dc.DataType = typeof(string);
						}

						// Codereferenzen auflösen
						foreach (List<string> row in csv.Table)
						{
							try
							{
								DataRow dr = partial_dt.NewRow();

								foreach (SqlSelectExpression selEx in directCols)
								{
									int index = colMap[selEx.colName];
									if (!(index < row.Count)) continue;

									dr[selEx.colNameResult] = row[index];
								}
								partial_dt.Rows.Add(dr);
							}
							catch (Exception e)
							{
								this.logger.logError(this.logSubject + "Error adding record " + String.Join(";", row) + " : " + e.ToString());
								throw;
							}
						}

						files.Add(tab.alias, partial_dt);
						this.logger.logInfo(this.logSubject + "Load data from " + tab.expression + " done");
					}
				}
				catch (Exception e)
				{
					this.logger.logError(this.logSubject + "Error loading table '" + tab.expression + "' : " + e.ToString());
					throw;
				}
			}
			await Task.Delay(0);

			return files;
			
		}

		/// <summary>Interner Streamwriter als Ausgabestrom zum Schreiben von CSV-Dateien</summary>
		private StreamWriter sw;

		/// <summary>Initialisier den Ausgabestrom und schreibe den Header raus</summary>
		/// <param name="dt">Daten die geschrieben werden sollen</param>
		/// <param name="columnMap">ColumnMapping des Schreibvorgangs</param>
		/// <returns></returns>
		private async Task CSVInterface_initFillContext(DataTable dt, TransferTableColumnList columnMap)
		{
			this.logger.logInfo(this.logSubject + "Check path '" + dt.TableName + "'");
			string path = Path.GetDirectoryName(dt.TableName);
			if (!Directory.Exists(path)) throw new ArgumentException(String.Format("Path {0} does not exist", path));
			this.sw = new StreamWriter(dt.TableName, false, Encoding.UTF8);

			if (columnMap != null)
			{
				await sw.WriteLineAsync(this.transformToCsvValue(columnMap));
			}
			else
			{
				await sw.WriteLineAsync(this.transformToCsvValue(dt.Columns));
			}
		}

		/// <summary>
		/// Löscht die CSV-Datei wieder
		/// </summary>
		/// <param name="dt">Daten die geschrieben werden sollen</param>
		/// <param name="ex">Ausnahme die das Problem verursacht hat</param>
		/// <returns>nichts</returns>
		private Task CSVInterface_rollbackFillContext(DataTable dt, Exception ex)
		{
			if (this.sw != null) this.sw.Close();
			try
			{
				File.Delete(dt.TableName);
			}
			catch { }
			return null;
		}

		/// <summary>
		/// Schreibt die CSV-Datei endgültig auf die Platte
		/// </summary>
		/// <param name="dt">Daten die geschrieben werden sollen</param>
		/// <returns>nichts</returns>
		private async Task CSVInterface_commitFillContext(DataTable dt)
		{
			await sw.FlushAsync();
			sw.Close();
			this.logger.logInfo(this.logSubject + "File written");
		}


		/// <summary>
		/// Schreibt einen Datensatz in die CSV-Datei
		/// </summary>
		/// <param name="dt">Daten die geschrieben werden sollen</param>
		/// <param name="dr">Die konkrete Datenrow die aktuell geschrieben werden soll</param>
		/// <param name="columnMap">ColumnMapping des Schreibvorgangs</param>
		/// <returns>nichts</returns>
		private async Task CSVInterface_insertHandler(DataTable dt, DataRow dr, TransferTableColumnList columnMap)
		{
			await sw.WriteLineAsync(this.transformToCsvValue(dr));
		}


		/// <summary> Transformiert einen Einzelwert in einen CSV-Wert </summary>
		/// <param name="input">Eingabetoken</param>
		/// <returns>CSV-kodierter Wert</returns>
		public string transformToCsvValue(string input)
		{
			if (input == null) return this.enclose + "null" + this.enclose;

			if (this.enclose != "")
				return this.enclose + input.Replace(this.enclose, this.enclose + this.enclose) + this.enclose;
			else
				return this.enclose + input + this.enclose;
		}

		/// <summary> Transformiert die TargetCol-Werte der ColumnList in eine HeaderZeile für CSV </summary>
		/// <param name="input">Ein Transfermapping</param>
		/// <returns>CSV-kodierte Header-Zeile</returns>
		public string transformToCsvValue(TransferTableColumnList input)
		{
			return String.Join(this.delimiter, from TransferTableColumn x in input select this.transformToCsvValue(x.targetCol));
		}

		/// <summary> Transformiert die Columnlist einer DataTable in eine HeaderZeile für CSV </summary>
		/// <param name="input">Eine DataColumnCollection</param>
		/// <returns>CSV-kodierte Header-Zeile</returns>
		public string transformToCsvValue(DataColumnCollection input)
		{
			return String.Join(this.delimiter, from DataColumn x in input select this.transformToCsvValue(x.ColumnName));
		}

		/// <summary> Transformiert die Columnlist einer DataTable in eine HeaderZeile für CSV </summary>
		/// <param name="input">Eine DataColumnCollection</param>
		/// <returns>CSV-kodierte Header-Zeile</returns>
		public string transformToCsvValue(DataRow input)
		{
			// Zum Textparsen/ausgeben von Datentypen verwende en-US und nicht die Culture des ausführenden Rechners!
			System.Threading.Thread.CurrentThread.CurrentCulture = Program.defaultCulture;
			return String.Join(this.delimiter, from object x in input.ItemArray select this.transformToCsvValue((x == DBNull.Value ? null : x.ToString())));
		}

		/// <summary>
		/// Löschungens-Handler - Wird nicht unterstützt
		/// </summary>
		/// <param name="tablename">Tabelle die zu löschen ist</param>
		/// <param name="where">Einschränkungen für Löschungen</param>
		/// <returns>immer Exception - nicht unterstüzt</returns>
		public override Task<int> deleteTableRows(string tablename, string where)
		{
			throw new ArgumentException(this.logSubject + "Deletions not supported for CSV-Data - Deactivate delete-Statement for this job or use another provider");
		}

	}
}
