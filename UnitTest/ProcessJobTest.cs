using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using msa.Data.Transfer;
using msa.Logging;
using System.Linq;
using System.Collections;
using msa.Data.Transfer.Model;

namespace UnitTest
{
	/// <summary> Test für Verarbeitung von Transferblöcken in verschiedenen Szenarien </summary>
	[TestClass]
	public class ProcessJobTest
	{

        /// <summary> Logger für Verarbeitung von Tests</summary>
        private Logger logger = Logger.getLogger("Test");


        /// <summary> Initialisierte Batchdatei mit den Test Transferblocks </summary>
#pragma warning disable CS8618 // Ein Non-Nullable-Feld muss beim Beenden des Konstruktors einen Wert ungleich NULL enthalten. Erwägen Sie die Deklaration als Nullable.
        private TransferBatch batch;
#pragma warning restore CS8618 

        /// <summary> Initialisiert die Testbatch für die Tests </summary>
        [TestInitialize]
		public void init()
		{
			logger = Logger.getLogger("Test");
			logger.autoFlush = true;
			logger.trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(System.Console.Out));
			batch = new TransferBatch("job.xml", logger);
		}

		/// <summary> Test Datentransfer Lotus zu MSSQL </summary>
		[TestMethod]
		public async Task processLDAPToCSV()
		{
			if (!await batch.processTransferBlock("LDAPBatch")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer LDAP zu CSV </summary>
		[TestMethod]
		public async Task processLDAPCustomToCSV()
		{
			if (!await batch.processTransferBlock("LDAPBatchCustom")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer Lotus zu MSSQL </summary>
		[TestMethod]
		public async Task processLotusToMSSQL()
		{
			if (!await batch.processTransferBlock("LotusBatch")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer DB2 zu CSV </summary>
		[TestMethod]
		public async Task processDB2ToCSV()
		{
			if (!await batch.processTransferBlock("CSV-Transfer")) throw new Exception("failed");
		}

		[TestMethod]
		public async Task processJSONToCSV_ProtocolSource()
		{
			if (!await batch.processTransferBlock("JSON_CSV-Transfer_Protocol")) throw new Exception("failed");
		}

		[TestMethod]
		public async Task processNotesXMLToCSV_ProtocolSource()
		{
			if (!await batch.processTransferBlock("NotesXML_CSV-Transfer_Protocol")) throw new Exception("failed");
		}

		[TestMethod]
		public async Task processNotesXMLToDB2_ProtocolSource()
		{
			if (!await batch.processTransferBlock("NotesXML_DB2-Transfer_Protocol")) throw new Exception("failed");
		}

		[TestMethod]
		public async Task processNotesXMLToDB2_ProtocolSource2()
		{
			if (!await batch.processTransferBlock("NotesXML_DB2-Transfer_Protocol2")) throw new Exception("failed");
		}

		[TestMethod]
        public async Task processCSVToCSV_ProtocolSource()
        {
            if (!await batch.processTransferBlock("CSV_CSV-Transfer_Protocol")) throw new Exception("failed");
        }

        [TestMethod]
        public async Task processCSVToCSV_ProtocolSourceHttp()
        {
            if (!await batch.processTransferBlock("CSV_CSV-Transfer_ProtocolHttp")) throw new Exception("failed");
        }

        /// <summary> Test Datentransfer CSV zu CSV Custom-SQL-Parser </summary>
        [TestMethod]
        public async Task processCSVToCSV_NamedSingleWhere()
        {
            if (!await batch.processTransferBlock("CSV_CSV-Transfer_NamedSingleWhere")) throw new Exception("failed");
        }

        /// <summary> Test Datentransfer CSV zu CSV Custom-SQL-Parser </summary>
		[TestMethod]
        public async Task processCSVToCSV_NonNamedSingleWhere()
        {
            if (!await batch.processTransferBlock("CSV_CSV-Transfer_NonNamedSingleWhere")) throw new Exception("failed");
        }

        /// <summary> Test Datentransfer CSV zu CSV Custom-SQL-Parser </summary>
		[TestMethod]
        public async Task processCSVToCSV_NamedSingleWhereWithNonSelectCols()
        {
            if (!await batch.processTransferBlock("CSV_CSV-Transfer_NamedSingleWhereWithNonSelectCols")) throw new Exception("failed");
        }

        /// <summary> Test Datentransfer CSV zu CSV Custom-SQL-Parser </summary>
		[TestMethod]
        public async Task processCSVToCSV_MultiJoinWhereWithNonSelectCols()
        {
            if (!await batch.processTransferBlock("CSV_CSV-Transfer_MultiJoinWhereWithNonSelectCols")) throw new Exception("failed");
        }

        /// <summary> Test Datentransfer CSV zu CSV Custom-SQL-Parser </summary>
		[TestMethod]
        public async Task processCSVToCSV_MultiJoinWhereWithNonSelectColsAndMultiComp()
        {
            if (!await batch.processTransferBlock("CSV_CSV-Transfer_MultiJoinWhereWithNonSelectColsAndCompMulti")) throw new Exception("failed");
        }

        /// <summary> Test Datentransfer CSV zu CSV Custom-SQL-Parser </summary>
		[TestMethod]
        public async Task processCSVToCSV_SourceTableSourceWhere()
        {
            if (!await batch.processTransferBlock("CSV_CSV-Transfer_SourceTableSourceWhere")) throw new Exception("failed");
        }

        /// <summary> Test Datentransfer DB2 zu JSON </summary>
		[TestMethod]
        public async Task processDB2ToJSON()
        {
            if (!await batch.processTransferBlock("JSON-Transfer")) throw new Exception("failed");
        }

        /// <summary> Test Datentransfer Lotus zu MSSQL </summary>
        [TestMethod]
		public async Task processMSSQLToLotus()
		{
            // Funktioniert aufgrund mangelndem Ziel nicht ( auf Forms mit Feldnamen und Feld muss Editable sein )
            batch.logger.logLevel = System.Diagnostics.SourceLevels.Verbose;
			TransferBlock? block = batch.config.transferBlocks.FirstOrDefault((block) => block.name == "LotusBatch2");
            if (block != null) block.targetMaxBatchSize = 1;
            if (!await batch.processTransferBlock("LotusBatch2")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer Access zu MSSQL </summary>
		[TestMethod]
		public async Task processAccessToMSSQL()
		{
			if (!await batch.processTransferBlock("AccessBatch")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer Excel zu MSSQL </summary>
		[TestMethod]
		public async Task processExcelToMSSQL()
		{
			if (!await batch.processTransferBlock("ExcelBatch")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer MySQL zu MSSQL </summary>
		[TestMethod]
		public async Task processMySQLToMSSQL()
		{
			if (!await batch.processTransferBlock("MySqlTest")) throw new Exception("failed");
		}

		/// <summary> Test Performance Test DB2 zu MSSQL ca. 50.000 Datensätze </summary>
		[TestMethod]
		public async Task processDB2ToMSSQLPerformanceTest()
		{
			if (!await batch.processTransferBlock("PerfTest")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer DB2 zu MSSQL mit einem custom Select und einem Spaltenmapping </summary>
		[TestMethod]
		public async Task processDB2ToMSSQLCustomSelectAndMapping()
		{
			if (!await batch.processTransferBlock("CustomMapping")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer DB2 zu MSSQL mit Folgemerge in MSSQL </summary>
		[TestMethod]
		public async Task processDB2ToMSSQLMerge()
		{
			if (!await batch.processTransferBlock("MergeTest")) throw new Exception("failed");
		}

        /// <summary> Test Datentransfer DB2 zu MSSQL mit Folgemerge in MSSQL </summary>
		[TestMethod]
        public async Task processDB2ToMSSQLSync()
        {
            if (!await batch.processTransferBlock("SyncTest")) throw new Exception("failed");
        }

        /// <summary> Test Datentransfer DB2 zu MSSQL mit einer Vorabbedingung </summary>
        [TestMethod]
		public async Task processDB2ToMSSQLCondition()
		{
			if (!await batch.processTransferBlock("ConditionTest")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer DB2 zu MSSQL mit einem DPAPI verschlüsselten Passwort für den Nutzer ehlertm </summary>
		[TestMethod]
		public async Task processDB2ToMSSQLEncryptedPwUser()
		{
			if (!await batch.processTransferBlock("EncryptedPWTestUserEncryption")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer DB2 zu MSSQL mit einem DPAPI verschlüsselten Passwort für den Host msgn00126 </summary>
		[TestMethod]
		public async Task processDB2ToMSSQLEncryptedPwMachine()
		{
			if (!await batch.processTransferBlock("EncryptedPWTestMachineEncryption")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer DB2 zu DB2 mit einem DPAPI verschlüsselten Passwort für den Host msgn00126 </summary>
		[TestMethod]
		public async Task processDB2ToDB2()
		{
			if (!await batch.processTransferBlock("PIM-Transfer")) throw new Exception("failed");
		}

		/// <summary> Test Datentransfer DB2 zu Oracle mit einem DPAPI verschlüsselten Passwort für den Host msgn00126 </summary>
		[TestMethod]
		public async Task processOracleToDB2()
		{
			// Funktioniert lokal nicht - kein ODAC x86 
			await batch.processTransferBlock("OracleTest");
		}

		/// <summary> Test Datentransfer - Parallelitätstests mit allen anderen Testfällen </summary>
		[TestMethod]
		public void processParallel()
		{
			batch.processAllTransferBlocks();
		}


		[TestMethod]
		public async Task processAccessToDB2CustomMapping()
		{
			if (!await batch.processTransferBlock("AccessToDB2CustomMapping-Transfer")) throw new Exception("failed");
		}

        [TestMethod]
        public async Task processLastModSync_UpdateExisting()
        {
            if (!await batch.processTransferBlock("SyncLastMod_UpdateExisingTest")) throw new Exception("failed");
        }

        [TestMethod]
        public async Task processLastModSync_Append()
        {
            if (!await batch.processTransferBlock("SyncLastMod_AppendTest")) throw new Exception("failed");
        }

        [TestMethod]
        public async Task variableTest()
        {
            if (!await batch.processTransferBlock("TestVariables")) throw new Exception("failed");
        }

        
    }
}
