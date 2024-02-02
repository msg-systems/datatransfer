using System;
using System.Collections.Generic;
using msa.DSL.CodeParser;
using System.Linq;

namespace msa.DSL
{

    /// <summary>
    /// Eine Schnittstelle die Werte anhand von Namen bereitstellen kann - wie sie das tut ist egal.
    /// Wird verwendet um Variablen in DSLs zu Werten aufzulösen
    /// </summary>
    public interface IValueProvider
    {
        /// <summary>
        /// Löst die Variable refName zum Wert auf
        /// </summary>
        /// <param name="refName">Die Name der Variablen die aufgelöst werden soll</param>
        /// <returns>Der aufgelöste Wert</returns>
        object getValue(string refName);  
    }

    /// <summary> Variante eines IValueProviders für DSLs </summary>
    public abstract class DSLValueProvider : IValueProvider
    {
        /// <summary> Verweis zur DSL die den Provider enthält </summary>
        public DSLDef parentDsl { get; internal set; }

        /// <summary> Löst die Variable refName zum Wert auf </summary>
        /// <param name="refName">Die Name der Variablen die aufgelöst werden soll</param>
        /// <returns>Der aufgelöste Wert</returns>
        public abstract object getValue(string refName);      

        /// <summary> Parameterloser Constructor </summary>
        public DSLValueProvider()
        {
        }
    }

    /// <summary>
    /// Eine Schnittstelle die Funktionen anhand eines Funktionsnamens und einer Parameterliste auswertet
    /// </summary>
    public interface IFunctionHandler
    {
        /// <summary> Validiert die übergebene Funktion mit den entsprechenden Argumenten und prüft auch Falschangaben </summary>
        /// <param name="funcName"> Name der Funktion die ausgewertet werden soll </param>
        /// <param name="arguments"> Argumente für den Funktionsaufruf </param>
        /// <returns> Das Ergebnis der Funktionsauswertung </returns>
        object handleFunction(string funcName, List<CodeElement> arguments);
    }

    /// <summary>Ein FunctionHandler der von einer DSL verwendet werden kann um beliebige Funktionen auszuführen.
    /// Funktionen werden im Dictionary functionHandler gehalten. Funktionsaufrufe sind case-invariant</summary>
    public abstract class DSLFunctionHandler
    {
        /// <summary> Verweis zur DSL die den Provider enthält </summary>
        public DSLDef parentDsl { get; internal set; }

        /// <summary> Liste der bekannten Funktionshandler - Funktionen werden generell Lowercase gespeichert </summary>
        public Dictionary<string, Func<List<object>, object>> functionHandler { get; private set; } = new Dictionary<string, Func<List<object>, object>>();

        /// <summary> Validiert die übergebene Funktion mit den entsprechenden Argumenten und prüft auch Falschangaben </summary>
        /// <param name="funcName"> Name der Funktion die ausgewertet werden soll </param>
        /// <param name="arguments"> Argumente für den Funktionsaufruf </param>
        /// <returns> Das Ergebnis der Funktionsauswertung </returns>
        public object handleFunction(string funcName, List<CodeElement> arguments)
        {
            string funcNameLower = funcName.ToLower();
            if (functionHandler.ContainsKey(funcNameLower))
            {
                try
                {
                    List<object> evaluatedArguments = new List<object>();
                    foreach (CodeElement codeEl in arguments)
                    {
                        evaluatedArguments.Add(codeEl.evaluate(parentDsl));
                    }

                    return functionHandler[funcNameLower](evaluatedArguments);
                }
                catch(Exception e)
                {
                    throw new Exception($"Error on executing function {funcName}", e);
                }
            }
            else
            {
                throw new Exception("Unknown function " + funcName);
            }
        }

        /// <summary>Setzt/fügt eine Funktionsdefintion hinzu</summary>
        /// <param name="funcName">Der Name der Funktion -> Case-Insensitiv</param>
        /// <param name="functionHandler">Logik für die Funktion</param>
        public void setFunc(string funcName, Func<List<object>, object> functionHandler)
        {
            if (this.functionHandler.ContainsKey(funcName)){
                this.functionHandler[funcName.ToLower()] = functionHandler;
            }
            else
            {
                this.functionHandler.Add(funcName.ToLower(), functionHandler);
            }
        }

        /// <summary>Entfernt eine Funktion aus der Registrierung</summary>
        /// <param name="funcName">Name der Funktion die entfernt werden soll -> Case-Insensitiv</param>
        public void removeFunc(string funcName)
        {
            this.functionHandler.Remove(funcName.ToLower());
        }

        /// <summary>Erlaubt Zugriff auf Funktionen nach Name - Ist die Funktion nicht definiert wird null zurückgegeben</summary>
        /// <param name="funcName">Name der Funktion die entfernt werden soll -> Case-Insensitiv</param>
        /// <returns>Funktionshandler oder null wenn Funktion unbekannt</returns>
        public Func<List<object>, object> this[string funcName]
        {
            get
            {
                string lowerFuncName = funcName.ToLower();
                if (this.functionHandler.ContainsKey(lowerFuncName))
                {
                    return this.functionHandler[lowerFuncName];
                }
                return null;
            }
            set
            {
                this.setFunc(funcName, value);
            }
        }

        /// <summary> Parameterloser Constructor </summary>
        public DSLFunctionHandler()
        {}
    }

    /// <summary>
    /// Eind DSL-Definition um DSL-spezifische Ausdrücke auszuwerten. Wird zusammen mit einem CodeEvaluator verwendet
    /// </summary>
    public class DSLDef
    {
        /// <summary> Der Provider der die Variablenausdrücke auflöst </summary>
        public DSLValueProvider valueProvider { get; protected set; }

        /// <summary> Handler für Funktionsevaluierung der DSL </summary>
        public DSLFunctionHandler functionHandler { get; protected set; }

        /// <summary>
        /// Erstellt eine neue DSL-Definition
        /// </summary>
        /// <param name="valueProvider"> Der Provider der die Variablenausdrücke der DSL auflöst </param>
        /// <param name="functionHandler"> Der Functionhandler der Funktionen in der DSL auswertet </param>
        public DSLDef(DSLValueProvider valueProvider, DSLFunctionHandler functionHandler)
        {
            this.valueProvider = valueProvider;
            this.valueProvider.parentDsl = this;
            this.functionHandler = functionHandler;
            this.functionHandler.parentDsl = this;
        }
    }
}
