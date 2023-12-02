using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceProcess;
using System.IO;
using System.Net;
using iControl;


namespace WebServerManager
{
    class Program
    {
        public static TimeSpan waitTime = new TimeSpan(0, 1, 0);
        public static long TimeOut = long.TryParse(ConfigurationManager.AppSettings["TimeOut"], out TimeOut) ? TimeOut : 600000;
        public static long ServiceTimeOut = long.TryParse(ConfigurationManager.AppSettings["ServiceTimeOut"], out ServiceTimeOut) ? ServiceTimeOut : 300000;

        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            string sFileName = DateTime.Now.ToString("yyyyMMdd")+ ".tab";
            string sStatsDir = ConfigurationManager.AppSettings["StatsOutputDir"];
            string LocalDomain = ConfigurationManager.AppSettings["DomainName"];

            if (!Directory.Exists(sStatsDir))
                Directory.CreateDirectory(sStatsDir);
                      
            StreamWriter sw = new StreamWriter(Path.Combine(sStatsDir, sFileName ));

            Process[] proc;
            PerformanceCounter perfc;
            PerformanceCounter perfErrorsScript;
            float fRequests;
            string sMessage;
            int ControlC = 0;
            int i = 0;

            int iTotalCores = Environment.ProcessorCount;
            int iMaxCores = iTotalCores - 2;
            int iTotalUsableCores = iTotalCores <= iMaxCores ? iTotalCores : iMaxCores;

            var options = new ParallelOptions();
            options.MaxDegreeOfParallelism = iTotalUsableCores;

            //todo::Move to db
            string[] Servers = ConfigurationManager.AppSettings["ServersToMonitor"].Split(',');

            string sPoolName = ConfigurationManager.AppSettings["IP"] ?? "BLANK";

            // Setup a cancel event handler.
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                if (ControlC == 0)
                {
                    Console.Write("\n");
                    Console.WriteLine("*** This program has been prematurely terminated by the user      ***");
                    Console.WriteLine("*** and will exit after the current server checks have completed processing. ***");
                    Console.Write("\n");
                }
                ControlC++;
            };


