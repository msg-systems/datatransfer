using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using msa.Logging.Model;

namespace msa.Logging
{

	/// <summary>
	/// Ein Logger der die vorhandenen Möglichkeiten von DotNet nutzt (TraceSource) um Logging mit vorimplementierten TraceListenern zu nutzen.
	/// Die Konfiguration geschieht sehr einfach über gleichnamige Switches mit angegebenen LogLevel. 
	/// Listener können nach Erstellung des Loggers zugefügt oder entfernt werden. <br/>
	/// Im Fehlerfall kann eine Mail versendet werden. Wenn der Logger mittels eines logFile-Constructors erzeugt wurde, wird dieses Logfile in der Mail zugefügt.<br/>
	/// Ein Logger sollte immer explizit disposed werden. Beim impliziten Finalizer geht die Mail evt. Errormail am ende nicht raus.
	/// </summary>
	public class Logger : IDisposable
	{
		/// <summary> Möglichkeit die Formatierung des Loggers für einfache Nachrichten zu beeinflussen </summary>
		public Func<String, String> formatMessageEvent = null;
		/// <summary> Möglichkeit die Formatierung des Loggers für einfache Nachrichten mit SourceLevel zu beeinflussen </summary>
		public Func<TraceEventType, String, String> formatMessageWithLogLevelEvent = null;
		
		/// <summary> Liste aller aktiven Logger </summary>
		protected static Dictionary<String, Logger> knownLogger = new Dictionary<String, Logger>();

		/// <summary> Lock für die Anfrage von Loggern (atomar) </summary>
		protected static object lockInst = new object();
		/// <summary> Lock für das Schreiben von LogMeldungen (atomar) </summary>
		protected static object lockInst2 = new object();

		/// <summary> Prefix der vor jedem Logeintrag geschrieben werden soll </summary>
		public string prefix { get; set; }

		/// <summary> Maileinstellungen für automatische Fehlermails oder manuelle Mailbenachrichtigungen </summary>
		public MailSettings mailSettings { get; set; }
		/// <summary> Die trace-Source die für das Logging verwendet wird - mir ihr kann man nachträglich Listener zufügen oder bearbeiten </summary>
		public TraceSource trace { get; protected set;}
		/// <summary> Gibt an ob autoflush aktiv ist </summary>
		public bool autoFlush { get; set; }

		/// <summary> Gibt den Logimpact als numerischen Wert an - je kleiner desto stärker </summary>
		protected int pLogImpact = 100000;
		/// <summary> Gibt die zugehörige Nachricht zum stärksten Logimpact an </summary>
		protected string pLogImpactMessage = "";

		/// <summary> Eine Liste an TextWriterTraceListenern die verwendet wird um den Datei-Pfad des Listeners zu vermerken </summary>
		protected Dictionary<TextWriterTraceListener, string> fileListenerPaths = new Dictionary<TextWriterTraceListener, string>();

		/// <summary> Gibt an ob autoflush aktiv ist </summary>
		public TraceEventType logImpact
		{
			get
			{
				return (TraceEventType)this.pLogImpact;
			}
			set
			{
				int newVal = (int)value;
				if (newVal < this.pLogImpact)
				{
					this.pLogImpact = newVal;
				}
			}
		}

		/// <summary>
		/// Setzt den aktuellen Logimpact und die zugehörige Nachricht, falls der Log stärker war als der vorige
		/// </summary>
		/// <param name="newImpact">Der aktuelle LogImpact der gesetzt werden soll, wenn er stärker als der aktuelle ist</param>
		/// <param name="lastMessage">Die aktuelle Lognachricht</param>
		protected void setLogImpact(TraceEventType newImpact, string lastMessage)
		{
			int newVal = (int)newImpact;
			if (newVal < this.pLogImpact){
				this.pLogImpact = newVal;
				this.pLogImpactMessage = lastMessage;
			}
		}


		
		

		/// <summary> Das Loglevel des Loggers, welches initial aus der Konfiguration gelesen wird configuration/system.diagnostics/switches/add/@name. <br/>
		/// Ist keine Konfiguration angegeben wird automatisch das LogLevel Information gesetzt. Nachträgliche Anpassung im Code ist möglich.
		/// Ist eine Nachricht nicht im angegebenen Loglevel enthalten, wird sie nicht geloggt </summary>
		public SourceLevels logLevel { 
			get{
				return this.trace.Switch.Level;
			}
			set{
				this.trace.Switch.Level = value;
			}
		}

