using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace msa.DSL
{
    /// <summary>
    /// Funktionabibliothek für Standardfunktionen die in Functionhandler eingebunden werden können mittels ExtensionMethods
    /// </summary>
    public static class FunctionHandlerLib
    {
        /// <summary>
        /// <para>Fügt mathematische Funktionen hinzu</para>
        /// <list type="bullet">
        ///   <item>sin(x) - Sinus-Funktion</item>
        ///   <item>cos(x) - Cosinus-Funktion</item>
        ///   <item>tan(x) - Tangens-Funktion</item>
        ///   <item>abs(x) - Betrags-Funktion</item>
        ///   <item>pi() - Konstante PI</item>
        ///   <item>ceiling(x) - Aufrund-Funktion</item>
        ///   <item>floor(x) - Abrund-Funktion</item>
        ///   <item>max(x,y) - Maximal-Funktion</item>
        ///   <item>min(x,y) - Minimal-Funktion</item>
        ///   <item>rnd(x[,y]) / round - Runden-Funktion - x wird gerundet auf y-Dezimalstallen, ohne y-parameter immer auf Ganzzahl</item>
        /// </list> 
        /// </summary>
        /// <param name="handlerProvider">Extensionobjekt</param>
        public static void addMathFunctions(this DSLFunctionHandler handlerProvider)
        {
            handlerProvider["sin"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("sin function has more or less than one argument");
                    return Math.Sin(Convert.ToDouble(args[0]));
                };

            handlerProvider["cos"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("cos function has more or less than one argument");
                    return Math.Cos(Convert.ToDouble(args[0]));
                };

            handlerProvider["tan"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("tan function has more or less than one argument");
                    return Math.Tan(Convert.ToDouble(args[0]));
                };

            handlerProvider["abs"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("abs function has more or less than one argument");
                    return Math.Abs(Convert.ToDouble(args[0]));
                };

            handlerProvider["pi"] = (args) => Math.PI;

            handlerProvider["ceiling"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("ceiling function has more or less than one argument");
                    return Math.Ceiling(Convert.ToDouble(args[0]));
                };

            handlerProvider["floor"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("floor function has more or less than one argument");
                    return Math.Floor(Convert.ToDouble(args[0]));
                };

            handlerProvider["rnd"] = handlerProvider["round"] =
                (args) =>
                {
                    if (args.Count < 1 || args.Count > 2) throw new ArgumentException("rnd function has not 1-2 arguments");
                    if (args.Count == 1)
                        return Math.Round(Convert.ToDouble(args[0]));
                    else
                        return Math.Round(Convert.ToDouble(args[0]), Convert.ToInt32(args[1]));
                };

            handlerProvider["max"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("max function has not 2 arguments");
                    return Math.Max(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]));
                };

            handlerProvider["min"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("min function has not 2 arguments");
                    return Math.Min(Convert.ToDouble(args[0]), Convert.ToDouble(args[1]));
                };
        }

        /// <summary>
        /// <para>Fügt Text Funktionen hinzu</para>
        /// <list type="bullet">
        ///   <item>toUpper(x) / upper - Umwandlung in Großbuchstaben</item>
        ///   <item>toLower(x) / lower - Umwandlung in Kleinbuchstaben</item>
        ///   <item>indexOf(str,search[, startindex]) / instr - Sucht in einem String str den Text search ab der Position startindex - Rückgabe Indexposition oder-1 wenn ncihts gefunden</item>
        ///   <item>replace(str, search, replace) - Ersetzungsfunktion-Funktion</item>
        ///   <item>substring(str, startindex[, lentgth]) - Ermittelt einen Teilstring von str ab der Position startindex mit der Länge von length</item>
        ///   <item>strcontains(str, search) / contains - Prüft ob in String str der Text search vorkommt - true/false</item>
        ///   <item>left/strleft(text, search) - sucht in Text das Vorkommen von Search und gibt den Text bis zu diesem Ausdruck von links gelesen zurück</item>
        ///   <item>right/strright(text, search) - sucht in Text das Vorkommen von Search und gibt den Text von diesem Ausdruck von links gelesen bis zum Schluss zurück</item>
        ///   <item>mid/strmid(text, searchstart, searchEnd) - sucht in Text das Vorkommen von Searchstart und searchEnd und gibt den Text dazwischen zurück</item>
        ///   <item>startsWith(text, searchText) - Prüft ob der Text text mit searchText beginnt - true/false</item>
        ///   <item>endsWith(text, searchText) - Prüft ob der Text text mit searchText endet - true/false</item>
        /// </list> 
        /// </summary>
        /// <param name="handlerProvider">Extensionobjekt</param>
        public static void addStringFunctions(this DSLFunctionHandler handlerProvider)
        {
            handlerProvider["toupper"] = handlerProvider["upper"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("toupper/upper function has more or less than one argument");
                    return args[0].ToString().ToUpper();
                };

            handlerProvider["tolower"] = handlerProvider["lower"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("tolower/lower function has more or less than one argument");
                    return args[0].ToString().ToLower();
                };

            handlerProvider["indexof"] = handlerProvider["instr"] =
                (args) =>
                {
                    if (args.Count < 2 || args.Count > 3) throw new ArgumentException("indexof/instr function has more or less than 2-3 arguments - searchstring, searchfor, startAtIndex");
                    if (args.Count == 2)
                    {
                        return args[0].ToString().IndexOf(args[1].ToString());
                    }
                    else
                    {
                        return args[0].ToString().IndexOf(args[1].ToString(), Convert.ToInt32(args[2]));
                    }
                };

            handlerProvider["replace"] =
                (args) =>
                {
                    if (args.Count != 3) throw new ArgumentException("replace function has more or less than 3 arguments - text, searchtext, replacetext");
                    return args[0].ToString().Replace(args[1].ToString(), args[2].ToString());
                };

            handlerProvider["substring"] =
                (args) =>
                {
                    if (args.Count < 2 || args.Count > 3) throw new ArgumentException("substring function has more or less than 2-3 arguments - text, startindex, length");
                    if (args.Count == 2)
                    {
                        return args[0].ToString().Substring(Convert.ToInt32(args[1]));
                    }
                    else
                    {
                        return args[0].ToString().Substring(Convert.ToInt32(args[1]), Convert.ToInt32(args[2]));
                    }
                };

            handlerProvider["strcontains"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("replace/contains function has more or less than 2 arguments - text, searchtext");
                    return args[0].ToString().Contains(args[1].ToString());
                };
            handlerProvider["contains"] = handlerProvider.functionHandler["strcontains"];

            handlerProvider["strLeft"] = handlerProvider["left"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("strleft/left function has more or less than 2 arguments - text, searchtext");
                    string searchString = args[0].ToString();
                    int index = searchString.IndexOf(args[1].ToString());
                    if (index == -1)
                        return "";
                    else
                        return searchString.Substring(0, index );
                };

            handlerProvider["strRight"] = handlerProvider["right"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("strRight/right function has more or less than 2 arguments - text, searchtext");
                    string searchString = args[0].ToString();
                    string searchExpr = args[1].ToString();
                    int index = searchString.LastIndexOf(searchExpr);
                    if (index == -1)
                        return "";
                    else
                        return searchString.Substring(index + searchExpr.Length);
                };

            handlerProvider["strMid"] = handlerProvider["mid"] =
                (args) =>
                {
                    if (args.Count != 3) throw new ArgumentException("strMid/mid function has more or less than 3 arguments - text, searchtextstart, searchTextEnd");
                    string searchString = args[0].ToString();
                    string searchExprStart = args[1].ToString();
                    string searchExprEnd = args[2].ToString();

                    int startIndex = searchString.IndexOf(searchExprStart);
                    if (startIndex == -1) return "";

                    startIndex += searchExprStart.Length;
                    int endIndex = searchString.IndexOf(searchExprEnd, startIndex);
                    if (endIndex == -1) return "";

                    return searchString.Substring(startIndex, endIndex-startIndex);
                };

            handlerProvider["startsWith"] =
                (args) =>
                {
                    if (args.Count < 2) throw new ArgumentException("startsWith function has more or less than 2 arguments - text, searchtextstart");
                    return args[0].ToString().StartsWith(args[1].ToString());
                };

            handlerProvider["endsWith"] =
                (args) =>
                {
                    if (args.Count < 2) throw new ArgumentException("ends function has more or less than 2 arguments - text, searchtextstart");
                    return args[0].ToString().EndsWith(args[1].ToString());
                };

        }

        /// <summary>
        /// <para>Fügt Text Funktionen hinzu</para>
        /// <list type="bullet">
        ///   <item>date(ticks), date(year, month, day [, hour, minute, second]) - Erstellung eines Datums anhand der Komponenten </item>
        ///   <item>addSeconds(date, adjust) - Fügt dem Datum [date] [adjust]-viele Sekunden hinzu </item>
        ///   <item>addMinutes(date, adjust) - Fügt dem Datum [date] [adjust]-viele Minuten hinzu </item>
        ///   <item>addHours(date, adjust) - Fügt dem Datum [date] [adjust]-viele Stunden hinzu </item>
        ///   <item>addDays(date, adjust) - Fügt dem Datum [date] [adjust]-viele Tage hinzu </item>
        ///   <item>addMonths(date, adjust) - Fügt dem Datum [date] [adjust]-viele Monate hinzu </item>
        ///   <item>addYears(date, adjust) - Fügt dem Datum [date] [adjust]-viele Jahre hinzu </item>
        ///   <item>Second(date) - Gibt den Sekunden-Teil eines Datums zurück </item>
        ///   <item>Minute(date) - Gibt den Minuten-Teil eines Datums zurück </item>
        ///   <item>Hour(date) - Gibt den Stunden-Teil eines Datums zurück </item>
        ///   <item>Day(date) - Gibt den Tag-Teil eines Datums zurück </item>
        ///   <item>Month(date) - Gibt den Monats-Teil eines Datums zurück </item>
        ///   <item>Year(date) - Gibt den Jahr-Teil eines Datums zurück </item>
        /// </list> 
        /// </summary>
        /// <param name="handlerProvider">Extensionobjekt</param>
        public static void addDateFunctions(this DSLFunctionHandler handlerProvider)
        {
            handlerProvider["date"] =
                (args) =>
                {
                    if (args.Count != 1 && args.Count != 3 && args.Count != 6) throw new ArgumentException("date function has more or less than 3 or 6 arguments - ticks / year, month, day, hour, minute, second");
                    if (args.Count == 1)
                    {
                        return new DateTime(Convert.ToInt64(args[0]));
                    }
                    else if (args.Count == 3)
                    {
                        return new DateTime(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]));
                    }
                    else
                    {
                        return new DateTime(Convert.ToInt32(args[0]), Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), Convert.ToInt32(args[3]), Convert.ToInt32(args[4]), Convert.ToInt32(args[5]));
                    }  
                };

            handlerProvider["addSeconds"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("addSeconds/adjustSeconds function has more or less than 2 arguments - date, adjustrange");
                    return Convert.ToDateTime(args[0]).AddSeconds(Convert.ToDouble(args[1]));
                };
            handlerProvider["adjustSeconds"] = handlerProvider["addSeconds"];

            handlerProvider["addMinutes"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("addMinutes/adjustMinutes function has more or less than 2 arguments - date, adjustrange");
                    return Convert.ToDateTime(args[0]).AddMinutes(Convert.ToDouble(args[1]));
                };
            handlerProvider["adjustMinutes"] = handlerProvider["addMinutes"];

            handlerProvider["addHours"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("addHours/adjustHours function has more or less than 2 arguments - date, adjustrange");
                    return Convert.ToDateTime(args[0]).AddHours(Convert.ToDouble(args[1]));
                };
            handlerProvider["adjustHours"] = handlerProvider["addHours"];

            handlerProvider["addDays"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("addDays/adjustDays function has more or less than 2 arguments - date, adjustrange");
                    return Convert.ToDateTime(args[0]).AddDays(Convert.ToDouble(args[1]));
                };
            handlerProvider["adjustDays"] = handlerProvider["addDays"];

            handlerProvider["addMonths"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("addMonths/adjustMonths function has more or less than 2 arguments - date, adjustrange");
                    return Convert.ToDateTime(args[0]).AddMonths(Convert.ToInt32(args[1]));
                };
            handlerProvider["adjustMonths"] = handlerProvider["addMonths"];

            handlerProvider["addYears"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("addYears/adjustYears function has more or less than 2 arguments - date, adjustrange");
                    return Convert.ToDateTime(args[0]).AddYears(Convert.ToInt32(args[1]));
                };
            handlerProvider["adjustYears"] = handlerProvider["addYears"];

            handlerProvider["second"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("seconds function has more or less than 1 argument");
                    return Convert.ToDateTime(args[0]).Second;
                };

            handlerProvider["Minute"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("Minutes function has more or less than 1 argument");
                    return Convert.ToDateTime(args[0]).Minute;
                };

            handlerProvider["Hour"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("Hours function has more or less than 1 argument");
                    return Convert.ToDateTime(args[0]).Hour;
                };

            handlerProvider["Day"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("Days function has more or less than 1 argument");
                    return Convert.ToDateTime(args[0]).Day;
                };

            handlerProvider["Month"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("Months function has more or less than 1 argument");
                    return Convert.ToDateTime(args[0]).Month;
                };

            handlerProvider["Year"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("Years function has more or less than 1 argument");
                    return Convert.ToDateTime(args[0]).Year;
                };
        }

        /// <summary>
        /// <para>Fügt Konvertierungs-Funktionen hinzu</para>
        /// <list type="bullet">
        ///   <item>cstr(x) - Umwandlung in Text</item>
        ///   <item>cbool(x) - Umwandlung in Boolean</item>
        ///   <item>cint(x) - Umwandlung in Integer</item>
        ///   <item>cdbl(x) - Umwandlung in Double</item>
        ///   <item>cdate(x) - Umwandlung in Date</item>
        ///   <item>cchar(x) - Umwandlung in Char</item>
        /// </list> 
        /// </summary>
        /// <param name="handlerProvider">Extensionobjekt</param>
        public static void addConversions(this DSLFunctionHandler handlerProvider)
        {
            handlerProvider["cstr"] = (args) => { if (args.Count == 1) { return args[0].ToString(); } else { throw new ArgumentException("cstr function has more or less than one argument"); } };
            handlerProvider["cbool"] = (args) => { if (args.Count == 1) { return Convert.ToBoolean(args[0]); } else { throw new ArgumentException("cbool function has more or less than one argument"); } };
            handlerProvider["cint"] = (args) => { if (args.Count == 1) { return Convert.ToInt64(args[0]); } else { throw new ArgumentException("cint function has more or less than one argument"); } };
            handlerProvider["cdbl"] = (args) => { if (args.Count == 1) { return Convert.ToDouble(args[0]); } else { throw new ArgumentException("cdbl function has more or less than one argument"); } };
            handlerProvider["cdate"] = (args) => { if (args.Count == 1) { return Convert.ToDateTime(args[0]); } else { throw new ArgumentException("cdate function has more or less than one argument"); } };
            handlerProvider["cchar"] = (args) => { if (args.Count == 1) { return Convert.ToChar(args[0]); } else { throw new ArgumentException("cchar function has more or less than one argument"); } };
        }

        /// <summary>
        /// <para>Fügt Logik-Funktionen hinzu</para>
        /// <list type="bullet">
        ///   <item>if(cond1, truecase1, cond2, truecase2, cond3, truecase3, ..., elsecase) / iif, case, casewhen
        ///   if - funktion die beliebig viele cases erlaubt - muss immer eine ungerade Zahl an rgumenten haben, geradzahlige Argumente sind Bedingungen, ungeradzahlige Ergebnisse </item>
        ///   <item> nvl(checkVal, elseVal) - prüft ob checkVal null is und wenn nicht gibt den Wert zurück, sonst elseVal </item>
        ///   <item> not(bool) - Kehrt einen Wahrheitswert um </item>
        /// </list> 
        /// </summary>
        /// <param name="handlerProvider">Extensionobjekt</param>
        public static void addLogic(this DSLFunctionHandler handlerProvider)
        {
            handlerProvider["if"] = handlerProvider["iif"] = handlerProvider["case"] = handlerProvider["casewhen"] =
                (args) =>
                {
                    if (args.Count < 3) throw new ArgumentException("if/iif/case/casewhen function needs at least 3 arguments - condition, true case, false case");
                    if (args.Count % 2 != 1) throw new ArgumentException("if/iif/case/casewhen function needs an uneven amaount of arguments - con1, truecase1, con2, truecase2, con3, truecase3, elseCase ");

                    int limit = args.Count - 1;
                    for (int i = 0; i < limit; i += 2)
                    {
                        if (Convert.ToBoolean(args[i]))
                        {
                            return args[i + 1];
                        }
                    }
                    return args[args.Count - 1];
                };

            handlerProvider["nvl"] =
                (args) =>
                {
                    if (args.Count != 2) throw new ArgumentException("nvl function needs 2 arguments - testVal, elseVal");

                    if (args[0] == null) return args[1];
                    if (args[0] is DBNull) return args[1];
                    return args[0];
                };

            handlerProvider["not"] =
                (args) =>
                {
                    if (args.Count != 1) throw new ArgumentException("not function needs 1 arguments");

                    if (args[0] == null) return args[1];
                    if (args[0] is DBNull) return args[1];
                    return !Convert.ToBoolean(args[0]);
                };
        }
    }
}
