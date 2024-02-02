using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using msa.Data.Transfer.Database;
using msa.Data.Transfer.Model;
using msa.Logging;
using System.Text.RegularExpressions;
using msa.DSL.CodeParser;
using msa.DSL;

namespace msa.Data.Transfer
{

	/// <summary>
	/// Klasse zur Verarbeitung von TransferJob-Files die laut job.xsd definiert wurden. <br/>
	/// Führt Verarbeitung von Tabellentransfers aus die TransferBlöcken organisiert sind. Transferblöcke können zudem parallel verarbeitet werden. <br/>
	/// Der standardmäßig verwendete Logger ist "msa.Data.Transfer.TransferBatch" <br/>
	/// Wird das Loglevel auf Verbose gesetzt wechselt der Prozess bei Batchläufen in Einzelverarbeitung und loggt jegliche Statements mit Parametern die verwendet werden <br/>
	/// </summary>
	public class TransferBatch :IDisposable
	{

		/// <summary> Pfad zur verwendeten ConfigFile für diesen Transferbatch </summary>
		public string configFile { get; protected set; }
		/// <summary> Die eingelesene Konfiguration für den TransferBatch - nachträgliche Änderungen sind möglich </summary>
		public TransferJob config { get; protected set; }
		/// <summary> Der verwendete Logger für den Transferbatch - default "msa.Data.Transfer.TransferBatch" </summary>
		public Logger logger { get; protected set; }
		/// <summary> Falls transaktionale Verarbeitung aktiviert ist (laut Config), wird standardmäßig dieser IsolationLevel verwendet - Default Serializable (strengste Form der Isolation mit meisten Sperren) </summary>
		public IsolationLevel defaultIsolationLevel = IsolationLevel.Serializable;


		/// <summary> Variablen für den Batch </summary>
		public Dictionary<String, Variable> variables = new Dictionary<string, Variable>();

		/// <summary>Code-Evaluierer für Variablen mit expression-Initialisierer</summary>
		CodeEvaluator codeEvaluator = new CodeEvaluator();
		/// <summary>Domain Specific Language für Variablen mit expression-Initialisierer - arbeitet mit Variable-Dictionary und standard Lib-Funktonen von msa.DSL</summary>
		public DSLDef batchDSL = null;

		/// <summary>
		/// Erstellt eine neue Transferbatch mit dem relativen/absolutem Pfad zur Konfigurationsdatei die die Operationen beschreibt. Der Defaultlogger kann hier per Parameter mit einem anderen ersetzt werden
		/// </summary>
		/// <param name="configFile">Absoluter oder relativer Pfad zur xml-Jobdatei die verwendet werden soll</param>
		/// <param name="logger">Ein alternativer externer Logger der verwendet werden soll - ersetzt den Default Logger "msa.Data.Transfer.TransferBatch"</param>
		/// <exception cref="System.ArgumentException">Tritt auf wenn keine ConfigFile angegeben wurde</exception>
		public TransferBatch(string configFile, Logger logger = null){

			if (String.IsNullOrEmpty(configFile.Trim())) throw new ArgumentException("configFile parameter not specified");
			if (!File.Exists(configFile)) throw new ArgumentException(String.Format("configFile '{0}' not found", configFile));

			this.configFile = configFile;

			// Job deserialisieren
			Console.WriteLine("Read Config");
			XmlSerializer xmls = new XmlSerializer(typeof(TransferJob));
			using (FileStream fs = new FileStream(this.configFile, FileMode.Open))
			{
				this.config = (TransferJob)xmls.Deserialize(fs);
			}

			// Logger initialisieren
			Console.WriteLine("Init Logger");

			if (logger == null)
			{
				if (String.IsNullOrEmpty(this.config.settings.writeLogToFile))
				{
					string logFile = @"Log\transferLog_[ddMMyyyy hh_mm_ss].log";
					Console.WriteLine("No log file specified - using " + logFile);
					this.config.settings.writeLogToFile = logFile;
				}

				//Console.WriteLine("############# " + this.config.settings.writeLogToFile);
				//this.config.settings.writeLogToFile = "Log\\xxx.log";

				this.logger = Logger.createNewLoggerInstance("msa.Data.Transfer.TransferBatch", this.config.settings.writeLogToFile);
			}
			else
			{ 
				this.logger = logger;
			}

			if (this.logger.mailSettings == null)
			{
				if (String.IsNullOrWhiteSpace(this.config.settings.mailSettings.smtpServer))
					this.logger.mailSettings = null;
				else
					this.logger.mailSettings = this.config.settings.mailSettings;
			}

			this.batchDSL = new DSLDef(new TransferBatchDSLValueProvider(variables), new TransferBatchDSLFunctionHandler());
		}


		/// <summary>
		/// Zugriff auf einen Transferblock über Namen
		/// </summary>
		/// <param name="blockName">Der Name des Transferblocks</param>
		/// <returns>Die Transferblock-Instanz</returns>
		/// <exception cref="System.ArgumentException">Tritt auf wenn der Block nicht gefunden wurde</exception>
		public TransferBlock this[string blockName]
		{
			get{
				TransferBlock selBlock = (from block in this.config.transferBlocks where block.name == blockName select block).FirstOrDefault();
				if (selBlock == null) throw new ArgumentException( String.Format("TransferBlock with name '{0}' does not exist in job '{1}'", blockName, this.configFile));
				return selBlock;
			}
			// Schreibender Zugriff legt einen leeren Transferblock an
			set{
				if (String.IsNullOrEmpty(blockName)) throw new ArgumentException("Blockname cannot be empty");
				value.name = blockName;
				this.config.transferBlocks.Add(value);
			}
		}

