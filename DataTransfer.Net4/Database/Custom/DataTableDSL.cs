using msa.DSL;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using msa.DSL.CodeParser;
using msa.Data.Transfer.SQL;

namespace msa.Data.Transfer.Database.Custom
{

    /// <summary>
    /// DSL die für Arbeiten mit DataTables ausgelegt ist. Enthält spezielle Logik um mit einem SQLParseTree zusammen zu arbeiten
    /// </summary>
    public class DataTableDSL : DSLDef
    {
        /// <summary> Ertellt die DSL mit den korrekten DataProvidern und FunctionHandlern </summary>
        public DataTableDSL() : base(new DataTableValueProvider(), new DataTableFunctionHandler())
        {
        }
    }

    /// <summary>
    /// Ein DataProvider für DataTables. Die Implementierung hat direkte Abhängigkeiten zum Verfahren wie ein SQLParseTree Spalten parst.
    /// Konkret werden zunächst alle Direktspalten in ein Mapping mit Spaltennamen und Alias gespeichert.
    /// Danach werden reine Berechnungsattribute geprüft und der . des Berechnungsattributes wird mit _ im Spaltenmapping ersetzt (Konvention des SQLParseTrees)
    /// </summary>
    public class DataTableValueProvider : DSLValueProvider
    {
        /// <summary>Fix dafür das man als Quellspalte identische Ausdrücke haben könnte, wie '' was mehrfach verwendet wird 
        /// - Der Counter ist die Anzahl der Freizeichen die angehangen werden</summary>
        int randomizeIdenticalExpressionCounter = 1;

        /// <summary>Kontexteintrag mit dem der ValueProvider arbeitet </summary>
        public DataRow context;

        /// <summary>Spaltenmapping um Alias-Begriffe und berechnete Spalten in der DataTable korrekt zu finden</summary>
        protected Dictionary<String, String> colMap = new Dictionary<string, string>();

        /// <summary>
        /// Initialisiert das Spaltenmapping für die Verarbeitung des ValueProviders aus einem SQLParsetree und einer zugehörigen Tabelle die der DataTable entsprechen soll
        /// </summary>
        /// <param name="tree">Der parseTree der den Tabellenausdruck enthält der für die Kontext-Datatable verwendet werden soll</param>
        /// <param name="tab">Die konkrete Tabelle für die die Aliase geladen werden sollen - ist der Wert null werden alle Spalten/Aliaswerte des kompletten Parsetrees initialisiert</param>
        public void initWithParseTree(SqlParseTree tree, SqlTableExpression tab)
        {
            if (tree == null) throw new ArgumentException("Parameter tree is empty - abort");

            colMap.Clear();
            foreach (SqlSelectExpression col in tree.columns)
            {
                // Ausnahme falls der Ausdruck mehrfach vorkommt und der X viele Freizeichen zufügt um den Ausdruck eindeutig zu machen
                if (colMap.ContainsKey(col.expression))
                {
                    colMap.Add(col.expression + new string(' ', randomizeIdenticalExpressionCounter), col.alias);
                    randomizeIdenticalExpressionCounter += 1;
                }
                else
                    colMap.Add(col.expression, col.alias);
            }
            if (tab != null)
            {
                foreach (string att in tab.attributesToLoad)
                {
                    if (!colMap.Keys.Contains(att))
                        colMap.Add(att, att.Replace(".", "_"));
                }
            }
            else
            {
                foreach (SqlTableExpression tabInner in tree.tables.Values)
                {
                    foreach (string att in tabInner.attributesToLoad)
                    {
                        if (!colMap.Keys.Contains(att))
                            colMap.Add(att, att.Replace(".", "_"));
                    }
                }
            }
        }

        /// <summary>Löst eine Variable einer DSL mit Hilfe des Columnmaps auf, welches mit <see cref="initWithParseTree(SqlParseTree, SqlTableExpression)"/> geladen wurde </summary>
        /// <param name="refName">Der Name/Alias der Variable</param>
        /// <returns>Der Ergebniswert</returns>
        public override object getValue(string refName)
        {
            return context[colMap[refName]];
        }
    }

    /// <summary>
    /// Ein Provider der Spaltennamen mit verschiedenen Subtabellen auflöst - er hat allerdings genau ein Kontextobjekt mit allen Spalten
    /// </summary>
    public class DataTableMultiDefinitionValueProvider : DSLValueProvider
    {
        /// <summary>Kontext zur Auflösung der Referenzen</summary>
        private DataRow _context;
        /// <summary>Kontext zur Auflösung der Referenzen</summary>
        public DataRow context {
            get { return this._context; }
            set
            {
                this._context = value;
                // Kontext wird weiter an die Subprovider gegeben die spezifische Syntaxlogik der Tabellen kennen
                foreach (DataTableValueProvider prov in resolveContext.Values)
                {
                    prov.context = value;
                }
            }
        }

        /// <summary>Liste aller bekannten Tabellen mit Alias->DataTabeValueProvider</summary>
        protected Dictionary<string, DataTableValueProvider> resolveContext { get; set; } = new Dictionary<string, DataTableValueProvider>();
        
        /// <summary>
        /// Fügt dem Provider zusätzliche Definitionen für Tabellen hinzu
        /// </summary>
        /// <param name="parseTree">parseTree aus dem die Definition der Tabelle stammt</param>
        /// <param name="tabs">Tabellendefinitionen die zugefügt werden sollen</param>
        public void addTables(SqlParseTree parseTree, params SqlTableExpression[] tabs)
        {
            if (tabs == null) throw new ArgumentException("Parameter tab is empty - abort");

            foreach (SqlTableExpression tab in tabs)
            {
                DataTableValueProvider prov = new DataTableValueProvider();
                prov.initWithParseTree(parseTree, tab);
                resolveContext.Add(tab.alias, prov);
            }
        }

        /// <summary>
        /// Löst Werte mit Hilfe der initialisierten Tabellensturktur auf
        /// </summary>
        /// <param name="refName">Der Name der aufzulösenden Spalte (muss im Format tab.col sein) </param>
        /// <returns>Der ermittelte Wert</returns>
        public override object getValue(string refName)
        {
            if (!refName.Contains(".")) throw new ArgumentException("Expression " + refName + " does not contain . for table resolve");
            string tabName = refName.Substring(0, refName.IndexOf("."));
            DataTableValueProvider prov = resolveContext[tabName];
            return prov.getValue(refName);
        }
    }

    /// <summary>
    /// Ein Funktionsinterpreter für die DataTableDSL. Unterstützt alle Basisfunktionen der msa.DSL Sprache
    /// </summary>
    public class DataTableFunctionHandler : DSLFunctionHandler
    {

        /// <summary>Erstellt einen neuen FunctionHandler für DataTables - initialsiiert Funktionen</summary>
        public DataTableFunctionHandler()
        {
            this.addConversions();
            this.addStringFunctions();
            this.addDateFunctions();
            this.addMathFunctions();
            this.addLogic();
        }
    }
}
