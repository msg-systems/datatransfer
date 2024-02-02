using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using msa.Data.Transfer.Database.Custom;
using msa.Data.Transfer.Model;
using msa.Logging;
using System.Data.SqlClient;
using System.Text.RegularExpressions;

namespace msa.Data.Transfer.Database
{

    /// <summary>
    /// Delegate event handler used with the <c>DbDataAdapter.RowUpdating</c> event.
    /// </summary>
    public delegate void RowUpdatingEventHandler(object sender, RowUpdatingEventArgs e);

    /// <summary>
    /// Delegate event handler used with the <c>DbDataAdapter.RowUpdated</c> event.
    /// </summary>
    public delegate void RowUpdatedEventHandler(object sender, RowUpdatedEventArgs e);




    /// <summary>
    /// Basisimplementierung für den Zugriff auf eine ADO-Datenquelle. <br/>
    /// Ermöglicht asynchrones Auslesen, Einlesen und Merge von Daten. <br/>
    /// Ermöglicht Entschlüsselung von verschlüsselten Passwörtern in ConnectionStrings (DPAPI-Encrypted). <br/>
    /// Ermöglicht transaktionale Verwaltung. Wird diese verwendet und die Transaktion wird nicht explizit commited, setzt der Garbage Collector die Transaktion zurück<br/>
    /// Verwendet standardmäßig den Logger/TraceSource msa.Data.Transfer.Database im Information-Mode ohne TraceListener/Target <br/>
    /// Wird das Loglevel auf Verbose gesetzt wechselt der Prozess bei Batchläufen in Einzelverarbeitung und loggt jegliche Statements mit Parametern die verwendet werden <br/>
    /// </summary>
    public class DBInterface : IDisposable
    {
        /// <summary>Der verwendete Logger für das Interface - defaut oder manuell gesetzt</summary>
        protected Logger p_logger;

        /// <summary> Typemapping zwischen DBType und Type für Konvertierugen</summary>
        public static Dictionary<Type, DbType> typeMap = new Dictionary<Type, DbType>()
        {
            {typeof(byte), DbType.Byte},
            {typeof(sbyte), DbType.SByte},
            {typeof(short), DbType.Int16},
            {typeof(ushort), DbType.UInt16},
            {typeof(int), DbType.Int32},
            {typeof(uint), DbType.UInt32},
            {typeof(long), DbType.Int64},
            {typeof(ulong), DbType.UInt64},
            {typeof(float), DbType.Single},
            {typeof(double), DbType.Double},
            {typeof(decimal), DbType.Decimal},
            {typeof(bool), DbType.Boolean},
            {typeof(string), DbType.String},
            {typeof(char), DbType.StringFixedLength},
            {typeof(Guid), DbType.Guid},
            {typeof(DateTime), DbType.DateTime},
            {typeof(DateTimeOffset), DbType.DateTimeOffset},
            {typeof(byte[]), DbType.Binary},
            {typeof(byte?), DbType.Byte},
            {typeof(sbyte?), DbType.SByte},
            {typeof(short?), DbType.Int16},
            {typeof(ushort?), DbType.UInt16},
            {typeof(int?), DbType.Int32},
            {typeof(uint?), DbType.UInt32},
            {typeof(long?), DbType.Int64},
            {typeof(ulong?), DbType.UInt64},
            {typeof(float?), DbType.Single},
            {typeof(double?), DbType.Double},
            {typeof(decimal?), DbType.Decimal},
            {typeof(bool?), DbType.Boolean},
            {typeof(char?), DbType.StringFixedLength},
            {typeof(Guid?), DbType.Guid},
            {typeof(DateTime?), DbType.DateTime},
            {typeof(DateTimeOffset?), DbType.DateTimeOffset}
        };

        /// <summary> Gibt an ob das Datenquellen-Interface Transaktionen unterstützt </summary>
        public virtual bool isTransactional { get; protected set; } = true;

        /// <summary>Der ADO-Provider der für diese Connection verwendet wird</summary>
        public string adoDriver { get; protected set; }
        /// <summary>Der ConnectionString der für diese Connection verwendet wird, bereinigt um die Passwortinformation</summary>
        public string conString { get; protected set; }
        /// <summary>Der logPrefix der vor jeden Logeintrag gesetzt wird</summary>
        public string logSubject { get; set; }
        /// <summary>Die ProviderFactory die verwendet wird um Objekte zu erzeugen</summary>
        protected DbProviderFactory dbFactory { get; set; }
        /// <summary>Das Connectionobjekt zur Datenquelle</summary>
        protected DbConnection dbConnection { get; set; }
        /// <summary>Die aktuell laufende Transaktion oder null, wenn keine Transaktion läuft</summary>
        public DbTransaction dbTransaction { get; protected set; }

        /// <summary>Formatiert eine Variable als ein Klartextliteral</summary>
        /// <param name="v">Die Variable die formatiert werden soll</param>
        /// <returns>Ein für ein SQL-Statement gültiges Typformat</returns>
        public string formatParameterAsValue(Variable v)
        {
            return this.formatParameterAsValue(v.value, v.type);
        }

        /// <summary>
        /// Formatiert einen Parameterwert in ein Klartextliteral in 
        /// </summary>
        /// <param name="value">Wert der Datenbankspezifisch formatiert werden soll</param>
        /// <param name="type">Der Typ zu dem formatiert werden soll</param>
        /// <returns>Ein für ein SQL-Statement gültiges Typformat</returns>
        public string formatParameterAsValue(object value, DbType type)
        {
            switch (type)
            {
                case DbType.String:
                case DbType.StringFixedLength:
                    return "'" + value.ToString().Replace("'", "''") + "'";
                case DbType.Date:
                    return "'" + ((DateTime)value).ToString("yyyy-MM-dd") + "'";
                case DbType.DateTime:
                case DbType.DateTime2:
                    return "'" + ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss") + "'";
                default:
                    return value.ToString();
            }
        }

        /// <summary>Gibt das Format für einen Parameternamen im entsprechenden System an</summary>
        /// <param name="paramName">Der gewünschte Name des Parameters</param>
        /// <returns>Der vom Zielsystem unterstützte Name des Parameters</returns>
        public virtual string getParamName(string paramName)
        {
            return "?";
        }