		/// <summary>
		/// Verarbeitet alle in der Aufzählung enthaltenen Transferblocks parallel
		/// </summary>
		/// <param name="blocks">Die Liste der zu verarbeitenden Transferblocks</param>
		/// <returns>true wenn alles erfolgreich gelaufen ist, sonst false</returns>
		public bool processTransferBlocks(IEnumerable<string> blocks)
		{
			try
			{
				List<TransferBlock> blockList = new List<TransferBlock>();
				foreach (string block in blocks)
				{
					
					blockList.Add(this[block]);
				}
				return this.processTransferBlocks(blockList);
			}
			catch (Exception e)
			{
				this.logger.logError("Job " + String.Join(",", blocks) + " : " + e.ToString());
				return false;
			}
		}

		/// <summary>
		/// Verarbeitet alle definierten Transferblöcke in paralleler Ausführung (Jobs einzelner Blöcke werden sequentiell verarbeitet)
		/// </summary>
		/// <returns>true wenn alles erfolgreich gelaufen ist, sonst false</returns>
		/// <exception cref="System.AggregateException">Eine mögliche Exception pro Transferblock der parallel ausgeführt wurde</exception>
		public bool processAllTransferBlocks()
		{
			return this.processTransferBlocks(this.config.transferBlocks);
		}

		/// <summary>
		/// Verarbeitet alle in der Aufzählung enthaltenen Transferblocks parallel
		/// </summary>
		/// <param name="blocks">Die Liste der zu verarbeitenden Transferblocks</param>
		/// <returns>true wenn alles erfolgreich gelaufen ist, sonst false</returns>
		public bool processTransferBlocks(IEnumerable<TransferBlock> blocks)
		{
			// Die längere Variante ist sauberer als die ursprünglich unten auskommentierte
			// Der Grund ist, das unten Threads erzeugt werden die Threads erzeugen
			// mit der obigen Variante gibt es genau einen Thread pro Block
			List<Task<bool>> taskList = new List<Task<bool>>();
			List<Exception> exceptions = new List<Exception>();
			bool result = true;
			foreach (TransferBlock block in blocks)
			{
				taskList.Add(Task.Run(new Func<bool>(() =>
				{
					Task<bool> t = this.processTransferBlock(block);
					t.Wait();
					return t.Result;
				})));
			}
			Task.WaitAll(taskList.ToArray());
			foreach (Task<bool> t in taskList)
			{
				result = result && t.Result;
				if (t.Exception != null) exceptions.Add(t.Exception);
			}
			if (exceptions.Count > 0) throw new AggregateException("Data transfer errors occured", exceptions);
			return result;
		}


		/// <summary>
		/// Führt alle Jobs des angegebenen Transferblocks sequentiell aus. Details siehe processTransferBlock(TransferBlock)
		/// </summary>
		/// <param name="blockName">Der Name des Transferblocks</param>
		/// <returns>true, wenn erfolgreich verarbeitet, sonst false</returns>
		public async Task<bool> processTransferBlock(string blockName)
		{
			return await this.processTransferBlock( this[blockName] );
		}