		/// <summary>
		/// Ermittelt oder holt einen vorhandenen Logger mit dem entsprechenden subject
		/// </summary>
		/// <param name="subject">Switchname des Loggers der für die Konfiguration verwendet wird</param>
		/// <returns></returns>
		public static Logger getLogger(string subject)
		{
			// Das Ermitteln eines Loggers ist atomar! - kommt sonst bei MultiThreading zu Problemen mit Mehrfachanlagen etc
			lock (lockInst)
			{
				Logger returnValue;
				if (knownLogger.ContainsKey(subject))
				{
					returnValue = knownLogger[subject];
					if (returnValue.disposed)
					{
						returnValue = new Logger(subject);
						knownLogger.Add(subject, returnValue);
					}
					return returnValue;
				}

				returnValue = new Logger(subject);
				knownLogger.Add(subject, returnValue);

				return returnValue;
			}
			
		}



		/// <summary>
		/// Erstellt einen seperaten neuen Logger mit einem Tracenamen im format subject###Guid mit Typ = Switchname aus der Config mit einem Console- und einem FileListener, dessen Log automatisch beim Fehler-Mailversand angefügt wird <br/>
		/// Der Trace-Name kann aus dem return-Wert Trace.Name abgefragt werden
		/// </summary>
		/// <param name="subject">Switchname der für die Konfiguration verwendet wird</param>
		/// <param name="logFile">Der Name des zu erstellenden Logfiles, wobei [datumsformat] mit dem aktuellen Datum ersetzt wird. Z.B. log_[dd.MM.yyyy].log wird zu log_31.12.2000.log </param>
		public static Logger createNewLoggerInstance(string subject, string logFile){
			// Das Ermitteln eines Loggers ist atomar! - kommt sonst bei MultiThreading zu Problemen mit Mehrfachanlagen etc
			lock (lockInst)
			{
				string subjectKey = subject + "###" + Guid.NewGuid().ToString();
				Logger returnValue;
				returnValue = new Logger(subjectKey, logFile);

				// Lauffähigkeit für Trace-Configs für .Net Core / 5+
				if (System.IO.File.Exists("App.config"))
				{
					XDocument xDoc = XDocument.Load("App.config");
					XElement[] switches = xDoc.XPathSelectElements("configuration/system.diagnostics/switches/add[@name='" +subject + "']").ToArray();
					if (switches.Count() > 0)
                    {
						returnValue.trace.Switch = new SourceSwitch(subject, switches[0].Attribute("value").Value);
					}
                    else
						returnValue.trace.Switch = new SourceSwitch(subject, "Information");
				}
				else
					returnValue.trace.Switch = new SourceSwitch(subject, "Information");

				knownLogger.Add(subjectKey, returnValue);
				return returnValue;
			}
		}

		/// <summary>
		/// Erstellt einen neuen Logger mit dem angegebenen Typ = Switchname aus der Config ohne weitere Listener
		/// </summary>
		/// <param name="subject">Switchname der für die Konfiguration verwendet wird</param>
		protected Logger(string subject)
		{
			// Erstelle Source
			this.trace = new TraceSource(subject);

			// Erstelle Switch (aus Config ausgelesen)
			SourceSwitch sourceSwitch = new SourceSwitch(subject, "Information"); // wenn in config nichts angegeben Information-Logging
			this.trace.Switch = sourceSwitch;
			this.prefix = "";

			this.logInfo("Init Logger " + subject); // kommt normalerweise nirgends an
		}

		/// <summary>
		/// Erstellt einen neuen Logger mit dem angegebenen Typ = Switchname aus der Config mit einem Console- und einem FileListener, dessen Log automatisch beim Fehler-Mailversand angefügt wird
		/// </summary>
		/// <param name="subject">Switchname der für die Konfiguration verwendet wird</param>
		/// <param name="logFile">Der Name des zu erstellenden Logfiles, wobei [datumsformat] mit dem aktuellen Datum ersetzt wird. Z.B. log_[dd.MM.yyyy].log wird zu log_31.12.2000.log </param>
		protected Logger(string subject, string logFile) : this(subject)
		{
			if (String.IsNullOrEmpty(logFile)) throw new ArgumentException("Logfile-Parameter should not be empty");

			// Initialisere Listener
			this.trace.Listeners.Clear();
			ConsoleTraceListener conListener = new ConsoleTraceListener();
			this.trace.Listeners.Add(conListener);

			this.addFileTraceListener(logFile);
		}


