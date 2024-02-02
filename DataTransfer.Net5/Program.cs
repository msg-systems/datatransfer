using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using IBM.Data.Db2;
using msa.Data.Transfer.Database;
using msa.Data.Transfer.Model;
using msa.Logging;

namespace msa.Data.Transfer
{
	internal class Program
	{
		public static Exception occuredException = null;
        public static CultureInfo defaultCulture = CultureInfo.GetCultureInfo("EN-US");

        /// <summary>
        /// Liest einen Parameter von der Kommandozeile ein
        /// </summary>
        /// <typeparam name="T">Der erwartete Typ etwa int, bool oder string</typeparam>
        /// <param name="argList">Alle Kommandozeilenparameter</param>
        /// <param name="flagName">Der name des Flags an dem der Parameter erkannt wird</param>
        /// <param name="defaultValue">Defaultwert wenn der Parameter nicht angegeben ist</param>
        /// <param name="required">Angabe ob der Parameter Pflicht ist</param>
        /// <param name="flagBehavior">Gibt an das der Parameter selbst keinen Wert hat, sondern nur da/nicht da sein kann (bool)</param>
        /// <returns>Der ausgelesene Parameterwert</returns>
        public static T readParam<T>(List<String> argList, string flagName, T defaultValue, bool required = true, bool flagBehavior = false)
		{
			int index = argList.IndexOf(flagName);
			if (index == -1)
			{
				if (required && defaultValue == null)
				{
					Console.WriteLine("Required Flag {0} not set", flagName);
					Environment.Exit(255);
				}
				else
				{
					return defaultValue;
				}
			}

			if (flagBehavior)
			{
				return (T)Convert.ChangeType(true, typeof(T));
			}

			if (argList.Count <= index + 1)
			{
				Console.WriteLine("Value omitted -> Flag {0}", flagName);
				Environment.Exit(255);
			}
			string value = argList[index + 1];
			if (value.StartsWith("-"))
			{
				Console.WriteLine("Value omitted -> Flag {0}", flagName);
				Environment.Exit(255);
			}
			if (typeof(T) == typeof(System.Int32))
				return (T)Convert.ChangeType(Int32.Parse(value), typeof(Int32));
			else if (typeof(T) == typeof(System.Boolean))
				return (T)Convert.ChangeType(Boolean.Parse(value), typeof(Boolean));
			else
				return (T)Convert.ChangeType(value, typeof(T));
		}



		/// <summary>
		/// Initialisiert Commandline-Parameter
		/// </summary>
		/// <param name="args"></param>
		private static void Main(string[] args)
		{
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // ProviderFactory muss ab .Net5 selbst gefüllt werden!
            DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);
            DbProviderFactories.RegisterFactory("IBM.Data.DB2", IBM.Data.Db2.DB2Factory.Instance);
            DbProviderFactories.RegisterFactory("MySql.Data.MySqlClient", MySql.Data.MySqlClient.MySqlClientFactory.Instance);
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);
			DbProviderFactories.RegisterFactory("System.Data.OracleClient", Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance);
            DbProviderFactories.RegisterFactory("Oracle.ManagedDataAccess.Client.OracleClient", Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DbProviderFactories.RegisterFactory("System.Data.OleDb", System.Data.OleDb.OleDbFactory.Instance);
            }
            // evt. dynamisch aus einer Config auslesen mit 
            /*
            Assembly assContext = Assembly.LoadFrom("Oracle.ManagedDataAccess.dll");
			TypeInfo typeContext = assContext.DefinedTypes.Where((dt => dt.IsClass && dt.FullName == "Oracle.ManagedDataAccess.Client.OracleClientFactory")).FirstOrDefault();
            DbProviderFactory contextFactory = (DbProviderFactory)typeContext.GetField("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
			DbProviderFactories.RegisterFactory("Oracle.ManagedDataAccess.Client.OracleClient", contextFactory);
			*/

            // Ende neu

            // Transformiere Flags zu Lowercase - Parameter selbst müssen bleiben wie sie sind, vor allem das Passwort!
            List<String> argList = new List<string>();
			args.ToList().ForEach((arg) => argList.Add(arg.StartsWith("-") ? arg.ToLower() : arg));

			// Hilfe
			if (argList.Contains("-h") || argList.Contains("-?"))
			{
				Console.WriteLine("Transfer program for ADO data sources to other ADO data sources.\n");
				Console.WriteLine("\n");
				Console.WriteLine("Valid options\n");
				Console.WriteLine("-f \t\t[text]  InputFile(s) to process - Delmiter is , for example file1.xml,file2.xml");
				Console.WriteLine("-b \t\t[text]  Block to process - Delmiter is , for example block1,block2");
				Console.WriteLine("-j \t\t[text]  TargetTable to process in the Block - Delmiter is , for example tab1,tab2 - only valid in combination with -b");
				Console.WriteLine("-l \t\t[text]  Logfilename - Default global_[yyyy.MM.dd_HH.mm.ss].log");
				Console.WriteLine("-sl\t\t[flag]  Use Sublogs - creates for every inputfile an own log file - Default false");
				Console.WriteLine("-s \t\t[flag]  Starts named files and blocks in predefined order from command line (as delmited with comma)");
                Console.WriteLine("-d \t\t[flag]  Starts in debugging mode and does not create mails");
                Console.WriteLine("-h/-? \t\t[flag]  Shows this help page");
				Console.WriteLine("\n");
				Console.WriteLine("If the loglevel for msa.Data.Transfer.TransferBatch is set to verbose in the config file, batchprocessing is deactivated and every single statement will be logged.");
				Console.WriteLine("\n");
				Console.WriteLine("Copyright 2024, msg systems ag, Martin Ehlert");
                Console.WriteLine("Apache 2.0 licensed");

                Environment.Exit(0);
			}