		/// <summary>
		/// Führt alle Jobs des angegebenen Transferblocks sequentiell aus.
		/// Dabei wird eine Verbindung zur Quell- und Zieldatenquelle geöffnet und ggf. eine einzelne Transaktion für alle Subjobs erzeugt. 
		/// Im Fehlerfall wird bei transaktionaler Verarbeitung die komplette Verarbeitung rückgängig gemacht.
		/// </summary>
		/// <param name="block">Der auszuführende Transferblock</param>
		/// <returns>true, wenn erfolgreich verarbeitet, sonst false</returns>
		public async Task<bool> processTransferBlock(TransferBlock block)
		{
			if (block == null) return false;

			string logSubjectBlock = String.Format("Transferblock '{0}': ", block.name);

			try
			{
				this.logger.logInfo(String.Format("{0}has started procssing", logSubjectBlock));

				using (DBInterface source = await this.initAndConnectDataSource(logSubjectBlock, block.conStringSourceType, block.conStringSource))
				using (DBInterface target = await this.initAndConnectDataSource(logSubjectBlock, block.conStringTargetType, block.conStringTarget))
				{
					// Transaction starten sofern nötig
					if (!block.disableTransaction)
					{
						if (target.isTransactional)
						{
							if (block.transactionIsolationLevel != 0)
								target.startTransaction(block.transactionIsolationLevel);
							else
								target.startTransaction(this.defaultIsolationLevel);
						}
						else
						{
							this.logger.logWarning(logSubjectBlock + "Transaction mode is active but target is not able for transactional processing");
						}
					}

					// PreCondition prüfen
					this.logger.logInfo(String.Format("{0}check PreCondition Block", logSubjectBlock));
					DBInterface checkOn = (block.preCondition != null && block.preCondition.checkOn == "target" ? target : source);
					if (!await this.checkCondition(logSubjectBlock, block.preCondition, checkOn))
					{
						this.logger.logError(String.Format("{0}PreConditionCheck Block failed - {1}", logSubjectBlock, block.preCondition.condition + " on " + block.preCondition.select));
						return false;
					}

					// Jobs durchgehen
					this.logger.logInfo(String.Format("{0}process Jobs", logSubjectBlock));
					foreach (TransferTableJob tablejob in block.transferJobs)
					{
						await this.processTableJob(block, tablejob, source, target);
					}

					source.reinitLogSubject(logSubjectBlock);
					target.reinitLogSubject(logSubjectBlock);

					// Transaction comitten sofern nötig
					if (!block.disableTransaction)
					{
						if (target.isTransactional)
						{
							target.commitTransaction();
						}
						else
						{
							this.logger.logWarning(logSubjectBlock + "Transaction mode is active but target is not able for transactional processing");
						}
					}

					// Verbindung trennen
					this.logger.logInfo(String.Format("{0}Disconnecting", logSubjectBlock));
					source.disconnect();
					target.disconnect();
					this.logger.logInfo(String.Format("{0}Disconnected", logSubjectBlock));
				}

				this.logger.logInfo(String.Format(logSubjectBlock + "has finished processing", block.name));
				return true;
			}
			catch (Exception e)
			{
				this.logger.logError(String.Format(logSubjectBlock + "has aborted with failure - {1}", block.name, e.ToString()));
				return false;
				//throw e;
			}
		}


		/// <summary>
		/// Verarbeitet einen konkreten Job eines Transferblocks. Details siehe Methode mit Signatur (TransferBlock, TransferTableJob)
		/// </summary>
		/// <param name="blockName">Der Name des Transferblocks in dem sich der Job befindet</param>
		/// <param name="targetTable">Der Name der Zieltabelle des Tablejobs im Transferblock</param>
		/// <param name="source">Die Datenquelle wenn vorhanden - wird sonst explizit für diesen Job erzeugt/verbunden und getrennt</param>
		/// <param name="target">Das Datenziel wenn vorhanden - wird sonst explizit für diesen Job erzeugt/verbunden und getrennt</param>
		/// <returns>nichts</returns>
		public async Task processTableJob(string blockName, string targetTable, DBInterface source = null, DBInterface target = null)
		{
			await this.processTableJob(this[blockName], targetTable, source, target);
		}


		/// <summary>
		/// Verarbeitet einen konkreten Job eines Transferblocks. Details siehe Methode mit Signatur (TransferBlock, TransferTableJob)
		/// </summary>
		/// <param name="block">Der Transferblock in dem sich der Job befindet</param>
		/// <param name="targetTable">Der Name der Zieltabelle des Tablejobs im Transferblock</param>
		/// <param name="source">Die Datenquelle wenn vorhanden - wird sonst explizit für diesen Job erzeugt/verbunden und getrennt</param>
		/// <param name="target">Das Datenziel wenn vorhanden - wird sonst explizit für diesen Job erzeugt/verbunden und getrennt</param>
		/// <returns>nichts</returns>
		public async Task processTableJob(TransferBlock block, string targetTable, DBInterface source = null, DBInterface target = null)
		{
			await processTableJob(block, block[targetTable], source, target);
		}


