using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.ServiceProcess;
using System.Text;
using System.IO;
using System.Threading;


namespace WebServerManager
{
    class Utility
    {
        public static void gMAIL(string sBody, string sSubject)
        {
            try
            {
                var fromAddress = new MailAddress(ConfigurationManager.AppSettings["GMAILUserName"], "Automated Alerts");
                var emailAddresses = ConfigurationManager.AppSettings["GMAILTo"].Split(',');

                var toAddress = new MailAddress(emailAddresses[0], emailAddresses[0].Split('@')[0]);

                string fromPassword = ConfigurationManager.AppSettings["GMAILPassword"];

                string subject = String.Format("Automated Alerts on :: {0}", sSubject);


                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword),
                    Timeout = 20000
                };
                using (var message = new MailMessage(fromAddress, toAddress)
                {

                    Subject = subject,
                    Body = sBody
                })
                {
                    for (int i = 1; i < emailAddresses.Length; ++i)
                    {
                        message.CC.Add(new MailAddress(emailAddresses[i], emailAddresses[i].Split('@')[0]));
                    }

                    smtp.Send(message);
                }
            }
            catch (Exception e) { }

        }

        public static bool StopService(ServiceController sc)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                while (sc.Status != ServiceControllerStatus.Stopped)
                {
                    if (stopwatch.ElapsedMilliseconds >= Program.ServiceTimeOut)
                    {

                        stopwatch.Stop();
                        return false;
                    }

                    Thread.Sleep(10);
                    sc.Refresh();
                }

            }

            return sc.Status == ServiceControllerStatus.Stopped;

        }

        public static bool StartService(ServiceController sc)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                while (sc.Status != ServiceControllerStatus.Running)
                {
                    if (stopwatch.ElapsedMilliseconds >= Program.ServiceTimeOut)
                    {

                        stopwatch.Stop();
                        return false;
                    }

                    Thread.Sleep(10);
                    sc.Refresh();
                }

            }

            return sc.Status == ServiceControllerStatus.Running;

        }


    }
}