		/// <summary>
		/// Fügt einen FileListener dem aktuellen Log hinzu, der bei Mailversand auch als Protokolldatei verwendet werden kann
		/// </summary>
		/// <param name="logFile">Der Name des zu erstellenden Logfiles, wobei [datumsformat] mit dem aktuellen Datum ersetzt wird. Z.B. log_[dd.MM.yyyy].log wird zu log_31.12.2000.log </param>
		public void addFileTraceListener(string logFile)
		{
			// Ersetze Datumsformat
			Match m = Regex.Match(logFile, "\\[(.*)\\]");
			if (m.Success)
			{
				// Konvertiere das aktuelle Datum in das Datumsformat was in der Datei angeben ist
				string replacement = DateTime.Now.ToString(m.Groups[1].Value);
				logFile = logFile.Replace(m.Value, replacement);
			}

			// Pfad anlegen falls angegeben und noch nicht vorhanden
			if (logFile.Contains("\\"))
			{
				string path = logFile.Substring(0, logFile.LastIndexOf("\\"));

				// Pfad erstellen falls nicht vorhanden
				if (!String.IsNullOrEmpty(path))
				{
					path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
					if (!Directory.Exists(path))
					{
						Directory.CreateDirectory(path);
					}
				}
			}

			// Erstelle File-Listener
			string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFile);
			TextWriterTraceListener fileLogger = new TextWriterTraceListener(logPath);
			this.trace.Listeners.Add(fileLogger);
			this.fileListenerPaths.Add(fileLogger, logPath);
		}

