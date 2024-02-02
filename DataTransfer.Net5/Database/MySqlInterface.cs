using msa.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer.Database
{
    /// <summary>
    /// Spezifische Implementierung des DBInterface für MySQL/MariaDB
    /// </summary>
    public class MySqlInterface : DBInterface
    {
        private Assembly mySqlAssembly = null;
        private uint counter = 0;

        /// <summary>
		/// Erzeugt ein neues MySql-Interface für die angegebene Verbindung
		/// </summary>
		/// <param name="conString">Der ConnectionString für die Verbindung ( Passwörter evt. DPAPI-verschlüsselt)</param>
		/// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
		public MySqlInterface(string conString, Logger logger = null) : base("MySql.Data.MySqlClient", conString, logger)
        {
            this.supportsDataAdapterCommands = false; // Adapter-Commands müssen manuell erstellt werden -- Bugfix für Sync da dort die IDs/AutoIncrements im Standard nicht übertragen werden
             this.supportsBatchCommands = false;
            this.mySqlAssembly = this.dbFactory.GetType().Assembly;
        }

        /// <summary>
        /// Datenadpater direkt mit Klassenname initialisiert, da MySql Factory dies nicht implementiert (Bug)
        /// </summary>
        /// <returns>Ein MySqlDataAdapter</returns>
        public override DbDataAdapter createDataAdapter()
        {
            Object temp = this.mySqlAssembly.CreateInstance("MySql.Data.MySqlClient.MySqlDataAdapter");
            return (DbDataAdapter)temp;
        }

        /// <summary>
        /// CommandBuilder direkt mit Klassenname initialisiert, da MySql Factory dies nicht implementiert (Bug)
        /// </summary>
        /// <returns>Ein MySqlCommandBuilder</returns>
        public override DbCommandBuilder createDbCommandBuilder()
        {
            Object temp = this.mySqlAssembly.CreateInstance("MySql.Data.MySqlClient.MySqlCommandBuilder");
            return (DbCommandBuilder)temp;
        }

        /// <summary>Gibt das Format für einen Parameternamen in MySql an - konkret Name[Zahl] </summary>
        /// <param name="paramName">Der gewünschte Name des Parameters</param>
        /// <returns>Der vom Zielsystem unterstützte Name des Parameters</returns>
        public override string getParamName(string paramName)
        {
            return paramName + counter++;
        }
    }
}