		/// <summary>
		/// Verarbeitet einen konkreten Job eines Transferblocks. <br/>
		/// Sofern Verbindungen übergeben werden, werden diese verwendet und auch nicht wieder getrennt (aber vt. verbunden). 
		/// Werden keine Verbindungen übergeben, werden explizit welche erzeugt und nach Verarbeitung wieder getrennt. 
		/// Nicht übergebene Verbindungen sorgen auch für eine explizite Transaktion für den TableJob, sofern nicht im Transferblock unterdrückt. 
		/// Bei übergebenen Verbindungen wird mit der aktuell aktiven Transaktion gearbeitet <br/><br/>
		/// Die Verarbeitung des Jobs läuft in mehreren Schritten: <br/>
		/// PreCondition: PreCondition auf Source, ReadTable auf Source, PreStatement auf Target, Fill auf Target, PostStatment auf Target, Merge auf Target
		/// und ein evt. commit bei nicht übergebener Datenquelle
		/// </summary>
		/// <param name="block">Der Transferblock in dem sich der Job befindet</param>
		/// <param name="tableJob">Der Tablejobs im Transferblock</param>
		/// <param name="source">Die Datenquelle wenn vorhanden - wird sonst explizit für diesen Job erzeugt/verbunden und getrennt</param>
		/// <param name="target">Das Datenziel wenn vorhanden - wird sonst explizit für diesen Job erzeugt/verbunden und getrennt</param>
		/// <returns>nichts</returns>
		public async Task processTableJob(TransferBlock block, TransferTableJob tableJob, DBInterface source = null, DBInterface target = null)
		{

			string logSubject = String.Format("Transferblock '{0}' TargetTable '{1}': ", block.name, tableJob.targetTable);

			bool localSource = false;
			bool localTarget = false;

			byte retry = 0;
			byte maxRetry = 1;
			bool success = false;

			while (retry < maxRetry && !success) 
			{
				try
				{
					retry++;

					logSubject = String.Format("Transferblock '{0}' TargetTable '{1}' retry {2}: ", block.name, tableJob.targetTable, retry);
					this.logger.logInfo(String.Format("{0}has started procssing", logSubject));
					

					// Source und Target initialisieren falls nicht vorhanden
					if (source == null)
					{
						localSource = true;
						source = await this.initAndConnectDataSource(logSubject, block.conStringSourceType, block.conStringSource);
					}
					else
					{
						await source.connect();
						source.reinitLogSubject(logSubject);
					}
					if (target == null)
					{
						localTarget = true;
						target = await this.initAndConnectDataSource(logSubject, block.conStringTargetType, block.conStringTarget);
						if (!block.disableTransaction)
						{
							if (target.isTransactional)
							{
								if (block.transactionIsolationLevel != 0)
									target.startTransaction(block.transactionIsolationLevel);
								else
									target.startTransaction(this.defaultIsolationLevel);
							}
							else
							{
								this.logger.logWarning(logSubject + "Transaction mode is active but target is not able for transactional processing");
							}
						}
					}
					else
					{
						await target.connect();
						target.reinitLogSubject(logSubject);
					}

					// Variablen initialisieren
					await this.initVariableDeclarations(tableJob.variables, source, target);

					// PreCondition prüfen
					DBInterface checkOn = (tableJob.preCondition != null && tableJob.preCondition.checkOn == "target" ? target : source);
					if (!await this.checkCondition(logSubject, tableJob.preCondition, checkOn))
					{
						throw new Exception("PreConditionCheck Job failed - " + tableJob.preCondition.condition + " on " + tableJob.preCondition.select);
					}

					// LastModDateTime für SyncByLastMod-Synchronisation
					if (tableJob.SyncByLastMod)
					{
						tableJob.sync = false;

						this.initVar("maxLastMod", DateTime.Today, DbType.DateTime);
						
						DataTable lastModTable = await target.readSelect($"SELECT max({tableJob.syncByLastModOptions.SyncByLastModField}) as maxLastMod FROM {tableJob.targetTable};");
						if (lastModTable.Rows.Count > 0)
						{
							if (lastModTable.Rows[0]["maxLastMod"] is DBNull)
							{
								variables["maxLastMod"].value = new DateTime(1900, 1, 1); 
							}
							else
							{
								variables["maxLastMod"].value = (DateTime)lastModTable.Rows[0]["maxLastMod"];
							}
						}

						this.initVar("SYS_LastModWhereClause",
							$" {tableJob.syncByLastModOptions.SyncByLastModField} " + 
								(tableJob.syncByLastModOptions.SyncByLastModMode == SyncByLastModMode.APPEND_INCLUDE_MAXDATE ? ">=" : ">") + 
								"${{maxLastMod}}",
							  DbType.Int64); // int64 wird ohne '' ausgegeben - Trick für Klartextausgabe
					}

					// Quelltabelle einlesen - danach Quellverbindung auflösen da nicht mehr nötig
					DataTable dsSource = await this.readSourceTable(logSubject, tableJob, source);
                    if (tableJob.identicalColumns && tableJob.identicalColumnsSource == DBContextEnum.Source)
                    {
                        tableJob.columnMap = new TransferTableColumnList();
                        tableJob.columnMap.AddRange(dsSource.Columns.OfType<DataColumn>().Select((dc) => new TransferTableColumn() { sourceCol = dc.ColumnName, targetCol = dc.ColumnName }));
                        
                    }
					if (localSource)
					{
						source.disconnect();
					}

					// Falls maxRecordDiff vorhanden, muss geprüft werden ob die Abweichung größer erlaubt
					if (tableJob.maxRecordDiff > 0)
					{
						this.logger.logInfo(String.Format("{0}Check RecordDiff not bigger than {1}% for table {2}", logSubject, tableJob.maxRecordDiff, tableJob.targetTable));
						long targetCount = Convert.ToInt64((await target.readSelect("SELECT Count(*) FROM " + tableJob.targetTable + (!String.IsNullOrWhiteSpace(tableJob.targetSyncWhere) ? " WHERE " + tableJob.targetSyncWhere : "") )).Rows[0][0]);
						long sourceCount = dsSource.Rows.Count;

						double percent = -0d; // Default kompletter Datenverlust 100%
						if (targetCount != 0) percent = (((double)sourceCount / (double)targetCount) - 1.0d) * 100.0d;

						string growthType = (percent >= 0 ? "increase" : "decrease");
						this.logger.logInfo(String.Format("{0}Sourcecount {1} to Targetcount {2} {3} is {4}%", logSubject, sourceCount, targetCount, growthType, (targetCount==0?"infinite ":percent.ToString())));

						if (percent < 0 && Math.Abs(percent) > tableJob.maxRecordDiff)
						{
							//this.logger.logError(String.Format("{0}Decrease of {1}% is bigger than the allowed of {2}%", logSubject, Math.Abs(percent), tableJob.maxRecordDiff));
							throw new Exception(String.Format("Decrease of {0}% is bigger than the allowed of {1}%", Math.Abs(percent), tableJob.maxRecordDiff));
						}
						else
						{
							if (growthType == "decrease")
								this.logger.logInfo(String.Format("{0}Decrease of {1}% < {2}% - looks good", logSubject, Math.Abs(percent), tableJob.maxRecordDiff));
							else
							{
								this.logger.logInfo(String.Format("{0}Increase is {1}% - looks good", logSubject, (targetCount == 0 ? "infinite " : percent.ToString())));
							}
						}
					}

					// Zielverbindung nutzen um PreStatements durchzuführen
					await this.processStatementList(logSubject + "PreProcessing", tableJob.preStatement, target);

					// Delete
					if (tableJob.deleteBefore)
					{
						this.logger.logInfo(String.Format("{0}Delete rows from table '{1}'", logSubject, tableJob.targetTable));
						await target.deleteTableRows(tableJob.targetTable, resolveStmtVariables( tableJob.deleteWhere, target) );
					}

					// Delete für SyncByLastMod
					if (tableJob.SyncByLastMod && dsSource.Rows.Count > 0 )
					{
						if (tableJob.syncByLastModOptions.SyncByLastModMode == SyncByLastModMode.UPDATE_EXISTING)
						{
							// Finde Sync-Key in Quelldaten und lösche Einträge aus Ziel die enthalten sind - die aktualisiert werden sollen
							this.logger.logInfo(String.Format("{0}Delete rows from table '{1}' for Sync ByLastMod", logSubject, tableJob.targetTable));
							string keyName = tableJob.getKeys()[0].targetCol;
							string where = $"{keyName} in (";
							if (dsSource.Columns[keyName].DataType == typeof(string))
							{
								foreach (DataRow r in dsSource.Rows)
								{
									where += "'" + r[keyName] + "',";
								}
							}
							else
							{
								foreach (DataRow r in dsSource.Rows)
								{
									where += r[keyName] + ",";
								}
							}
							if (dsSource.Rows.Count > 0) where = where.Substring(0, where.Length - 1); // letztes Komma entfernen
							where += ")";
							int countDelete = await target.deleteTableRows(tableJob.targetTable, where);
							this.logger.logInfo(String.Format("{0}Deleted {1} rows from table '{2}' for Sync ByLastMod", logSubject, countDelete, tableJob.targetTable));
						}
						else if (tableJob.syncByLastModOptions.SyncByLastModMode == SyncByLastModMode.APPEND_INCLUDE_MAXDATE)
						{
							// Löschung des aktuellen Datums notwendig
							this.logger.logInfo(String.Format("{0}Delete rows from table '{1}' which are equal zo  max(LastModDate)", logSubject, tableJob.targetTable));
							int countDelete = await target.deleteTableRows(tableJob.targetTable, tableJob.syncByLastModOptions.SyncByLastModField + " = " + target.formatParameterAsValue(variables["maxLastMod"].value, variables["maxLastMod"].type));
							this.logger.logInfo(String.Format("{0}Deleted {1} rows from table '{1}' which are equal zo  max(LastModDate)", logSubject, countDelete, tableJob.targetTable));
						}
						else if (tableJob.syncByLastModOptions.SyncByLastModMode == SyncByLastModMode.APPEND)
						{
							// Keine Löschung notwendig
							this.logger.logInfo(String.Format("{0}Delete rows from table '{1}' for Sync ByLastMod not needed - in APPEND Mode - Just append", logSubject, tableJob.targetTable));
						}
					}

					// stures zufügen - evt. PK-Probleme - kein Datenabgleich
					await this.fillTargetTable(logSubject, dsSource, target, block, tableJob, (tableJob.identicalColumns ? null : tableJob.columnMap));

					// Zielverbindung nutzen um PostStatements durchzuführen
					await this.processStatementList(logSubject + "PostProcessing", tableJob.postStatement, target);

					// Merge
					if (tableJob.mergeOptions.merge)
					{
						await target.merge(tableJob.targetTable, tableJob.mergeOptions);
					}

					if (localTarget)
					{
						if (!block.disableTransaction)
						{
							if (target.isTransactional)
							{
								target.commitTransaction();
							}
							else
							{
								this.logger.logWarning(logSubject + "Transaction mode is active but target is not able for transactional processing");
							}
						}
						target.disconnect();
					}

					this.logger.logInfo(String.Format(logSubject + "has finished procssing", block.name, tableJob.targetTable));
					success = true;
				}
				catch (Exception e)
				{
					if (retry < maxRetry)
					{
						this.logger.logWarning(String.Format(logSubject + "has aborted processing with failure - {2}", block.name, tableJob.targetTable, e.ToString()));
					}
					else // oft genug probiert - Abbruch
					{
						this.logger.logError(String.Format(logSubject + "has aborted processing with failure - {2}", block.name, tableJob.targetTable, e.ToString()));
						throw;
					}
					
				}
				finally
				{
					if (localSource && source != null) source.Dispose();
					if (localTarget && target != null) target.Dispose();
				}
			}
		}


