﻿using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ApiHooker.Communication;
using ApiHooker.Utils;
using ApiHooker.VisualStudio;

namespace ApiHooker
{
    class Program
    {
        static void Main(string[] args)
        {
            var serverPort = 1337;
            var tcpServer = new TcpListener(IPAddress.Loopback, serverPort);
            tcpServer.Start();

            foreach (var pkill in Process.GetProcessesByName("TestApp"))
                pkill.Kill();

            var testApp = ProcessManager.LaunchSuspended(AppDomain.CurrentDomain.BaseDirectory + "TestApp.exe");
            testApp.InjectHookerLib(serverPort);

            using (var client = new HookedClient(tcpServer.AcceptTcpClient()))
            {
                //client.ShowMessageBox("Hello World!");
                var methodsToHook = new[]
                {
                    ApiDefinitions.SetConsoleTitleA,
                    ApiDefinitions.SetConsoleWindowInfo,
                    ApiDefinitions.SetConsoleScreenBufferSize,
                    ApiDefinitions.WriteConsoleOutputA,
                    ApiDefinitions.GetConsoleTitleA
                };

                foreach (var m in methodsToHook)
                    m.SaveCallback = true;

                var hookedMethods = client.HookFuncs(methodsToHook);

                VsDebuggerHelper.AttachToProcess(testApp.Process.Id);

                testApp.ResumeMainThread();

                Thread.Sleep(500);

                var buffer = client.ReadBuffer();
                var callRecs = SerializationHelper.ProcessCallRecords(buffer, hookedMethods);
                foreach(var callRec in callRecs)
                    Console.WriteLine(callRec);

                client.TerminateInjectionThread();
                testApp.Process.WaitForExit();
            }

            Console.WriteLine("Before exit.");
            Console.ReadLine();
        }
    }
}