        /// <summary> Gibt an ob der aktuelle Treiber automatische Commanderstellung mittels DBCommandBuilder unterstützt - sonst wird ein Standardformat mit ? verwendet </summary>
        public bool supportsDataAdapterCommands = true;

        /// <summary> Gibt an ob Parameter für die Schnittstelle akzeptiert sind und ob der DataAdapter überhaupt verwendet werden kann, selbst wenn man die Statements manuell generiert </summary>
        public bool supportsParameter = true;

        /// <summary>Wenn Batchcommands unterstützt werden wird dies hierüber angegeben</summary>
        public bool supportsBatchCommands = true;

        /// <summary> Gibt an ob die Datenquelle Parameter unterstützt </summary>
        public bool supportsParameters
        {
            get
            {
                return true;
            }
        }

        /// <summary>Der Logger des DBInterfaces - Default "msa.Data.Transfer.Database" - kann aber auch manuell gesetzt werden </summary>
        public Logger logger
        {
            get
            {
                if (this.p_logger == null)
                {
                    //Console.WriteLine("Use Default DBInterface-Logger");
                    this.p_logger = Logger.getLogger("msa.Data.Transfer.Database");
                }
                return this.p_logger;
            }
            set
            {
                this.p_logger = value;
            }
        }


        /// <summary>
        /// Aktualisiert das Logsubject mit dem übergenenem + standardmäßigen Subject
        /// </summary>
        /// <param name="newLogSubject">Das neue Logsubject (Prefix) welches für Logeinträge der Komponente verwendet werden soll</param>
        public void reinitLogSubject(string newLogSubject)
        {
            this.logSubject = newLogSubject + String.Format("{0} > {1}: ", this.adoDriver, this.getSecureConString(this.conString));
        }

        /// <summary>
        /// Internal Konstruktor für leere Initialisierung z.B. für Custom-Provider
        /// </summary>
        internal DBInterface()
        { }

        /// <summary>
        /// Erstellt ein neues DBInterface mit dem angegebenen Treiber und ConnectionString. Ein alternativer Logger kann mitgegeben werden.
        /// </summary>
        /// <param name="driver">Der ADO-Provider für den Verbindungsaufbau</param>
        /// <param name="conString">Der ConnectionString zum Verbindungsaufbau - pwd/password Parameter können DPAPI verschlüsselt angegeben werden für Nutzer oder Maschinen</param>
        /// <param name="logger">Evt. Angabe eines alternativen Loggers , sonst wird "msa.Data.Transfer.Database" verwendet</param>
        /// <exception cref="System.ArgumentException">Tritt auf wenn die Parameter driver oder confString leer sind</exception>
        public DBInterface(string driver, string conString, Logger logger = null)
        {
            // Argumentprüfung
            if (String.IsNullOrEmpty(driver)) throw new ArgumentException("Argument driver cannot be empty");
            if (String.IsNullOrEmpty(conString)) throw new ArgumentException("Argument conString cannot be empty");

            if (logger != null) this.logger = logger;

            // Werte initialisieren - Übersetzungen nur wenn nicht custom
            //this.logSubject = String.Format("{0} > {1}: ", driver, this.conString);
            this.logSubject = String.Format("{0} > {1}: ", driver, this.getSecureConString(conString));
            this.adoDriver = driver;
            this.conString = conString;


            this.dbFactory = DbProviderFactories.GetFactory(this.adoDriver);
            this.dbConnection = this.dbFactory.CreateConnection();
            this.dbConnection.ConnectionString = this.getDecryptedConString(conString);
            this.dbTransaction = null;
        }


        /// <summary>
        /// Entschlüsselt das Passwort aus dem ConnectionString wobei die Attribute pwd und password geprüft werden. 
        /// Schlägt die Entschlüsselung fehl wird eine Warnung geloggt und der Passwort-Wert wird direkt verwendet
        /// </summary>
        /// <param name="connectionString">Der Connectionstring dessen Passwort entschlüsselt werden soll</param>
        /// <returns>Der Connectionstring mit entschlüsseltem Passwort, sofern dies möglich war. Wenn es nicht möglich war, kommt der identische ConnectionString zurück.</returns>
        protected string getDecryptedConString(string connectionString)
        {

            // Lies Connectionstring ein
            DbConnectionStringBuilder conBuilder;
            if (this.adoDriver == "System.Data.Odbc")
                conBuilder = new DbConnectionStringBuilder(true);
            else
                conBuilder = new DbConnectionStringBuilder(false);

            // Ermittel Attribut für Passwort
            conBuilder.ConnectionString = connectionString;
            string pwKey = null;
            if (conBuilder.ContainsKey("pwd")) pwKey = "pwd";
            if (conBuilder.ContainsKey("password")) pwKey = "password";
            if (conBuilder.ContainsKey("Password")) pwKey = "Password";
            if (pwKey == null) return conBuilder.ConnectionString;

            // Versuche Passwort zu entschlüsseln und schreibe den Wert zurück
            // Verursacht etwa 40-90 MilliSekunden Laufzeit
            string encryptedPwVal = conBuilder[pwKey].ToString();
            bool isBase64 = (encryptedPwVal.Length % 4 == 0) && Regex.IsMatch(encryptedPwVal, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);

            if (isBase64)
            {
                byte[] base64Source = Convert.FromBase64String(encryptedPwVal);
                try
                {
                    byte[] decryptedPw = ProtectedData.Unprotect(base64Source, null, DataProtectionScope.CurrentUser);
                    conBuilder[pwKey] = Encoding.UTF8.GetString(decryptedPw);
                }
                catch (Exception)
                {
                    try
                    {
                        byte[] decryptedPw = ProtectedData.Unprotect(base64Source, null, DataProtectionScope.LocalMachine);
                        conBuilder[pwKey] = Encoding.UTF8.GetString(decryptedPw);
                    }
                    catch (Exception)
                    {
                        this.logger.logWarning(this.logSubject + "Cannot decrypt password from connection string - take it as is");
                    }
                }
            }


            // Rückgabe des angepassten ConnectionStrings
            return conBuilder.ConnectionString;
        }