		/// <summary>
		/// Stellt eine Verbindung zur angegebenen Datenquelle her. Dabei wird der Logger des Transferbatches zur Datenquelle weitergegeben
		/// </summary>
		/// <param name="logSubject">Prefix für das Loggen</param>
		/// <param name="targetType">Der ADO-Treibertyp der verwendet werden soll</param>
		/// <param name="conString">Der ConnectionString zur Datenquelle der verwendet werden soll</param>
		/// <returns>Die erstellte und verbundene Datenquelle</returns>
		protected async Task<DBInterface> initAndConnectDataSource(string logSubject, string targetType, string conString)
		{
			this.logger.logInfo(String.Format("{0}Init and connect to target {0} - {1}", logSubject, targetType, conString));
			DBInterface target = DBInterface.getInterface(targetType, conString, this.logger);
			target.logSubject = logSubject + target.logSubject;
			await target.connect();
			return target;
		}


		/// <summary>
		/// Liest die Quelltabelle für einen TableJob aus der angegebenen Datenquelle ein 
		/// </summary>
		/// <param name="logSubject">Prefix für das Loggen</param>
		/// <param name="tableJob">Der TableJob in der die Quelltabelle benannt ist</param>
		/// <param name="source">Die Datenquelle gegen die die Anfrage ausgeführt werden soll</param>
		/// <returns>Daten der Tabelle als lokale DataTable</returns>
		protected async Task<DataTable> readSourceTable(string logSubject, TransferTableJob tableJob, DBInterface source)
		{
			this.logger.logInfo(String.Format("{0}read source", logSubject ));

			// eigenes Select
			if (!String.IsNullOrEmpty(tableJob.customSourceSelect)) 
			{
				string selectStmt = this.resolveStmtVariables(tableJob.customSourceSelect, source);
				if (tableJob.SyncByLastMod)
				{
					if (selectStmt.Contains("WHERE ")) selectStmt += " AND ";
					selectStmt += variables["SYS_LastModWhereClause"].value;
				}
				this.logger.logInfo(String.Format("{0}custom select '{1}'", logSubject, selectStmt));
				DataTable table = await source.readSelect(selectStmt);

				// Mappe Spalten in der lokalen Tabelle
				if (!tableJob.identicalColumns)
				{
					foreach (TransferTableColumn map in tableJob.columnMap)
					{
						if (table.Columns.Contains(map.sourceCol))
						{
							DataColumn dc = table.Columns[map.sourceCol];
							dc.ColumnName = map.targetCol;
						}
					}
					this.setKeyColumns(source, tableJob, table, tableJob.columnMap);
				}
				else
				{
					this.setKeyColumns(source, tableJob, table, tableJob.customKeys, true);
				}
				return table;

			}
			else
			{
				string whereClause = tableJob.sourceWhere;
				if (tableJob.SyncByLastMod) whereClause += (whereClause != null ? " AND ": "") + variables["SYS_LastModWhereClause"].value;

				// auto-select - Spaltenmapping erfolgt standardmäßig mit SELECT 
				this.logger.logInfo(String.Format("{0}table '{1}'", logSubject, tableJob.sourceTable));
				if (tableJob.identicalColumns) // identische Spalten
				{
					DataTable data = await source.readTable(this.resolveStmtVariables(tableJob.sourceTable, source), resolveStmtVariables(whereClause, source));
					this.setKeyColumns(source, tableJob, data, tableJob.customKeys, true);
					return data;
				}
				else // Spaltenangabe aus Mapping
				{
					DataTable data = await source.readTable(this.resolveStmtVariables(tableJob.sourceTable, source), resolveStmtVariables(whereClause, source), tableJob.columnMap);
					this.setKeyColumns(source, tableJob, data, tableJob.columnMap);
					return data;
				}
			}
		}

