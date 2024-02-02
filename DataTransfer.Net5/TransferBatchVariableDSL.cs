using msa.Data.Transfer.Database;
using msa.Data.Transfer.Model;
using msa.DSL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.Data.Transfer
{
    /// <summary>DSL-Value Provider der mit einem Variable-Dictionary umgehen kann</summary>
    public class TransferBatchDSLValueProvider : DSLValueProvider
    {
        /// <summary>Verwaltung der bekannten Variablen</summary>
        public Dictionary<String, Variable> variables;

        /// <summary>Erstellt eine neue Instanz</summary>
        /// <param name="variables">Dictionary mit dem gearbeitet werden soll - dadurch kann mittels Referenz dieses von außen beeinflusst werden</param>
        public TransferBatchDSLValueProvider(Dictionary<String, Variable> variables)
        {
            this.variables = variables;
            if (this.variables == null) this.variables = new Dictionary<string, Variable>();
        }

        /// <summary>Auflösung eines Variablenwertes in den tatsächlichen Wert - Interface-Methode</summary>
        /// <param name="refName">Name der Variable</param>
        /// <returns>Wert der Variable</returns>
        public override object getValue(string refName)
        {
            Variable var;
            if (variables.TryGetValue(refName, out var))
            {
                return var.value;
            }
            else
            {
                throw new Exception("Unknown Identifier " + refName);
            }
        }
    }

    /// <summary> Handler für Funktionen für TransferBatchExpressions</summary>
    public class TransferBatchDSLFunctionHandler : DSLFunctionHandler
    {
        /// <summary> Initialisiert die Standardfunktionen der msa.DSL </summary>
        public TransferBatchDSLFunctionHandler()
        {
            this.addConversions();
            this.addMathFunctions();
            this.addStringFunctions();
            this.addLogic();
            this.addDateFunctions();
        }
    }


}