        /// <summary>
        /// Entfernt aus einem ConnectionString Angaben zu Passwörtern und gibt das Ergebnis zurück. Entfernt werden die Attribute pwd und password.
        /// </summary>
        /// <param name="connectionString">Der ConnectionString der angepasst werden soll</param>
        /// <returns>Ein ConnectionString ohne Passwortinformationen</returns>
        protected string getSecureConString(string connectionString)
        {
            // Lies Connectionstring ein
            DbConnectionStringBuilder conBuilder;
            if (this.adoDriver == "System.Data.Odbc")
                conBuilder = new DbConnectionStringBuilder(true);
            else
                conBuilder = new DbConnectionStringBuilder(false);

            conBuilder.ConnectionString = connectionString;

            // entferne Passwortattribute
            conBuilder.Remove("pwd");
            conBuilder.Remove("password");
            conBuilder.Remove("Password");

            // Rückgabe des angepassten ConnectionStrings
            return conBuilder.ConnectionString;
        }


        /// <summary>
        /// Erstellt einen DBCommand im Kontext der Datenverbindung und der aktuell laufenden Transaktion
        /// </summary>
        /// <returns>Ein DBCommand-Objekt</returns>
        public virtual DbCommand createDbCommand()
        {
            // Erstelle Command und verknüpfe mit Connection und der aktuell gültigen Transaktion
            DbCommand dbCommand = this.dbFactory.CreateCommand();
            dbCommand.Connection = this.dbConnection;
            if (this.dbTransaction != null) dbCommand.Transaction = this.dbTransaction;
            dbCommand.CommandTimeout = 900; // 15 Minuten clientseitiger Timeout (default 30 Sekunden)
            return dbCommand;
        }

        /// <summary> Anlage über ADO.NET Framework abhängig vom ADO-Treiber (dynamisch) </summary>
        /// <returns> Ein passender DBDataAdapter </returns>
        public virtual DbDataAdapter createDataAdapter()
        {
            return this.dbFactory.CreateDataAdapter();
        }

        /// <summary> Anlage über ADO.NET Framework abhängig vom ADO-Treiber (dynamisch) </summary>
        /// <returns> Ein passender DBCommandBuilder </returns>
        public virtual DbCommandBuilder createDbCommandBuilder()
        {
            return this.dbFactory.CreateCommandBuilder();
        }



        /// <summary>
        /// Verbindet sich mit der Datenbank
        /// </summary>
        /// <returns>nichts</returns>
        public async virtual Task connect()
        {
            if (this.dbConnection.State != ConnectionState.Open)
            {
                this.logger.logInfo(this.logSubject + "Connecting");
                await this.dbConnection.OpenAsync();
                this.logger.logInfo(this.logSubject + "Connected");
            }
        }


        /// <summary>
        /// Trennt sich von der Datenbank
        /// </summary>
        /// <returns>nichts</returns>
        public virtual void disconnect()
        {
            if (this.dbConnection.State != ConnectionState.Closed)
            {
                this.logger.logInfo(this.logSubject + "Disconnecting");
                this.dbConnection.Close();
                this.logger.logInfo(this.logSubject + "Disconnected");
            }
        }


        /// <summary> Startet eine Transaktion, sofern noch keine läuft </summary>
        /// <param name="level">Der zu verwendende Isolationslevel</param>
        /// <exception cref="System.InvalidOperationException">Tritt auf wenn bereits eine Transaktion läuft</exception>
        public virtual void startTransaction(IsolationLevel level = IsolationLevel.RepeatableRead)
        {
            if (!this.isTransactional) throw new InvalidOperationException(this.logSubject + "Interface has not the ability for transactions");
            if (this.dbTransaction != null) throw new InvalidOperationException(this.logSubject + "A Transaction is currently running - cannot start a new one");
            this.logger.logInfo(this.logSubject + "Starting transaction");
            this.dbTransaction = this.dbConnection.BeginTransaction(level);
        }


        /// <summary> Commited die aktuell laufende Transaktion </summary>
        /// /// <exception cref="System.InvalidOperationException">Tritt auf wenn aktuell keine Transaktion läuft</exception>
        public virtual void commitTransaction()
        {
            if (!this.isTransactional) throw new InvalidOperationException(this.logSubject + "Interface has not the ability for transactions");
            if (this.dbTransaction == null) throw new InvalidOperationException(this.logSubject + "No transaction is running currently");
            this.logger.logInfo(this.logSubject + "Commit transaction");
            this.dbTransaction.Commit();
            this.dbTransaction.Dispose();
            this.dbTransaction = null;
        }


        /// <summary> Führt einen Rollback aus für die aktuell laufende Transaktion </summary>
        /// <exception cref="System.InvalidOperationException">Tritt auf wenn aktuell keine Transaktion läuft</exception>
        public virtual void rollbackTransaction()
        {
            if (!this.isTransactional) throw new InvalidOperationException(this.logSubject + "Interface has not the ability for transactions");
            if (this.dbTransaction == null) throw new InvalidOperationException("No transaction is running currently");
            this.logger.logInfo(this.logSubject + "Rollback transaction");
            this.dbTransaction.Rollback();
            this.dbTransaction.Dispose();
            this.dbTransaction = null;
        }


        /// <summary>
        /// Verarbeitet das SQL-Statement gegen die Datenbank (im Kontext der aktuellen Transaktion)
        /// </summary>
        /// <param name="stmt">Ein SQL-Statement welches ausgeführt werden soll</param>
        /// <returns>Die Anzahl der betroffenen Datensätze</returns>
        public async virtual Task<int> processStmt(string stmt)
        {
            this.logger.logInfo(this.logSubject + "Start Statement: '" + stmt + "'");

            int result = 0;

            // erstelle Command und führe es aus
            using (DbCommand dbCommand = this.createDbCommand())
            {
                dbCommand.CommandText = stmt;
                this.logger.logVerbose(this.logSubject + dbCommand.CommandText);

                result = await dbCommand.ExecuteNonQueryAsync();
            }

            this.logger.logInfo(this.logSubject + "Statement finished: '" + stmt + "' # " + result + " rows processed");
            return result;
        }

