using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using msa.Data.Transfer.Model;
using msa.Logging;

namespace msa.Data.Transfer.Database
{
	/// <summary>
	/// Spezifische Implementierung des DBInterface für OleDB-Access-MDB-Dateien
	/// </summary>
	class OleAccessInterface : DBInterface
	{

		/// <summary>
		/// Erzeugt ein neues MSSQL-Interface für die angegebene Verbindung
		/// </summary>
		/// <param name="conString">Der ConnectionString für die Verbindung ( Passwörter evt. DPAPI-verschlüsselt)</param>
		/// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
		public OleAccessInterface(string conString, Logger logger = null) : base("System.Data.OleDb", conString, logger)
		{
            this.isTransactional = false;
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
					(el) => String.Format("{0} as [{1}]", el.sourceCol, el.targetCol) // Spaltenmapping auf Zieltabelle
				)),
				tablename
			);
			select += this.createWherePart(where, parameters);

			return await this.readSelect(select);
		}


	}
}
