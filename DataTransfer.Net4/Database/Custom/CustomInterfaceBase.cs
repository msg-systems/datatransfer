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
using System.Diagnostics;
using msa.Data.Transfer.SQL;
using msa.DSL.CodeParser;
using msa.DSL;

namespace msa.Data.Transfer.Database.Custom
{

	/// <summary>
	/// <para>Vorgabeklasse für einen CustomExport in ein definiertes Ausgabeformat. In geerbten Klassen kann man das Verhalten über Eventhandler steuern und mit Properties</para>
	/// <para>Neben den Eventhandler können natürlich beliebige Funktionen überschrieben werden um ein angepasstes Verhalten zu ermöglichen.</para>
	/// Handler zum Lesen <br/> <br/>
	/// <list type="bullet">
	/// <item>fillFromSQLParseTree: <see cref="fillFromSQLParseTree"/></item>
	/// </list>
	/// Handler zum Schreiben (chronologisch) <br/> <br/>
	/// <list type="bullet">
	///	<item>initFillContext <see cref="initFillContext"/></item>
	///	<item>insertHandler <see cref="insertHandler"/></item>
	///	<item>updateHandler <see cref="updateHandler"/></item>
	///	<item>deleteHandler <see cref="deleteHandler"/></item>
	///	<item>commitFillContext <see cref="commitFillContext"/></item>
	///	<item>rollbackFillContext <see cref="rollbackFillContext"/></item>
	/// </list>
	/// </summary>
	public class CustomInterfaceBase : DBInterface
	{
		/// <summary> Initialisierung eines CustomHandlers bevor das Fill beginnt. <br/>
		/// <list type="bullet">
		/// <item>Parameter DataTable: Die Tabelle mit den Daten die geschrieben werden sollen in die Custom-Zielumgebung</item>
		/// <item>Parameter TransferTableColumnList: Das Spaltenmapping zwischen Quelle und Ziel</item>
		/// <item>Rückgabe Task: Dient nur dafür das die Funktion als async definieren kann</item>
		/// </list>
		/// </summary>
		protected event Func<DataTable, TransferTableColumnList, Task> initFillContext;
		/// <summary> Insert-Event für eine Zeile - Insert muss hier implementiert sein 
		/// <list type="bullet">
		/// <item>Parameter DataTable: Die Tabelle mit den Daten die geschrieben werden sollen in die Custom-Zielumgebung</item>
		/// <item>Parameter DataRow: Die Daten des aktuellen Datensatzes der eingefügt werden soll</item>
		/// <item>Parameter TransferTableColumnList: Das Spaltenmapping für den aktuellen Transfer</item>
		/// <item>Rückgabe Task: Dient nur dafür das die Funktion als async definieren kann</item>
		/// </list>
		/// </summary>
		protected event Func<DataTable, DataRow, TransferTableColumnList, Task> insertHandler;
		/// <summary> update-Event für eine Zeile - Update muss hier implementiert sein 
		/// <list type="bullet">
		/// <item>Parameter DataTable: Die Tabelle mit den Daten die geschrieben werden sollen in die Custom-Zielumgebung</item>
		/// <item>Parameter DataRow: Die Daten des aktuellen Datensatzes der aktualisiert werden soll</item>
		/// <item>Parameter TransferTableColumnList: Das Spaltenmapping für den aktuellen Transfer</item>
		/// <item>Rückgabe Task: Dient nur dafür das die Funktion als async definieren kann</item>
		/// </list>
		/// </summary>
		protected event Func<DataTable, DataRow, TransferTableColumnList, Task> updateHandler;
		/// <summary> delete-Event für eine Zeile - Delete muss hier implementiert sein
		/// <list type="bullet">
		/// <item>Parameter DataTable: Die Tabelle mit den Daten die geschrieben werden sollen in die Custom-Zielumgebung</item>
		/// <item>Parameter DataRow: Die Daten des aktuellen Datensatzes der gelöscht werden soll</item>
		/// <item>Parameter TransferTableColumnList: Das Spaltenmapping für den aktuellen Transfer</item>
		/// <item>Rückgabe Task: Dient nur dafür das die Funktion als async definieren kann</item>
		/// </list>
		/// </summary>
		protected event Func<DataTable, DataRow, TransferTableColumnList, Task> deleteHandler;
		/// <summary> commit-Event für das Fill 
		/// <list type="bullet">
		/// <item>Parameter DataTable: Die Tabelle mit den Daten die geschrieben werden sollen in die Custom-Zielumgebung</item>
		/// <item>Rückgabe Task: Dient nur dafür das die Funktion als async definieren kann</item>
		/// </list>
		/// </summary>
		protected event Func<DataTable, Task> commitFillContext;
		/// <summary> rollback-Event für das Fill 
		/// <list type="bullet">
		/// <item>Parameter DataTable: Die Tabelle mit den Daten die geschrieben werden sollen in die Custom-Zielumgebung</item>
		/// <item>Parameter Exception: Grund für den Rollback</item>
		/// <item>Rückgabe Task: Dient nur dafür das die Funktion als async definieren kann</item>
		/// </list>
		/// </summary>
		protected event Func<DataTable, Exception, Task> rollbackFillContext;