        /// <summary>
        /// Führt einen Select auf der Datenquelle durch und liefert das Ergebnis als lokales DataTable-Objekt zurück
        /// </summary>
        /// <param name="query">Die zu verwendende Select-Anfrage</param>
        /// <param name="parameters">Zusätzliche Parameter die im Select where-Statement angefügt werden sollen (wenn nicht schon vorhanden)</param>
        /// <returns>Eine DataTable mit den Daten der Tabelle (kein Cursor)</returns>
        public async virtual Task<DataTable> readSelect(string query, Model.ParameterDef[] parameters = null)
        {
            this.logger.logInfo(this.logSubject + "Start Query: '" + query + "'");
            DataTable returnVal = new DataTable();

            // Erstelle Befehl
            using (DbCommand dbCommand = this.createDbCommand())
            {
                dbCommand.CommandText = query;

                if (this.supportsParameters)
                {
                    // Parameter zufügen soweit sinnvoll
                    if (parameters != null)
                    {
                        foreach (Model.ParameterDef parameter in parameters)
                        {
                            DbParameter param = dbCommand.CreateParameter();
                            param.DbType = parameter.type;
                            param.Direction = ParameterDirection.Input;
                            param.ParameterName = parameter.paramName;
                            param.Value = parameter.value;
                            dbCommand.Parameters.Add(param);
                        }
                    }
                }

                this.logger.logVerbose(this.logSubject + dbCommand.CommandText);

                // Erstelle DataAdapter und fülle Tabelle
                using (DbDataAdapter dbDataAdapter = this.createDataAdapter())
                {
                    dbDataAdapter.SelectCommand = dbCommand;
                    Exception taskException = null;
                    await Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            dbDataAdapter.Fill(returnVal);
                        }
                        catch (Exception e)
                        {
                            taskException = e;
                        }
                    });
                    if (taskException != null) throw taskException;
                }
            }

            this.logger.logInfo(this.logSubject + "Select Result for '" + query + "' are " + returnVal.Rows.Count);

            return returnVal;
        }


        /// <summary>
        /// Befüllt eine angebene Ziel-Tabelle in der Datenquelle mit den übergebenen Daten und führt ein evt. Mapping von Spalten durch
        /// </summary>
        /// <param name="data">Daten die in die Tabelle geladen werden sollen</param>
        /// <param name="block">Der Transferblock mit den Daten wie etwa TargetMaxBatchSize</param>
        /// <param name="job">Der Transferjob mit entsprechenden Attributen für sync, identical-Columns usw.</param>
        /// <param name="columnMap">Ein optionales Mapping der Quelldaten in die Zieltabellenspalten </param>
        /// <returns>Keine Rückgabe - nur einen Task zur asynchronen Ausführung</returns>
        public async virtual Task fillTable(DataTable data, TransferBlock block, TransferTableJob job, TransferTableColumnList columnMap = null)
        {
            this.logger.logInfo(this.logSubject + "Start fill table: '" + job.targetTable + "' with " + data.Rows.Count + " records");
            DataTable target = null;

            // Erstelle ein Pseudo-Select-Command um den Data-Adapter zu befüllen für ein "Update"
            using (DbCommand dbCommand = this.createDbCommand())
            {
                // Führe ColumnMap durch sofern nötig (im Select)
                if (columnMap != null)
                {
                    dbCommand.CommandText = String.Format("SELECT {0} FROM {1};",
                        String.Join(", ", columnMap.Select(
                        (el) => String.Format("{0}", (el.targetCol == "Key" && this.adoDriver == "System.Data.SqlClient" ? "[key]" : el.targetCol)) // Spaltenmapping auf Zieltabelle
                        )),
                        job.targetTable);
                }
                else
                {
                    if (job.identicalColumnsSource == DBContextEnum.Target)
                    {
                        dbCommand.CommandText = String.Format("SELECT * FROM {0};", job.targetTable);
                    }
                    else
                    {
                        dbCommand.CommandText = String.Format("SELECT {0} FROM {1};", String.Join(",", data.Columns.OfType<DataColumn>().Select((col) => col.ColumnName)), job.targetTable);
                    }

                }
                dbCommand.CommandTimeout = 300;

                this.logger.logInfo(this.logSubject + "Start fill table: '" + job.targetTable + "' select command: " + dbCommand.CommandText);
                // Initialisere DataAdapter und CommandBuilder 
                //DbDataAdapter dbDataAdapter = null;
                //DbCommandBuilder targetComBuilder = null;
                using (DbDataAdapter dbDataAdapter = this.createDataAdapter())
                using (DbCommandBuilder targetComBuilder = this.createDbCommandBuilder())
                {
                    dbDataAdapter.SelectCommand = dbCommand;
                    targetComBuilder.DataAdapter = dbDataAdapter;

                    // Passe die Quelltabelle so an, dass sie auf die neue DB-Tabelle zeigt
                    data.TableName = job.targetTable;

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

                        // Lies Zieldaten ein
                        target = await this.readSelect(String.Format("SELECT {0} FROM {1} {2}",
                            String.Join(", ",
                                data.Columns.Cast<DataColumn>().Select(

                                    (dc) => (dc.ColumnName.ToLower() == "key" && this.adoDriver == "System.Data.SqlClient" ? "[Key]" : dc.ColumnName)
                                    )
                            ),
                            job.targetTable,
                            (String.IsNullOrWhiteSpace(job.targetSyncWhere) ? "" : "WHERE " + job.targetSyncWhere)
                            ));

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
                        try
                        {
                            targetComBuilder.ConflictOption = ConflictOption.OverwriteChanges;
                        }
                        catch
                        {
                        }
                        targetComBuilder.RefreshSchema();

                        // Wenn der echte Primärschlüssel im Select nicht vorhanden ist, scheitert der Command Builder 
                        // --> Merke dies und wechsel in den manuellen CommandBuilder Modus
                        bool selectCommandBuilderCompatible = false;
                        if (supportsDataAdapterCommands)
                        {
                            try
                            {
                                targetComBuilder.GetUpdateCommand();
                                selectCommandBuilderCompatible = true;
                            }
                            catch (System.InvalidOperationException)
                            {
                                selectCommandBuilderCompatible = false;
                            }
                        }

                        if (supportsDataAdapterCommands && selectCommandBuilderCompatible)
                        {
                            dbDataAdapter.UpdateCommand = targetComBuilder.GetUpdateCommand();
                            dbDataAdapter.DeleteCommand = targetComBuilder.GetDeleteCommand();
                        }
                        else
                        {
                            // Manuelles zusammenbauen der Commands
                            // UPDATE
                            dbDataAdapter.UpdateCommand = this.createDbCommand();
                            dbDataAdapter.UpdateCommand.CommandText = "UPDATE " + job.targetTable +
                                                                        " SET " + String.Join(",", target.Columns.OfType<DataColumn>().Select((dc) => dc.ColumnName + " = " + getParamName(dc.ColumnName))) +
                                                                        " WHERE " + String.Join(",", target.PrimaryKey.Select((dc) => dc.ColumnName + " = " + getParamName(dc.ColumnName) +  " ")) + ";";

                            if ((int)this.logger.logLevel < (int)SourceLevels.Verbose) // Bei Batchverarbeitung muss dies none sein - also nur relevant wenn Loglevel sehr hoch ist
                            {
                                dbDataAdapter.UpdateCommand.UpdatedRowSource = UpdateRowSource.None;
                            }

                            foreach (DataColumn dc in target.Columns.OfType<DataColumn>())
                            {
                                DbParameter param = this.dbFactory.CreateParameter();
                                param.Direction = ParameterDirection.Input;
                                param.ParameterName = this.getParamName(dc.ColumnName);
                                param.DbType = typeMap[dc.DataType];
                                param.SourceColumn = dc.ColumnName;
                                dbDataAdapter.UpdateCommand.Parameters.Add(param);
                            }

                            foreach (DataColumn dc in target.PrimaryKey)
                            {
                                DbParameter param = this.dbFactory.CreateParameter();
                                param.Direction = ParameterDirection.Input;
                                param.ParameterName = this.getParamName(dc.ColumnName);
                                param.DbType = typeMap[dc.DataType];
                                param.SourceColumn = dc.ColumnName;
                                if (!(param.ParameterName.Contains("@") && dbDataAdapter.UpdateCommand.Parameters.Contains(param.ParameterName)))
                                {
                                    dbDataAdapter.UpdateCommand.Parameters.Add(param);
                                }
                            }
                            

                            // DELETE
                            dbDataAdapter.DeleteCommand = this.createDbCommand();
                            dbDataAdapter.DeleteCommand.CommandText = "DELETE FROM " + job.targetTable + " WHERE " + String.Join(",", target.PrimaryKey.Select((dc) => dc.ColumnName + " = " + getParamName(dc.ColumnName) + " "));
                            if ((int)this.logger.logLevel < (int)SourceLevels.Verbose) // Bei Batchverarbeitung muss dies none sein - also nur relevant wenn Loglevel sehr hoch ist
                            {
                                dbDataAdapter.DeleteCommand.UpdatedRowSource = UpdateRowSource.None;
                            }

                            foreach (DataColumn dc in target.PrimaryKey)
                            {
                                DbParameter param = this.dbFactory.CreateParameter();
                                param.Direction = ParameterDirection.Input;
                                param.ParameterName = this.getParamName(dc.ColumnName);
                                param.DbType = typeMap[dc.DataType];
                                param.SourceColumn = dc.ColumnName;
                                dbDataAdapter.DeleteCommand.Parameters.Add(param);
                            }
                        }


                        dbDataAdapter.DeleteCommand.CommandTimeout = 6000;
                        dbDataAdapter.UpdateCommand.CommandTimeout = 6000;

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

                        // Prüfung Typen für Merge
                        int colIndex = 0;
                        int colCount = data.Columns.Count;
                        bool pkChanged = false;
                        List<DataColumn> oldPK = data.PrimaryKey.ToList();
                        data.PrimaryKey = new DataColumn[0];
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

                        target.Merge(data, false); // Mergen, muss false sein um neue Werte korrekt zu übernehmen

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
                    if (this.supportsBatchCommands)
                    {
                        if ((int)this.logger.logLevel >= (int)SourceLevels.Verbose)
                            dbDataAdapter.UpdateBatchSize = 1; // = Einzelstatements
                        else
                            dbDataAdapter.UpdateBatchSize = block.targetMaxBatchSize; // = maximale BatchSize
                    }
                    else
                    {
                        dbDataAdapter.UpdateBatchSize = 1; // = Einzelstatements
                    }

                    // Führe Update durch
                    this.logger.logInfo(this.logSubject + "Start fill table: '" + job.targetTable + "' Start filling");

                    // Führe Update durch
                    if (this.supportsDataAdapterCommands)
                    {
                        dbDataAdapter.InsertCommand = targetComBuilder.GetInsertCommand();
                    }
                    else
                    {
                        // INSERT
                        DbCommand insertCommand = this.createDbCommand();

                        if (target == null) // Leeres Ergebnis gewünscht, nur Schema abfragen -- Wird auch benötigt wenn man ohne DataAdapater arbeitet! notwendig!!
                        {
                            target = await this.readSelect(String.Format("SELECT {0} FROM {1} where 1=2",
                            String.Join(", ",
                                data.Columns.Cast<DataColumn>().Select(

                                    (dc) => (dc.ColumnName.ToLower() == "key" && this.adoDriver == "System.Data.SqlClient" ? "[Key]" : dc.ColumnName)
                                    )
                            ),
                            job.targetTable));
                        }

                        if (this.supportsParameter)
                        {
                            insertCommand.CommandText = "INSERT INTO " + job.targetTable + "(" + String.Join(",", target.Columns.OfType<DataColumn>().Select((dc) => dc.ColumnName)) + ")" +
                                                                    " VALUES (" + String.Join(",", target.Columns.OfType<DataColumn>().Select((dc) => "?")) + ")";

                            if ((int)this.logger.logLevel < (int)SourceLevels.Verbose) // Bei Batchverarbeitung muss dies none sein - also nur relevant wenn Loglevel sehr hoch ist
                            { 
                                insertCommand.UpdatedRowSource = UpdateRowSource.None; 
                            }

                            foreach (DataColumn dc in target.Columns.OfType<DataColumn>()) // TODO target
                            {
                                DbParameter param = insertCommand.CreateParameter();
                                //System.Data.Odbc.OdbcParameter param = new System.Data.Odbc.OdbcParameter();

                                param.Direction = ParameterDirection.Input;
                                param.ParameterName = "@" + dc.ColumnName;
                                param.DbType = typeMap[dc.DataType];
                                param.SourceColumn = dc.ColumnName;
                                insertCommand.Parameters.Add(param);
                            }
                        }
                        dbDataAdapter.InsertCommand = insertCommand;
                    }
                    dbDataAdapter.InsertCommand.CommandTimeout = 6000;

                    Exception taskException = null;

                    string output = "";

                    // Im Verbose-Mode wird das RowUpdating-Event ermittelt (geht nur so umständlich) und mit einem Handler verknüpft
                    RowUpdatingEventHandler updatingHandler = null;
                    RowUpdatedEventHandler updatedHandler = null;
                    if ((int)this.logger.logLevel >= (int)SourceLevels.Verbose)
                    {
                        // Handler der das aktuelle Statement und Parameter speichert
                        updatingHandler = (sender, e) =>
                        {
                            if (e.Status == UpdateStatus.SkipCurrentRow) return;
                            output = e.StatementType.ToString() + ": " + e.Command.CommandText + "# Parameter: ";
                            foreach (DbParameter para in e.Command.Parameters)
                            {
                                output += para.ParameterName + " -> " + para.Value + ", ";
                            }

                            if (e.Errors != null)
                            {
                                throw e.Errors;
                            }

                            if ((int)this.logger.logLevel >= (int)SourceLevels.Verbose)
                                this.logger.logVerbose(this.logSubject + "table '" + job.targetTable + "Process Line " + output);
                        };

                        // Hanlder der das aktuelle Statement und Parameter speichert
                        updatedHandler = (sender, e) =>
                        {
                            if (e.Status == UpdateStatus.SkipCurrentRow) return;
                            if (e.Errors != null)
                            {
                                output = e.StatementType.ToString() + ": " + e.Command.CommandText + " ; Action = " + e.Row.RowState.ToString() + "; Error = " + e.Row.RowError;
                                for (int i = 0; i < e.Row.ItemArray.Length; i++)
                                {
                                    output += e.Row.Table.Columns[i].ColumnName + " -> " + e.Row.ItemArray[i].ToString() + ", ";
                                }
                                this.logger.logError(this.logSubject + "table '" + job.targetTable + "Error in line " + output);
                                // Kein Throw um Verarbeitung nicht abzubrechen
                            }
                        };

                        EventInfo evInfoUpdating = dbDataAdapter.GetType().GetEvent("RowUpdating", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        // Wenn Event gefunden dann verknüpfe es mit dem DataAdapter
                        if (evInfoUpdating != null)
                        {
                            try
                            { evInfoUpdating.AddEventHandler(dbDataAdapter, Delegate.CreateDelegate(evInfoUpdating.EventHandlerType, updatingHandler.Target, updatingHandler.Method)); }
                            catch (Exception e)
                            { this.logger.logError(this.logSubject + "table '" + job.targetTable + "Error on set RowUpdating-Event: " + e.ToString()); }
                        }

                        EventInfo evInfoUpdated = dbDataAdapter.GetType().GetEvent("RowUpdated", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        // Wenn Event gefunden dann verknüpfe es mit dem DataAdapter
                        if (evInfoUpdated != null)
                        {
                            try
                            { evInfoUpdated.AddEventHandler(dbDataAdapter, Delegate.CreateDelegate(evInfoUpdated.EventHandlerType, updatedHandler.Target, updatedHandler.Method)); }
                            catch (Exception e)
                            { this.logger.logError(this.logSubject + "table '" + job.targetTable + "Error on set RowUpdated-Event: " + e.ToString()); }
                        }
                    }

                    dbDataAdapter.ContinueUpdateOnError = true;
                    dbDataAdapter.FillError += DbDataAdapter_FillError; // Weitermachen bei Fehler - Auswertung erfolgt danach

                    if (this.supportsParameter)
                    {
                        await Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                if (job.sync)
                                {
                                    data = target;
                                }
                                dbDataAdapter.Update(data);
                            }
                            catch (Exception e)
                            {
                                if ((int)this.logger.logLevel >= (int)SourceLevels.Verbose) Console.WriteLine(e + " LastBatch (only if verbose-logging active) = " + output);
                                taskException = e;
                            }
                        });
                    }
                    else
                    {
                        await this.fillDataWithoutDataAdapter(dbDataAdapter.UpdateBatchSize, job.targetTable, target, data, columnMap);
                    }


                    // Gibt Fehler ein 2. mal aus, falls verbose, da im UpdatedHandler bereits behandelt
                    if (data.HasErrors)
                    {
                        IEnumerable<DataRow> errorRows = (from DataRow r in data.Rows where r.HasErrors select r).AsParallel();
                        if (errorRows.Count() > 25) // Dies führt sonst evt. zu Memory-Überlauf wenn ein Batch mit Millionen Datensätzen fehlschlägt und so viele Logs geschrieben werden
                        {
                            this.logger.logWarning(this.logSubject + "More than 100 errors detected - shorten to 100");
                            errorRows = errorRows.Take(25);
                        }
                        foreach (DataRow row in errorRows)
                        {
                            output = "Action = " + row.RowState.ToString() + "; Error = " + row.RowError;

                            for (int i = 0; i < row.ItemArray.Length; i++)
                            {
                                output += row.Table.Columns[i].ColumnName + " -> " + row.ItemArray[i].ToString() + ", ";
                            }
                            this.logger.logError(this.logSubject + "table '" + job.targetTable + "': " + output);
                        }
                        throw new Exception("Errors occured on fill for table " + data.TableName);
                    }

                    if (taskException != null) throw new Exception("Wrapper-Exception", taskException);
                }
            }
            this.logger.logInfo(this.logSubject + "table '" + job.targetTable + "' filled successfully");

        }

        /// <summary>
        /// Tritt bei Konvertierungsfehlern von Datentypen auf - hat nichts mit den eigentlichen Statements zu tun
        /// </summary>
        /// <param name="sender">Der Dataadapter bei dem das Fill fehlgeschlagen ist</param>
        /// <param name="e">Angaben zu dem Datensatz bei dem die Datentypkonvertierung fehlgeschlagen ist</param>
        private void DbDataAdapter_FillError(object sender, FillErrorEventArgs e)
        {
            e.Continue = false;
            DbDataAdapter dbDataAdapter = (DbDataAdapter)sender;
            string targetTable = dbDataAdapter.SelectCommand.CommandText;
            targetTable = Regex.Match(targetTable, @"FROM ([^\s;]+)").Groups[1].Value;
            targetTable = e.DataTable.TableName;
            this.logger.logError(this.logSubject + "table '" + targetTable + "' error " + e.Errors.ToString());
            string values = "";
            for (int i = 0; i < e.Values.Length; i++)
            {
                values += "{" + i + "} : '" + e.Values[i] + "';";
            }
            this.logger.logError(this.logSubject + "table '" + targetTable + "' Parameters " + values);
        }


        /// <summary>
        /// Baut manuell SQL-Statements zusammen und erstellt entsprechende Batches
        /// </summary>
        /// <param name="batchSize">Größe der Batches</param>
        /// <param name="targetTable">Zieltabelle</param>
        /// <param name="target"></param>
        /// <param name="data"></param>
        /// <param name="colMap"></param>
        /// <returns></returns>
        private async Task fillDataWithoutDataAdapter(int batchSize, string targetTable, DataTable target, DataTable data, TransferTableColumnList colMap)
        {
            DbCommand batchCommand = this.createDbCommand();
            List<TransferTableColumn> keys = colMap.Where((cm) => cm.isKey).ToList();

            string insertCommandBase = "INSERT INTO " + targetTable + "(" + String.Join(",", colMap.Select((dc) => dc.targetCol)) + ") VALUES (";
            string deleteCommandBase = "DELETE FROM " + targetTable + " where ";
            string updateCommandBase = "UPDATE " + targetTable + " SET ";

            int counterBatch = 0;
            if (!this.supportsBatchCommands) batchSize = 1;

            foreach (DataRow row in data.Rows)
            {
                counterBatch++;

                switch (row.RowState)
                {
                    case DataRowState.Added:
                        batchCommand.CommandText += insertCommandBase + String.Join(",", colMap.Select((cm) => this.formatParameterAsValue(row[cm.sourceCol], typeMap[target.Columns[cm.targetCol].DataType]))) + ");\n";
                        break;

                    case DataRowState.Deleted:
                        row.RejectChanges(); // Gibt sonst Fehler beim Zeilenzugriff auf row
                        batchCommand.CommandText += deleteCommandBase + String.Join(" and ", keys.Select((key) => key.targetCol + " = " + this.formatParameterAsValue(row[key.sourceCol], typeMap[target.Columns[key.targetCol].DataType])));
                        break;

                    case DataRowState.Modified:
                        batchCommand.CommandText += updateCommandBase +
                            String.Join(",", colMap.Select((cm) => cm.targetCol + " = " + this.formatParameterAsValue(row[cm.sourceCol], typeMap[target.Columns[cm.targetCol].DataType]))) +
                            " WHERE " + String.Join(" and ", keys.Select((key) => key.targetCol + " = " + this.formatParameterAsValue(row[key.sourceCol], typeMap[target.Columns[key.targetCol].DataType])));
                        break;
                }

                if (counterBatch >= batchSize)
                {
                    await batchCommand.ExecuteNonQueryAsync();
                    counterBatch = 0;
                    batchCommand.CommandText = "";
                }
            }
        }

        /// <summary>
        /// Erzeugt einen Where-Part für die angegebene subwhere-clause und die Parameter (die im Zweifel ergänzt werden, wenn sie noch nicht vorhanden sind)
        /// </summary>
        /// <param name="where">Direkter Where Ausdruck der verwendet werden soll</param>
        /// <param name="parameters">Weitere dynamische Parameter die verwendet werden sollen (werden dynamisch zum Where ergänzt)</param>
        /// <returns>Ein WHERE-Statement mit enthaltenem " WHERE " - Clause oder Leerstring</returns>
        public string createWherePart(string where, ParameterDef[] parameters = null)
        {
            string result = "";
            if (parameters == null) parameters = new ParameterDef[] { };
            if (!String.IsNullOrWhiteSpace(where) || (parameters != null && parameters.Count() > 0)) result += " WHERE ";
            if (!String.IsNullOrWhiteSpace(where)) result += where;
            if ((parameters != null && parameters.Count() > 0))
            {
                string paramWhere = "";
                if (this.supportsParameters)
                {
                    paramWhere = String.Join(" ", parameters.Where((param) => !result.Contains(param.paramName)).Select((param) => $"{param.logicJoin} {param.colName} {param.relation} {param.paramName}").ToArray());
                }
                else
                {
                    paramWhere = String.Join(" ", parameters.Where((param) => !result.Contains(param.paramName)).Select((param) => $"{param.logicJoin} {param.colName} {param.relation} {this.formatParameterAsValue(param.value, param.type)}").ToArray());
                }

                if (String.IsNullOrWhiteSpace(where))
                {
                    paramWhere = paramWhere.Substring(paramWhere.IndexOf(' '));
                }
                result += paramWhere;
            }
            return result;
        }

        /// <summary>
        /// Führt einen Select auf der Datenquelle für die angegeben Tabelle durch und liefert das Ergebnis als lokales DataTable-Objekt zurück
        /// </summary>
        /// <param name="tablename">Die abzufragende Tabelle</param>
        /// <param name="where">Einschränkung für Select</param>
        /// <param name="parameters">Zusätzliche Parameter die im Select where-Statement angefügt werden sollen (wenn nicht schon vorhanden)</param>
        /// <returns>Eine DataTable mit den Daten der Tabelle (kein Cursor)</returns>
        public async virtual Task<DataTable> readTable(string tablename, string where, ParameterDef[] parameters = null)
        {
            return await this.readSelect(String.Format("SELECT * FROM {0}{1};", tablename, createWherePart(where, parameters)), parameters);
        }

        /// <summary>
        /// Führt einen Select auf der Datenquelle für die angegeben Tabelle durch und liefert das Ergebnis als lokales DataTable-Objekt zurück
        /// </summary>
        /// <param name="tablename">Die abzufragende Tabelle</param>
        /// <param name="where">Einschränkung für Select</param>
        /// <param name="columnMap">Ein durchzuführendes Spaltenmapping beim SELECT</param>
        /// <param name="parameters">Zusätzliche Parameter die im Select where-Statement angefügt werden sollen (wenn nicht schon vorhanden)</param>
        /// <returns>Eine DataTable mit den Daten der Tabelle (kein Cursor)</returns>
        public async virtual Task<DataTable> readTable(string tablename, string where, TransferTableColumnList columnMap, ParameterDef[] parameters = null)
        {
            string select = String.Format("SELECT {0} FROM {1}",
                    String.Join(", ", columnMap.Select(
                        (el) => String.Format("{0} as \"{1}\"", el.sourceCol, el.targetCol) // Spaltenmapping auf Zieltabelle
                    )),
                    tablename
                );

            select += createWherePart(where, parameters);
            select += ";";

            // Lies beim Select mit TransferTableColumns Keys direkt ein
            DataTable data = await this.readSelect(select, parameters);

            return data;
        }


        /// <summary>
        /// Löscht die Daten der angegebenen Tabelle
        /// </summary>
        /// <param name="tablename">Der Name der Tabelle deren Inhalt gelöscht werden soll</param>
        /// <param name="where">Einschränkungen für den Löschbefehl in SQL-Syntax</param>
        /// <returns>Anzahl gelöschter Werte</returns>
        public async virtual Task<int> deleteTableRows(string tablename, string where)
        {
            this.logger.logInfo(this.logSubject + "Delete table content for " + tablename + " with condition '" + where + "'");
            // Erzeuge Command und führe ihn aus
            using (DbCommand dbCommand = this.createDbCommand())
            {
                if (String.IsNullOrWhiteSpace(where))
                {
                    dbCommand.CommandText = String.Format("Delete FROM {0};", tablename);
                }
                else
                {
                    dbCommand.CommandText = String.Format("Delete FROM {0} WHERE {1};", tablename, where);
                }

                this.logger.logVerbose(this.logSubject + dbCommand.CommandText);
                int result = await dbCommand.ExecuteNonQueryAsync();
                this.logger.logInfo(this.logSubject + "Deleted table content for " + tablename + " -> " + result + " rows");
                return result;
            }
        }


        /// <summary>
        /// Merge-Operation wird nicht unterstützt von allen Datenquellen und ist nur in den spezialisierten Implementierungen der Klasse vorhanden
        /// </summary>
        /// <param name="sourceTable">Name der Tabelle die Daten für den Merge zur Verfügung stellt</param>
        /// <param name="mergeOptions">Konfiguration des Merges mit Zieltabelle, Spaltenmappings, Keys, etc</param>
        /// <returns>nichts</returns>
        /// <exception cref="System.NotImplementedException">Tritt immer auf</exception>
        public async virtual Task merge(string sourceTable, TransferTableMergeOptions mergeOptions)
        {
            await Task.Factory.StartNew(() => { });
            throw new NotImplementedException(this.logSubject + "The generic Database interface does not support merge.");
        }


        /// <summary>
        /// Liefert die korrekte Implementierung des Datenbankinterfaces je nach driver-Typ
        /// </summary>
        /// <param name="driver">Der Name des ADO-Treibers</param>
        /// <param name="conString">Der ConnectionString für die Verbindung ( Passwörter evt. DPAPI-verschlüsselt)</param>
        /// <param name="logger">Ein Logger der explizit verwendet werden soll, statt dem DefaultLogger</param>
        /// <returns>Die spezifische Implementierung des DBInterfaces</returns>
        public static DBInterface getInterface(string driver, string conString, Logger logger = null)
        {
            DBInterface returnVal = null;

            // Mapping zwischen Treibertypen und Klassen
            switch (driver)
            {
                case "System.Data.SqlClient":
                    returnVal = new MSSQLInterface(conString, logger);
                    break;

                case "System.Data.OleDb":
                    if (conString.Contains(".mdb"))
                        returnVal = new OleAccessInterface(conString, logger);
                    else if (conString.Contains(".Oracle"))
                        returnVal = new OracleInterface(conString, logger);
                    else
                        goto default;

                    break;

                case "Custom.Export.XML":
                case "Custom.XML":
                    returnVal = new XMLInterface(conString, logger);
                    break;

                case "Custom.Export.CSV":
                case "Custom.CSV":
                    returnVal = new CSVInterface(conString, logger);
                    break;

                case "Custom.Export.JSON":
                case "Custom.JSON":
                    returnVal = new JSONInterface(conString, logger);
                    break;


                case "Custom.Import.LDAP":
                    returnVal = new Import_LDAP(conString, logger);
                    break;

                case "MySql.Data.MySqlClient":
                    returnVal = new MySqlInterface(conString, logger);
                    break;

                case "System.Data.Odbc":
                    if (conString.Contains("Lotus Notes SQL Driver"))
                        returnVal = new LotusInterface(conString, logger);
                    else
                        goto default;

                    break;

                default:
                    returnVal = new DBInterface(driver, conString, logger);
                    break;
            }

            return returnVal;
        }


        #region IDisposable Members

        /// <summary>Gibt an ob die Verbindung disposed ist</summary>
        private bool disposed = false;

        /// <summary> Gibt die Ressourcen des Objekts frei. Führt Rollback offener Transaktionen aus und schließt die DB-Verbindung </summary>
        /// <param name="disposing">true, wenn durch Code ausgeführt, false wenn durch Garbage Collector</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing) // nur wenn explizit disposed - Speicherfreigabe managed Objekte 
            {

            }

            // immer ausführen - Speicherfreigabe unmanaged Objects
            if (this.dbTransaction != null) this.rollbackTransaction();
            if (this.dbConnection != null) this.dbConnection.Dispose();

            disposed = true;
        }

        /// <summary>UserCode-Dispose</summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Garbage Collector Finalizer</summary>
        ~DBInterface()
        {
            this.Dispose(false);
        }

        #endregion
    }
}
