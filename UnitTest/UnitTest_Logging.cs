using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using msa.Logging;

namespace UnitTest
{
	[TestClass]
	public class UnitTest_Logging
	{
		[TestInitialize]
		public void init()
		{
			/*ExeConfigurationFileMap configFileMap = new ExeConfigurationFileMap
				{
					ExeConfigFilename = "App.config"
				};
			*/
			//Configuration cfg = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
			
		}

		/// <summary>
		/// Testfall für das Einlesen eines ConfigSwitches
		/// </summary>
		[TestMethod]
		public void readSwitch()
		{
			SourceSwitch sourceSwitch = new SourceSwitch("TestLog");
			Console.WriteLine("TestLog = " + sourceSwitch.Level.ToString());
		}

		/// <summary>
		/// Testfall für das normale loggen
		/// </summary>
		[TestMethod]
		public void createLog()
		{
			using (Logger logger = Logger.getLogger("TestLog"))
			{
				// Füge Log zur Konsole hinzu - Lies aus app.Config aus
				logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener() { Writer = System.Console.Out });

				logger.logCritical("Katastrophe");
				logger.logInfo("doch nicht so schlimm");
				logger.logVerbose("kommt nicht an da app.config so konfiguriert das es nur bis Info funktioniert");
			}
		}