        /// <summary>
        /// Setzt die Key-Spalten in der Datentabelle data für einen Sync
        /// </summary>
        /// <param name="source">Typ der DB-Verbindung um auf Besonderheiten einzugehen</param>
        /// <param name="tableJob">Der Tabellenjob - ist sync nicht true, bricht die Methode ab</param>
        /// <param name="data">Die Datentabelle bei der die Schlüssel gesetzt werden sollen</param>
        /// <param name="ttcl">Die Liste von Spalten die als Keys gelten</param>
        /// <param name="isKeyList">Angabe das alle TransferTableColumnList-Einträge Keys sind</param>
        public void setKeyColumns(DBInterface source, TransferTableJob tableJob, DataTable data, TransferTableColumnList ttcl, bool isKeyList = false)
        {
			if (!tableJob.sync) return;

			if (ttcl == null) throw new ArgumentException("Sync is needed, but no Keys defined or identicalColumns with columnmap is set");

			int countKeys = ttcl.Count((ttc) => ttc.isKey || isKeyList);
			if (countKeys == 0) throw new ArgumentException("Sync is needed, but no Keys defined or identicalColumns with columnmap is set");

			int index = 0;
			DataColumn[] keys = new DataColumn[countKeys];
			foreach (TransferTableColumn ttc in ttcl)
			{
				if (ttc.isKey || isKeyList)
				{
					if (ttc.targetCol.Contains("[") && source.adoDriver == "System.Data.SqlClient")
					{
						string shortColName = ttc.targetCol.Replace("[", "").Replace("]", "");
						keys[index] = data.Columns[shortColName];
					}
					else
					{
						keys[index] = data.Columns[ttc.targetCol];
					}
					
					index++;
				}
			}
			data.PrimaryKey = keys;
		}


