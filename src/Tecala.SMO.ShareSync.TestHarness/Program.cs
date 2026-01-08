using SourceCode.SmartObjects.Services.ServiceSDK;
using SourceCode.SmartObjects.Services.ServiceSDK.Objects;
using System;
using System.Configuration;
using System.IO;
using Tecala.SMO.ShareSync.Services;

namespace Tecala.SMO.ShareSync.TestHarness
{
    /// <summary>
    /// Test harness for ShareSync K2 Broker
    /// Allows testing broker methods without deploying to K2
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("ShareSync K2 Broker - Test Harness");
            Console.WriteLine("==============================================");
            Console.WriteLine();

            try
            {
                // Create service configuration from app.config
                var config = CreateServiceConfiguration();

                bool running = true;
                while (running)
                {
                    Console.WriteLine("\nSelect an operation:");
                    Console.WriteLine("1. Sync Interaction Permissions");
                    Console.WriteLine("2. Create Interaction");
                    Console.WriteLine("3. Create Interactions from CSV");
                    Console.WriteLine("4. Get Job Status");
                    Console.WriteLine("5. Exit");
                    Console.Write("\nChoice: ");

                    string choice = Console.ReadLine();

                    switch (choice)
                    {
                        case "1":
                            TestSyncInteractionPermissions(config);
                            break;
                        case "2":
                            TestCreateInteraction(config);
                            break;
                        case "3":
                            TestCreateInteractions(config);
                            break;
                        case "4":
                            TestGetJobStatus(config);
                            break;
                        case "5":
                            running = false;
                            break;
                        default:
                            Console.WriteLine("Invalid choice. Please try again.");
                            break;
                    }
                }

                Console.WriteLine("\nExiting...");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nFATAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Console.ResetColor();
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void TestSyncInteractionPermissions(ServiceConfiguration config)
        {
            Console.WriteLine("\n--- Sync Interaction Permissions ---");
            Console.WriteLine("NOTE: ProjectId, EngagementId, and SiteUrl are now queried from database using InteractionId");
            Console.WriteLine();

            try
            {
                // Get input from user
                Console.Write("Interaction ID (GUID): ");
                string interactionId = Console.ReadLine();

                Console.Write("Environment (DEV/UAT/PROD): ");
                string environment = Console.ReadLine();

                Console.Write("Internal Permission (Read/Contribute/Edit): ");
                string internalPerm = Console.ReadLine();

                Console.Write("Internal User Emails (semicolon separated): ");
                string internalEmails = Console.ReadLine();

                Console.Write("External Permission (optional): ");
                string externalPerm = Console.ReadLine();

                Console.Write("External User Emails (semicolon separated, optional): ");
                string externalEmails = Console.ReadLine();

                Console.Write("Priority (Low/Medium/High/Critical): ");
                string priority = Console.ReadLine();

                // Create service and execute
                var service = new ShareSyncService(config)
                {
                    InteractionId = interactionId,
                    Environment = environment,
                    InternalPermission = string.IsNullOrWhiteSpace(internalPerm) ? "Read" : internalPerm,
                    InternalUserEmails = internalEmails,
                    ExternalPermission = externalPerm,
                    ExternalUserEmails = externalEmails,
                    Priority = string.IsNullOrWhiteSpace(priority) ? "Medium" : priority
                };

                Console.WriteLine("\nExecuting SyncInteractionPermissions...");
                var result = service.SyncInteractionPermissions();

                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void TestCreateInteraction(ServiceConfiguration config)
        {
            Console.WriteLine("\n--- Create Interaction ---");
            Console.WriteLine();

            try
            {
                // Get input from user
                Console.Write("Interaction ID (GUID): ");
                string interactionId = Console.ReadLine();

                Console.Write("Environment (DEV/UAT/PROD): ");
                string environment = Console.ReadLine();
                if (string.IsNullOrEmpty(environment)) { environment = "UAT"; }

                Console.Write("Project Subfolder (optional): ");
                string subfolder = Console.ReadLine();

                Console.Write("Internal Permission (Read/Contribute/Edit): ");
                string internalPerm = Console.ReadLine();

                Console.Write("Internal User Emails (semicolon separated): ");
                string internalEmails = Console.ReadLine();

                Console.Write("External Permission (optional): ");
                string externalPerm = Console.ReadLine();

                Console.Write("External User Emails (semicolon separated, optional): ");
                string externalEmails = Console.ReadLine();

                Console.Write("Priority (Low/Medium/High/Critical): ");
                string priority = Console.ReadLine();

                // Create service and execute
                var service = new ShareSyncService(config)
                {
                    InteractionId = interactionId,
                    Environment = environment,
                    ProjectSubfolder = subfolder,
                    InternalPermission = string.IsNullOrWhiteSpace(internalPerm) ? "Read" : internalPerm,
                    InternalUserEmails = internalEmails,
                    ExternalPermission = externalPerm,
                    ExternalUserEmails = externalEmails,
                    UploadedBy = $"{Environment.UserDomainName}\\{Environment.UserName}",
                    Priority = string.IsNullOrWhiteSpace(priority) ? "Medium" : priority
                };

                Console.WriteLine("\nExecuting CreateInteraction...");
                var result = service.CreateInteraction();

                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void TestCreateInteractions(ServiceConfiguration config)
        {
            Console.WriteLine("\n--- Create Interactions from CSV File ---");
            Console.WriteLine();

            try
            {
                // Get input from user
                Console.Write("Environment (DEV/UAT/PROD): ");
                string environment = Console.ReadLine();
                if (string.IsNullOrEmpty(environment)) { environment = "UAT"; }

                Console.Write("CSV File Path: ");
                string csvFilePath = Console.ReadLine();
                if (string.IsNullOrEmpty(csvFilePath)) csvFilePath = @"C:\Temp\Test.csv";
                string fileText = ConvertFileToK2Format(csvFilePath);

                Console.Write("Priority (Low/Medium/High/Critical): ");
                string priority = Console.ReadLine();

                // Create service and execute
                var service = new ShareSyncService(config)
                {
                    Environment = environment,
                    CsvFile = fileText,
                    UploadedBy = $"{Environment.UserDomainName}\\{Environment.UserName}",
                    Priority = string.IsNullOrWhiteSpace(priority) ? "Medium" : priority
                };

                Console.WriteLine("\nExecuting CreateInteraction...");
                var result = service.CreateInteractionBulk();

                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static string ConvertFileToK2Format(string csvFilePath)
        {
            var fileName = Path.GetFileName(csvFilePath);
            var content = System.IO.File.ReadAllBytes(csvFilePath);
            var base64Content = Convert.ToBase64String(content);

            return $"<file><name>{fileName}</name><content>{base64Content}</content></file>";
        }

        static void TestGetJobStatus(ServiceConfiguration config)
        {
            Console.WriteLine("\n--- Get Job Status ---");

            try
            {
                Console.Write("Job ID (GUID): ");
                string jobId = Console.ReadLine();

                var service = new ShareSyncService(config)
                {
                    JobId = jobId
                };

                Console.WriteLine("\nExecuting GetJobStatus...");
                var result = service.GetJobStatus();

                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }

        static void DisplayResult(ShareSyncService result)
        {
            Console.WriteLine("\n--- Result ---");

            if (result.ErrorNumber == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("SUCCESS");
                Console.ResetColor();

                if (!string.IsNullOrWhiteSpace(result.JobId))
                    Console.WriteLine($"Job ID: {result.JobId}");

                if (!string.IsNullOrWhiteSpace(result.MessageId))
                    Console.WriteLine($"Message ID: {result.MessageId}");

                if (!string.IsNullOrWhiteSpace(result.Status))
                    Console.WriteLine($"Status: {result.Status}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAILED (Error #{result.ErrorNumber})");
                Console.WriteLine($"Message: {result.ErrorMessage}");
                Console.ResetColor();
            }
        }

        static ServiceConfiguration CreateServiceConfiguration()
        {
            var config = new ServiceConfiguration();

            // Load from app.config
            config.Add("SQL Connection String", true, ConfigurationManager.AppSettings["SQL Connection String"]);
            config.Add("RabbitMQ Host", true, ConfigurationManager.AppSettings["RabbitMQ Host"]);
            config.Add("RabbitMQ Port", true, ConfigurationManager.AppSettings["RabbitMQ Port"]);
            config.Add("RabbitMQ Username", true, ConfigurationManager.AppSettings["RabbitMQ Username"]);
            config.Add("RabbitMQ Password", true, ConfigurationManager.AppSettings["RabbitMQ Password"]);
            config.Add("RabbitMQ VirtualHost", true, ConfigurationManager.AppSettings["RabbitMQ VirtualHost"]);
            config.Add("Queue InteractionPermissions", true, ConfigurationManager.AppSettings["Queue InteractionPermissions"]);
            config.Add("Queue InteractionCreation", true, ConfigurationManager.AppSettings["Queue InteractionCreation"]);
            config.Add("Queue RemovePermissions", true, ConfigurationManager.AppSettings["Queue RemovePermissions"]);

            Console.WriteLine("Configuration loaded from App.config:");
            Console.WriteLine($"  Database: {ConfigurationManager.AppSettings["SQL Connection String"]}");
            Console.WriteLine($"  RabbitMQ: {ConfigurationManager.AppSettings["RabbitMQ Host"]}:{ConfigurationManager.AppSettings["RabbitMQ Port"]}");
            Console.WriteLine();

            return config;
        }
    }
}