		/// <summary>
		/// Testfall für das Dateilogging mit paralleler Verarbeitung
		/// </summary>
		[TestMethod]
		public void createFileLogParallel()
		{
			Logger loggerGlob = Logger.getLogger("TestLog");
			loggerGlob.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener() { Writer = System.Console.Out });
			loggerGlob.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener("testParallel.log"));
			loggerGlob.logInfo("StartParallelLogging");

			Random r = new Random();

			Parallel.For(1, 5, 
				(i) => 
				{
					Thread.Sleep(r.Next(4000));
					Logger logger = Logger.getLogger("TestLog");
					logger.logInfo("LogThread " + i);
					Thread.Sleep(r.Next(4000));
					logger.logInfo("LogThread " + i + " - 2");
				}
			);

			loggerGlob.Dispose();
		}

		/// <summary>
		/// Testfall für loggen mit logLevel-Anpassung im Code
		/// </summary>
		[TestMethod]
		public void createLogWithVerbose()
		{
			using (Logger logger = Logger.getLogger("TestLog"))
			{
				// Füge Log zur Konsole hinzu
				logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener() { Writer = System.Console.Out });

				// Setze Loglevel manuell auf Verbose
				logger.logLevel = SourceLevels.Verbose;

				logger.logVerbose("LogLevel manuell angepasst - Verbose kommt an");
			}
		}

		/// <summary>
		/// Testfall für loggen mit MailSettings und automatischen Mailversand im Fehlerfall
		/// </summary>
		[TestMethod]
		public void testMailOnError()
		{
			using (Logger logger = Logger.getLogger("TestLog"))
			{
				// Füge Log zur Konsole hinzu
				logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener() { Writer = System.Console.Out });

				logger.logInfo("Init MailSettings");

				logger.mailSettings = new msa.Logging.Model.MailSettings()
				{
					smtpServer = "smtpserver",
					sendFrom = "testadresse@test.de",
					message = "Die Nachricht kommt genau hier: <message> <br/> Danach steht auch was",
					subject = "msa.Logging-Unittest",
					sendMailOnErrorLevel = TraceEventType.Error,
					sendTo = new List<string>() { "recipient@test.de"}
				};

				logger.logInfo("Test Info");
				logger.logWarning("Test Warning");
				logger.logError("Test Error");
				logger.logCritical("Test Crititcal");
			}
		}


		/// <summary>
		/// Testfall für loggen mit MailSettings und automatischen Mailversand im Fehlerfall ohne Using Statement
		/// </summary>
		[TestMethod]
		public void testMailOnErrorWoUsing()
		{
			Logger logger = Logger.getLogger("TestLog");
			// Füge Log zur Konsole hinzu
			logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener() { Writer = System.Console.Out });

			logger.logInfo("Init MailSettings");

			logger.mailSettings = new msa.Logging.Model.MailSettings()
			{
				smtpServer = "smtpserver",
				sendFrom = "testadresse@test.de",
				message = "Die Nachricht kommt genau hier: <message> <br/> Danach steht auch was",
				subject = "msa.Logging-Unittest",
				sendMailOnErrorLevel = TraceEventType.Error,
				sendTo = new List<string>() { "recipient@test.de" }
            };

			logger.logInfo("Test Info");
			logger.logWarning("Test Warning");
			logger.logError("Test Error");
			logger.logCritical("Test Crititcal");
		}

		/// <summary>
		/// Testfall für loggen mit MailSettings und automatischen Mailversand im Fehlerfall
		/// </summary>
		[TestMethod]
		public void testMailOnFinalizer()
		{
			Logger logger = Logger.getLogger("TestLog");
			// Füge Log zur Konsole hinzu
			logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener() { Writer = System.Console.Out });

			logger.logInfo("Init MailSettings");

			logger.mailSettings = new msa.Logging.Model.MailSettings()
			{
                smtpServer = "smtpserver",
                sendFrom = "testadresse@test.de",
                message = "Die Nachricht kommt genau hier: <message> <br/> Danach steht auch was",
                subject = "msa.Logging-Unittest",
                sendMailOnErrorLevel = TraceEventType.Error,
                sendTo = new List<string>() { "recipient@test.de" }
            };

			logger.logInfo("Test Info");
			logger.logWarning("Test Warning");
			logger.logError("Test Error");
			logger.logCritical("Test Crititcal");
		}

		/// <summary>
		/// Testfall für loggen mit MailSettings und automatischen Mailversand im Fehlerfall
		/// </summary>
		[TestMethod]
		public void testMailWithLogOnFinalizer()
		{
			Logger logger = Logger.getLogger("TestLog");
			// Füge Log zur Konsole hinzu
			logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener() { Writer = System.Console.Out });
			logger.addFileTraceListener("testFile2.log");
			logger.logInfo("Init MailSettings");

			logger.mailSettings = new msa.Logging.Model.MailSettings()
			{
                smtpServer = "smtpserver",
                sendFrom = "testadresse@test.de",
                message = "Die Nachricht kommt genau hier: <message> <br/> Danach steht auch was",
                subject = "msa.Logging-Unittest",
                sendMailOnErrorLevel = TraceEventType.Error,
                sendTo = new List<string>() { "recipient@test.de" }
            };

			logger.logInfo("Test Info");
			logger.logWarning("Test Warning");
			logger.logError("Test Error");
			logger.logCritical("Test Crititcal");
		}

		/// <summary>
		/// Testfall für loggen mit MailSettings und automatischen Mailversand im Fehlerfall
		/// </summary>
		[TestMethod]
		public void testMailOnErrorWithLog()
		{
			using (Logger logger = Logger.createNewLoggerInstance("TestLog2", "testFile.log"))
			{
				// Füge Log zur Konsole hinzu
				logger.logInfo("Init MailSettings");

				logger.mailSettings = new msa.Logging.Model.MailSettings()
				{
                    smtpServer = "smtpserver",
                    sendFrom = "testadresse@test.de",
                    message = "Die Nachricht kommt genau hier: <message> <br/> Danach steht auch was",
                    subject = "msa.Logging-Unittest",
                    sendMailOnErrorLevel = TraceEventType.Error,
                    sendTo = new List<string>() { "recipient@test.de" }
                };

				logger.logError("Test Error");
			}
		}


		/// <summary>
		/// Testfall für Verwendung eines anderen Formats
		/// </summary>
		[TestMethod]
		public void testLogFormat()
		{
			using (Logger logger = Logger.getLogger("TestLog"))
			{
				logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener() { Writer = System.Console.Out });

				logger.formatMessageEvent = ((m) => "Nachricht: " + m);
				logger.formatMessageWithLogLevelEvent = ((ll,m) => "LogSeverity = " + ll.ToString() + ": " + m);

				logger.logInfo("TestInfo");
				logger.logWarning("Test Warnung");
				logger.log(TraceEventType.Start, "Ich starte");
			}
		}


		[TestMethod]
		public void testLogMailWithAuth()
		{
			Logger logger = Logger.getLogger("TestLog");
			// Füge Log zur Konsole hinzu
			logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener() { Writer = System.Console.Out });

			logger.logInfo("Init MailSettings");

			logger.mailSettings = new msa.Logging.Model.MailSettings()
			{
                smtpServer = "smtpserver",
                sendFrom = "testadresse@test.de",
                message = "Die Nachricht kommt genau hier: <message> <br/> Danach steht auch was",
                subject = "msa.Logging-Unittest",
                sendMailOnErrorLevel = TraceEventType.Error,
                sendTo = new List<string>() { "recipient@test.de" },
                smtpUser = "user",
				smtpPassword = "FalschesPasswort"
			};

			logger.logInfo("Test Info");
			logger.logWarning("Test Warning");
			logger.logError("Test Error");
			logger.logCritical("Test Crititcal");
		}

		/// <summary>
		/// Listet die Text- und Zahlenwerte des TraceEventType-Enums
		/// </summary>
		[TestMethod]
		public void traceEventType()
		{
			foreach (string val in Enum.GetNames(typeof(TraceEventType)))
			{
				Console.WriteLine(val + " = " + (int)Enum.Parse(typeof(TraceEventType), val));
			}
		}

	}
}