		/// <summary>
		/// Befüllt die Zieltabelle mit den übergebenen Daten
		/// </summary>
		/// <param name="logSubject">Prefix für das Loggen</param>
		/// <param name="data">Daten die in die Zieltabelle geschrieben werden sollen - Spaltennamen müssen übereinstimmen mit DataTable.</param>
		/// <param name="target">Ziel-Datenquelle in der sich die Zieltabelle befindet</param>
		/// <param name="colMap">Spaltenmapping falls eines verwendet wurde</param>
		/// <param name="block">Der aktuelle Block in dessen Kontext Fill aufegrufen wird</param>
		/// <param name="job">Der aktuelle Tablejob in dessen Kontext Fill aufegrufen wird</param>
		/// <returns>nichts</returns>
		protected async Task fillTargetTable(string logSubject, DataTable data, DBInterface target, TransferBlock block, TransferTableJob job, TransferTableColumnList colMap = null )
		{
			this.logger.logInfo(String.Format("{0}update target table {1}", logSubject, job.targetTable));
			if (job.SyncByLastMod)
			{
				job.targetSyncWhere = "1=0"; // TargetSyncWhere macht in dem Falle keinen Sinn
			}
			await target.fillTable(data, block, job, colMap);
			this.logger.logInfo(String.Format("{0}target table {1} updated", logSubject, job.targetTable));
		}


		/// <summary>
		/// Prüft Bedingungen auf einer Datenquelle und gibt das Ergebnis zurück - kann länger dauern da retries enthalten sind
		/// </summary>
		/// <param name="logSubject">Prefix für das Loggen</param>
		/// <param name="condition">Bedingung die geprüft werden soll</param>
		/// <param name="source">Datenquelle auf der die Bedingung geprüft wird</param>
		/// <returns>true wenn Bedingung erfolgreich ausgewertet werden konnte, sonst false. Wenn die Bedingung nicht definiert ist, ist das Ergebnis true</returns>
		protected async Task<bool> checkCondition(String logSubject, TransferTableCondition condition, DBInterface source){
			if (condition == null) return true;
			if (condition.select == null || condition.condition == null) return true;

			int retry = 0;
			bool evalResult = false;
			if (condition.retryCount == 0) condition.retryCount = 1; // wenn kein RetryCount angegeben, prüfe genau 1 mal

			// Wiederhole die Prüfung solange bis der retryCount ausgeschöpft ist oder die Bedingung als true evaluiert wurde
			while (retry < condition.retryCount && !evalResult)
			{
				retry++;
				this.logger.logInfo(String.Format("{0}retry {1} - check PreCondition '{2}' -> {3}",
				logSubject, retry, condition.select, condition.condition));

				// Ein Datensatz erwartet
				using (DataTable result = await source.readSelect(condition.select))
				{
					if (result.Rows.Count != 1) throw new ArgumentException(String.Format("{0}check PreCondition Select has not exactly one row", logSubject));
					DataRow condResult = result.Rows[0];

					// Prüfe Conditions - initial true, wenn eine falsch, gesamt falsch
					evalResult = true;
					foreach (string conditionPart in condition.condition.Split(';'))
					{
						this.logger.logInfo(String.Format("{0}Check condition '{1}'", logSubject, conditionPart));
						string[] condPart = conditionPart.Split(':');
						string colname = condPart[0];
						string conditionVal = condPart[1];
						if (condResult[colname].ToString().Trim() != conditionVal) // eine Subbedingung ist falsch - Gesamtevaluierung falsch
						{
							this.logger.logInfo(String.Format("{0}Condition '{1}' not passed - wait for {2} seconds", logSubject, conditionPart, condition.retryDelay));
							evalResult = false;
						}
						else
						{
							this.logger.logInfo(String.Format("{0}Condition '{1}' passed", logSubject, conditionPart));
						}
					}

					// wenn evaluierung fehlgeschlagen und noch Versuche ausstehen, warte das retryDelay in Sekunden ab
					if (!evalResult && retry < condition.retryCount) Thread.Sleep(1000 * condition.retryDelay);
				}
				
				
			}

			return evalResult;
		}


		/// <summary>
		/// Verarbeitet eine Liste von Statements in der angegebenen Datenquelle
		/// </summary>
		/// <param name="logSubject">Prefix für das Loggen</param>
		/// <param name="statementList">Liste von Textstatements</param>
		/// <param name="db">Die Datenquelle gegen die die Statements ausgeführt werden sollen</param>
		/// <returns>nichts</returns>
		protected async Task processStatementList(string logSubject, IEnumerable<String> statementList, DBInterface db)
		{
			if (statementList.Count() == 0) return;

			this.logger.logInfo(String.Format("{0}Processing statement", logSubject));
			foreach (String stmt in statementList)
			{
				string substStmt = this.resolveStmtVariables(stmt, db);
				this.logger.logInfo(String.Format("{0}Execute statement '{1}'", logSubject, substStmt));
				int result = await db.processStmt(substStmt);
				this.logger.logInfo(String.Format("{0}Execute statement '{1}' done # {2} rows processed", logSubject, substStmt, result));
			}
		}