		/// <summary>
		/// Formatiert eine Nachricht im Format Timestamp;;Nachricht oder im selbst angegebenen Format über den Handler formatMessageEvent, sofern angegeben
		/// </summary>
		/// <param name="message">die zu formatierende Nachricht</param>
		/// <returns>Die formatierte Nachricht</returns>
		protected string formatMessage(string message)
		{
			if (this.formatMessageEvent != null)
				return this.formatMessageEvent(message);
			else
				return String.Format("{0};;Thread {1}; \"{2}\"", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), Thread.CurrentThread.ManagedThreadId, this.prefix + message);
		}

		/// <summary>
		/// Formatiert eine Nachricht im Format Timestamp;EventTyp;Nachricht oder im selbst angegebenen Format über den Handler formatMessageWithLogLevelEvent, sofern angegeben
		/// </summary>
		/// <param name="logLevel">Das Loglevel welches der Nachricht entspricht</param>
		/// <param name="message">die zu formatierende Nachricht</param>
		/// <returns>Die formatierte Nachricht</returns>
		protected string formatMessage(TraceEventType logLevel, string message)
		{
			if (this.formatMessageWithLogLevelEvent != null)
				return this.formatMessageWithLogLevelEvent(logLevel, message);
			else
				return String.Format("{0};{1};Thread {2};\"{3}\"", DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), logLevel.ToString(), Thread.CurrentThread.ManagedThreadId, this.prefix + message);
		}


		/// <summary>
		/// Loggt eine Nachricht vom Typ Verbose (geschwätzig - noch granularer als Info) - wird nur bei entsprechendem logLevel geloggt
		/// </summary>
		/// <param name="message">Die zu loggende Nachricht</param>
		/// <see cref="msa.Logging.Logger.logLevel"/>
		public void logVerbose(string message){
			this.log(TraceEventType.Verbose, message );
		}


		/// <summary>
		/// Loggt eine Nachricht vom Typ Info - wird nur bei entsprechendem logLevel geloggt
		/// </summary>
		/// <param name="message">Die zu loggende Nachricht</param>
		/// <see cref="msa.Logging.Logger.logLevel"/>
		public void logInfo(string message)
		{
			this.log(TraceEventType.Information, message);
		}


		/// <summary>
		/// Loggt eine Nachricht vom Typ Warnung - wird nur bei entsprechendem logLevel geloggt
		/// </summary>
		/// <param name="message">Die zu loggende Nachricht</param>
		/// <see cref="msa.Logging.Logger.logLevel"/>
		public void logWarning(string message)
		{
			this.log(TraceEventType.Warning, message);
		}


		/// <summary>
		/// Loggt eine Nachricht vom Typ Error - wird nur bei entsprechendem logLevel geloggt. Löst bei entsprechender Konfiguration einen Mailversand aus
		/// </summary>
		/// <param name="message">Die zu loggende Nachricht</param>
		/// <see cref="msa.Logging.Logger.logLevel"/>
		public void logError(string message)
		{
			this.log(TraceEventType.Error, message);
		}

		/// <summary>
		/// Loggt eine Nachricht vom Typ Error - wird nur bei entsprechendem logLevel geloggt. Löst bei entsprechender Konfiguration einen Mailversand aus. Der Fehler wird danach sofort wieder geworfen.
		/// </summary>
		/// <param name="e">Die Exception die geloggt werden soll und die dann geworfen wird</param>
		/// <see cref="msa.Logging.Logger.logLevel"/>
		public void logErrorAndRethrow(Exception e)
		{
			this.logError(e.ToString());
			throw e;
		}


		/// <summary>
		/// Loggt eine Nachricht vom Typ Critical - wird nur bei entsprechendem logLevel geloggt. Löst bei entsprechender Konfiguration einen Mailversand aus. 
		/// </summary>
		/// <param name="message">Die zu loggende Nachricht</param>
		/// <see cref="msa.Logging.Logger.logLevel"/>
		public void logCritical(string message)
		{
			this.log(TraceEventType.Critical, message);
		}

		/// <summary>
		/// Loggt eine Nachricht vom Typ Critical - wird nur bei entsprechendem logLevel geloggt. Löst bei entsprechender Konfiguration einen Mailversand aus. Der Fehler wird danach sofort wieder geworfen.
		/// </summary>
		/// <param name="e">Die Exception die geloggt werden soll und die dann geworfen wird</param>
		/// <see cref="msa.Logging.Logger.logLevel"/>
		public void logCriticalAndRethrow(Exception e)
		{
			this.logCritical(e.ToString());
			throw e;
		}

		/// <summary>
		/// Loggt eine Nachricht vom übergebenen Typ - wird nur bei entsprechendem logLevel geloggt - kann eine Mail bei logLevel Error/Critical auslösen sofern entsprechend konfiguriert
		/// </summary>
		/// <param name="logLevel">Der Logtyp für die Nachricht</param>
		/// <param name="message">Die zu loggende Nachricht</param>
		/// <see cref="msa.Logging.Logger.logLevel"/>
		public void log(TraceEventType logLevel, string message)
		{
			// Übernahme Loglevel
			this.setLogImpact(logLevel, message); 

			// Muss so gemacht werden um das Meldungsformat zu definieren/überschreiben
			if (this.trace.Switch.ShouldTrace(logLevel))
			{	
				// Nachricht formatieren
				string formattedMessage = this.formatMessage(logLevel, message); 

				// Das schreiben in den Log ist atomar! - kommt sonst bei MultiThreading zu Problemen
				lock (lockInst2)
				{
					foreach (TraceListener tl in this.trace.Listeners)
					{
						tl.WriteLine(formattedMessage);
					}
					if (this.autoFlush) this.trace.Flush();
				}
			}
		}

		/// <summary>
		/// Sendet unabhängig vom Logging eine Mail mit den aktuellen mailSetting-Einstellungen
		/// </summary>
		/// <param name="ex">Die Exception die als Mail gesendet werden soll</param>
		/// <param name="withLog">Gib an ob der Log eines evt. angehangenen TextWriterTraceListener mit verschickt werden soll</param>
		/// <see cref="msa.Logging.Logger.mailSettings"/>
		public void sendErrorMail(Exception ex, bool withLog = false)
		{
			this.sendErrorMail(ex.ToString(), withLog);
		}

		/// <summary>
		/// Sendet unabhängig vom Logging eine Mail mit den aktuellen mailSetting-Einstellungen
		/// </summary>
		/// <param name="message">Die Nachricht die per Mail gesendet werden soll (in HTML)</param>
		/// <param name="withLog">Gib an ob der Log eines evt. angehangenen TextWriterTraceListener mit verschickt werden soll</param>
		/// <see cref="msa.Logging.Logger.mailSettings"/>
		public void sendErrorMail(string message, bool withLog = false)
		{
			this.logInfo("Send errormail");
			try
			{
				if (this.mailSettings != null)
				{
					if (withLog)
					{
						bool mailSent = false;
						for (int i = 0; i < this.trace.Listeners.Count; i++)
						{
							TraceListener tl = this.trace.Listeners[i];
							if (tl is TextWriterTraceListener)
							{
								tl.Close(); // schließen für File-Logs
								if ((int)this.mailSettings.sendMailOnErrorLevel >= this.pLogImpact)
								{
									TextWriterTraceListener x = (TextWriterTraceListener)tl;
									if (this.fileListenerPaths.ContainsKey(x))
									{
										this.mailSettings.sendMail("Error for " + this.trace.Name + ": " + message, this.fileListenerPaths[x]);
										return;
									}
								}
							}
						}
						// Wenn keinen TraceListener mit Datei gefunden, sende einfach so
						if ( !mailSent ){
							this.mailSettings.sendMail("Error for " + this.trace.Name + ": " + message);
						}
					}
					else
					{
						this.mailSettings.sendMail(message);
					}
				}
			}
			catch (Exception mailEx)
			{
				this.logInfo("Error on send mail: \n" + mailEx);
			}
		}

		#region IDisposable Members

		/// <summary> IDisposable-Logik - gibt an ob bereits disposed für Garbage Collector </summary>
		protected bool disposed = false;

		/// <summary>
		/// IDisposable-Logik - gibt Logger alle wieder frei
		/// </summary>
		/// <param name="disposing">Angabe ob durch Dispose = true oder durch Finalizer = false aufgerufen wurde </param>
		protected void Dispose(bool disposing)
		{
			if (disposed) return;

			// Aus Liste bekannter Logger entfernen
			try	{ knownLogger.Remove(this.trace.Name); }
			catch { } // Fehler ignorieren

			// nur wenn explizit disposed - Speicherfreigabe managed Objekte 
			if (disposing)
			{
				// keine Objekte vorhanden
			}
			
			this.logInfo("Dispose logger " + this.trace.Name);
			//System.Console.WriteLine("Dispose logger " + this.trace.Name);
			TraceListener toRemove = null;
			this.trace.Flush();

			// Prüfe Logs durch und versende ggf. eine Mail wenn angefordert und TextWriterListener vorhanden
			bool mailSent = false;
			for (int i = 0; i < this.trace.Listeners.Count; i++ )
			{
				TraceListener tl = this.trace.Listeners[i];
				tl.Flush(); // Logs flushen
				if (tl is TextWriterTraceListener)
				{
					tl.Close(); // schließen für File-Logs
					toRemove = tl;
					if (!mailSent && this.mailSettings != null && (int)this.mailSettings.sendMailOnErrorLevel >= this.pLogImpact)
					{
						TextWriterTraceListener x = (TextWriterTraceListener)tl;
						if (this.fileListenerPaths.ContainsKey(x))
						{
							this.mailSettings.sendMail("Error for " + this.trace.Name + ": " + this.pLogImpactMessage, this.fileListenerPaths[x]); 
							mailSent = true;
						}
					}
				}
			}
			// Wenn Mail angefordert aber kein TextWriterListener vorhanden war, dann sende einfache Mail ohne Anhang
			if (!mailSent && this.mailSettings != null && (int)this.mailSettings.sendMailOnErrorLevel >= this.pLogImpact){
				this.sendErrorMail("Error for " + this.trace.Name + ": " + this.pLogImpactMessage);		
			}
			

			//System.Console.WriteLine("Dispose logger done " + this.trace.Name);

			// immer ausführen - Speicherfreigabe unmanaged Objects
			disposed = true;
		}

		/// <summary>
		/// IDisposable-Logik - Reguläres manuelles Dispose 
		/// </summary>
		public void Dispose()
		{
			//Console.WriteLine("Dispose regular");
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// IDisposable-Logik - Garbage Collector Dispose 
		/// </summary>
		~Logger()
		{
			//Console.WriteLine("Dispose Finalizer");
			this.Dispose(false);
		}

		#endregion
	}
}
