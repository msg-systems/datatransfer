using msa.Data.Transfer.Model;
using msa.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer.Database
{
    /// <summary>
    /// Lotus-Notes Speziallogik für das Einfügen
    /// </summary>
    public class LotusInterface : DBInterface
    {
        /// <summary>
		/// Erzeugt ein neues Lotus-Interface für die angegebene Verbindung
		/// </summary>
		/// <param name="conString">Der ConnectionString für die Verbindung ( Passwörter evt. DPAPI-verschlüsselt)</param>
		/// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
		public LotusInterface(string conString, Logger logger = null) : base("System.Data.Odbc", conString, logger)
        {
            this.supportsDataAdapterCommands = false; // Adapter-Commands müssen manuell erstellt werden
            this.supportsParameter = false; // Commands müssen ohne Parameter ausgeführt werden
            this.supportsBatchCommands = false; // Nur Einzelverarbeitung erlaubt - keine Batches
        }
    }
}
