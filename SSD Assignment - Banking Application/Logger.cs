using Banking_Application;
using System;
using System.Diagnostics;
using System.Reflection;

namespace Banking_Application
{
    public static class Logger
    {
        private const string SOURCE_NAME = "SSD Banking Application";
        private const string LOG_NAME = "Application";

        public static void LogTransaction(string tellerName, string accountNo, string accHolderName, string transType, string reason = "N/A")
        {
            try
            {
                if (!EventLog.SourceExists(SOURCE_NAME))
                {
                    EventLog.CreateEventSource(SOURCE_NAME, LOG_NAME);
                }

                // WHERE: Get MAC or IP
                string where = $"IP: {SecurityHelper.GetLocalIPAddress()} | MAC: {SecurityHelper.GetMacAddress()}";

                // HOW: Metadata
                Assembly assem = Assembly.GetExecutingAssembly();
                AssemblyName assemName = assem.GetName();
                string how = $"App: {assemName.Name}, Version: {assemName.Version}";

                // WHEN: DateTime.Now
                // WHO: Teller Name + Account Holder
                // WHAT: Transaction Type

                string message = $"TRANSACTION RECORD\n" +
                                 $"------------------\n" +
                                 $"WHO (Teller): {tellerName}\n" +
                                 $"WHO (Customer): {accHolderName} (Acc: {accountNo})\n" +
                                 $"WHAT: {transType}\n" +
                                 $"WHEN: {DateTime.Now}\n" +
                                 $"WHERE: {where}\n" +
                                 $"WHY: {reason}\n" +
                                 $"HOW: {how}";

                EventLog.WriteEntry(SOURCE_NAME, message, EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                // fallback to file logging if EventLog fails (permissions issue)
                Console.WriteLine("Warning: Could not write to Event Log. " + ex.Message);
            }
        }

        public static void LogSecurityEvent(string message)
        {
            try
            {
                if (!EventLog.SourceExists(SOURCE_NAME)) EventLog.CreateEventSource(SOURCE_NAME, LOG_NAME);
                EventLog.WriteEntry(SOURCE_NAME, "SECURITY ALERT: " + message, EventLogEntryType.Warning);
            }
            catch { }
        }
    }
}