			// Parameter einlesen
			string inputFileList = readParam<String>(argList, "-f", null, false);
			string blockList = readParam<String>(argList, "-b", null, false);
			string jobList = readParam<String>(argList, "-j", null, false);
			string logFile = readParam<String>(argList, "-l", null, false);
			bool useSublogs = readParam<bool>(argList, "-sl", false, flagBehavior: true);
			bool useSequential = readParam<bool>(argList, "-s", false, flagBehavior: true);
            bool debugMode = readParam<bool>(argList, "-d", false, flagBehavior: true);

            if (blockList == null && jobList != null)
			{
				Console.WriteLine("Used Parameter -j without -b - thats disallowed");
				Environment.Exit(255);
			}

			Directory.CreateDirectory("Log");
			if (logFile == null) logFile = @"Log\global_" + DateTime.Now.ToString("yyyy.MM.dd_HH.mm.ss") + ".log";

			// Verarbeiten
			using (Logger log = Logger.createNewLoggerInstance("msa.Data.Transfer.TransferBatch", logFile))
			{			
				//log.trace.Listeners.Add(new ConsoleTraceListener());

				//log.trace.Listeners.Add(new TextWriterTraceListener( logFile ));
				log.autoFlush = true;

				//log.logVerbose("TestVerbose");

                if(debugMode){
                    log.logInfo($"debugmode = {debugMode}");
                }
                log.logInfo($"Machine name: { System.Environment.MachineName}\nPath: { System.AppDomain.CurrentDomain.BaseDirectory}");
				log.logInfo("Start Batch - check Params");

				// Parameterübergabe von Konsole erlauben
				List<String> inputFiles = new List<string>();
				if (inputFileList == null){
					inputFiles.Add("job.xml");
				}
				else{
					inputFiles.AddRange( inputFileList.Split(',') );
				}

				if (useSequential)
				{
					// Verarbeitung Files
					foreach (string targetFile in inputFiles){
						log.logInfo("Process configFile '" + targetFile + "'");

						log.logInfo("Init TransferBatch and read config");

						// Initialiser Batch mit eigenem oder vorhandenen Log
						using (TransferBatch batch = (useSublogs ? new TransferBatch(targetFile) : new TransferBatch(targetFile, log)))
                        {
                            batch.logger.autoFlush = true;
                            if (debugMode)
                            {
                                initDebugMode(log, batch);
                            }

                            log.logInfo("Init done - process Jobs");

                            // Entscheidung ob alles oder nur einzelne Blöcke
                            if (blockList == null)
                            {
                                processCompleteFile(batch, log);
                            }
                            else
                            {
                                // einzelne Blöcke verarbeiten
                                foreach (string block in blockList.Split(','))
                                {
                                    // Einzelne Jobs einzelner Blöcke oder ganzer Block
                                    if (jobList == null)
                                    {
                                        // ganzer Block
                                        log.logInfo("process block " + block);
                                        try
                                        {
                                            Task<bool> blockJob = Task.Run(
                                                new Func<Task<bool>>(async () =>
                                                { return await batch.processTransferBlock(block); }
                                                )
                                            );
                                            blockJob.Wait();
                                            if (!blockJob.Result)
                                            {
                                                Environment.ExitCode = 255;
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            log.logCritical("Error: " + e.ToString());
                                            Environment.ExitCode = 255;
                                        }

                                        /*
										Task.Run(async () =>
										{
											try
											{
												if (!await batch.processTransferBlock(block))
												{
													Environment.ExitCode = 255;
												}
											}
											catch (Exception e)
											{
												log.logCritical("Error: " + e.ToString());
												Environment.ExitCode = 255;
											}
										}).Wait();*/
                                    }
                                    else
                                    {
                                        // einzelner Job in einem Block
                                        foreach (string subjob in jobList.Split(','))
                                        {
                                            try
                                            {
                                                processTableJob(batch, block, subjob, log);
                                            }
                                            catch (Exception e)
                                            {
                                                log.logCritical("Error: " + e.ToString());
                                                Environment.ExitCode = 255;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
				}
				else
				{
					Parallel.ForEach(inputFiles, (targetFile) =>
					{
						log.logInfo("Process configFile '" + targetFile + "'");
						log.logInfo("Init TransferBatch and read config");

						// Initialiser Batch mit eigenem oder vorhandenen Log
						using (TransferBatch batch = (useSublogs ? new TransferBatch(targetFile) : new TransferBatch(targetFile, log)))
						{
                            if (debugMode)
                            {
                                initDebugMode(log, batch);
                            }

                            if (blockList == null)
							{
								processCompleteFile(batch, log);
							}
							else // einzelne Blöcke Parrallel verarbeiten
							{
								// Einzelne Jobs einzelner Blöcke oder ganzer Block
								string[] blockListArray = blockList.Split(',');
								if (jobList == null)
								{
									log.logInfo("process blocks " + blockList);
									try { 
										if (!batch.processTransferBlocks(blockListArray))
										{
											Environment.ExitCode = 255;
										}
									}
									catch (Exception e)
									{
										log.logCritical("Error: " + e.ToString());
										Environment.ExitCode = 255;
									}
								}
								else
								{
									foreach (string subjob in jobList.Split(',')) // es kann nur einen Block geben
									{
										try
										{
											processTableJob(batch, blockList, subjob, log);
										}
										catch (Exception e)
										{
											log.logCritical("Error: " + e.ToString());
											Environment.ExitCode = 255;
										}
									}
								}
								
							}
						}

					});
				}

				//Console.ReadLine();
				log.logInfo("Processing done");
			}

		}

        /// <summary> Konfiguriere den aktuellen Batch in den Verbose-Modus um </summary>
        /// <param name="log">der aktuelle Log der umkonfiguriert werden soll</param>
        /// <param name="batch">Der aktuelle Batch der umkonfiguriert werden soll</param>
        private static void initDebugMode(Logger log, TransferBatch batch)
        {
            
                // TODO
                batch.config.settings.mailSettings = null;

                // Debugmodus anschalten
                foreach (TransferBlock b in batch.config.transferBlocks)
                {
                    b.targetMaxBatchSize = 1;
                }
                batch.logger.logLevel = SourceLevels.Verbose;
        }


        /// <summary>
        /// Bearbeitet einen kompletten Batch mit allen TransferBlocks. Setzt im Fehlerfall den Return Code der Anwendung auf 255.
        /// </summary>
        /// <param name="batch">Der auszuführende Batch</param>
        /// <param name="log">Der zu verwendende Logger</param>
        /// <returns>true wenn alles erfolgreich verlief, sonst false</returns>
        public static bool processCompleteFile(TransferBatch batch, Logger log)
		{
			// Entscheidung ob alles oder nur einzelne Blöcke

			bool result = false;
			try
			{
				// alles
				result = batch.processAllTransferBlocks();
				if (!result)
				{
					Environment.ExitCode = 255;
				}
			}
			catch (AggregateException e)
			{
				foreach (Exception ex in e.InnerExceptions)
				{
					log.logCritical("Error: " + ex.ToString());
				}
				Environment.ExitCode = 255;
			}
			return result;
		}


		/// <summary>
		/// Verarbeitet einen einzelnen Tabellenjob in einem Transferblock. Setzt im Fehlerfall den Return Code der Anwendung auf 255.
		/// </summary>
		/// <param name="batch">Die batch mit dem Transferblock</param>
		/// <param name="block">Name des Transferblocks</param>
		/// <param name="subjob">Name der Zieltabelle des Tabellenjobs</param>
		/// <param name="log">Der zu verwendende Logger</param>
		/// <returns>true bei Erfolg, sonst false</returns>
		public static bool processTableJob(TransferBatch batch, string block, string subjob, Logger log)
		{
			log.logInfo("process block " + block + " targetTable " + subjob);
			Task t = batch.processTableJob(block, subjob);
			t.Wait();
			if (t.Exception != null)
			{
				log.logCritical("Error: " + t.ToString());
				Environment.ExitCode = 255;
				return false;
			}
			else
			{
				return true;
			}
		}

		/// <summary>
		/// Errorhandler falls irgendwo ein Fehler nicht behandelt sein sollte - loggt zur Konsole und versucht eine Mail zu senden
		/// </summary>
		/// <param name="sender">leer</param>
		/// <param name="e">Die aufgetretene Ausnahme</param>
		static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Console.WriteLine("Critical unhandled Exception: " + e.ExceptionObject.ToString());

			// Versuche Mailversand
			try
			{
				/* Das ist msg intern - evt. über app.config konfigurierbar machen
			    Logging.Model.MailSettings mailSettings = new Logging.Model.MailSettings()
				{
					smtpServer = "mailServer",
					sendTo = new List<String>(){"DefaultRecipient"},
					sendFrom = "sendFrom@test.de", 
                    subject = "Critical unhandled Exception",
					message = "<message>"
				};
				mailSettings.sendMail($"Machine name: {System.Environment.MachineName}\nPath: {System.AppDomain.CurrentDomain.BaseDirectory}\nCritical unhandled Exception: " + e.ExceptionObject.ToString());
				*/
			}
			catch (Exception ex) { System.Console.WriteLine("MailSend failed " + ex.Message); }
			GC.Collect(100, GCCollectionMode.Forced, true);
			System.Environment.Exit(255);
		}
	}
}