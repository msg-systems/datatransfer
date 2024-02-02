using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Xml.Serialization;

namespace msa.Logging.Model
{
	/// <summary>
	/// Einstellungen für einen Mailversand in der Loggingkomponente
	/// </summary>
	[Serializable]
	public class MailSettings
	{

		/// <summary> Name des SMTP-Servers </summary>
		[XmlAttribute()]
		public string smtpServer { get; set; }

		/// <summary> Name des SMTP-Anmeldeusers - keine Angabe = keine Authentifizierung </summary>
		[XmlAttribute()]
		public string smtpUser { get; set; }

		/// <summary> Name des SMTP-User-Passworts - keine Angabe = keine Authentifizierung </summary>
		[XmlAttribute()]
		public string smtpPassword { get; set; }

		/// <summary> Angabe des Absenders der Email </summary>
		[XmlAttribute()]
		public string sendFrom { get; set; }

		/// <summary> Betreff der Email </summary>
		[XmlAttribute()]
		public string subject { get; set; }

		/// <summary> Empfänger der Mail </summary>
		[XmlElement("sendTo")]
		public List<string> sendTo = new List<string>();

		/// <summary> Nachricht der Mail - &lt;message&gt; als Platzhalter für die vom Code übergebene Nachricht - ansonsten ist reguläre HTML-Formatierung möglich </summary>
		public string message { get; set; }

		/// <summary> LogImpact ab dem einmalig eine Mail beim Dispose des Loggers gesendet werden soll </summary>
		[XmlAttribute()]
		public TraceEventType sendMailOnErrorLevel = TraceEventType.Critical;

		/// <summary> Initialisiert eine leere MailSettings-Instanz </summary>
		public MailSettings()
		{
		}

		/// <summary>
		/// Sendet eine Mail mit den lokalen Einstellungen
		/// </summary>
		/// <param name="messageParam">Nachricht die gesendet bzw. in das Nachrichten-Template message (&lt;message&gt;) eingefügt werden soll</param>
		/// <param name="attachments">Anhänge für die Mail als Pfad</param>
		public void sendMail(string messageParam, params string[] attachments)
		{
			try
			{
				// Empfänger wird hier als dummy initialisiert, da er angegeben werden muss
				using (MailMessage mailMessage = new MailMessage(this.sendFrom, "dummy@msg.de"))
				{
					// Lösche Dummy-Empfänger und füge reale Empfänger ein
					mailMessage.To.Clear();
					foreach (string sendTo in this.sendTo)
					{
						mailMessage.To.Add(sendTo);
					}

					mailMessage.Subject = this.subject;

					// <message> ist ein Platzhalter in der Config
					mailMessage.IsBodyHtml = true;
					if (this.message.Contains("<message>"))
					{
						mailMessage.Body = this.message.Replace("<message>", messageParam);
					}
					else
					{
						mailMessage.Body = "Nachricht: " + messageParam;
					}

					mailMessage.Priority = MailPriority.High;

					// Anhänge anfügen
					foreach (String attachment in attachments)
					{
						if (File.Exists(attachment))
							mailMessage.Attachments.Add(new Attachment(attachment));
					}

					// Mail client initialisieren und senden
					using (SmtpClient client = new SmtpClient(this.smtpServer))
					{
						if (!String.IsNullOrWhiteSpace(this.smtpUser) && !String.IsNullOrWhiteSpace(this.smtpPassword))
						{
							client.Credentials = new System.Net.NetworkCredential(this.smtpUser, this.smtpPassword);
							client.UseDefaultCredentials = false;
						}

						client.Send(mailMessage);
					}
				}
			}
			catch (Exception e)
			{
				throw e;
			}
		}
	}
}