		/// <summary>
		/// (Re)-Initialisiert eine Variable mit Werten. Das heißt sollte sie noch nicht existieren wird sie zugefügt und ansonsten überschrieben
		/// </summary>
		/// <param name="varName">Der Name der Variable</param>
		/// <param name="varValue">Der Wert der Variablen</param>
		/// <param name="varType">Der Typ der Variablen</param>
		protected void initVar(string varName, object varValue, DbType varType)
		{
			if (variables.ContainsKey(varName))
			{
				variables[varName] = new Variable() { value = varValue, type = varType };
			}
			else
			{
				variables.Add(varName, new Variable() { value = varValue, type = varType });
			}
		}

		/// <summary>
		/// Substituiert in einem String sämtliche Variablenvorkommen mit den dort gültigen Werten
		/// </summary>
		/// <param name="stmt">Der String wo Variablen substituiert werden sollen</param>
		/// <param name="db">Kontext für die Formatierung der Variablen je nach DB</param>
		/// <returns>Der Quellausdruck mit substituierten/ersetzten Variablen</returns>
		protected string resolveStmtVariables(string stmt, DBInterface db)
		{
			if (stmt == null) return null;

			string substStmt = stmt;
			MatchCollection matchCol = Regex.Matches(stmt, @"\$\{\{(\w+)\}\}");
			foreach (Match match in matchCol)
			{
				Variable v = null;
				if (variables.TryGetValue(match.Groups[1].Value, out v))
				{
					substStmt = substStmt.Replace(match.Groups[0].Value, db.formatParameterAsValue(v));
				}
				else
				{
					substStmt = substStmt.Replace(match.Groups[0].Value, "NotDefined");
				}
			}

			return substStmt;
		}

		/// <summary>
		/// Initialisiert Variablendeklarationen in das variables-Dictionary mit den 3 erlaubten Varianten Direktwert value, Select mit selectStmt und expression für Berechnungen
		/// </summary>
		/// <param name="declarations">Die Deklarationen der Variablen</param>
		/// <param name="source">Wird benötigt für Variablen die gegen die Quellverbindung initialisiert werden</param>
		/// <param name="target">Wird benötigt für Variablen die gegen die Zielverbindung initialisiert werden</param>
		/// <returns>Nichts</returns>
		protected async Task initVariableDeclarations(List<TransferTableVariableDeclaration> declarations, DBInterface source, DBInterface target)
		{
			if (declarations == null) return;

			foreach (TransferTableVariableDeclaration declaration in declarations)
			{
				try
				{
					if (!String.IsNullOrWhiteSpace(declaration.value))
					{
						this.initVar(declaration.name, declaration.value, declaration.type);
					}
					else if (!String.IsNullOrWhiteSpace(declaration.selectStmt))
					{
						DBInterface context = (declaration.selectContext == DBContextEnum.Source ? source : target);
						DataTable resultTab = await context.readSelect(this.resolveStmtVariables(declaration.selectStmt, context));
						object resultVal = null;
						if (resultTab.Rows.Count > 0)
						{
							resultVal = resultTab.Rows[0][0];
						}
						Type resultType = DBInterface.typeMap.First((tm) => tm.Value == declaration.type).Key;
						if (resultType != resultVal.GetType()) throw new ArgumentException($"Got type {resultVal.ToString()} but expected {resultType.ToString()} for Variable {declaration.name}");

						this.initVar(declaration.name, resultVal, declaration.type);
					}
					else if (!String.IsNullOrWhiteSpace(declaration.expression))
					{
						CodeElement elem = codeEvaluator.parse(declaration.expression);

						object resultVal = codeEvaluator.evaluate<object>(elem, this.batchDSL);
						this.initVar(declaration.name, resultVal, declaration.type);
					}
					else
					{
						throw new ArgumentException("Neither value, selectStmt or expression is set");
					}
				}
				catch(Exception e)
				{
					throw new Exception($"Variable {declaration.name} type {declaration.type.ToString()} has errors", e);
				}
			}
		}


		#region IDisposable Members

		/// <summary>Gibt an ob die Verbindung disposed ist</summary>
		private bool disposed = false;

		/// <summary> Gibt die Ressourcen des Objekts frei </summary>
		/// <param name="disposing">true, wenn durch Code ausgeführt, false wenn durch Garbage Collector</param>
		protected virtual void Dispose(bool disposing)
		{
			//Console.WriteLine("dispose tb " + this.logger.trace.Name );
			if (disposed) return;

			if (disposing) // nur wenn explizit disposed - Speicherfreigabe managed Objekte 
			{
				if (this.logger != null)
				{
					this.logger.Dispose();
				}
			}

			// immer ausführen - Speicherfreigabe unmanaged Objects

			//Console.WriteLine("dispose tb done " + this.logger.trace.Name);
			disposed = true;
		}

		/// <summary>UserCode-Dispose</summary>
		public void Dispose()
		{
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>Garbage Collector Finalizer</summary>
		~TransferBatch()
		{
			this.Dispose(false);
		}

		#endregion
		
	}
}
