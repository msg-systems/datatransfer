using msa.DSL.CodeParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer.SQL
{
    /// <summary> Entspricht einem Ausdruck/Spalte einer SQL-Query im SELECT-Teil </summary>
    public class SqlSelectExpression
    {
        /// <summary> Geparstes Ausdruckselement für den Selectabschnitt</summary>
        public CodeElement expressionElement { get; set; }
        /// <summary> Text für den Selectabschnitt</summary>
        public string expression { get; protected set; }

        /// <summary> Alias des SELECT-Ausdrucks - Wenn nicht vorhanden identisch wie die expression</summary>
        public string alias { get; protected set; }

        /// <summary> Gibt an ob ein Alias existiert, wenn also expression ungleich alias ist </summary>
        public bool hasAlias { get; protected set; }

        /// <summary> Spaltenname der Spalte sofern ein Direktverweis vorliegt ohne Angabe der Tabelle </summary>
        public string colName { get; protected set; }

        /// <summary>Name der Spalte wie er in der Ergebnistabelle sein wird</summary>
        public string colNameResult { get; set; }
        
        /// <summary> Name der Basistabelle wenn .-Notation verwendet wird z.B. tab.Col - Wenn nicht im Ausdruck enthalten ist der Wert null - 
        /// Bei komplexen Ausdrücken mit multiplen Tabellen ist der Wert ebenso null</summary>
        public string baseTable { get; protected set; }

        /// <summary>Quelltabellen falls ein komplexer Berechnungsausdruck aus multiplen Tabellen vorliegt</summary>
        public List<string> baseTables { get; protected set; }

        /// <summary>Erstellt eine neue SqlSelectExpression für das angegebene expressionElement mit dem angegebenen Alias</summary>
        /// <param name="expressionElement">Der geparste Ausdruck der einer Spalte entspricht</param>
        /// <param name="alias">Der Alias sofern vorhanden, falls nicht vorhanden sollte man expressionElement.parsedExpression verwenden</param>
        public SqlSelectExpression(CodeElement expressionElement, string alias)
        {
            if (expressionElement == null) throw new ArgumentException("expressionElement is missing for SqlSelectExpression");
            this.expressionElement = expressionElement;
            this.expression = expressionElement.ToString();
            if (alias == null) alias = expressionElement.content; // Sicherheit
            this.alias = alias;
            this.hasAlias = (alias != expression);

            // Falls Codereferenz direkt prüfen und auflösen
            if (expressionElement is CodeReference)
            {
                if (expression.Contains("."))
                {
                    this.colName = this.expression.Substring(expression.IndexOf(".") + 1);
                    this.baseTable = this.expression.Substring(0, expression.IndexOf("."));
                }
                else
                {
                    this.colName = this.expression;
                    this.baseTable = null;
                }
            }
            else // wenn komplexer prüfen ob multible Basistabellen betroffen sind
            {
                this.baseTables = new List<string>();
                foreach (CodeElement codeRef in expressionElement.childElementsOf<CodeReference>())
                {
                    SqlSelectExpression selEx = new SqlSelectExpression(codeRef, codeRef.content);
                    if (!this.baseTables.Contains(selEx.baseTable))
                        this.baseTables.Add(selEx.baseTable);
                }
                if (this.baseTables.Count == 1) // wenn nur eine Tabelle betroffen übernimm diese
                    this.baseTable = this.baseTables[0];
            }

            if (!this.hasAlias)
                this.colNameResult =  this.colName;
            else
                this.colNameResult = this.alias;

        }
        
    }
}