		/// <summary>
		/// In diesem Event muss man in einem Custom-Provider DataTables befüllen, pro Tabelle welche im übergebenen parseTree ermittelt wurde. Die Tabellen erhält man mit parseTree.tables.Values .
		/// Die zu befüllenden Spalten pro Teiltabelle erhält man mit tab.createDataTable() und tab.getDirectColReferences()
		/// Jede direkte Referenz muss von der Funktion befüllt werden.
		/// Der Rückgabewert erwartet den table.Alias als Key und die DataTable mit den gefüllten Basiswerten als Wert <br/><br/>
		/// <list type="bullet">
		/// <item>Parameter SqlParseTree: Der Parsetree der angibt welche Tabellen eingelesen werden sollen</item>
		/// <item>Rückgabe Task&lt;Dictionary&lt;string, DataTable&gt;&gt;: Das Ergebnis als Mapping Tabellenname -&gt; eingelesene Tabellendaten </item>
		/// </list>
		/// </summary>
		protected event Func<SqlParseTree, Task<Dictionary<string, DataTable>>> fillFromSQLParseTree;

		/// <summary> Größe des Batches - nur relevant, wenn überhaupt machbar - meist keine Wirkung </summary>
		public long updateBatchSize { get; set; }
		/// <summary> Gibt an ob der FROM-Teil vom System via DSL geparst werden soll, oder ob er anders verwendet wird </summary>
		public bool parseFrom { get; set; }
		/// <summary>Menge von Sonderzeichen die als Identifizierer zugelassen sind '.' ist typisch bei SQL-Parsing als Trenner zwischen Tabelle und Spalte </summary>
		public List<char> parseAdditonalAllowedIdentifiers { get; set; } = new List<char>();

		/// <summary>
		/// Erzeugt einen neuen Custom-Export-Provider
		/// </summary>
		/// <param name="conString">Der ConnectionString wird Plaintext übernommen</param>
		/// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
		public CustomInterfaceBase(string conString, Logger logger = null)	: base()
		{
			if (logger != null) this.logger = logger;

			this.logSubject = String.Format("{0} > {1}: ", "Custom.Export", this.getSecureConString(this.conString));
			this.adoDriver = "Custom.Export";
			this.conString = conString;
			this.dbTransaction = null;
		}


		/// <summary>
		/// Für Custom-Provider existiert standardmäßig kein Connect
		/// </summary>
		/// <returns>nichts</returns>
		public override async Task connect()
		{
			await Task.Run(() => { });
			this.logger.logInfo(this.logSubject + "does not need connect");
		}



		/// <summary>
		/// Für Custom-Provider existiert standardmäßig kein Disconnect
		/// </summary>
		/// <returns>nichts</returns>
		public override void disconnect()
		{
			this.logger.logInfo(this.logSubject + "does not need disconnect");
		}



