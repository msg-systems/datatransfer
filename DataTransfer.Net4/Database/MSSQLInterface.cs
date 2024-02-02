using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using msa.Data.Transfer.Model;
using msa.Logging;

namespace msa.Data.Transfer.Database
{
	/// <summary>
	/// Spezifische Implementierung des DBInterface für MSSQL
	/// </summary>
	public class MSSQLInterface : DBInterface
	{

		/// <summary>
		/// Erzeugt ein neues MSSQL-Interface für die angegebene Verbindung
		/// </summary>
		/// <param name="conString">Der ConnectionString für die Verbindung ( Passwörter evt. DPAPI-verschlüsselt)</param>
		/// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
		public MSSQLInterface(string conString, Logger logger = null)	: base("System.Data.SqlClient", conString, logger)
		{
		}

        /// <summary>Gibt das Format für einen Parameternamen im entsprechenden System an</summary>
        /// <param name="paramName">Der gewünschte Name des Parameters</param>
        /// <returns>Der vom Zielsystem unterstützte Name des Parameters</returns>
        public override string getParamName(string paramName)
        {
            return "@" + paramName;
        }

        /// <summary>
        /// Löscht die Daten der angegebenen Tabelle
        /// </summary>
        /// <param name="tablename">Der Name der Tabelle deren Inhalt gelöscht werden soll</param>
        /// <param name="where">Einschränkung für Select</param>
        /// <returns>Anzahl gelöschter Werte</returns>
        public override async Task<int> deleteTableRows(string tablename, string where)
		{
			this.logger.logInfo(this.logSubject + "Delete table content for " + tablename);
			if (String.IsNullOrWhiteSpace(where))
			{
				using (SqlCommand dbCommand = (SqlCommand)this.createDbCommand())
				{
					dbCommand.CommandText = String.Format("TRUNCATE TABLE {0}", tablename);
					this.logger.logVerbose(dbCommand.CommandText);
					int result = await dbCommand.ExecuteNonQueryAsync();
					this.logger.logInfo(this.logSubject + "Deleted table content for " + tablename + " -> " + result + " rows");
					return result;
				}
			}
			else
				return await base.deleteTableRows(tablename, where);
		}

        


        /// <summary>
        /// Merge-Operation Für ein UPSERT von Daten
        /// </summary>
        /// <param name="sourceTable">Name der Tabelle die Daten für den Merge zur Verfügung stellt</param>
        /// <param name="mergeOptions">Konfiguration des Merges mit Zieltabelle, Spaltenmappings, Keys, etc</param>
        /// <returns>nichts</returns>
        public override async Task merge(string sourceTable, Model.TransferTableMergeOptions mergeOptions)
		{
			this.logger.logInfo(this.logSubject + "Merge table " + sourceTable + " into " + mergeOptions.targetTable);

			// Erstelle Merge-Befehl (nur MSSQL)
			StringBuilder mergeString = new StringBuilder();

			// Merge-Kopfbereich - Zieltabelle
			mergeString.Append("MERGE INTO " + mergeOptions.targetTable + " as Target ");

			// Merge-Kopfbereich - Quelltabelle
			TransferTableColumnList colList = new TransferTableColumnList();
			if (mergeOptions.autoMergeColumns) // auto = * Alles aus Source
			{
				
				DataTable sourceSchema = await this.readSelect( String.Format("SELECT TOP(1) * FROM {0}", sourceTable ));
				foreach (DataColumn col in sourceSchema.Columns)
				{
					colList.Add(col.ColumnName, col.ColumnName);
				}
				
				mergeString.Append("USING (SELECT * FROM " + sourceTable + ") AS Source ");
			}
			else // Sonst Spalten aus Mapping
			{
				colList = mergeOptions.columnMap;
				mergeString.Append("USING (SELECT " + String.Join(", ", colList.Select((el) => "\"" + el.sourceCol + "\"")) + " FROM " + sourceTable + ") AS Source ");
			}

			// Join Statement
			mergeString.Append(" ON ");
			bool firstEntry = true;
			foreach (TransferTableColumn mergeKey in mergeOptions.mergeKey)
			{
				if (!firstEntry) mergeString.Append(" AND ");
				mergeString.Append(" Target.\"" + mergeKey.targetCol + "\" = Source.\"" + mergeKey.sourceCol + "\" ");
				firstEntry = false;
			}

			// Match-Statement
			mergeString.Append(" WHEN MATCHED THEN ");
			mergeString.Append(" UPDATE SET ");
			firstEntry = true;
			foreach (TransferTableColumn mergeCol in colList)
			{
				if (!firstEntry) mergeString.Append(", ");
				mergeString.Append("\"" + mergeCol.targetCol + "\" = source.\"" + mergeCol.sourceCol + "\"");
				firstEntry = false;
			}

			// Not Matched TARGET-Statement
			mergeString.Append(" WHEN NOT MATCHED BY TARGET THEN ");
			mergeString.Append("INSERT (" + String.Join(", ", colList.Select((el) => "\"" + el.targetCol + "\"")) + ") ");
			mergeString.Append("VALUES (" + String.Join(", ", colList.Select((el) => "Source.\"" + el.sourceCol + "\"")) + ") ");

			// Not Matched SOURCE-Statement
			mergeString.Append(" WHEN NOT MATCHED BY SOURCE THEN DELETE");

			// Ende Statement
			mergeString.Append(";");

			// Kommando-Ausführung
			using (SqlCommand dbCommand = (SqlCommand)this.createDbCommand())
			{
				dbCommand.CommandText = mergeString.ToString();
				this.logger.logVerbose(this.logSubject + dbCommand.CommandText);
				await dbCommand.ExecuteNonQueryAsync();
				this.logger.logInfo(this.logSubject + "Merged table " + sourceTable + " into " + mergeOptions.targetTable);
			}
			
		}
		
	}
}
