using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using msa.Data.Transfer;
using msa.Logging;
using System.Collections.Generic;

namespace UnitTest
{
	/// <summary> Testklasse für Initialisierung - Folgetests sind sinnlos wenn diese Tests scheitern </summary>
	[TestClass]
	public class InitTest
	{
		/// <summary> Logger für Verarbeitung von Tests</summary>
		Logger logger = Logger.getLogger("Test");

		/// <summary> Basisinitialisierung Logger </summary>
		[TestInitialize]
		public void init()
		{
			logger = Logger.getLogger("Test");
			logger.autoFlush = true;
			logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(System.Console.Out));
		}

		/// <summary> Test für das Einlesen der Config-Datei </summary>
		[TestMethod]
		public void testInit()
		{
			logger.logInfo("Init job.xml");
			TransferBatch batch = new TransferBatch("job.xml", logger);
			logger.logInfo("Init job.xml done");

            List<string> x = new List<string>();
            Dictionary<string, string> test = new Dictionary<string, string>();
		}
	}
}