		/// <summary>
		/// Siehe <see cref="DBInterface.fillTable(DataTable, TransferBlock, TransferTableJob, TransferTableColumnList)"/> - Muss implementiert werden
		/// </summary>
		/// <param name="data">siehe Definition</param>
		/// <param name="block">siehe Definition</param>
		/// <param name="job">siehe Definition</param>
		/// <param name="columnMap">siehe Definition</param>
		/// <returns>siehe Definition</returns>
		public override async Task fillTable(DataTable data, TransferBlock block, TransferTableJob job, TransferTableColumnList columnMap = null)
		{
			this.logger.logInfo(this.logSubject + "Start fill table: '" + job.targetTable + "' with " + data.Rows.Count + " records");
			DataTable target = null;

			// Passe die Quelltabelle so an, dass sie auf die neue DB-Tabelle zeigt
			data.TableName = job.targetTable;

			// Initialisiere bei NonSync (Zieltabelle wird nicht eingelesen) eine Fake-Zieltabelle damit die Spaltennamen für Debugmeldungen möglich sind
			if (job.identicalColumns && !job.sync)
			{
				target = data;
			}
			else if (!job.sync && !job.identicalColumns)
			{
				target = new DataTable();
				foreach (TransferTableColumn tc in columnMap)
				{
					target.Columns.Add(tc.targetCol);
				}
			}

			// Setze alle Datensätze in den Zustand Zugefügt, damit das "Update" sie einfügt
			//data.AcceptChanges();
			foreach (DataRow row in data.Rows)
			{
				row.SetAdded();
			}

			if (job.sync)
			{
				this.logger.logInfo(this.logSubject + "Start fill table: '" + job.targetTable + "' use Sync - Options: " +
					"Inserts " + (job.syncOptions != null ? (!job.syncOptions.noInserts).ToString() : "true") +
					", Updates " + (job.syncOptions != null ? (!job.syncOptions.noUpdates).ToString() : "true") +
					", Deletes " + (job.syncOptions != null ? (!job.syncOptions.noDeletes).ToString() : "true") +
					" - Reading target table for sync now");

				if (job.syncOptions != null)
				{
					if (job.syncOptions.noDeletes == false && this.deleteHandler == null) { throw new ArgumentException($"Sync Delete is not supported for this connector type {this.adoDriver}"); }
					if (job.syncOptions.noUpdates == false && this.updateHandler == null) { throw new ArgumentException($"Sync Update is not supported for this connector type {this.adoDriver}"); }
					if (job.syncOptions.noInserts == false && this.insertHandler == null) { throw new ArgumentException($"Sync Insert is not supported for this connector type {this.adoDriver}"); }
				}
				else
				{
					if (this.deleteHandler == null) { throw new ArgumentException($"Sync Delete is not supported for this connector type {this.adoDriver}"); }
					if (this.updateHandler == null) { throw new ArgumentException($"Sync Update is not supported for this connector type {this.adoDriver}"); }
					if (this.insertHandler == null) { throw new ArgumentException($"Sync Insert is not supported for this connector type {this.adoDriver}"); }
				}

				// Bei identischen Spalten, übernehme diese aus den Daten
				if (job.identicalColumns && columnMap == null)
				{
					columnMap = new TransferTableColumnList();
					foreach (DataColumn dc in data.Columns)
					{
						columnMap.Add(new TransferTableColumn() { sourceCol = dc.ColumnName, targetCol = dc.ColumnName });
					}
				}
				target = await this.readTable(job.targetTable, job.targetSyncWhere, columnMap);
				target.TableName = job.targetTable;

				// Übernehme Primary Keys - 
				DataColumn[] keys = new DataColumn[data.PrimaryKey.Count()];
				int index = 0;

				foreach (DataColumn dc in data.PrimaryKey)
				{
					keys[index] = target.Columns[dc.ColumnName];
					index++;
				}
				target.PrimaryKey = keys;
				target.AcceptChanges();

				// Prüfe Schemata von data und target ( case sensitive Spalten können für Probleme sorgen)
				foreach (DataColumn dc in data.Columns)
				{
					if (!target.Columns.Contains(dc.ColumnName))
					{
						// Spalte existiert nicht - Fehler mit Aussagekraft ( sonst spätestens beim Merge mit NullPointerException)
						throw new Exception("Column " + dc.ColumnName + " does not exist in target - check case and type-o´s");
					}
					else
					{
						// Lösung des Case-Sensitiv-Problems
						DataColumn dc2 = target.Columns[dc.ColumnName];
						dc.ColumnName = dc2.ColumnName;
						dc.Caption = dc2.Caption;
					}
				}

				// Passe Typen an...
				bool pkChanged = false;
				List<DataColumn> oldPK = data.PrimaryKey.ToList();
				int colIndex = 0;
				int colCount = data.Columns.Count;

				foreach (DataColumn colTarget in target.Columns)
				{
					DataColumn colSource = data.Columns[colTarget.ColumnName];
					if (colTarget.DataType != colSource.DataType && (colSource.DataType == typeof(string) || colSource.DataType == typeof(int) || colTarget.DataType == typeof(string)))
					{
						pkChanged = true;
						colSource.ColumnName += "temp";
						DataColumn newCol = data.Columns.Add(colTarget.ColumnName, colTarget.DataType);
						if (oldPK.Exists((dc) => dc.ColumnName == colSource.ColumnName))
						{
							oldPK.Remove(colSource);
							oldPK.Add(newCol);
						}
						foreach (DataRow row in data.Rows)
						{
							row[colCount] = row[colIndex];
						}
						data.Columns.RemoveAt(colIndex);
						newCol.SetOrdinal(colIndex);
					}
					colIndex++;
				}
				if (pkChanged) data.PrimaryKey = oldPK.ToArray();

				target.Merge(data, false); // Mergen . muss false sein um neue Werte korrekt zu übernehmen

				long countDelete = 0, countInsert = 0, countUpdate = 0;

				// Löschungen initialisieren wenn nicht verboten
				if (job.syncOptions != null && !job.syncOptions.noDeletes)
				{
					foreach (DataRow drl in target.Rows)
					{
						if (drl.RowState == DataRowState.Unchanged)
						{
							drl.Delete();
							countDelete++;
						}
					}
				}

				// Inserts entfernen wenn verboten
				List<DataRow> temp = target.Rows.Cast<DataRow>().Where((dr) => dr.RowState == DataRowState.Added).AsParallel().ToList();
				countInsert = temp.Count;
				if (job.syncOptions != null && job.syncOptions.noInserts)
				{
					countInsert = 0;
					foreach (DataRow dr in temp)
						dr.AcceptChanges();
				}

				// Updates entfernen wenn verboten
				temp = target.Rows.Cast<DataRow>().Where((dr) => dr.RowState == DataRowState.Modified).AsParallel().ToList();
				countUpdate = temp.Count;
				if (job.syncOptions != null && job.syncOptions.noUpdates)
				{
					countUpdate = 0;
					foreach (DataRow dr in temp)
						dr.AcceptChanges();
				}

				this.logger.logInfo(this.logSubject + "Start fill table: '" + job.targetTable + "' use Sync - Counts: " +
					"Inserts " + countInsert + ", Updates " + countUpdate + ", Deletes " + countDelete);

				data = target;
			}

			// BatchSize vorgeben. Im Verbose-Modus wird einzeln verarbeitet und zusätzliche Handler initialisiert
			//this.logger.logInfo("Loglevel=" + this.logger.logLevel);
			if ((int)this.logger.logLevel >= (int)SourceLevels.Verbose)
				updateBatchSize = 1; // = Einzelstatements
			else
				updateBatchSize = block.targetMaxBatchSize; // = maximale BatchSize


			// Führe Update durch
			this.logger.logInfo(this.logSubject + "Start fill table: '" + job.targetTable + "' Start filling");

			if (this.initFillContext != null) await this.initFillContext(data, columnMap);
			DataTable changes = data.GetChanges();
			if (changes != null)
			{
				try
				{
					foreach (DataRow row in changes.Rows)
					{
						if ((int)this.logger.logLevel >= (int)SourceLevels.Verbose)
						{
							string output = "Row " + row.RowState.ToString() + " Values ";
							for (int i = 0; i < target.Columns.Count; i++)
							{
								output += target.Columns[i].ColumnName + " => " + row[i].ToString() + ", ";
							}
							this.logger.logVerbose(this.logSubject + "table '" + job.targetTable + "Process Line " + output);
						}

						switch (row.RowState)
						{
							case DataRowState.Added:
								await this.insertHandler(target, row, columnMap);
								break;
							case DataRowState.Deleted:
								await this.deleteHandler(target, row, columnMap);
								break;
							case DataRowState.Modified:
								await this.updateHandler(target, row, columnMap);
								break;
						}
					}
				}
				catch (Exception ex)
				{
					if (this.isTransactional)
					{
						if (this.rollbackFillContext != null) await this.rollbackFillContext(data, ex);
					}
					throw ex;
				}
			}
			if (this.commitFillContext != null) await this.commitFillContext(data);

			this.logger.logInfo(this.logSubject + "table '" + job.targetTable + "' filled successfully");
		}