            //run for-evah-evah, or until CTRL-C
            do
            {
                if (ControlC != 0)
                    break;

                Parallel.ForEach(Servers, options, async myServer =>
                {
                    try
                    {

                        IPAddress hostIPAddress = IPAddress.Parse(myServer);
                        IPHostEntry hostInfo = Dns.GetHostEntry(hostIPAddress);
                        // Get the IP address list that resolves to the host names contained in
                        // the Alias property.
                        IPAddress[] address = hostInfo.AddressList;

                        // Get the alias names of the addresses in the IP address list.
                        string sMachineName = hostInfo.HostName.Replace("." + LocalDomain, "");
                        proc = Process.GetProcesses(myServer);
                        string sPool;

                        double mem = double.Parse("0");
                        double acmem = double.Parse("0");

                        foreach (Process myProc in proc)
                        {
                            if (myProc.ProcessName == "DLLHOST")
                                mem += ((myProc.WorkingSet64) / 1024f);

                            if (myProc.ProcessName == "AddressCorrecti")
                                acmem += ((myProc.WorkingSet64) / 1024f);


                        }
                        perfc = new PerformanceCounter("Active Server Pages", "Requests Executing", "", myServer);
                        perfErrorsScript = new PerformanceCounter("Active Server Pages", "Errors From Script Compilers", "", myServer);
                        fRequests = perfc.NextValue();
                        
                        string Message = String.Format("{0}\t{1}\t{2}K\t{3}K\t{4}", sMachineName, DateTime.Now.ToString("yyyy-MM-dd HH:mm"), mem.ToString("###,###,##0"), acmem.ToString("###,###,##0"), fRequests.ToString());
                        Console.WriteLine(Message);                        
                        await sw.WriteLineAsync(Message);

                        if (mem >= 825000.00 || acmem >= 350000.00 || perfErrorsScript.RawValue > 0)
                        {
                            //Get BigIP PoolName to use
                            //TODO: Grab Pool Name from BigIP instead of having to app.config the pools
                            sPool = ConfigurationManager.AppSettings[myServer.Substring(0,
                                    myServer.LastIndexOf('.') + 1)] ?? "BLANK";

                            try
                            {
                                //make call to BigIP to disable state
                                f5 BigIP = new f5();
                                if (BigIP.bInitialized)
                                {
                                    if (BigIP.getPoolMemberState(sPool, myServer + ":80").Contains("STATE_ENABLED"))
                                    {

                                        BigIP.setPoolMemberState(sPool, myServer + ":80", "disable");

                                        //wait until ASPs are done executing
                                        Stopwatch aspCounter = new Stopwatch();
                                        aspCounter.Start();
                                        do
                                        {
                                            if (aspCounter.ElapsedMilliseconds > TimeOut)
                                            {
                                                aspCounter.Stop();
                                                Utility.gMAIL(string.Format("Still waiting for ASP to die on {0}, please manually reset", sMachineName), myServer);
                                                goto Finish;

                                            }
                                            Thread.Sleep(1);
                                        }
                                        while (perfc.NextValue() > 0);


                                        ServiceController scIIS = new ServiceController("W3Svc", myServer);
                                        ServiceController scAC = new ServiceController("AddressCorrection", myServer);

                                        bool bStopIIS = Utility.StopService(scIIS);
                                        bool bStopAC = Utility.StopService(scAC);
                                        bool bACStarted = Utility.StartService(scAC);
                                        bool bIISStarted = Utility.StartService(scIIS);

                                        bool bSuccess = bStopAC && bStopIIS && bACStarted && bIISStarted;
                                        if (!bSuccess)
                                            Utility.gMAIL("ERROR Automatically Starting and/or Stopping, please manually reset", myServer);

                                        //call BigIP to enable state
                                        BigIP.setPoolMemberState(sPool, myServer + ":80", "enable");
                                    }//is it enabled, if not, don't mess with the Zohan
                                }
                                else
                                    Utility.gMAIL("Issue connecting to BigIP", sMachineName);

                            }
                            catch (Exception ex)
                            {
                                if (mem >= 825000.00)
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    sMessage = String.Format("Server {0} at {1}, {2}K of MEM & {3} requests", sMachineName, DateTime.Now.ToString("yyyy-MM-dd HH:mm"), mem.ToString("###,###,##0"), fRequests.ToString());
                                    Console.WriteLine(sMessage);
                                    Console.ForegroundColor = ConsoleColor.Green;
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Blue;
                                    sMessage = String.Format("Server {0} at {1}, {2}K of AC mem & {3} reuests", sMachineName, DateTime.Now.ToString("yyyy-MM-dd HH:mm"), acmem.ToString("###,###,##0"), fRequests.ToString());
                                    Console.WriteLine(sMessage);
                                    Console.ForegroundColor = ConsoleColor.Green;
                                }
                                sMessage = "Error in automation:\r\n" + sMessage;
                                Utility.gMAIL(sMessage, sMachineName);
                            }

                        }


                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("ERROR connecting to server {0}", myServer);
                        Console.ForegroundColor = ConsoleColor.Green;
                    }
                Finish:
                    sMessage = "";

                });
            
                if (i > 0 && i % 2 == 0)
                    Console.WriteLine("There has been {0} checks", i.ToString("###,###,##0"));
                
                if(ControlC == 0)
                    Thread.Sleep(waitTime);
                i++;

                GetFile(ref sw, ref sFileName,ref sStatsDir);
            } while (ControlC == 0);

            sw.Close();
            Console.WriteLine("DONE");
            Console.ReadLine();
        }


        static void GetFile(ref StreamWriter sw, ref string sFileNm,ref string sStatsDir)
        {
            //switch filenames if date has changed.
            string sDt = DateTime.Now.ToString("yyyyMMdd") + ".tab";
            if (sDt != sFileNm)
            {
                sFileNm = sDt;
                sw.Close();
                sw = new StreamWriter(Path.Combine(sStatsDir, sFileNm));
            }

        }



    }
}