		/// <summary>
		/// Führt einen Select auf der Datenquelle für die angegeben Tabelle durch und liefert das Ergebnis als lokales DataTable-Objekt zurück
		/// </summary>
		/// <param name="tablename">Die abzufragende Tabelle</param>
		/// <param name="where">Einschränkung für Select</param>
		/// <param name="columnMap">Ein durchzuführendes Spaltenmapping beim SELECT</param>
		/// <param name="parameters">Zusätzliche Parameter die im Select where-Statement angefügt werden sollen (wenn nicht schon vorhanden)</param>
		/// <returns>Eine DataTable mit den Daten der Tabelle (kein Cursor)</returns>
		public override async Task<DataTable> readTable(string tablename, string where, TransferTableColumnList columnMap, ParameterDef[] parameters = null)
		{
			string select = String.Format("SELECT {0} FROM {1}",
					String.Join(", ", columnMap.Select(
						(el) => String.Format("{0} as {1}", el.sourceCol, el.targetCol) // Spaltenmapping auf Zieltabelle
					)),
					tablename
				);

			if (!String.IsNullOrWhiteSpace(where)) select += " WHERE " + where;
			select += ";";

			// Lies beim Select mit TransferTableColumns Keys direkt ein
			DataTable data = await this.readSelect(select);

			return data;
		}



		/// <summary>
		/// Siehe <see cref="DBInterface.readSelect(string, ParameterDef[])"/>.
		/// </summary>
		/// <param name="query">Die Anfrage die verwendet werden soll für den Select</param>
		/// <param name="parameters">Zusätzliche Parameter die im Select where-Statement angefügt werden sollen (wenn nicht schon vorhanden)</param>
		/// <returns>Siehe <see cref="DBInterface.readSelect(string, ParameterDef[])"/> </returns>
		public override async Task<DataTable> readSelect(string query, ParameterDef[] parameters = null)
		{
			SqlParseTree parseTree = new SqlParseTree(this.logger, this.logSubject);
			parseTree.parse(query, true, this.parseFrom, parseAdditonalAllowedIdentifiers.ToArray());
			if (fillFromSQLParseTree == null) return parseTree.createDataTable();

			// Lies alle Daten
			Dictionary<string, DataTable> tabList = await fillFromSQLParseTree(parseTree);
			foreach( SqlTableExpression tab in parseTree.tables.Values)
			{
				if (!tabList.Keys.Contains(tab.alias)) throw new ArgumentException($"Event fillFromSQLParseTree has not filled the base table {tab.alias} which is needed - error in custom provider implementation for {this.adoDriver}");
			}

			DataTableDSL dataTableDsl = new DataTableDSL();
			DataTableDSL dataTableDslJoin = new DataTableDSL();
			CodeEvaluator codeEvaluator = new CodeEvaluator();

			// Auswertung berechnete Attribute und Vorfilterung
			foreach (SqlTableExpression tab in parseTree.tables.Values)
			{
				DataTableValueProvider dtValueProvider = dataTableDsl.valueProvider as DataTableValueProvider;
				dtValueProvider.initWithParseTree(parseTree, tab);
				DataTable tempTab = tabList[tab.alias];

				// Vorfiltern -- Sonst Mem-Overflows bei Joins
				if (parseTree.conditionsElement != null)
				{
					// Suche Teilausdrücke die für diese Tabelle gelten - ist allerdings kritisch wenn viel mit Oder gearbeitet wird
					List<CodeElement> refs = parseTree.conditionsElement.childElementsOf<CodeReference>();
					List<CodeElement> refsToCheck = new List<CodeElement>();

					foreach (CodeElement refEl in refs)
					{
						// Prüfe ob ref für diese Tabelle gilt
						SqlSelectExpression sqlEx = new SqlSelectExpression(refEl, refEl.content);
						if (sqlEx.baseTable == tab.alias)
						{
							// Prüfe ob der Ausdruck in einem zwingendem AND ist
							CodeElement checkElement = refEl;
							bool canBeUsed = true;
							while (checkElement.parent != null)
							{
								checkElement = checkElement.parent;
								if (checkElement is CodeBinaryOp)
								{
									// wenn Ausdruck im Kontext eines Oders verwendet wird, kann er nicht verwendet werden
									if ((checkElement as CodeBinaryOp).operatorType == "||")
									{
										canBeUsed = false;
										break;
									}
								}
								if (checkElement is CodeBracing) // verdächtig, da nur mit ODER oder NICHT sinnvoll
								{
									canBeUsed = false;
									break;
								}
							}

							// Falls ok, dann merken - Parent weil das normalerweise 
							if (canBeUsed)
							{
								// Suche CodeBinaryOp Parent des Elements, welcher für eine Bedingung steht
								checkElement = refEl;
								CodeElement checkElementBefore = null;
								while (checkElement.parent != null)
								{
									checkElementBefore = checkElement;
									checkElement = checkElement.parent;
									if (checkElement is CodeBinaryOp)
									{
										// Suche Binärvergleichselement welches schlussendlich mit dieser CodeReferenz arbeiten
										if (CodeBinaryOp.codeRelationType.Contains((checkElement as CodeBinaryOp).operatorType))
										{
											refsToCheck.Add(checkElement);
											break;
										}
										// evt. könnte da aber auch eine Logikbedingung mit and/or stehen - Dann ist das Element (wahrscheinlich FunctionCall) selbst auszuwerten
										if (CodeBinaryOp.logicRelationType.Contains((checkElement as CodeBinaryOp).operatorType))
										{
											// versuche den Unterausdruck auszuwerten als Boolean, da oben drüber weiteres and/or
											refsToCheck.Add(checkElementBefore); 
										}

									}
								}
							}
						}
					}

					// Führe Vorab-Prüfungen durch
					foreach (CodeElement refToCheck in refsToCheck)
					{
						Console.WriteLine("refToCheck");
						for (int i = 0; i < tempTab.Rows.Count; i++)
						{
							dtValueProvider.context = tempTab.Rows[i];
							if (!codeEvaluator.evaluate<bool>(refToCheck, dataTableDsl))
							{
								tempTab.Rows.RemoveAt(i);
								i--;
							}
						}
					}
				}

				// Alias-Namen zufügen zu den Spalten beim Einlesen falls noch nicht vorhanden (es sind normalerweise nur DirectCols enthalten, aber keine AS XXX Spaltennamen
				foreach (SqlSelectExpression selEx in tab.attributeMap.Values.Where((v) => !(v.expressionElement is CodeReference)))
				{
					if (!tempTab.Columns.Contains(selEx.colNameResult))
					{
						tempTab.Columns.Add(selEx.colNameResult);
					}
				}

				// Berechnung von berechneten Attributen
				foreach (DataRow dr in tempTab.Rows)
				{
					dtValueProvider.context = dr;
					foreach (SqlSelectExpression selEx in tab.attributeMap.Values.Where((v) => !(v.expressionElement is CodeReference)))
					{
						dr[selEx.alias] = codeEvaluator.evaluate<object>(selEx.expressionElement, dataTableDsl);
					}
				}				
			}

			DataTable result = null;
			if (tabList.Count == 1)
			{
				result = tabList.Values.First();
			}
			else
			{
				foreach (SqlJoinExpression join in parseTree.tables.Values.Where((t) => t.join != null).Select((j) => j.join))
				{
					CodeElement joinElement = join.joinElement;
					List<CodeElement> references = joinElement.childElementsOf<CodeReference>();

					DataTable baseTable = tabList[join.baseTable.alias];
					DataTable joinTable = tabList[join.joinTable.alias];

					// Schemas vermischen
					if (result == null)
					{
						result = baseTable.Clone();
					}
					else
					{
						result.Columns.AddRange(baseTable.Columns.Cast<DataColumn>().Select((dc) => new DataColumn(dc.ColumnName)).ToArray());
					}
					List<String> baseColNames = result.Columns.Cast<DataColumn>().Select((dc) => dc.ColumnName).ToList();
					List<DataColumn> joinColumns = new List<DataColumn>();
					Dictionary<int, int> joinColumnMap = new Dictionary<int, int>();
					for (int i = 0; i < joinTable.Columns.Count; i++)
					{
						DataColumn dc = joinTable.Columns[i];
						if (!baseColNames.Contains(dc.ColumnName))
						{
							joinColumnMap[joinColumns.Count] = i;
							joinColumns.Add(dc);
						}
					}

					result.Columns.AddRange(joinColumns
						.Select((dc) => new DataColumn(dc.ColumnName)).ToArray());

					// Join auswerten
					DataTableValueProvider dtValueProvider = dataTableDsl.valueProvider as DataTableValueProvider;
					dtValueProvider.initWithParseTree(parseTree, join.baseTable);

					DataTableValueProvider dtValueProvider2 = dataTableDslJoin.valueProvider as DataTableValueProvider;
					dtValueProvider2.initWithParseTree(parseTree, join.joinTable);

					for (int baseI = 0; baseI < baseTable.Rows.Count; baseI++)
					{
						DataRow baseRow = baseTable.Rows[baseI];
						dtValueProvider.context = baseRow;

						object baseCompare = codeEvaluator.evaluate<object>(join.baseTableEvaluation, dataTableDsl);
						for (int joinI = 0; joinI < joinTable.Rows.Count; joinI++)
						{
							DataRow joinRow = joinTable.Rows[joinI];
							dtValueProvider2.context = joinRow;
							object joinCompare = codeEvaluator.evaluate<object>(join.joinTableEvaluation, dataTableDslJoin);

							if (baseCompare?.ToString() == joinCompare?.ToString()) // nur Gleichheit
							{
								DataRow mergedRow = result.NewRow();
								for (int colI = 0; colI < baseTable.Columns.Count; colI++)
								{
									mergedRow[colI] = baseRow[colI];
								}
								for (int colI = 0; colI < joinColumns.Count; colI++)
								{
									mergedRow[colI + baseTable.Columns.Count] = joinRow[ joinColumnMap[colI]];
								}
								result.Rows.Add(mergedRow);
							}
						}
					}
				}
			}

			// Where auswerten
			if (parseTree.conditionsElement != null)
			{
				DataTableValueProvider dtValueProvider = dataTableDsl.valueProvider as DataTableValueProvider;
				dtValueProvider.initWithParseTree(parseTree, null);
				for (int i = 0; i < result.Rows.Count; i++)
				{
					dtValueProvider.context = result.Rows[i];
					if (!codeEvaluator.evaluate<bool>(parseTree.conditionsElement, dataTableDsl))
					{
						result.Rows.RemoveAt(i);
						i--;
					}
				}
			}

			// Berechnung von Spalten die aus multiplen Tabellen kommen
			if (parseTree.tables.Count > 1)
			{
				IEnumerable<SqlSelectExpression> multiCalc = parseTree.columns.Where((c) => c.baseTables?.Count() > 1);
				if (multiCalc.Count() > 0 )
				{
					DSLDef multiDsl = new DSLDef(new DataTableMultiDefinitionValueProvider(), new DataTableFunctionHandler());
					DataTableMultiDefinitionValueProvider valProv = multiDsl.valueProvider as DataTableMultiDefinitionValueProvider;
					valProv.addTables(parseTree, parseTree.tables.Values.ToArray());
					foreach (SqlSelectExpression selEx in multiCalc)
					{
						// da Diese Spalten nie automatisch erzeugt werden, müssen sie hier manuell zugefügt werden (nicht eindeutig zu SubTab zuordenbar)
						if (!selEx.hasAlias) throw new ArgumentException($"Select expression {selEx.expression} has no alias - computed columns need one ");
						result.Columns.Add(selEx.alias);

						foreach (DataRow dr in result.Rows)
						{
							valProv.context = dr;
							dr[selEx.alias] = codeEvaluator.evaluate<object>(selEx.expressionElement, multiDsl);
						}
					}
				}
			}

			// Reine Berechnungsspalten entfernen
			for (int i = 0; i< result.Columns.Count; i++)
			{
				DataColumn dc = result.Columns[i];
				if (!parseTree.columns.Exists( (se) => se.alias == dc.ColumnName || se.colName == dc.ColumnName))
				{
					result.Columns.Remove(dc);
					i = i - 1;
				}
			}

			// Konstante Spalten zufügen - müssen immer einen Alias haben
			foreach (SqlSelectExpression col in parseTree.columns)
			{
				if (col.expressionElement.childElementsOf<CodeReference>().Count == 0)
				{
					if (!result.Columns.Contains(col.alias))
						result.Columns.Add(col.alias);

					foreach (DataRow dr in result.Rows)
					{
						dr[col.alias] = codeEvaluator.evaluate<object>(col.expressionElement);
					}
				}
			}

			// Spalten in SELECT-Format umsortieren
			if (tabList.Count > 1)
			{
				for (int i = 0; i < parseTree.columns.Count; i++)
				{
					SqlSelectExpression col = parseTree.columns[i];
					result.Columns[col.colNameResult].SetOrdinal(i);
				}
			}
			
			result.AcceptChanges();

			return result;
		}

	}
}
